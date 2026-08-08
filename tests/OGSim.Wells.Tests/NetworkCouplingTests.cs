// R6-V14 and R6-V9 through the REAL SOLVER — the phase's most valuable tests
// (R6 §2.3, §4).
//
// R6-V14 is an emergent consequence the player will meet, it is written down
// nowhere as a rule, and if it does not appear then the network coupling is not
// real. Nothing in this file, in OGSim.Wells or in OGSim.Flow says "a strong
// well suppresses a weak one on a shared line". It has to fall out.
//
// This is also where the "one engine" claim is settled. R4's solver was written
// and proved months before any well existed, against synthetic elements with no
// domain meaning. If accommodating a real completion needed the solver changed,
// the claim was false. It needed one thing — R6.0's finding 107, which REMOVED
// a parameter rather than adding one.

using OGSim.Contracts;
using OGSim.Flow;
using OGSim.Kernel;
using OGSim.Wells;

namespace OGSim.Wells.Tests;

public class NetworkCouplingTests
{
    /// <summary>Oil and gas, matching the completion the shipped catalogue
    /// builds — a synthetic element narrower than the wells feeding it would
    /// fail SDD-002 §2's ordinal check rather than the coupling under test.</summary>
    private const int Materials = 2;

    private static readonly SegmentContext WholeTick =
        new(DurationDays: 30, Temperature.FromCelsius(15.0), WeatherSeverity: 0.0);

    private static Completion Well(ulong id, double reservoirBarA, double permeabilityM2)
    {
        var outflow = new HydrostaticFrictionOutflowModel(
            Fixtures.Tubing(), Density.FromSpecificGravity(0.85), lift: null);

        return new Completion(
            new EntityId<ICompletion>(id),
            new EntityId<IWellbore>(id),
            [Fixtures.Perf()],
            new CompositeInflowModel(
                Fixtures.Conditions(permeabilityM2: permeabilityM2, bubblePointPa: 5.0e6)),
            outflow,
            new CompletionFluid(
                Density.FromSpecificGravity(0.85),
                new FormationVolumeFactor(1.2),
                Allocation.FromSingle(new EntityRef(EntityKind.Compartment, id)),
                new Pressure(reservoirBarA * 1e5),
                Temperature.FromCelsius(80.0),
                Fx.GasDensity,
                Fx.NoSolutionGas),
            ChokeSetting.Open,
            oilOrdinal: 0,
            gasOrdinal: 1,
            materialCount: 2,
            lift: null);
    }

    private static FlowConnection Edge(ulong from, int fromPort, ulong to, int toPort) =>
        new(new EntityId<IFlowElement>(from), new PortId(fromPort),
            new EntityId<IFlowElement>(to), new PortId(toPort));

    private static (FlowSolver Solver, AuditTrail Trail) NewSolver()
    {
        var clock = new SimulationClock(new GameDate(1965, 1));
        var trail = new AuditTrail(clock, new AuditRetention(500));
        return (new FlowSolver(SolverSettings.Pinned, trail), trail);
    }

    private static double RateOf(SolveReport report, ulong id)
    {
        foreach (CompletionState state in report.CompletionStates)
            if (state.Completion.Value == id) return state.Rate.CubicMetresPerSecond;

        throw new InvalidOperationException($"no completion {id} in the report");
    }

    // ---------------------------------------------------------- R6-V14

    [Fact] // R6-V14: a strong well on a shared line suppresses a weaker one
    public void R6V14_a_strong_well_reduces_a_weak_well_on_the_same_line()
    {
        // The weak well alone, then the same well with a strong neighbour tied
        // into the same manifold. Nothing else changes.
        double alone = Solve(strongNeighbour: false);
        double shared = Solve(strongNeighbour: true);

        // The strong well raises the pressure in the common line; that
        // backpressure reaches the weak well's wellhead, raises its Pwf, and
        // takes away its drawdown. No rule anywhere says any of that.
        Assert.True(shared < alone,
            $"commingling did not suppress the weak well: {shared} not below {alone}");

        static double Solve(bool strongNeighbour)
        {
            Completion weak = Well(1, reservoirBarA: 210.0, permeabilityM2: 2.0e-14);

            // The manifold's second inlet is simply LEFT UNFED in the solo case.
            // A first draft put a stub element there to keep the topology
            // identical, and the stub's own outlet pressure created a 170-bar
            // drop the manifold then imposed on the well — suppressing it far
            // harder than any neighbour would have.
            var elements = new List<IFlowElement>
                { weak, new Manifold(3), new Restrictor(4), new Sink(5) };
            var edges = new List<FlowConnection>
                { Edge(1, 0, 3, 0), Edge(3, 2, 4, 0), Edge(4, 1, 5, 0) };

            if (strongNeighbour)
            {
                elements.Add(Well(2, reservoirBarA: 400.0, permeabilityM2: 5.0e-13));
                edges.Add(Edge(2, 0, 3, 1));
            }

            (FlowSolver solver, _) = NewSolver();
            return RateOf(solver.Solve(WholeTick, new FlowTopology(elements, edges)), 1);
        }
    }

    [Fact] // R6-V14: pushed far enough, the weak well is suppressed to nothing
    public void R6V14_a_sufficiently_strong_neighbour_can_shut_the_weak_well_in()
    {
        Completion weak = Well(1, reservoirBarA: 175.0, permeabilityM2: 1.0e-14);
        Completion strong = Well(2, reservoirBarA: 450.0, permeabilityM2: 1.0e-12);

        var topology = new FlowTopology(
            [weak, strong, new Manifold(3), new Restrictor(4)],
            [Edge(1, 0, 3, 0), Edge(2, 0, 3, 1), Edge(3, 2, 4, 0)]);

        (FlowSolver solver, _) = NewSolver();
        SolveReport report = solver.Solve(WholeTick, topology);

        // The strong well keeps producing; the weak one is at or near zero,
        // having been pushed off its own IPR by its neighbour's backpressure.
        Assert.True(RateOf(report, 2) > 0.0, "the strong well should still flow");
        Assert.True(RateOf(report, 1) < RateOf(report, 2) * 0.05,
            "the weak well was not materially suppressed");
    }

    // ----------------------------------------------------------- R6-V9

    [Fact] // R6-V9: backpressure from downstream reaches the reservoir
    public void R6V9_a_downstream_restriction_reduces_reservoir_withdrawal()
    {
        double gentle = Solve(resistance: 1.0e3);
        double harsh = Solve(resistance: 5.0e6);

        // FV5 from R4 proved this with synthetic elements; here the thing being
        // pushed back on is a real IPR, and the chain is the design's:
        // restriction → manifold pressure → wellhead → Pwf → drawdown → rate.
        Assert.True(harsh < gentle,
            $"the restriction did not reach the reservoir: {harsh} not below {gentle}");

        static double Solve(double resistance)
        {
            Completion well = Well(1, reservoirBarA: 300.0, permeabilityM2: 1.0e-13);

            var topology = new FlowTopology(
                [well, new Restrictor(2) { Resistance = resistance }, new Sink(3)],
                [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

            (FlowSolver solver, _) = NewSolver();
            return RateOf(solver.Solve(WholeTick, topology), 1);
        }
    }

    // --------------------------------------------------- conservation

    [Fact] // FV1 with a REAL completion: the well conserves like any element
    public void FV1_a_real_completion_conserves_through_the_network()
    {
        Completion well = Well(1, reservoirBarA: 300.0, permeabilityM2: 1.0e-13);

        var topology = new FlowTopology(
            [well, new Restrictor(2), new Sink(3)],
            [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

        (FlowSolver solver, _) = NewSolver();
        SolveReport report = solver.Solve(WholeTick, topology);

        // The completion sources mass; the sink discharges it. That the solver's
        // per-element INV1 check passed at all is the real assertion — it runs
        // after every transform, so a completion that lost mass would have
        // faulted before this line.
        double sourced = 0.0, disposed = 0.0;
        foreach (ElementSolution solution in report.Solutions)
        {
            var id = new MaterialId(0);
            sourced += solution.Converged.Sourced[id].KgPerSecond;
            disposed += solution.Converged.Disposed.Discharged[id].KgPerSecond;
        }

        Assert.True(sourced > 0.0, "the well produced nothing");
        Assert.Equal(sourced, disposed, precision: 9);
    }

    /// <summary>
    /// A flowline: <c>ΔP = k·ṁ²</c>, the rate-dependent drop of any real line.
    ///
    /// <para><b>R6-V14 does not emerge without this, and that is the finding.</b>
    /// The mechanism by which one well suppresses another is entirely
    /// rate-mediated — more total throughput, more drop across the shared line,
    /// higher manifold pressure, higher wellhead pressure on BOTH wells. An
    /// element whose drop is a constant transmits nothing between its feeders: a
    /// first draft of this fixture used R4's fixed-ΔP restrictor and both wells
    /// solved to exactly the rates they had alone, to the last digit. Nothing was
    /// wrong with the solver; there was simply no channel for the interaction.</para>
    ///
    /// <para>R4's synthetic restrictor was right for what it proved — FV5
    /// compares two different fixed settings — and could never have shown this.
    /// R8 supplies the real hydraulics; this is the minimum that makes the
    /// coupling exist at all.</para>
    /// </summary>
    private sealed class Restrictor(ulong id) : IFlowElement
    {
        public EntityId<IFlowElement> Id { get; } = new(id);

        /// <summary>Pa per (kg/s)². Sized so a few kg/s costs a few bar.</summary>
        public double Resistance { get; init; } = 5.0e4;

        public IReadOnlyList<PortSpec> Ports { get; } =
        [
            new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
            new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Main),
        ];

        public TransformResult Transform(TransformInput input)
        {
            if (input.Inlets.Count == 0)
                return new TransformResult(
                    [new MaterialStream(Composition.Zero(Materials), Pressure.FromBar(1.0),
                                        Temperature.FromCelsius(60.0), OneCompartment)],
                    Composition.Zero(Materials), Composition.Zero(Materials), NoDisposal, new Power(0.0));

            MaterialStream inlet = input.Inlets[0];
            double massRate = inlet.MassRates.Total.KgPerSecond;

            var outlet = inlet with
            {
                P = new Pressure(Math.Max(0.0,
                    inlet.P.Pascals - Resistance * massRate * massRate)),
            };

            return new TransformResult(
                [outlet], Composition.Zero(Materials), Composition.Zero(Materials), NoDisposal, new Power(0.0));
        }

        public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
    }

    /// <summary>Where mass leaves the network, discharged so the balance closes.</summary>
    private sealed class Sink(ulong id) : IFlowElement
    {
        public EntityId<IFlowElement> Id { get; } = new(id);

        public IReadOnlyList<PortSpec> Ports { get; } =
            [new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main)];

        public TransformResult Transform(TransformInput input)
        {
            Composition received = input.Inlets.Count > 0
                ? input.Inlets[0].MassRates
                : Composition.Zero(Materials);

            return new TransformResult(
                [], Composition.Zero(Materials), Composition.Zero(Materials),
                new DisposedMass(Composition.Zero(Materials), Composition.Zero(Materials), received),
                new Power(0.0));
        }

        public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
    }

    private static Allocation OneCompartment { get; } =
        Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1));

    private static DisposedMass NoDisposal { get; } =
        new(Composition.Zero(Materials), Composition.Zero(Materials), Composition.Zero(Materials));

    /// <summary>Two inlets, one outlet, taking the lower inlet pressure — the
    /// shared line R6-V14 is about.</summary>
    private sealed class Manifold(ulong id) : IFlowElement
    {
        public EntityId<IFlowElement> Id { get; } = new(id);

        public IReadOnlyList<PortSpec> Ports { get; } =
        [
            new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
            new PortSpec(new PortId(1), PortDirection.Inlet, PortRole.Main),
            new PortSpec(new PortId(2), PortDirection.Outlet, PortRole.Main),
        ];

        public TransformResult Transform(TransformInput input)
        {
            Composition total = Composition.Zero(Materials);
            double pressure = double.MaxValue;

            var parts = new List<(Allocation Part, Mass Weight)>(input.Inlets.Count);
            for (int i = 0; i < input.Inlets.Count; i++)
            {
                total = total.Plus(input.Inlets[i].MassRates);
                pressure = Math.Min(pressure, input.Inlets[i].P.Pascals);
                parts.Add((input.Inlets[i].Provenance,
                           new Mass(input.Inlets[i].MassRates.Total.KgPerSecond)));
            }

            if (input.Inlets.Count == 0) pressure = Pressure.FromBar(40.0).Pascals;

            Allocation blended = total.Total.KgPerSecond > 0.0
                ? Allocation.Blend(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(parts))
                : Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1));

            return new TransformResult(
                [new MaterialStream(total, new Pressure(pressure),
                                    Temperature.FromCelsius(60.0), blended)],
                Composition.Zero(Materials), Composition.Zero(Materials),
                new DisposedMass(Composition.Zero(Materials), Composition.Zero(Materials), Composition.Zero(Materials)),
                new Power(0.0));
        }

        public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
    }
}
