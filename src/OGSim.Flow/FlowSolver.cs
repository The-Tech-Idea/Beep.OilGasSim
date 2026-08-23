// R4.3–R4.7 — the one flow engine (SDD-002 §7, design 04).
//
// The algorithm is pinned step by step in SDD-002 §7 and this file implements
// exactly that: S0 seed, S1 wells, S2 forward, S3 throttle, S4 backward,
// S5 converged?, S6 budget → shut-in ladder. Every tolerance, the damping
// factor and the ladder's tie-break come from the SDD rather than from taste.
//
// The solver knows only IFlowElement. It never asks what an element IS, only
// what its transform returns and which constraints it reports — which is why
// adding equipment never touches this file (design 04 §1).

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Flow;

/// <summary>Solver tunables. Content-supplied, defaults as pinned in SDD-002 §7.</summary>
public sealed record SolverSettings(
    double Damping,                    // λ, S1
    double RateTolerance,              // S5 relative
    double PressureTolerancePa,        // S5
    double RateFloor,                  // q_floor, S5
    int MaxOuterIterations,            // S6
    double SinkBoundaryPressurePa)     // S4's terminal condition
{
    /// <summary>SDD-002 §7's pinned values. Not a fallback — the shipped policy.</summary>
    public static SolverSettings Pinned { get; } = new(
        Damping: 0.5,
        RateTolerance: 1e-4,
        PressureTolerancePa: 1_000.0,
        RateFloor: 1e-8,
        MaxOuterIterations: 200,
        // Atmospheric: a network's terminal sink discharges to somewhere, and
        // zero absolute would let a completion flow against a vacuum.
        SinkBoundaryPressurePa: 101_325.0);
}

public sealed class FlowSolver : IFlowSolver
{
    private readonly SolverSettings _settings;
    private readonly IAuditTrail _audit;

    public FlowSolver(SolverSettings settings, IAuditTrail audit)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(audit);

        _settings = settings;
        _audit = audit;
    }

    public SolveReport Solve(SegmentContext segment, FlowTopology topology)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(topology);

        if (FlowNetwork.Build(topology) is not NetworkBuilt built)
            throw new InvariantFault("SDD-002 §10", null,
                "network validation failed; a malformed network is a composition fault " +
                "and must be caught before a solve is attempted");

        FlowNetwork network = built.Network;

        // The completions are IN the network, because ICompletion : IFlowElement.
        // Taking them as a second constructor argument — which this solver did
        // until R6.0's finding 107 — let the two disagree: a completion the
        // solver capped need not be the object the network took its mass from.
        // Found in the network, in the order the network already fixed, that is
        // no longer expressible.
        IReadOnlyList<ICompletion> completions = CompletionsIn(network);

        var state = new SolveState(completions, _settings);
        var forcedShutIns = new List<ForcedShutIn>();

        // S6's ladder: at most one completion is shut per round, so the loop is
        // bounded by the completion count — termination by construction, not by
        // hope (SDD-002 §7).
        for (int ladderStep = 0; ladderStep <= completions.Count; ladderStep++)
        {
            SolveOutcome outcome = RunToConvergence(network, segment, state);
            if (outcome.Converged)
            {
                state.AttributeDeferrals(network, segment);      // §8
                return Report(state, outcome, forcedShutIns);
            }

            ICompletion victim = state.LargestResidual();
            state.ForceShutIn(victim.Id);
            forcedShutIns.Add(new ForcedShutIn(victim.Id, outcome.WorstResidual));

            _audit.Record(AuditCategory.ForcedShutIn, null, null,
                new Dictionary<string, AuditValue>
                {
                    ["completion"] = new(Format(victim.Id.Value)),
                    ["relativeResidual"] = new(Format(outcome.WorstResidual)),
                    ["cause"] = new("solver-stability"),
                    ["ladderStep"] = new(Format(ladderStep + 1)),
                });

            state.ResetForLadderStep();
        }

        // Every completion shut and still not converged: the ladder's floor is a
        // network with zero flow, which is trivially convergent, so reaching here
        // means the network itself is inconsistent.
        throw new InvariantFault("SDD-002 §7", null,
            "the shut-in ladder exhausted every completion without converging; " +
            "a zero-rate network must converge, so the topology or an element transform is wrong");
    }

    // ------------------------------------------------------------- S1–S5

    private SolveOutcome RunToConvergence(
        FlowNetwork network, SegmentContext segment, SolveState state)
    {
        for (int iteration = 0; iteration < _settings.MaxOuterIterations; iteration++)
        {
            double worstRateResidual = state.AdvanceRates();            // S1
            ForwardPass(network, segment, state);                       // S2
            bool throttled = Throttle(network, state, segment);         // S3
            double worstPressureChange = BackwardPass(network, state);  // S4

            // S5: rates settled, no capacity violated, pressures settled.
            if (!throttled
                && worstRateResidual < _settings.RateTolerance
                && worstPressureChange < _settings.PressureTolerancePa)
                return new SolveOutcome(true, worstRateResidual, iteration + 1);
        }

        return new SolveOutcome(false, state.WorstResidual, _settings.MaxOuterIterations);
    }

    /// <summary>
    /// S2. Topological order, so every element's inlets are known before it is
    /// asked to transform. Element-level conservation is checked after EVERY
    /// transform — that is what makes an INV1 breakdown attributable to one
    /// element instead of to the network.
    /// </summary>
    private void ForwardPass(FlowNetwork network, SegmentContext segment, SolveState state)
    {
        state.ClearPass();

        IReadOnlyList<IFlowElement> ordered = network.Ordered;
        for (int i = 0; i < ordered.Count; i++)
        {
            IFlowElement element = ordered[i];

            // S2: "build streams from q_w". A completion is handed the rate S1
            // damped and S3 capped; every other element is handed null, which
            // says the solver holds no rate for it — not that its rate is zero.
            var input = new TransformInput(
                state.InletsOf(element, network), segment, state.SolvedRateOf(element.Id));

            TransformResult result = element.Transform(input);
            AssertElementConservation(element, input, result);

            state.RecordSolution(element, input, result);
            state.RecordConstraints(element, element.EvaluateConstraints(input));
        }
    }

    /// <summary>
    /// R4.6 / SDD-002 §5. Σ inlets + Sourced == Σ outlets + FuelConsumed +
    /// Disposed, per material. Checked here rather than only at commit so the
    /// element that broke it is named while the stack still knows which one.
    /// </summary>
    private static void AssertElementConservation(
        IFlowElement element, TransformInput input, TransformResult result)
    {
        double inbound = result.Sourced.Total.KgPerSecond;
        for (int i = 0; i < input.Inlets.Count; i++)
            inbound += input.Inlets[i].MassRates.Total.KgPerSecond;

        double outbound = result.FuelConsumed.Total.KgPerSecond
                        + result.Disposed.Flared.Total.KgPerSecond
                        + result.Disposed.Vented.Total.KgPerSecond
                        + result.Disposed.Discharged.Total.KgPerSecond;
        for (int i = 0; i < result.Outlets.Count; i++)
            outbound += result.Outlets[i].MassRates.Total.KgPerSecond;

        if (double.IsNaN(inbound) || double.IsNaN(outbound)
            || double.IsInfinity(inbound) || double.IsInfinity(outbound))
            throw new ModelFault("INV6", null,
                $"element {element.Id.Value} produced a non-finite mass rate");

        double error = Math.Abs(inbound - outbound);
        double tolerance = Math.Max(1e-12 * Math.Max(inbound, outbound), 1e-9);

        if (error > tolerance)
            throw new ModelFault("INV1", null,
                $"element {element.Id.Value} does not conserve: in {Format(inbound)} kg/s, " +
                $"out {Format(outbound)} kg/s, error {Format(error)} exceeds {Format(tolerance)}");
    }

    /// <summary>
    /// S3. Every violated capacity throttles its contributing completions
    /// PRO-RATA to their share through the binding element — the only rule that
    /// is deterministic, explainable in the bottleneck report and free of hidden
    /// favouritism. Priority throttling is a player policy expressed through
    /// chokes, never a solver default (SDD-002 §7).
    /// </summary>
    private bool Throttle(FlowNetwork network, SolveState state, SegmentContext segment)
    {
        bool anyThrottled = false;

        IReadOnlyList<IFlowElement> ordered = network.Ordered;
        for (int i = 0; i < ordered.Count; i++)
        {
            IFlowElement element = ordered[i];
            IReadOnlyList<ConstraintEvaluation> constraints = state.ConstraintsOf(element.Id);

            for (int c = 0; c < constraints.Count; c++)
            {
                ConstraintEvaluation constraint = constraints[c];
                if (constraint.Load <= constraint.Capacity) continue;

                double factor = constraint.Capacity / constraint.Load;

                // S3 throttles COMPLETIONS. A violated constraint with no live
                // completion upstream cannot be relieved by any amount of
                // iterating: the load is fixed, so S5 could never pass and the
                // ladder would have nothing to shut. Faulting here names the
                // element and the constraint; spinning to the budget and then
                // reporting ladder exhaustion would blame the wrong thing.
                if (!state.HasThrottleableCompletion())
                    throw new ModelFault("SDD-002 §7", null,
                        $"element {element.Id.Value} reports {constraint.Kind} over capacity " +
                        $"({Format(constraint.Load)} > {Format(constraint.Capacity)}) but no live " +
                        "completion feeds it — the constraint cannot be relieved by throttling");

                // The cap is all S3 does. What the constraint REFUSED is a
                // question about the converged state, answered once in §8's
                // attribution pass — see SolveState.AttributeDeferrals.
                state.ApplyProRataCap(factor);
                anyThrottled = true;
            }
        }

        return anyThrottled;
    }

    /// <summary>
    /// S4. Rates fixed, pressures propagate from each sink upward. A completion
    /// whose choke reports critical flow is PRESSURE-DECOUPLED and keeps its
    /// rate — which is why a choked well survives backpressure swings, and what
    /// damps oscillation on a shared line.
    /// </summary>
    private double BackwardPass(FlowNetwork network, SolveState state)
    {
        // One reverse sweep computes every element's demanded inlet pressure;
        // each completion then reads the one it faces.
        state.BackwardPass(network, _settings.SinkBoundaryPressurePa);
        return state.UpdateBackpressures(network);
    }

    /// <summary>
    /// The completions in the network, in the topological order the network
    /// already fixed — which is stable across input orderings, so S1 iterates
    /// them identically on every run without a second sort.
    /// </summary>
    private static IReadOnlyList<ICompletion> CompletionsIn(FlowNetwork network)
    {
        var completions = new List<ICompletion>();

        IReadOnlyList<IFlowElement> ordered = network.Ordered;
        for (int i = 0; i < ordered.Count; i++)
            if (ordered[i] is ICompletion completion)
                completions.Add(completion);

        return completions;
    }

    /// <summary>
    /// A projection of the converged state, not a second computation. The solver
    /// holds NO per-solve fields: everything a solve produces lives in the
    /// SolveState passed through it, so two solves cannot interfere and the
    /// solver itself is safely reusable across segments (law L2's spirit — no
    /// mutable state where a caller cannot see it).
    /// </summary>
    private static SolveReport Report(
        SolveState state,
        SolveOutcome outcome,
        IReadOnlyList<ForcedShutIn> forcedShutIns) =>
        new(state.Solutions,
            state.CompletionStates(),
            state.Deferrals,
            state.Utilisations,
            forcedShutIns,
            outcome.Iterations);

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(ulong value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private readonly record struct SolveOutcome(bool Converged, double WorstResidual, int Iterations);
}
