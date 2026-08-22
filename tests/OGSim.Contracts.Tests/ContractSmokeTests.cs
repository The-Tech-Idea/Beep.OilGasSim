// Contract-layer smoke: proves the type graph is connected and the few pinned
// one-liners behave as their SDDs state. No engine implementations exist yet —
// these tests exercise only what the contract layer itself carries.

using OGSim.Contracts;
using OGSim.Kernel;
using Xunit;

namespace OGSim.Tests;

public class ContractSmokeTests
{
    [Fact] // SDD-009 §1: half-even, the one double→Money rule
    public void Money_rounds_half_even_at_the_ledger_boundary()
    {
        Assert.Equal(2L, Money.RoundHalfEven(2.5).Cents);   // to even, not away
        Assert.Equal(4L, Money.RoundHalfEven(3.5).Cents);
        Assert.Equal(new Money(300_000_000_000L), Money.FromMillions(3000.0));
    }

    [Fact] // SDD-001 §8: checked arithmetic — overflow throws, never wraps
    public void Money_overflow_is_loud()
    {
        var nearMax = new Money(long.MaxValue - 1);
        Assert.Throws<OverflowException>(() => _ = nearMax + new Money(2));
    }

    [Fact] // SDD-001 §8 pass 4: integer scaling is EXACT — day-rate × days never rounds
    public void Money_integer_scaling_is_exact()
    {
        var dayRate = new Money(123_456_789);            // an awkward rig day-rate
        Assert.Equal(new Money(3_703_703_670L), dayRate * 30L);
        Assert.Equal(dayRate * 30L, 30L * dayRate);
        Assert.Throws<OverflowException>(() => _ = new Money(long.MaxValue / 2) * 3L);
    }

    [Fact] // SDD-001 §1.1: rb ↔ stb only through an FVF, and it round-trips
    public void Volume_conversion_requires_the_fvf_and_round_trips()
    {
        var bo = new FormationVolumeFactor(1.25);
        var rb = new ReservoirVolume(1000.0);
        var back = bo.Swell(bo.Shrink(rb));
        Assert.Equal(rb.CubicMetres, back.CubicMetres, 9);
        // ReservoirVolume + SurfaceVolume does not compile — the double-count
        // killer is a compile-time fact, asserted by the R1-V2 compile-failure
        // corpus once the architecture test project exists.
    }

    [Fact] // Pass 6 (finding 77): gas has its OWN bridge — to StandardGasVolume, never stock-tank
    public void Gas_fvf_round_trips_through_the_standard_gas_family()
    {
        var bg = new GasFormationVolumeFactor(0.005);
        var rv = new ReservoirVolume(1000.0);
        StandardGasVolume surfaceGas = bg.Shrink(rv);          // typed: NOT SurfaceVolume
        Assert.Equal(200_000.0, surfaceGas.CubicMetres, 9);
        Assert.Equal(rv.CubicMetres, bg.Swell(surfaceGas).CubicMetres, 9);
    }

    [Fact] // SDD-001 §1.2: API gravity is a conversion, and it round-trips
    public void Api_gravity_round_trips_through_density()
    {
        var api = new ApiGravity(35.0);
        Assert.Equal(35.0, ApiGravity.FromDensity(api.ToDensity()).Degrees, 9);
    }

    [Fact] // SDD-001 §3: 30/360 — quarters and seasons from real-labelled months
    public void Calendar_is_30_360_with_real_labels()
    {
        Assert.Equal(30.0, Duration.DaysPerTick);
        var jan = new GameDate(1965, 1);
        Assert.Equal(Quarter.Q1, jan.Quarter);
        Assert.Equal(Season.Winter, jan.SeasonAt(ClimateHemisphere.Northern));
        Assert.Equal(Season.Summer, jan.SeasonAt(ClimateHemisphere.Southern));
    }

    [Fact] // SDD-001 §2 / design 21 §5.3: EntityRef ordering is total and deterministic
    public void EntityRef_ordering_is_total()
    {
        var a = new EntityRef(EntityKind.Well, 2);
        var b = new EntityRef(EntityKind.Well, 10);
        var c = new EntityRef(EntityKind.Compartment, 1);
        Assert.True(a.CompareTo(b) < 0);
        Assert.True(a.CompareTo(c) != 0);
    }

    [Fact] // SDD-004 §2: ContentId charset is enforced at construction
    public void ContentId_rejects_bad_charset()
    {
        _ = new ContentId("esp-c");
        Assert.Throws<ArgumentException>(() => new ContentId("ESP C"));
        Assert.Throws<ArgumentException>(() => new ContentId(""));
    }

    [Fact] // The cross-assembly graph: a Contracts record composed of Kernel types
    public void Contract_graph_connects_kernel_and_domain()
    {
        var gate = new GateFail(new MissingItem[]
        {
            new MissingTechnology(new TechnologyId(new ContentId("esp-ht-gassy"))),
            new EnvelopeExceeded(EnvelopeKind.MaxDrillingDepth, 5000.0, 3000.0),
        });
        Assert.Equal(2, gate.Missing.Count);

        var perf = new Perforation(
            new EntityId<IReservoirCompartmentEntity>(1),
            Length.FromFeet(9000), Length.FromFeet(9060), Skin: 4.0, Isolated: false);
        Assert.False(perf.Isolated);

        OperatingPoint dead = new Dead();
        Assert.IsNotType<Flowing>(dead);   // DEAD is a distinct outcome, not q = 0 (R6-V6)
    }

    [Fact] // The interfaces are implementable — a fake element wires ports,
           // topology, transform and conservation shapes without friction
    public void IFlowElement_is_implementable_and_wires_into_a_topology()
    {
        var choke = new FakePassThrough(new EntityId<IFlowElement>(7));
        var sep = new FakePassThrough(new EntityId<IFlowElement>(8));
        var topology = new FlowTopology(
            new IFlowElement[] { choke, sep },
            new[] { new FlowConnection(choke.Id, new PortId(1), sep.Id, new PortId(0)) });

        var inStream = new MaterialStream(
            new Composition([100.0, 20.0]),
            new Pressure(5_000_000), new Temperature(330),
            new Allocation([(new EntityRef(EntityKind.Compartment, 1), 1.0)]));
        var result = choke.Transform(new TransformInput(
            [inStream], new SegmentContext(30, new Temperature(288), 0.0), SolvedRate: null));

        Assert.Single(topology.Connections);
        Assert.Single(result.Outlets);
        Assert.Equal(inStream.MassRates, result.Outlets[0].MassRates); // Σin + 0 = Σout + 0 + 0
    }

    private sealed class FakePassThrough(EntityId<IFlowElement> id) : IFlowElement
    {
        public EntityId<IFlowElement> Id => id;
        public IReadOnlyList<PortSpec> Ports { get; } =
        [
            new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
            new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Main),
        ];
        public TransformResult Transform(TransformInput input) =>
            new([input.Inlets[0]],
                new Composition([0.0, 0.0]),
                new Composition([0.0, 0.0]),
                new DisposedMass(
                    new Composition([0.0, 0.0]),
                    new Composition([0.0, 0.0]),
                    new Composition([0.0, 0.0])),
                new Power(0));
        public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
    }

    [Fact] // Pass 3: the content front door — a definition derives, gates, and fails loudly
    public void Content_contracts_carry_the_moddability_surface()
    {
        var esp = new TestPumpDefinition(
            new ContentId("esp-c"), new ContentId("tech-esp"), Era.E2, SlotKind.LiftSocket, 250.0);
        Assert.Equal(SlotKind.LiftSocket, esp.Fits);

        var failure = new LoadFailure(
            "base", "pumps/esp-c.json", "$.datasheet.headCurve", LoadStage.Consistency,
            "head curve must be monotone-decreasing");
        Assert.Equal(LoadStage.Consistency, failure.Stage);

        EngineStartResult refused = new EngineRefused([failure]);
        Assert.Single(((EngineRefused)refused).Reasons);   // ALL reasons, engine does not start
    }

    private sealed record TestPumpDefinition(
        ContentId Id, ContentId? RequiresTech, Era AvailableFromEra, SlotKind Fits,
        double MaxHeadMetres) : GatedDefinition(Id, RequiresTech, AvailableFromEra, Fits);

    [Fact] // Pass 3: the commit family is implementable — the only mutation path has types
    public void Commit_targets_are_implementable()
    {
        var compartment = new FakeWithdrawalTarget(new EntityRef(EntityKind.Compartment, 3));
        compartment.CommitWithdrawal(new Composition([500.0, 80.0]));
        Assert.Equal(500.0, compartment.Withdrawn[0]);
        ICommitTarget asBase = compartment;
        Assert.Equal(EntityKind.Compartment, asBase.Target.Kind);
    }

    private sealed class FakeWithdrawalTarget(EntityRef target) : IWithdrawalTarget
    {
        public EntityRef Target => target;
        public double[] Withdrawn { get; private set; } = [];
        public void CommitWithdrawal(Composition mass) =>
            Withdrawn = [.. mass.KgPerSecondByOrdinal];
    }

    [Fact] // Pass 5: the world handoff is implementable — truth flows through the
           // sink's typed records, beliefs through the Observation door only
    public void World_sink_is_implementable_and_beliefs_use_the_observation_door()
    {
        var sink = new FakeWorldSink();
        sink.AddAccumulation(new GeneratedAccumulation(
            new ContentId("play-rift-a"),
            new Polygon([new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(0, 1)]),
            DetectClass.D2,
            new AccessRequirements(DepthClass.Deep, WaterDepthClass.Onshore, Hpht: false, Tight: false, Sour: true),
            FluidForm.BlackOil,
            new ReservoirVolume(3.0e7),
            [new GeneratedCompartment(
                new ReservoirVolume(2.0e7), 0.22, 0.75,
                new Pressure(3.2e7), new Temperature(370), Length.FromFeet(9500),
                new ContentId("medium-crude"))]));
        sink.DeliverRegionalObservation(new Observation(
            new EntityRef(EntityKind.Play, 1), new ContentId("source-maturity"),
            0.6, 0.3, BeliefSpace.Linear, Provenance.Analogue));

        Assert.Single(sink.Accumulations);
        Assert.Equal(DetectClass.D2, sink.Accumulations[0].Subtlety);
        Assert.Single(sink.Observations);   // the ONLY belief path (R15-V10)
    }

    private sealed class FakeWorldSink : IWorldSink
    {
        public List<GeneratedAccumulation> Accumulations { get; } = [];
        public List<Observation> Observations { get; } = [];
        public void AddAccumulation(GeneratedAccumulation accumulation) => Accumulations.Add(accumulation);
        public void SetSurface(GeneratedSurface surface) { }
        public void AddClimateRegion(ClimateRegion region) { }
        public void AddJurisdiction(Jurisdiction jurisdiction) { }
        public void DeliverRegionalObservation(Observation observation) => Observations.Add(observation);
    }

    [Fact] // Design 03 §6: the fourteen stages, numbered as documented
    public void Tick_pipeline_has_fourteen_stages_numbered_per_design()
    {
        Assert.Equal(0, (int)StageId.Open);
        Assert.Equal(4, (int)StageId.Availability);
        Assert.Equal(5, (int)StageId.SolveFlow);
        Assert.Equal(12, (int)StageId.Objectives);
        Assert.Equal(13, (int)StageId.Close);
        Assert.Equal(14, Enum.GetValues<StageId>().Length);
    }
}
