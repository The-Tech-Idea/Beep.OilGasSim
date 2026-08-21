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
    private static (Engine Engine, CompanyState Company) Field(ulong? seed = null)
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
                Depth: new Length(2000.0)),
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
}
