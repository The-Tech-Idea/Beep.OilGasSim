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
    private static (Engine Engine, CompanyState Company) Field()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
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
            permeability: new Permeability(2.0e-13),
            netThickness: new Length(30.0),
            drainageArea: new Area(2.0e5),
            rockCompressibility: 4.5e-10,
            gasOilContact: new Length(1900.0),
            oilWaterContact: new Length(2100.0),
            Defaults.Wettability, Defaults.Drive,
            Defaults.AquiferStrength, Defaults.AquiferResponseTime);

        built.Engine.Provided.Resolve<FieldControl>().OpenWell(Well(compartment), compartment);

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
    [Fact]
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

        Money before = company.Ledger.Cash;
        engine.Pipeline.AdvanceTick();
        Money firstMonth = company.Ledger.Cash - before;

        before = company.Ledger.Cash;
        engine.Pipeline.AdvanceTick();
        Money secondMonth = company.Ledger.Cash - before;

        Assert.True(secondMonth < firstMonth,
            $"month two ({secondMonth.Cents}c) must earn less than month one " +
            $"({firstMonth.Cents}c) — a well whose reservoir has fallen produces less");
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
}
