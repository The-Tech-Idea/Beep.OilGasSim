// R4.3–R4.7 — the solver, proven against synthetic elements (SDD-002 §7).
//
// Nothing here is a well or a separator. That is the point of R4 preceding R5:
// if these pass, the solver is correct independently of any domain model, so a
// later reservoir bug cannot be mistaken for a solver bug or hide behind one.
//
// Test names carry the FV number from design 04 §9 — the canonical suite — so
// coverage can be read off the test list. An earlier draft of this file invented
// its own numbering and three tests claimed FV numbers belonging to entirely
// different checks; the names below are the design's.

using OGSim.Contracts;
using OGSim.Flow;
using OGSim.Kernel;

namespace OGSim.Flow.Tests;

public class SolverTests
{
    private static readonly SegmentContext WholeTick =
        new(DurationDays: 30, Temperature.FromCelsius(15.0), WeatherSeverity: 0.0);

    private static FlowConnection Edge(ulong from, int fromPort, ulong to, int toPort) =>
        new(new EntityId<IFlowElement>(from), new PortId(fromPort),
            new EntityId<IFlowElement>(to), new PortId(toPort));

    /// <summary>The solver takes no completion list: they are IN the network
    /// (R6.0 finding 107), so a fixture cannot hand it a completion the topology
    /// does not contain — the disagreement FV7 exposed at R4.</summary>
    private static (FlowSolver Solver, AuditTrail Trail) NewSolver()
    {
        var clock = new SimulationClock(new GameDate(1965, 1));
        var trail = new AuditTrail(clock, new AuditRetention(500));
        return (new FlowSolver(SolverSettings.Pinned, trail), trail);
    }

    // ------------------------------------------------------- FV1 conservation

    [Fact] // FV1: what enters the network leaves it, per material
    public void FV1_a_chain_conserves_mass_per_material()
    {
        var source = new Source(1, oil: 40.0, gas: 10.0);
        var sink = new Sink(3);

        var topology = new FlowTopology(
            [source, new Restrictor(2, 1000.0), sink],
            [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

        (FlowSolver solver, _) = NewSolver();
        SolveReport report = solver.Solve(WholeTick, topology);

        Assert.NotEmpty(report.Solutions);
        Assert.Equal(40.0, sink.Received[new MaterialId(0)].KgPerSecond, 9);
        Assert.Equal(10.0, sink.Received[new MaterialId(1)].KgPerSecond, 9);
    }

    [Fact] // FV1: splitting and re-merging returns exactly what went in
    public void FV1_a_split_and_merge_conserves_exactly()
    {
        var source = new Source(1, oil: 90.0, gas: 30.0);
        var sink = new Sink(5);

        var topology = new FlowTopology(
            [source, new Splitter(2, 0.25), new Manifold(4), sink],
            [
                Edge(1, 0, 2, 0),      // source -> splitter
                Edge(2, 1, 4, 0),      // splitter leg A -> manifold inlet 0
                Edge(2, 2, 4, 1),      // splitter leg B -> manifold inlet 1
                Edge(4, 2, 5, 0),      // manifold -> sink
            ]);

        (FlowSolver solver, _) = NewSolver();
        solver.Solve(WholeTick, topology);

        // Exactly, not approximately: Composition.Split takes the remainder.
        Assert.Equal(90.0, sink.Received[new MaterialId(0)].KgPerSecond, 12);
        Assert.Equal(30.0, sink.Received[new MaterialId(1)].KgPerSecond, 12);
    }

    [Fact] // R4.6: an element that loses mass is named, and the tick does not proceed
    public void FV1_an_element_that_does_not_conserve_is_a_model_fault_naming_it()
    {
        var topology = new FlowTopology(
            [new Source(1, 50.0, 0.0), new LeakyElement(2), new Sink(3)],
            [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

        (FlowSolver solver, _) = NewSolver();
        var fault = Assert.Throws<ModelFault>(() => solver.Solve(WholeTick, topology));

        Assert.Equal("INV1", fault.Fault.Rule);
        Assert.Contains("element 2", fault.Fault.Detail);
        Assert.Contains("does not conserve", fault.Fault.Detail);
    }

    [Fact] // §7's numeric guard: a non-finite rate never propagates
    public void FV1_a_non_finite_rate_is_refused_immediately()
    {
        var topology = new FlowTopology(
            [new Source(1, double.NaN, 0.0), new Sink(2)],
            [Edge(1, 0, 2, 0)]);

        (FlowSolver solver, _) = NewSolver();
        Assert.Throws<InvariantFault>(() => solver.Solve(WholeTick, topology));
    }

    [Theory] // FV1 on RANDOMISED networks — the shape the design asks for
    [InlineData(20240101UL)]
    [InlineData(19650301UL)]
    [InlineData(77777777UL)]
    public void FV1_randomised_networks_conserve_globally(ulong seed)
    {
        // 200 randomly-shaped networks per seed. The design asks for 1,000 ticks;
        // R4 has no tick loop yet (that is R7), so this proves the per-solve half
        // of FV1 and the tick-loop half is verified where the loop lives.
        IRandomStream stream = new RandomSource(seed).Stream(StreamId.WorldGen);

        for (int trial = 0; trial < 200; trial++)
        {
            RandomNetwork built = RandomNetwork.Generate(stream, trial);

            (FlowSolver solver, _) = NewSolver();
            SolveReport report = solver.Solve(WholeTick, built.Topology);

            // Network-wide: everything sourced left as disposal or fuel. No
            // element may hold mass back between segments — a network that
            // balances element by element can still leak at the boundary if a
            // sink quietly swallows what it received.
            for (int material = 0; material < Synthetic.MaterialCount; material++)
            {
                var id = new MaterialId(material);
                double sourced = 0.0, left = 0.0;

                foreach (ElementSolution solution in report.Solutions)
                {
                    TransformResult r = solution.Converged;
                    sourced += r.Sourced[id].KgPerSecond;
                    left += r.FuelConsumed[id].KgPerSecond
                          + r.Disposed.Flared[id].KgPerSecond
                          + r.Disposed.Vented[id].KgPerSecond
                          + r.Disposed.Discharged[id].KgPerSecond;
                }

                Assert.True(Math.Abs(sourced - left) <= Math.Max(1e-9, 1e-12 * sourced),
                    $"trial {trial} seed {seed} material {material}: " +
                    $"sourced {sourced}, left {left}");
            }
        }
    }

    // --------------------------------------------------- FV3 operating point

    [Fact] // S1: a completion's rate approaches its operating point under damping
    public void FV3_a_completion_converges_toward_its_operating_point()
    {
        var completion = new SyntheticCompletion(1, productivityIndex: 1e-9, reservoirBar: 200.0);

        var topology = new FlowTopology(
            [completion, new Sink(2)],
            [Edge(1, 0, 2, 0)]);

        (FlowSolver solver, _) = NewSolver();
        SolveReport report = solver.Solve(WholeTick, topology);

        CompletionState state = Assert.Single(report.CompletionStates);
        Assert.Equal(1UL, state.Completion.Value);
        Assert.True(state.Rate.CubicMetresPerSecond > 0.0, "a live completion should flow");
        Assert.True(report.OuterIterations >= 1);

        // The converged point satisfies the IPR at the converged backpressure —
        // recomputed here from the report, not read back from the solver.
        //
        // To the solver's OWN tolerance, deliberately: S5 declares convergence at
        // 1e-4 relative, so demanding more would be asserting something the
        // algorithm never promised and would fail the moment the tolerance moved.
        var independent = (Flowing)completion.SolveOperatingPoint(state.WellheadBackpressure);
        double ipr = independent.Rate.CubicMetresPerSecond;

        Assert.True(
            Math.Abs(ipr - state.Rate.CubicMetresPerSecond) / ipr
                < SolverSettings.Pinned.RateTolerance,
            $"converged rate {state.Rate.CubicMetresPerSecond} is not on the IPR ({ipr})");
    }

    [Fact] // R6-V6: DEAD is not a zero rate, and a dead well simply contributes none
    public void FV3_a_dead_completion_flows_nothing_without_failing_the_solve()
    {
        // Reservoir below the backpressure it is asked to lift against.
        var dead = new SyntheticCompletion(1, productivityIndex: 1e-9, reservoirBar: 0.5);

        var topology = new FlowTopology(
            [dead, new Sink(2)],
            [Edge(1, 0, 2, 0)]);

        (FlowSolver solver, _) = NewSolver();
        SolveReport report = solver.Solve(WholeTick, topology);

        Assert.Equal(0.0, Assert.Single(report.CompletionStates).Rate.CubicMetresPerSecond, 12);
        Assert.Empty(report.ForcedShutIns);      // dead is not a solver failure
    }

    // ----------------------------------------------- FV4 bottleneck attribution

    [Fact] // FV4: the solver names the undersized element and the deferred volume
    public void FV4_a_binding_capacity_defers_volume_and_names_the_element()
    {
        // Wide open the completion wants ~85 kg/s; the restrictor passes 30.
        var well = new SyntheticCompletion(1, productivityIndex: 5e-9, reservoirBar: 200.0);

        var topology = new FlowTopology(
            [well, new Restrictor(2, capacityKgPerSecond: 30.0), new Sink(3)],
            [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

        (FlowSolver solver, _) = NewSolver();
        SolveReport report = solver.Solve(WholeTick, topology);

        (EntityId<IFlowElement> element, ConstraintKind kind, Mass deferred) =
            Assert.Single(report.Deferrals);

        Assert.Equal(2UL, element.Value);
        Assert.Equal(ConstraintKind.TotalCapacity, kind);

        // The cap bound: the network delivered capacity, not demand.
        CompletionState state = Assert.Single(report.CompletionStates);
        Assert.Equal(30.0,
            state.Rate.CubicMetresPerSecond * SyntheticCompletion.DensityKgPerCubicMetre, 6);

        // THE ANALYTIC ANSWER (FV4's real requirement): what the well wanted at
        // the converged backpressure, less what the restrictor passed, over the
        // segment. Computed here from the well's own IPR — independently of
        // anything the solver recorded.
        var wanted = (Flowing)well.SolveOperatingPoint(state.WellheadBackpressure);
        double analytic =
            (wanted.Rate.CubicMetresPerSecond * SyntheticCompletion.DensityKgPerCubicMetre - 30.0)
            * WholeTick.DurationDays * 86_400.0;

        Assert.Equal(analytic, deferred.Kilograms, precision: 3);
    }

    [Fact] // A capacity that is not exceeded defers nothing
    public void FV4_an_unbound_capacity_produces_no_deferral()
    {
        var topology = new FlowTopology(
            [new Source(1, 10.0, 0.0), new Restrictor(2, 1000.0), new Sink(3)],
            [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

        (FlowSolver solver, _) = NewSolver();
        Assert.Empty(solver.Solve(WholeTick, topology).Deferrals);
    }

    [Fact] // FV4: the deferred volume must not depend on how long convergence took
    public void FV4_the_deferred_volume_is_independent_of_the_iteration_count()
    {
        static Mass Deferred(double damping)
        {
            var well = new SyntheticCompletion(1, productivityIndex: 5e-9, reservoirBar: 200.0);
            var topology = new FlowTopology(
                [well, new Restrictor(2, 30.0), new Sink(3)],
                [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

            var clock = new SimulationClock(new GameDate(1965, 1));
            var solver = new FlowSolver(
                SolverSettings.Pinned with { Damping = damping },
                new AuditTrail(clock, new AuditRetention(500)));

            return solver.Solve(WholeTick, topology).Deferrals.Single().Deferred;
        }

        // Heavier damping takes more iterations to reach the same answer. If
        // deferrals accumulated per iteration — the shape the first draft had —
        // these two would differ by a factor of several.
        Assert.Equal(Deferred(0.5).Kilograms, Deferred(0.1).Kilograms, precision: 3);
    }

    [Fact] // A constraint nothing can relieve is a composition fault, not a solver failure
    public void FV4_an_unrelievable_constraint_names_the_element_rather_than_looping()
    {
        // The Source is not a completion, so S3 has no lever: the load can never
        // fall below the capacity however many iterations it is given.
        var topology = new FlowTopology(
            [new Source(1, oil: 50.0, gas: 0.0), new Restrictor(2, 30.0), new Sink(3)],
            [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

        (FlowSolver solver, _) = NewSolver();
        var fault = Assert.Throws<ModelFault>(() => solver.Solve(WholeTick, topology));

        Assert.Contains("element 2", fault.Fault.Detail);
        Assert.Contains("cannot be relieved", fault.Fault.Detail);
    }

    // ------------------------------------------------ FV5 backpressure propagation

    [Fact] // FV5: pressure downstream measurably reduces withdrawal upstream
    public void FV5_a_higher_downstream_drop_reduces_reservoir_withdrawal()
    {
        static double RateWithDrop(double dropBar)
        {
            var well = new SyntheticCompletion(1, productivityIndex: 1e-9, reservoirBar: 200.0);
            var topology = new FlowTopology(
                [well, new Restrictor(2, 1e9) { DropBar = dropBar }, new Sink(3)],
                [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

            var clock = new SimulationClock(new GameDate(1965, 1));
            var solver = new FlowSolver(
                SolverSettings.Pinned, new AuditTrail(clock, new AuditRetention(500)));

            return solver.Solve(WholeTick, topology)
                         .CompletionStates.Single().Rate.CubicMetresPerSecond;
        }

        // S4 carries the extra drop back to the wellhead, S1 sees less drawdown.
        // Note the SIGN: a bigger drop across the restrictor means the well must
        // deliver at a HIGHER inlet pressure, so it produces less.
        double gentle = RateWithDrop(5.0);
        double harsh = RateWithDrop(120.0);

        Assert.True(harsh < gentle,
            $"backpressure did not propagate: {harsh} not less than {gentle}");
    }

    /// <summary>
    /// SDD-002 §7 S4, finding 158. A controller HOLDS its inlet: the well behind
    /// it sees the set point, not the network's terminal sink boundary.
    ///
    /// <para>Before the distinction existed, the solver inferred every element's
    /// pressure from what the stream lost crossing it — so a vessel emitting at
    /// its set point looked like a drop of "whatever arrived minus the set
    /// point", which grows with the pressure upstream. A separator fed from a
    /// completion at reservoir pressure demanded an inlet high enough to shut the
    /// well it exists to receive from.</para>
    /// </summary>
    [Fact]
    public void FV5_a_controller_holds_the_wellhead_at_its_set_point()
    {
        var well = new SyntheticCompletion(1, productivityIndex: 1e-9, reservoirBar: 200.0);
        var topology = new FlowTopology(
            [well, new Controller(2, setPointBar: 30.0), new Sink(3)],
            [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

        var clock = new SimulationClock(new GameDate(1965, 1));
        var solver = new FlowSolver(
            SolverSettings.Pinned, new AuditTrail(clock, new AuditRetention(500)));

        CompletionState state = solver.Solve(WholeTick, topology).CompletionStates.Single();

        Assert.Equal(30.0e5, state.WellheadBackpressure.Pascals, precision: 0);
    }

    /// <summary>
    /// A FLOOR, not a fixed value — and R8-V5 is why. A controller holds pressure
    /// UP; it is a restriction, not a pump. When something downstream demands
    /// more than the set point, the valve is wide open and the demand passes
    /// through.
    ///
    /// <para>Pinning the set point outright would make every facility a wall: a
    /// filling tank could never back up through the separator ahead of it, and
    /// the one verification the whole backpressure chain exists for would become
    /// unpassable.</para>
    /// </summary>
    [Fact]
    public void R8V5_a_demand_above_the_set_point_passes_through_the_controller()
    {
        static double BackpressureBehind(double dropBar)
        {
            var well = new SyntheticCompletion(1, productivityIndex: 1e-9, reservoirBar: 200.0);

            // The restrictor is DOWNSTREAM of the controller, so its drop is
            // exactly the "something backing up" case.
            var topology = new FlowTopology(
                [
                    well,
                    new Controller(2, setPointBar: 30.0),
                    new Restrictor(3, 1e9) { DropBar = dropBar },
                    new Sink(4),
                ],
                [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0), Edge(3, 1, 4, 0)]);

            var clock = new SimulationClock(new GameDate(1965, 1));
            var solver = new FlowSolver(
                SolverSettings.Pinned, new AuditTrail(clock, new AuditRetention(500)));

            return solver.Solve(WholeTick, topology)
                         .CompletionStates.Single().WellheadBackpressure.Pascals;
        }

        // Below the set point: the controller holds, and the well sees 30 bar.
        Assert.Equal(30.0e5, BackpressureBehind(5.0), precision: 0);

        // Above it: the tank is filling, the valve is open, and the well feels it.
        Assert.True(BackpressureBehind(120.0) > 30.0e5,
            "a demand above the set point must reach the wellhead, or a full tank " +
            "could never shut a well in (R8-V5)");
    }

    [Fact] // S4: a pressure-decoupled completion holds its rate through the swing
    public void FV5_a_choked_completion_is_not_moved_by_backpressure()
    {
        static double RateWithDrop(double dropBar)
        {
            var well = new SyntheticCompletion(1, 1e-9, 200.0) { IsPressureDecoupled = true };
            var topology = new FlowTopology(
                [well, new Restrictor(2, 1e9) { DropBar = dropBar }, new Sink(3)],
                [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

            var clock = new SimulationClock(new GameDate(1965, 1));
            var solver = new FlowSolver(
                SolverSettings.Pinned, new AuditTrail(clock, new AuditRetention(500)));

            return solver.Solve(WholeTick, topology)
                         .CompletionStates.Single().Rate.CubicMetresPerSecond;
        }

        // This is what damps oscillation on a shared line (SDD-002 §7 S4).
        Assert.Equal(RateWithDrop(5.0), RateWithDrop(120.0), precision: 12);
    }

    // ------------------------------------------------------- FV8 determinism

    [Fact] // FV8: the same inputs give bit-identical outputs
    public void FV8_two_identical_solves_agree_to_the_bit()
    {
        static SolveReport Run()
        {
            var well = new SyntheticCompletion(1, 5e-9, 200.0);
            var topology = new FlowTopology(
                [well, new Restrictor(2, 30.0), new Manifold(3), new Sink(4)],
                [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0), Edge(3, 2, 4, 0)]);

            var clock = new SimulationClock(new GameDate(1965, 1));
            var solver = new FlowSolver(
                SolverSettings.Pinned, new AuditTrail(clock, new AuditRetention(500)));

            return solver.Solve(WholeTick, topology);
        }

        SolveReport a = Run(), b = Run();

        Assert.Equal(a.OuterIterations, b.OuterIterations);
        Assert.Equal(a.CompletionStates[0].Rate.CubicMetresPerSecond,
                     b.CompletionStates[0].Rate.CubicMetresPerSecond);
        Assert.Equal(a.CompletionStates[0].WellheadBackpressure.Pascals,
                     b.CompletionStates[0].WellheadBackpressure.Pascals);
        Assert.Equal(a.Deferrals.Single().Deferred.Kilograms,
                     b.Deferrals.Single().Deferred.Kilograms);
    }

    // ------------------------------------------------- FV9 convergence / ladder

    [Fact] // FV9: budget exhaustion engages the ladder, audited, and the tick completes
    public void FV9_the_shut_in_ladder_lets_the_tick_complete()
    {
        // An oscillator never settles: its rate flips regardless of backpressure,
        // so S5's rate tolerance can never be met and the budget must run out.
        var oscillator = new OscillatingCompletion(1);

        var topology = new FlowTopology(
            [oscillator, new Sink(2)],
            [Edge(1, 0, 2, 0)]);

        (FlowSolver solver, AuditTrail trail) = NewSolver();
        SolveReport report = solver.Solve(WholeTick, topology);

        // The ladder shut it in, and the solve COMPLETED — ending the game on a
        // numerics failure would punish the player for the engine's limits
        // (design 04 §4.0b).
        ForcedShutIn shutIn = Assert.Single(report.ForcedShutIns);
        Assert.Equal(1UL, shutIn.Completion.Value);

        // Audited, with the cause, so the shut-in is explicable.
        IReadOnlyList<AuditEntry> audited =
            trail.Query(new AuditQuery(null, AuditCategory.ForcedShutIn, null, null));
        Assert.Single(audited);
        Assert.Equal("solver-stability", audited[0].Data["cause"].Value);
    }

    // -------------------------------------------------------- FV10 allocation

    [Fact] // FV10: commingled production allocates back to compartments exactly
    public void FV10_a_commingle_allocates_back_in_mass_proportion()
    {
        var manifold = new Manifold(4);

        var topology = new FlowTopology(
            [
                new Source(1, 75.0, 0.0) { Compartment = 10 },
                new Source(2, 25.0, 0.0) { Compartment = 20 },
                manifold, new Sink(5),
            ],
            [Edge(1, 0, 4, 0), Edge(2, 0, 4, 1), Edge(4, 2, 5, 0)]);

        (FlowSolver solver, _) = NewSolver();
        SolveReport report = solver.Solve(WholeTick, topology);

        ElementSolution merged = report.Solutions.Single(s => s.Element.Value == 4);
        Allocation blended = merged.Converged.Outlets[0].Provenance;

        Assert.Equal(2, blended.Shares.Length);
        Assert.Equal(10UL, blended.Shares[0].Compartment.Value);
        Assert.Equal(0.75, blended.Shares[0].Fraction, 12);
        Assert.Equal(20UL, blended.Shares[1].Compartment.Value);
        Assert.Equal(0.25, blended.Shares[1].Fraction, 12);

        // Exactly, because an allocation that sums to 1 ± ε would misallocate
        // revenue between partners (SDD-002 §3).
        Assert.Equal(1.0, blended.Shares[0].Fraction + blended.Shares[1].Fraction, 15);
    }

    [Fact] // One compartment in, one share out — the arithmetic still had to run
    public void FV10_provenance_survives_a_single_source_commingle()
    {
        var topology = new FlowTopology(
            [new Source(1, 75.0, 0.0), new Source(2, 25.0, 0.0), new Manifold(4), new Sink(5)],
            [Edge(1, 0, 4, 0), Edge(2, 0, 4, 1), Edge(4, 2, 5, 0)]);

        (FlowSolver solver, _) = NewSolver();
        SolveReport report = solver.Solve(WholeTick, topology);

        Allocation blended = report.Solutions
            .Single(s => s.Element.Value == 4).Converged.Outlets[0].Provenance;

        (EntityRef compartment, double fraction) = Assert.Single(blended.Shares);
        Assert.Equal(1UL, compartment.Value);
        Assert.Equal(1.0, fraction, 12);
    }

    // ----------------------------------------------------- S2's rate channel

    [Fact] // §5: null SolvedRate means "not a completion", never "rate zero"
    public void S2_hands_the_solved_rate_only_to_completions()
    {
        var well = new SyntheticCompletion(1, 1e-9, 200.0);
        var probe = new RateProbe(2);

        var topology = new FlowTopology(
            [well, probe, new Sink(3)],
            [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

        (FlowSolver solver, _) = NewSolver();
        solver.Solve(WholeTick, topology);

        Assert.Null(probe.LastSolvedRate);
    }

    [Fact] // A shut-in completion IS given a rate — zero — and must produce nothing
    public void S2_a_shut_in_completion_is_given_zero_rather_than_null()
    {
        var oscillator = new OscillatingCompletion(1);

        var topology = new FlowTopology(
            [oscillator, new Sink(2)],
            [Edge(1, 0, 2, 0)]);

        (FlowSolver solver, _) = NewSolver();
        SolveReport report = solver.Solve(WholeTick, topology);

        Assert.Single(report.ForcedShutIns);
        Assert.NotNull(oscillator.LastSolvedRate);
        Assert.Equal(0.0, oscillator.LastSolvedRate!.Value.CubicMetresPerSecond, 12);
    }

    // ------------------------------------------------------------- fixtures

    [Fact] // A malformed network never reaches the solve
    public void A_malformed_network_is_refused_before_any_solve()
    {
        var topology = new FlowTopology(
            [new Restrictor(1, 100.0), new Restrictor(2, 100.0)],
            [Edge(1, 1, 2, 0), Edge(2, 1, 1, 0)]);      // cycle

        (FlowSolver solver, _) = NewSolver();
        Assert.Throws<InvariantFault>(() => solver.Solve(WholeTick, topology));
    }

    /// <summary>Discards a tenth of its inlet without accounting for it.</summary>
    private sealed class LeakyElement(ulong id) : IFlowElement
    {
        public EntityId<IFlowElement> Id { get; } = new(id);

        public IReadOnlyList<PortSpec> Ports { get; } =
            [Synthetic.Inlet(0), Synthetic.Outlet(1)];

        public TransformResult Transform(TransformInput input)
        {
            MaterialStream inlet = input.Inlets[0];
            (MaterialStream kept, _) = inlet.Split(0.9);    // the other 0.1 vanishes
            return new TransformResult(
                [kept], Synthetic.Zero, Synthetic.Zero, Synthetic.NoDisposal, new Power(0.0));
        }

        public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
    }

    /// <summary>Passes everything through and remembers what it was handed.</summary>
    private sealed class RateProbe(ulong id) : IFlowElement
    {
        public EntityId<IFlowElement> Id { get; } = new(id);
        public ReservoirRate? LastSolvedRate { get; private set; }

        public IReadOnlyList<PortSpec> Ports { get; } =
            [Synthetic.Inlet(0), Synthetic.Outlet(1)];

        public TransformResult Transform(TransformInput input)
        {
            LastSolvedRate = input.SolvedRate;
            return new TransformResult(
                [input.Inlets.Count > 0 ? input.Inlets[0] : Synthetic.Stream(0.0, 0.0)],
                Synthetic.Zero, Synthetic.Zero, Synthetic.NoDisposal, new Power(0.0));
        }

        public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
    }

    /// <summary>Alternates its rate on every call, so S5 can never be satisfied.</summary>
    private sealed class OscillatingCompletion(ulong id) : ICompletion
    {
        private bool _high;

        public EntityId<IFlowElement> Id { get; } = new(id);
        public EntityId<ICompletion> CompletionId { get; } = new(id);
        public EntityId<IWellbore> Wellbore { get; } = new(id);
        public ILiftMethod? Lift => null;
        public bool IsPressureDecoupled => false;
        public ReservoirRate? LastSolvedRate { get; private set; }

        public IReadOnlyList<Perforation> Perforations { get; } =
            [new Perforation(new EntityId<IReservoirCompartmentEntity>(1),
                             new Length(0.0), new Length(1.0), Skin: 0.0, Isolated: false)];

        public IReadOnlyList<PortSpec> Ports { get; } = [Synthetic.Outlet(0)];

        public OperatingPoint SolveOperatingPoint(Pressure wellheadBackpressure)
        {
            _high = !_high;
            return new Flowing(new ReservoirRate(_high ? 1.0 : 100.0), Pressure.FromBar(150.0));
        }

        public TransformResult Transform(TransformInput input)
        {
            LastSolvedRate = input.SolvedRate;

            Composition produced =
                Synthetic.Comp(input.SolvedRate?.CubicMetresPerSecond ?? 0.0, 0.0);

            return new TransformResult(
                [new MaterialStream(produced, Pressure.FromBar(150.0),
                                    Temperature.FromCelsius(60.0), Synthetic.OneCompartment)],
                produced, Synthetic.Zero, Synthetic.NoDisposal, new Power(0.0));
        }

        public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
    }
}
