// R20c.7 — the loop, end to end (design 03 §6 stages 5, 6, 8).
//
// This is the first test in the engine that is about the GAME rather than about
// a model. A well produces, the compartment it drained loses pressure, the oil
// is sold, and next month the same well produces less because of what this month
// took. Every one of those steps was proven separately; none of it was a game
// until they ran in one tick, in order, against one another.

using OGSim.Company;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Subsurface;
using OGSim.Wells;

namespace OGSim.Composition.Tests;

public sealed class ProductionLoopTests
{
    /// <summary>A composed engine with one compartment and one well on it.</summary>
    private static (Engine Engine, CompanyState Company) Field(
        ulong? seed = null, ContentId? fluidSystem = null)
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(
            seed is ulong s ? Fixture.Settings() with { WorldSeed = s } : Fixture.Settings()));
        CompanyState company = Find<CompanyState>(built.Engine);

        EntityId<IReservoirCompartmentEntity> compartment = built.Engine.Provided.Resolve<FieldControl>().AddCompartment(
            // A field, not a puddle. At 2e6 m³ of pore volume one wide-open well
            // took 39 % of the reservoir pressure in a single month and the
            // material balance refused the step — correctly: SDD-003 §3.1 will
            // not integrate that far in one jump. The engine was right and the
            // fixture was wrong, which is the good direction for that to happen
            // in.
            new GeneratedCompartment(
                PoreVolume: new ReservoirVolume(100.0e6),
                Porosity: 0.22,
                OilSaturation: 0.7,
                InitialPressure: new Pressure(30.0e6),
                Temperature: Temperature.FromCelsius(93.3),
                Depth: new Length(2000.0),
                FluidSystem: fluidSystem ?? new ContentId("medium-crude")),
            // GOOD ROCK, kept. This fixture is the one that always built its
            // own well to match — 2e-13 and 30 m in both places — so it is the
            // one place finding 170 never bit, and the facility-limited test
            // below needs exactly this deliverability to have a separator to
            // jam. The other fixtures declared this rock and drilled wells at
            // Defaults.Inflow's 1e-13 and 20 m; those were brought down to what
            // their wells actually were.
            permeability: new Permeability(2.0e-13),
            netThickness: new Length(30.0),
            drainageArea: new Area(2.0e5),
            rockCompressibility: 4.5e-10,
            gasOilContact: new Length(1900.0),
            oilWaterContact: new Length(2100.0),
            Defaults.Wettability, Defaults.Drive,
            Defaults.AquiferStrength, Defaults.AquiferResponseTime);

        // A SCENARIO DECLARING A KNOWN FIELD (SDD-010 §4b). These fixtures place
        // their reservoir directly rather than generating a basin, so it is
        // already known to be there — placed and found in one step, carrying no
        // exploration risk because there is nothing left to be wrong about.
        built.Engine.Provided.Resolve<WorldState>().DeclareKnownField(compartment, new ReservoirVolume(100.0e6));

        built.Engine.Provided.Resolve<FieldControl>().Drill(compartment, new Length(2000.0));

        return (built.Engine, company);
    }

    /// <summary>
    /// Reaches into the composed engine for a module's state. The engine does
    /// not publish these — a host reads the read model — so the test resolves
    /// them the way composition did.
    /// </summary>
    private static T Find<T>(Engine engine) where T : class, IStateOwner
    {
        // The state registry is the one place every owner lands, in key order.
        foreach (IStateOwner owner in engine.State.Owners)
            if (owner is T found) return found;

        throw new InvalidOperationException($"{typeof(T).Name} was not composed");
    }

    private static Completion Well(EntityId<IReservoirCompartmentEntity> drains) =>
        new(new EntityId<ICompletion>(1),
            new EntityId<IWellbore>(1),
            [new Perforation(drains, new Length(2000.0), new Length(2030.0), Skin: 0.0, Isolated: false)],
            new CompositeInflowModel(new InflowConditions(
                new Permeability(2.0e-13), new Length(30.0), new Area(2.0e5),
                new Length(0.108), new Viscosity(2.0e-3), new Pressure(10.0e6))),
            new HydrostaticFrictionOutflowModel(
                new TubingGeometry(new Length(2000.0), new Length(2000.0), new Length(0.0889), 4.6e-5),
                Density.FromSpecificGravity(0.85), lift: null),
            new CompletionFluid(
                Density.FromSpecificGravity(0.85),
                new FormationVolumeFactor(1.2),
                Allocation.Validated(
                    [(new EntityRef(EntityKind.Compartment, drains.Value), 1.0)]),
                new Pressure(30.0e6),
                Temperature.FromCelsius(93.3),
                Defaults.GasSurfaceDensity,
                Defaults.Fluid.SolutionGorAtBubblePoint,
                Defaults.WaterSurfaceDensity,
                0.0),
            ChokeSetting.Open,
            oilOrdinal: 0,
            gasOrdinal: 1,
            waterOrdinal: 2,
            materialCount: 3,
            lift: null);

    /// <summary>
    /// The whole loop in one tick: oil comes out, it is sold, cash goes up.
    ///
    /// <para>Asserted on CASH rather than on reservoir pressure, and not only
    /// because the truth is unreachable from here — cash is what the player
    /// sees. A test that reached past the belief wall to check the answer would
    /// be checking something no game ever shows.</para>
    /// </summary>
    [Fact]
    public void One_tick_produces_oil_and_earns_cash()
    {
        (Engine engine, CompanyState company) = Field();

        Money openingCash = company.Ledger.Cash;

        Assert.IsType<TickCompleted>(engine.Pipeline.AdvanceTick());

        Assert.True(company.Ledger.Cash > openingCash,
            "a producing month must leave the company better off than it started");

        company.Ledger.AssertBalanced();
        company.Ledger.AssertCashReconciles();
    }

    /// <summary>
    /// DECLINE — the thing that makes this a game rather than a spreadsheet. The
    /// same well, unchanged, earns less than it used to because the reservoir it
    /// drains is lower. Before finding 137 the completion held its pressure in a
    /// readonly field, and this test could not have failed however broken the
    /// physics was.
    ///
    /// <para><b>Measured after the vessel stops binding, and that is not a
    /// workaround.</b> The shipped separator's liquid leg caps this field's early
    /// production, so the first months are flat at the cap — the FACILITY is the
    /// constraint, not the reservoir, and decline is genuinely invisible until
    /// the well falls below it. That is the real behaviour of a facility-limited
    /// field and the reason a player debottlenecks; asserting decline across
    /// months when something else is binding would be asserting that the cap does
    /// not work.</para>
    /// </summary>
    /// <summary>What the company's cash did over a run of months.</summary>
    private static Money Earned(Engine engine, CompanyState company, int months)
    {
        Money before = company.Ledger.Cash;

        for (var month = 0; month < months; month++) engine.Pipeline.AdvanceTick();

        return company.Ledger.Cash - before;
    }

    [Fact]
    [Trait("Speed", "Slow")]
    public void Earnings_decline_as_the_reservoir_depletes()
    {
        (Engine engine, CompanyState company) = Field();

        // Debottleneck first, as a player would, then run past the plateau: the
        // reservoir cannot be seen to decline while a FACILITY is what limits
        // the field, and with the shipped aquifer holding pressure up the first
        // vessel would bind for longer than a field's life.
        engine.Commands.Submit(new InstallSeparatorCommand());

        for (var month = 0; month < 480; month++)
        {
            engine.Pipeline.AdvanceTick();
            if (engine.ReadModel!.Bottlenecks.Count == 0) break;
        }

        Assert.Empty(engine.ReadModel!.Bottlenecks);

        // A YEAR AGAINST THE NEXT YEAR, not a month against the month after it.
        // Weather is SEASONAL (SDD-016 §1): a January loses days a July does not,
        // so two adjacent months differ by which months they are as well as by
        // how depleted the reservoir is, and the pair can invert without decline
        // having stopped. Twelve months contain one of each, so the comparison is
        // of reservoirs rather than of calendars (finding 214).
        Money firstYear = Earned(engine, company, months: 12);
        Money secondYear = Earned(engine, company, months: 12);

        Assert.True(secondYear < firstYear,
            $"the second year ({secondYear.Cents}c) must earn less than the first " +
            $"({firstYear.Cents}c) — a well whose reservoir has fallen produces less");
    }

    /// <summary>
    /// THE PLATEAU, which is the other half of the same fact. While the vessel
    /// binds, a field produces its capacity and not its potential — the months
    /// are flat, the separator is named on the read model as the thing refusing,
    /// and the deferred mass is what the player is losing by not building.
    ///
    /// <para>This is the shape an operations game is played on: the constraint is
    /// visible, it is nameable, and it is bought past.</para>
    /// </summary>
    [Fact]
    public void R20dV1_a_facility_limited_field_produces_its_capacity_not_its_potential()
    {
        (Engine engine, _) = Field();

        engine.Pipeline.AdvanceTick();
        double first = engine.ReadModel!.ProducedThisTick.CubicMetres;

        ChainElementView jammed = Assert.Single(engine.ReadModel.Bottlenecks);
        Assert.Equal("separator", jammed.DisplayId);
        Assert.Equal(ConstraintKind.LiquidCapacity, Assert.Single(jammed.Deferred).Kind);
        Assert.True(Assert.Single(jammed.Deferred).Deferred.Kilograms > 0.0,
            "a bottleneck must say how much it is costing, or a player cannot price the fix");

        engine.Pipeline.AdvanceTick();

        // Flat while the vessel binds: the reservoir has fallen and the field
        // has not, because the reservoir was never what was limiting it.
        Assert.Equal(first, engine.ReadModel!.ProducedThisTick.CubicMetres, precision: 6);
    }

    /// <summary>
    /// Forty years, the length of a field's life. A loop that works for two
    /// months and then faults on an accumulating history is not a game either.
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void The_field_runs_for_forty_years()
    {
        (Engine engine, CompanyState company) = Field();

        for (int month = 0; month < 480; month++)
            Assert.IsType<TickCompleted>(engine.Pipeline.AdvanceTick());

        Assert.Equal(480, engine.Pipeline.CurrentTick.Value);

        // The ledger is still coherent to the cent after 480 months of postings.
        company.Ledger.AssertBalanced();
        company.Ledger.AssertCashReconciles();
    }

    /// <summary>
    /// A field with no wells still costs money to run. That is the late-life
    /// decision in one line — and it must not be conditional on production, or
    /// a shut-in field would be free to keep forever.
    /// </summary>
    [Fact]
    public void A_field_with_no_wells_still_pays_its_operating_cost()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        CompanyState company = Find<CompanyState>(built.Engine);

        Money before = company.Ledger.Cash;
        built.Engine.Pipeline.AdvanceTick();

        Assert.True(company.Ledger.Cash < before,
            "a field that produced nothing still has to be paid for");
    }

    /// <summary>
    /// Revenue is caused by a custody transfer and by nothing else (SDD-009 §1).
    /// The ledger asks the audit trail, so a posting cannot claim to be a sale —
    /// it can only cite an entry that was one.
    /// </summary>
    [Fact]
    public void Revenue_is_caused_by_a_custody_transfer()
    {
        (Engine engine, CompanyState company) = Field();

        engine.Pipeline.AdvanceTick();

        Movement? revenue = null;
        foreach (Movement movement in company.Ledger.Movements)
            if (movement.Credit == Account.Revenue) revenue = movement;

        Assert.NotNull(revenue);

        IReadOnlyList<AuditEntry> transfers = engine.Audit.Query(
            new AuditQuery(null, AuditCategory.CustodyTransfer, null, null));

        Assert.Contains(transfers, entry => entry.Id == revenue!.Cause);
    }

    /// <summary>The tick's oil sale, however many <c>Account.Revenue</c>
    /// credits post this tick (sales gas posts one too) — the LAST one, the
    /// same selector <see cref="Revenue_is_caused_by_a_custody_transfer"/>
    /// already relies on, since the oil sale is posted after the gas
    /// sale within <c>ProductionLoop.PostEconomics</c>.</summary>
    private static Money OilRevenueThisTick(CompanyState company)
    {
        Movement? revenue = null;
        foreach (Movement movement in company.Ledger.Movements)
            if (movement.Credit == Account.Revenue) revenue = movement;

        Assert.NotNull(revenue);
        return revenue!.Amount;
    }

    /// <summary>
    /// SDD-009 §6's finding-271 amendment: the shipped default grade IS the
    /// benchmark's own reference (`Defaults.Fluid`'s 35° API — the same
    /// grade `Defaults.Economics`'s "$377/m³ ÷ 0.85 t/m³" comment already
    /// assumed), and this amendment must not have moved a single cent of it.
    /// Checked against the reference itself: two fields built the same way,
    /// differing only in which `Field()` overload names the grade, still
    /// price identically. The sibling test below is where the amendment's
    /// real claim — a DIFFERENT grade prices differently, by the pinned
    /// amount — is actually proven; the full fast gate and slow suite (both
    /// clean, unchanged, on the shipped default grade throughout) are the
    /// broader evidence for this one.
    /// </summary>
    [Fact]
    public void A_field_on_the_shipped_default_grade_prices_identically_however_named()
    {
        (Engine implicitGrade, CompanyState implicitCompany) = Field();
        (Engine explicitGrade, CompanyState explicitCompany) =
            Field(fluidSystem: new ContentId("medium-crude"));

        implicitGrade.Pipeline.AdvanceTick();
        explicitGrade.Pipeline.AdvanceTick();

        Assert.Equal(
            OilRevenueThisTick(implicitCompany).Cents,
            OilRevenueThisTick(explicitCompany).Cents);
    }

    /// <summary>
    /// SDD-009 §6's finding-271 amendment: a heavier grade sells at a real
    /// discount, not a cosmetic one. Two otherwise-identical fields — same
    /// rock, same well, same month's production — differing only in fluid
    /// system, so the ratio of realised revenue IS the quality factor:
    /// 1 + 0.007 × (22 − 35) = 0.909 for heavy-sour-crude against the 35°
    /// API reference.
    /// </summary>
    [Fact]
    public void A_heavier_grade_sells_at_the_priced_discount()
    {
        (Engine medium, CompanyState mediumCompany) = Field();
        (Engine heavy, CompanyState heavyCompany) =
            Field(fluidSystem: new ContentId("heavy-sour-crude"));

        medium.Pipeline.AdvanceTick();
        heavy.Pipeline.AdvanceTick();

        Money mediumRevenue = OilRevenueThisTick(mediumCompany);
        Money heavyRevenue = OilRevenueThisTick(heavyCompany);

        Assert.True(mediumRevenue > Money.Zero);
        Assert.True(heavyRevenue < mediumRevenue,
            "a heavier, sourer grade priced no lower than the reference grade");

        double ratio = heavyRevenue.Cents / (double)mediumRevenue.Cents;
        Assert.Equal(0.909d, ratio, 3);
    }

    /// <summary>
    /// SDD-006 §7a.3's finding-268 amendment: one cargo at a time, gated on
    /// what the tank actually holds. Set well short of a full parcel directly
    /// (<c>Tank.RestoreTo</c>, the same door a reload uses) rather than played
    /// to it — a played field would take dozens of ticks to approach the
    /// threshold and this is a fact about one tick's gate, not about the
    /// field's own rate.
    ///
    /// <para>40 million kg, not one: <c>Receive</c> runs before the gate
    /// checks <c>Held</c>, so this tick's OWN production counts too, and this
    /// fixture's one well — good rock, kept for the facility-limited test
    /// above — turned out to clear tens of millions of kg in a single tick,
    /// comfortably crossing a margin that looked generous until it was
    /// measured. Held BEFORE the tick still bounds what a wrongly-open gate
    /// could have drawn it down to (well under the seed), which the threshold
    /// check alone cannot tell apart from the gate correctly staying
    /// shut.</para>
    /// </summary>
    [Fact]
    public void A_cargo_does_not_lift_below_a_full_parcel()
    {
        (Engine engine, _) = Field();
        var tank = engine.Provided.Resolve<SurfaceChain>().Tank;

        var kilograms = new double[Defaults.MaterialCount];
        kilograms[Defaults.OilOrdinal.Ordinal] = 40_000_000.0;

        tank.RestoreTo(
            MaterialInventory.Of(kilograms),
            Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

        engine.Pipeline.AdvanceTick();

        double held = engine.ReadModel!.Storage.Held.Kilograms;

        // The threshold itself: whatever this tick's own well cleared, the
        // gate stays shut below a full parcel.
        Assert.True(held < Defaults.CargoSize.Kilograms,
            $"the tank drew a cargo before it was full: held={held} against " +
            $"a {Defaults.CargoSize.Kilograms} kg cargo");

        // AND held did not fall below the seed: a wrongly-open gate would draw
        // at the berth's rate (tens of millions of kg) regardless of this
        // tick's production, which the threshold check above cannot catch
        // starting from a value already under it.
        Assert.True(held >= 40_000_000.0 - 2_000_000.0,
            $"the tank drew from a cargo that was not full: held={held}, seeded at 40,000,000");
    }

    /// <summary>SDD-006 §7a.3's finding-268 amendment: the other half of the
    /// same gate — a full cargo departs at the berth's rate rather than
    /// waiting for a schedule step 2 does not build.</summary>
    [Fact]
    public void A_full_cargo_lifts_at_the_berths_rate()
    {
        (Engine engine, _) = Field();
        var tank = engine.Provided.Resolve<SurfaceChain>().Tank;

        var kilograms = new double[Defaults.MaterialCount];
        kilograms[Defaults.OilOrdinal.Ordinal] = Defaults.CargoSize.Kilograms + 1_000_000.0;

        tank.RestoreTo(
            MaterialInventory.Of(kilograms),
            Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

        Mass before = tank.Held.Total;

        engine.Pipeline.AdvanceTick();

        Mass after = engine.ReadModel!.Storage.Held;

        Assert.True(after.Kilograms < before.Kilograms - 1_000_000.0,
            $"a full cargo did not lift: before={before.Kilograms} after={after.Kilograms}");
    }

    private static IReadOnlyList<AuditEntry> DemurrageEntries(Engine engine) =>
        [.. engine.Audit.Query(new AuditQuery(null, AuditCategory.Financial, null, null))
            .Where(entry => entry.Data.TryGetValue("accrual", out AuditValue accrual)
                             && accrual.Value == "demurrage")];

    /// <summary>SDD-006 §7a.4's finding-269 amendment: a cargo that clears
    /// inside the 60-day laytime costs nothing extra — demurrage is an
    /// OVERRUN charge, not a tax on every lifting.</summary>
    [Fact]
    public void A_cargo_that_clears_within_laytime_is_not_charged_demurrage()
    {
        (Engine engine, _) = Field();
        var tank = engine.Provided.Resolve<SurfaceChain>().Tank;

        // Just over the line: at the shipped E1 rate (20 kg/s, 51.84e6
        // kg/tick) this clears inside the first tick, nowhere near 60 days.
        var kilograms = new double[Defaults.MaterialCount];
        kilograms[Defaults.OilOrdinal.Ordinal] = Defaults.CargoSize.Kilograms + 1_000_000.0;

        tank.RestoreTo(
            MaterialInventory.Of(kilograms),
            Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

        engine.Pipeline.AdvanceTick();

        Assert.Empty(DemurrageEntries(engine));
    }

    /// <summary>SDD-006 §7a.4's finding-269 amendment: a cargo that takes
    /// long enough to clear — the shipped E1 rate against a tank sized well
    /// past one cargo — overruns the 60-day laytime and is charged for it,
    /// once, the tick it finally departs.</summary>
    [Fact]
    public void An_overrunning_cargo_is_charged_demurrage()
    {
        (Engine engine, _) = Field();
        var tank = engine.Provided.Resolve<SurfaceChain>().Tank;

        // Sized so the E1 berth (51.84e6 kg/tick) needs several ticks —
        // 180+ days — to clear it, comfortably past the 60-day laytime.
        var kilograms = new double[Defaults.MaterialCount];
        kilograms[Defaults.OilOrdinal.Ordinal] = 300_000_000.0;

        tank.RestoreTo(
            MaterialInventory.Of(kilograms),
            Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

        var ticks = 0;
        while (engine.ReadModel is null
               || engine.ReadModel.Storage.Held.Kilograms >= Defaults.CargoSize.Kilograms)
        {
            engine.Pipeline.AdvanceTick();
            ticks++;
            Assert.True(ticks < 20, "the seeded cargo never departed");
        }

        IReadOnlyList<AuditEntry> demurrage = DemurrageEntries(engine);

        Assert.Single(demurrage);
        Assert.True(
            double.Parse(
                demurrage[0].Data["overrun-days"].Value,
                System.Globalization.CultureInfo.InvariantCulture) > 0.0,
            "the charged entry names no positive overrun");
    }

    /// <summary>
    /// R23-V16, the reachability half. SDD-012 §4b's R23.1 amendment, and
    /// <b>a company can clean up.</b> A
    /// field that flares everything it produces is Ruined, buys the gas plant
    /// that stops it, and its ESG standing RECOVERS — which is CI4's first exit
    /// existing rather than being claimed in a comment.
    ///
    /// <para>Against the lifetime-cumulative record this test cannot pass, and
    /// that is the point of it: a ratio of everything ever burned to everything
    /// ever produced does not move for anything a player buys in year ten, so the
    /// lender charged the worst spread to every company always, the gas plant
    /// rewarded nothing, and an incident was subtracted from a number already
    /// clamped at zero. One arithmetic defect wearing three faces
    /// (findings 223, 228).</para>
    ///
    /// <para><c>HS12_the_loop_has_two_exits</c> claimed this row and proved only
    /// that <c>Standing</c> is monotonic in its argument — true, and green for
    /// the whole time the exit was unreachable. This is the half that was
    /// missing: not that a clean record scores better, but that a company which
    /// has been dirty can BECOME one.</para>
    ///
    /// <para>The recovery is SLOW on purpose — a decade of clean operation, not a
    /// quarter — because the half-life is three years and the record is what a
    /// lender has watched. Twenty-four clean months still measure 0.0000 here;
    /// it is the tenth year that reaches 0.79. A standing that could be repaired
    /// in a quarter would price behaviour nobody had to sustain.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R23V16_a_company_that_stops_flaring_recovers_its_standing()
    {
        // PINNED TO ITS OWN SEED, not the file's shared default (finding 184):
        // R9.1's compressor is a registered flow element from tick 0 that can
        // independently fail and get repaired, so it draws extra outcomes from
        // the scheduler's shared `_outcomes` stream (Scheduler.cs) across the
        // dirty decade before this test's own InstallGasPlantCommand draws its
        // success/failure grade — under the file's default seed that shift
        // lands the gas plant's own install on a failed grade (it never fits
        // past `gas-plant-none`, confirmed by a throughput trace showing 100%
        // of gas still reaching the flare a decade after the purchase), where
        // clean master's unshifted draw sequence lands on success. A seed is
        // just as valid a way to play this field as another; this one was
        // checked against the same fixture and clears the 0.5 bar with a wide
        // margin (0.85).
        (Engine engine, _) = Field(seed: 6UL);
        engine.Commands.Submit(new InstallSeparatorCommand());

        // A decade of burning everything the separator's gas leg produces.
        Fixture.Run(engine, months: 120);

        double ruined = engine.ReadModel!.EsgStanding;

        Assert.Equal(0.0, ruined);

        // The purchase the mechanic is supposed to reward.
        engine.Commands.Submit(new InstallGasPlantCommand());
        Fixture.Run(engine, months: 120);

        double cleaned = engine.ReadModel!.EsgStanding;

        Assert.True(cleaned > 0.5,
            $"the standing recovered to {cleaned} after a clean decade; a record " +
            "that cannot rise is a punishment rather than a decision (CI4)");
    }

    /// <summary>
    /// R21-V6. <b>All seven audit-backed features of design 09 §7 are
    /// answerable</b> — asked of one field that produced, jammed, learned,
    /// broke and refused a command, because a feature is answerable only if a
    /// game that DID the thing can be asked about it.
    ///
    /// <para>Seven, not eleven: §7's table has eleven rows and four of them name
    /// something other than an audit entry as their backing — barrier status from
    /// the integrity model, standing indicators from the read model, and the
    /// weather account from the segment plan. This is the audit trail's share of
    /// that table, which is what the verification row means by seven.</para>
    ///
    /// <para><b>Asserted on the DATA rather than on the count</b>, which is the
    /// whole difference between this test and a green one. *Where did my money go
    /// this quarter?* was backed by two hundred and forty entries a month apart
    /// over twenty years, and every one of them carried an empty dictionary: the
    /// operating cost added its three components together inside the method and
    /// audited the sum as nothing at all. A test counting entries would have
    /// passed against that (finding 230).</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R21V6_every_audit_backed_feature_of_the_diagnostics_design_is_answerable()
    {
        (Engine engine, _) = Field();

        // A command that cannot be honoured, so there is something to have got
        // wrong: no element carries this id.
        engine.Commands.Submit(new RepairEquipmentCommand(
            new EntityRef(EntityKind.FlowElement, 999_999)));

        engine.Commands.Submit(new InstallSeparatorCommand());

        Fixture.Run(engine, months: 8);

        // And something learned, which costs money and takes months.
        engine.Commands.Submit(new WellTestCommand(new EntityId<IReservoirCompartmentEntity>(1)));

        Fixture.Run(engine, months: 4);

        // 1 — "Why is this well shut in?" and
        // 3 — "Production loss report": the same entries, read two ways. The
        // element names what refused and the deferred mass is what it cost.
        AuditEntry binding = First(engine, AuditCategory.ConstraintBinding);

        Assert.True(binding.Data.ContainsKey("element"), "a binding must name what bound");
        Assert.True(binding.Data.ContainsKey("kind"), "and which limit it hit");
        Assert.True(binding.Data.ContainsKey("deferred-kg"),
            "and what it cost, or the loss report has nothing to attribute");
        Assert.NotNull(binding.Subject);

        // 2 — "Where did my money go this quarter?"
        AuditEntry spend = Find(engine, AuditCategory.Financial, "spend", "field-operating");

        Assert.True(spend.Data.ContainsKey("standing"), "the charge abandonment ends");
        Assert.True(spend.Data.ContainsKey("lifting"), "what the barrels cost to lift");
        Assert.True(spend.Data.ContainsKey("injection-water"), "and what the flood is drinking");

        // 4 — "What did I learn from this well?"
        AuditEntry learned = First(engine, AuditCategory.BeliefUpdate);

        Assert.True(learned.Data.ContainsKey("subject"), "a belief is about something");
        Assert.True(learned.Data.ContainsKey("posteriorMu"), "and it moved to somewhere");
        Assert.True(learned.Data.ContainsKey("posteriorSigma"),
            "with a confidence, or the company cannot tell a survey from a guess");

        // 5 — "Field history timeline"
        AuditEntry moved = First(engine, AuditCategory.StateTransition);

        Assert.True(moved.Data.ContainsKey("state"), "a transition is to a state");

        // 6 — "Was that fair?"
        AuditEntry drawn = First(engine, AuditCategory.StochasticOutcome);

        Assert.True(drawn.Data.ContainsKey("stream"), "which of the eight streams");
        Assert.True(drawn.Data.ContainsKey("draw"), "what came up");
        Assert.True(drawn.Data.ContainsKey("threshold"),
            "and what it had to beat — all three, or 'was that fair' is unanswerable");

        // 7 — "What did I do wrong?"
        AuditEntry refused = First(engine, AuditCategory.Rejection);

        Assert.True(refused.Data.ContainsKey("command"), "a refusal names the command");
        Assert.True(refused.Data.ContainsKey("reason.0"),
            "and gives a domain reason, not a status code");
    }

    /// <summary>The first entry of a category, with a failure that says which
    /// feature went dark rather than "sequence contains no elements".</summary>
    private static AuditEntry First(Engine engine, AuditCategory category)
    {
        IReadOnlyList<AuditEntry> found =
            engine.Audit.Query(new AuditQuery(null, category, null, null));

        Assert.True(found.Count > 0,
            $"nothing in the trail is a {category} entry, so the design 09 §7 " +
            "feature it backs cannot be answered at all");

        return found[0];
    }

    /// <summary>The first entry of a category carrying a given field, for the
    /// categories several different events share.</summary>
    private static AuditEntry Find(
        Engine engine, AuditCategory category, string key, string value)
    {
        IReadOnlyList<AuditEntry> found =
            engine.Audit.Query(new AuditQuery(null, category, null, null));

        for (int i = 0; i < found.Count; i++)
            if (found[i].Data.TryGetValue(key, out AuditValue carried)
                && carried.Value == value)
                return found[i];

        Assert.Fail($"no {category} entry carries {key}={value}");
        return found[0];
    }

    /// <summary>
    /// R22.7 / SDD-016 §3 — <b>the field solves at the weather it is having</b>,
    /// not at a constant.
    ///
    /// <para>`SegmentContext.Ambient` is a real solver input — `Compressor`
    /// derates on it through design 13 §3.3's k_derate — and the loop handed it a
    /// fixed 15 °C every month of every game, with `WeatherSeverity: 0.0` beside
    /// it, while `WeatherState` computed the seasonal values a few metres away
    /// and only the read model read them (finding 233).</para>
    ///
    /// <para>Asserted through the READ MODEL, which is now the same number: the
    /// projection used to ask `TemperatureOn` for the month's last day while the
    /// solve used a per-segment mean, so a host was shown a temperature the field
    /// never ran at. One fact, one owner, and the reason this test can see the
    /// solver's input at all.</para>
    ///
    /// <para>A YEAR, and asserted on the SPREAD rather than on a shape: the
    /// shipped climate is temperate and its baseline runs 15.4 °C in August to
    /// 5.6 °C in February, so a field that solved at one temperature all year
    /// would show a spread of zero. Which month is warmest is the content's
    /// business and not this test's.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R22V15_the_field_solves_at_the_ambient_it_is_having()
    {
        (Engine engine, _) = Field();

        var coldest = double.MaxValue;
        var warmest = double.MinValue;

        for (var month = 0; month < 12; month++)
        {
            engine.Pipeline.AdvanceTick();

            double ambient = engine.ReadModel!.Weather.Ambient.Kelvin;

            if (ambient < coldest) coldest = ambient;
            if (ambient > warmest) warmest = ambient;
        }

        Assert.True(warmest - coldest > 5.0,
            $"a year of solving spanned {warmest - coldest:F2} K, so the field is " +
            "running at a constant rather than at its own weather");
    }

    // ------------------------------------------------ working-interest sale
    // SDD-011 §4's finding-275 amendment — R13.10's second restructuring lever.

    /// <summary>The tick's own <c>Account.PartnerPayable</c> credit — the
    /// partner's share of revenue, the same LAST-movement selector
    /// <see cref="OilRevenueThisTick"/> already uses.</summary>
    private static Money PartnerPayableCreditThisTick(CompanyState company)
    {
        Movement? credit = null;
        foreach (Movement movement in company.Ledger.Movements)
            if (movement.Credit == Account.PartnerPayable) credit = movement;

        Assert.NotNull(credit);
        return credit!.Amount;
    }

    /// <summary>
    /// Seeds a real belief about oil in place for the fixture's one
    /// compartment, the same door <c>DrillWellCommand</c>'s applier
    /// delivers through (<c>Defaults.OilInPlaceKind</c> is log-space) — a
    /// plausible in-place figure off this fixture's own declared rock
    /// (pore volume x porosity x oil saturation), not an arbitrary one.
    /// </summary>
    private static void SeedOilInPlaceBelief(Engine engine)
    {
        WorldState world = engine.Provided.Resolve<WorldState>();
        EntityId<IReservoirCompartmentEntity> compartment = world.Beneath(world.Prospects[0])!.Value;

        double trueOilInPlace = 100.0e6 * 0.22 * 0.7;

        engine.Provided.Resolve<IBeliefStore>().Apply(new Observation(
            new EntityRef(EntityKind.Compartment, compartment.Value),
            Defaults.OilInPlaceKind,
            Math.Log(trueOilInPlace),
            Sigma: 0.3,
            BeliefSpace.Log,
            Provenance.WellTest));
    }

    /// <summary>Pushes cash below zero directly, the same way
    /// <c>GameplayTests</c>' insolvency fixture does — a restructuring lever
    /// is gated on distress, and this is the fastest honest way to produce
    /// it without a multi-decade covenant-breach run.</summary>
    private static void ForceCashNegative(Engine engine, CompanyState company)
    {
        Money overdraw = company.Ledger.Cash + Money.FromMillions(1.0);

        company.Ledger.Post(new Movement(
            new Tick(0), Account.Opex, Account.Cash, overdraw,
            MovementCategory.Operating, Asset: null,
            Cause: engine.Audit.Record(
                AuditCategory.Financial, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal))));

        Assert.True(company.Ledger.Cash < Money.Zero, "the fixture failed to force distress");
    }

    /// <summary>
    /// SDD-011 §4's finding-275 amendment: the sale prices at
    /// <c>Bank.Terms.ReserveValue x fraction x (1 - discount)</c> — the SAME
    /// DCF walk the borrowing base already runs, read at the tick the sale
    /// APPLIES (stage 2, before stage 8 recomputes it that same tick), not a
    /// value the test hands the command directly.
    /// </summary>
    [Fact]
    public void A_sale_prices_at_the_reserve_value_times_fraction_at_the_discount()
    {
        (Engine engine, CompanyState company) = Field();

        // Field()'s own FieldControl.Drill is a direct test shortcut around
        // DrillWellCommand's applier, which is the one place a discovery
        // delivers an oil-in-place OBSERVATION (SDD-008 §3) — so a
        // hand-declared field carries no belief of its own, and
        // ReservesBook (which reads beliefs, not truth) prices it at zero
        // without one.
        SeedOilInPlaceBelief(engine);

        engine.Pipeline.AdvanceTick();

        Bank bank = Find<Bank>(engine);
        Money reserveValue = bank.Terms.ReserveValue;
        Assert.True(reserveValue > Money.Zero, "the fixture has no reserves to price a sale off");

        ForceCashNegative(engine, company);

        Assert.IsType<Accepted>(engine.Commands.Submit(new SellWorkingInterestCommand(0.2)));

        engine.Pipeline.AdvanceTick();

        Money price = LastEquityCreditThisTick(company);
        Assert.Equal(Money.RoundHalfEven(reserveValue.Cents * 0.2 * 0.75), price);
    }

    private static Money LastEquityCreditThisTick(CompanyState company)
    {
        Movement? credit = null;
        foreach (Movement movement in company.Ledger.Movements)
            if (movement.Credit == Account.Equity) credit = movement;

        Assert.NotNull(credit);
        return credit!.Amount;
    }

    /// <summary>A healthy company — Clear covenant, positive cash — cannot
    /// use a restructuring lever: it is not a routine financing tool.</summary>
    [Fact]
    public void A_sale_while_financially_healthy_refuses()
    {
        (Engine engine, _) = Field();

        engine.Pipeline.AdvanceTick();

        var result = Assert.IsType<Rejected>(
            engine.Commands.Submit(new SellWorkingInterestCommand(0.1)));

        Assert.Contains(result.Reasons, r => r.LocId == "$loc:reject.not-distressed");
    }

    /// <summary>Selling past the 50% cumulative cap refuses and names the
    /// ceiling rather than silently clamping.</summary>
    [Fact]
    public void A_sale_above_the_sellable_cap_refuses_naming_the_ceiling()
    {
        (Engine engine, CompanyState company) = Field();

        engine.Pipeline.AdvanceTick();
        ForceCashNegative(engine, company);

        var result = Assert.IsType<Rejected>(
            engine.Commands.Submit(new SellWorkingInterestCommand(0.6)));

        Assert.Contains(result.Reasons, r =>
            r.LocId == "$loc:reject.beyond-sellable-cap" && r.Detail.Contains("0.5"));
    }

    /// <summary>Two sales accumulate rather than replace, and the cap is
    /// read against the CUMULATIVE share, not each sale in isolation.</summary>
    [Fact]
    public void Two_sequential_sales_accumulate_and_the_second_still_respects_the_cap()
    {
        (Engine engine, CompanyState company) = Field();

        engine.Pipeline.AdvanceTick();
        ForceCashNegative(engine, company);

        Assert.IsType<Accepted>(engine.Commands.Submit(new SellWorkingInterestCommand(0.3)));
        engine.Pipeline.AdvanceTick();

        WorkingInterest stake = Find<WorkingInterest>(engine);
        Assert.Equal(0.3, stake.PartnerShare, 9);

        // The first sale's proceeds can have carried cash back above zero
        // this same tick — re-forced rather than assumed, so what is being
        // tested here is the CAP, not whether distress persisted on its own.
        ForceCashNegative(engine, company);

        // 0.3 + 0.3 = 0.6, past the 0.5 cap.
        var overCap = Assert.IsType<Rejected>(
            engine.Commands.Submit(new SellWorkingInterestCommand(0.3)));
        Assert.Contains(overCap.Reasons, r => r.LocId == "$loc:reject.beyond-sellable-cap");

        // 0.3 + 0.2 = 0.5, exactly at the cap — still sellable.
        Assert.IsType<Accepted>(engine.Commands.Submit(new SellWorkingInterestCommand(0.2)));
        engine.Pipeline.AdvanceTick();

        Assert.Equal(0.5, stake.PartnerShare, 9);
    }

    /// <summary>
    /// Once a stake is sold, the field's OWN production splits every tick
    /// (`ProductionLoop.PostRevenueSplit`/`PostCostSplit`): the partner's
    /// share of that same tick's oil-sale revenue lands in
    /// `Account.PartnerPayable` rather than `Account.Revenue`, in
    /// proportion to `PartnerShare` — set directly here (bypassing the
    /// distress gate) because this test is about the SPLIT, not about
    /// reaching it.
    /// </summary>
    [Fact]
    public void Production_after_a_sale_splits_revenue_with_the_partner()
    {
        (Engine engine, CompanyState company) = Field();

        Find<WorkingInterest>(engine).Sell(0.4);

        engine.Pipeline.AdvanceTick();

        Money companyRevenue = OilRevenueThisTick(company);
        Money partnerRevenue = PartnerPayableCreditThisTick(company);

        Assert.True(companyRevenue > Money.Zero);
        Assert.True(partnerRevenue > Money.Zero);

        double revenueShare = partnerRevenue.Cents / (double)(companyRevenue.Cents + partnerRevenue.Cents);
        Assert.Equal(0.4, revenueShare, 3);

        Money companyOpex = LastMovementWhere(company, m => m.Debit == Account.Opex).Amount;
        Money partnerOpex = LastMovementWhere(company, m => m.Debit == Account.PartnerPayable).Amount;

        Assert.True(companyOpex > Money.Zero);
        Assert.True(partnerOpex > Money.Zero);

        double opexShare = partnerOpex.Cents / (double)(companyOpex.Cents + partnerOpex.Cents);
        Assert.Equal(0.4, opexShare, 3);
    }

    private static Movement LastMovementWhere(CompanyState company, Func<Movement, bool> match)
    {
        Movement? found = null;
        foreach (Movement movement in company.Ledger.Movements)
            if (match(movement)) found = movement;

        Assert.NotNull(found);
        return found!;
    }

    // -------------------------------------------------------------- takeover
    // SDD-014 §5a's finding-276 amendment — R13.10's third and last
    // restructuring finding.

    /// <summary>Drives a real, unambiguous covenant breach directly — debited
    /// against Capex_PPE like <see cref="Fixture"/>'s own drawn-debt setup,
    /// so <see cref="Bank"/>'s own DCF walk (which prices this fixture's
    /// reserves at zero) makes ANY debt exceed the borrowing base.</summary>
    private static void ForceDrawnDebt(Engine engine, CompanyState company, Money amount)
    {
        company.Ledger.Post(new Movement(
            new Tick(0), Account.Capex_PPE, Account.Debt, amount,
            MovementCategory.Development, Asset: null,
            Cause: engine.Audit.Record(
                AuditCategory.Financial, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal))));
    }

    /// <summary>
    /// A company with $0 reserves and any real debt breaches on tick 1
    /// (Clear → Curing), the 6-tick cure window elapses on tick 7
    /// (Curing → Amortising), and 12 further ticks Amortising — tick 18 —
    /// is the takeover threshold. $100M against a fixture that can sweep at
    /// most a few million a month never comes close to curing on its own.
    /// </summary>
    [Fact]
    public void A_company_stuck_amortising_with_no_more_room_to_sell_is_taken_over()
    {
        (Engine engine, CompanyState company) = Field();

        ForceDrawnDebt(engine, company, Money.FromMillions(100.0));
        Find<WorkingInterest>(engine).Sell(0.5);   // at the sellable cap

        for (var month = 0; month < 18; month++) engine.Pipeline.AdvanceTick();

        Assert.True(engine.ReadModel!.TakenOver);

        IReadOnlyList<AuditEntry> entries = engine.Audit.Query(
            new AuditQuery(null, AuditCategory.StateTransition, null, null));

        Assert.Contains(entries, e =>
            e.Data.TryGetValue("kind", out AuditValue kind) && kind.Value == "company.taken-over");
    }

    /// <summary>Stuck in Amortising exactly as long, but with sellable room
    /// left — the OTHER lever is still available, so this is not yet a
    /// last resort.</summary>
    [Fact]
    public void A_company_that_can_still_sell_more_working_interest_is_not_taken_over()
    {
        (Engine engine, CompanyState company) = Field();

        ForceDrawnDebt(engine, company, Money.FromMillions(100.0));
        Find<WorkingInterest>(engine).Sell(0.3);   // under the 0.5 cap

        for (var month = 0; month < 20; month++) engine.Pipeline.AdvanceTick();

        Assert.False(engine.ReadModel!.TakenOver);
    }

    /// <summary>
    /// A covenant cured before the threshold resets the clock rather than
    /// pausing it — proven with a SECOND breach, not merely a cured one left
    /// alone: 8 ticks into the first Amortising spell, the debt is cleared,
    /// then re-drawn. A clock that only paused would need just 4 more
    /// Amortising ticks (8 + 4 = 12) to trigger; a clock that genuinely
    /// reset needs a full fresh 12 — so 10 ticks after the second breach is
    /// enough to prove one from the other (short of the reset clock's own
    /// 18-tick requirement, past what a merely-paused one would have taken).
    /// </summary>
    [Fact]
    public void A_company_that_cures_its_covenant_before_the_threshold_is_never_at_risk()
    {
        (Engine engine, CompanyState company) = Field();

        ForceDrawnDebt(engine, company, Money.FromMillions(100.0));
        Find<WorkingInterest>(engine).Sell(0.5);

        for (var month = 0; month < 14; month++) engine.Pipeline.AdvanceTick();   // 8 ticks Amortising

        Bank bank = Find<Bank>(engine);
        ClearDrawnDebt(engine, company, bank);

        for (var month = 0; month < 3; month++) engine.Pipeline.AdvanceTick();    // confirm cured, Clear

        ForceDrawnDebt(engine, company, Money.FromMillions(100.0));               // breach again

        for (var month = 0; month < 10; month++) engine.Pipeline.AdvanceTick();

        Assert.False(engine.ReadModel!.TakenOver);
    }

    /// <summary>
    /// <b>The read model's water cut is the real field's, not a second
    /// computation of it</b> (SDD-017 §2's `FieldView.WaterCut` row, finding
    /// 279) — a truth-side figure the custody meter already delivers, `loop.
    /// WaterCut` (SDD-012 §1's own `k_w` term), and until now invisible to a
    /// host. Ten ticks is enough for a small connate cut to appear behind a
    /// single well on good rock, before that well needs its first repair.
    /// </summary>
    [Fact]
    public void The_read_model_publishes_the_real_field_water_cut()
    {
        (Engine engine, _) = Field();
        ProductionLoop loop = Find<ProductionLoop>(engine);

        for (var month = 0; month < 10; month++) engine.Pipeline.AdvanceTick();

        Assert.True(loop.WaterCut > 0.0, "the field made no water at all by month 10");
        Assert.Equal(loop.WaterCut, engine.ReadModel!.WaterCut);
    }

    private static void ClearDrawnDebt(Engine engine, CompanyState company, Bank bank)
    {
        if (bank.Drawn <= Money.Zero) return;

        company.Ledger.Post(new Movement(
            new Tick(0), Account.Debt, Account.Capex_PPE, bank.Drawn,
            MovementCategory.Development, Asset: null,
            Cause: engine.Audit.Record(
                AuditCategory.Financial, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal))));
    }
}
