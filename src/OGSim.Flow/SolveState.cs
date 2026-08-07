// R4.3–R4.5 — everything one solve knows (SDD-002 §7).
//
// All per-solve state lives here rather than on the solver, so a solver instance
// carries nothing between segments and two solves cannot interfere. Every
// collection walked during a pass is a List or is sorted before walking: rule
// D-5 in the one place where iteration order would silently change results.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Flow;

internal sealed class SolveState
{
    private readonly SolverSettings _settings;
    private readonly IReadOnlyList<ICompletion> _completions;
    private readonly CompletionSolveState[] _byCompletion;

    private readonly Dictionary<EntityId<IFlowElement>, TransformResult> _results = [];
    private readonly Dictionary<EntityId<IFlowElement>, IReadOnlyList<ConstraintEvaluation>> _constraints = [];

    public SolveState(IReadOnlyList<ICompletion> completions, SolverSettings settings)
    {
        _completions = completions;
        _settings = settings;

        _byCompletion = new CompletionSolveState[completions.Count];
        for (int i = 0; i < completions.Count; i++)
        {
            // S0 INIT: first segment of a tick seeds from the previous tick's
            // committed rates. R4 has no committed history, so a fresh solve
            // starts from zero and converges up — which is also exactly the
            // "new well" case the SDD names.
            _byCompletion[i] = new CompletionSolveState
            {
                Rate = 0.0,
                Backpressure = Pressure.FromBar(1.0),
                Cap = double.PositiveInfinity,
                ShutIn = false,
            };
        }

        Solutions = [];
        Deferrals = [];
    }

    public List<ElementSolution> Solutions { get; }

    public List<(EntityId<IFlowElement> Element, ConstraintKind Kind, Mass Deferred)> Deferrals { get; }

    public double WorstResidual { get; private set; }

    /// <summary>
    /// S1. Each completion is asked for its operating point at its current
    /// backpressure, then moved a DAMPED fraction of the way there. λ = 0.5 is
    /// pinned: undamped, two wells on a shared line oscillate against each other
    /// and never settle.
    /// </summary>
    public double AdvanceRates()
    {
        double worst = 0.0;

        for (int i = 0; i < _completions.Count; i++)
        {
            ICompletion completion = _completions[i];
            ref CompletionSolveState state = ref _byCompletion[i];

            if (state.ShutIn)
            {
                state.Rate = 0.0;
                continue;
            }

            OperatingPoint point = completion.SolveOperatingPoint(state.Backpressure);
            double target = point switch
            {
                Flowing flowing => flowing.Rate.CubicMetresPerSecond,
                Dead => 0.0,     // DEAD is q* = 0 with a distinct meaning (R6-V6)
                _ => 0.0,
            };

            if (double.IsNaN(target) || double.IsInfinity(target))
                throw new ModelFault("INV6", null,
                    $"completion {completion.Id.Value} returned a non-finite rate");

            // Kept before the cap: §8's attribution needs what the completion
            // WANTED, which is the only reference against which a refusal can be
            // measured. After capping, that number is gone.
            state.UncappedTarget = target;
            if (target > state.Cap) target = state.Cap;

            double previous = state.Rate;
            state.Rate = previous + _settings.Damping * (target - previous);

            double residual = Math.Abs(state.Rate - previous)
                            / Math.Max(state.Rate, _settings.RateFloor);
            if (residual > worst) worst = residual;
        }

        WorstResidual = worst;
        return worst;
    }

    public void ClearPass()
    {
        _results.Clear();
        _constraints.Clear();
        Solutions.Clear();
    }

    /// <summary>
    /// The inlet streams for an element: whatever its upstream neighbours put on
    /// the ports feeding it, in INLET PORT ORDER so an element can rely on
    /// index 0 being port 0 (SDD-002 §5).
    /// </summary>
    public IReadOnlyList<MaterialStream> InletsOf(IFlowElement element, FlowNetwork network) =>
        InletsFrom(element, network, _results);

    private static IReadOnlyList<MaterialStream> InletsFrom(
        IFlowElement element,
        FlowNetwork network,
        Dictionary<EntityId<IFlowElement>, TransformResult> results)
    {
        IReadOnlyList<FlowConnection> upstream = network.Upstream(element.Id);
        if (upstream.Count == 0) return [];

        var byPort = new List<(int Port, MaterialStream Stream)>(upstream.Count);
        for (int i = 0; i < upstream.Count; i++)
        {
            FlowConnection edge = upstream[i];
            if (!results.TryGetValue(edge.From, out TransformResult? produced)) continue;

            int outletIndex = OutletIndexOf(network[edge.From], edge.FromPort);
            if (outletIndex < 0 || outletIndex >= produced.Outlets.Count) continue;

            byPort.Add((edge.ToPort.Index, produced.Outlets[outletIndex]));
        }

        byPort.Sort((a, b) => a.Port.CompareTo(b.Port));

        var inlets = new List<MaterialStream>(byPort.Count);
        for (int i = 0; i < byPort.Count; i++) inlets.Add(byPort[i].Stream);
        return inlets;
    }

    private static int OutletIndexOf(IFlowElement element, PortId port)
    {
        int index = 0;
        IReadOnlyList<PortSpec> ports = element.Ports;
        for (int i = 0; i < ports.Count; i++)
        {
            if (ports[i].Direction != PortDirection.Outlet) continue;
            if (ports[i].Id == port) return index;
            index++;
        }
        return -1;
    }

    public void RecordSolution(
        IFlowElement element, TransformInput input, TransformResult result)
    {
        _results[element.Id] = result;

        // Remembered so S4 can recover this element's ΔP without re-running the
        // transform: what it was given, against what it produced.
        _inletSeenPa[element.Id] = input.Inlets.Count > 0
            ? input.Inlets[0].P.Pascals
            : (result.Outlets.Count > 0 ? result.Outlets[0].P.Pascals : 0.0);

        Solutions.Add(new ElementSolution(element.Id, result));
    }

    public void RecordConstraints(
        IFlowElement element, IReadOnlyList<ConstraintEvaluation> constraints) =>
        _constraints[element.Id] = constraints;

    public IReadOnlyList<ConstraintEvaluation> ConstraintsOf(EntityId<IFlowElement> id) =>
        _constraints.TryGetValue(id, out IReadOnlyList<ConstraintEvaluation>? found) ? found : [];

    /// <summary>
    /// S3's pro-rata rule. Every contributing completion is capped by the same
    /// factor, which is what makes the bottleneck report sayable in one sentence
    /// — "SEP-01 throttled all three wells 18%" — and what keeps the solver free
    /// of hidden favouritism.
    /// </summary>
    /// <summary>
    /// S2's q_w for an element, or null if it is not a completion. A shut-in
    /// completion still gets a rate — 0.0 — because "produce nothing" and "the
    /// solver has no opinion about you" are different instructions.
    /// </summary>
    public ReservoirRate? SolvedRateOf(EntityId<IFlowElement> id)
    {
        int index = IndexOf(id);
        return index < 0 ? null : new ReservoirRate(_byCompletion[index].Rate);
    }

    /// <summary>
    /// Whether S3 has any lever at all. A constraint violated with every
    /// completion shut in — or with none in the network — cannot be relieved by
    /// throttling, and iterating would only burn the budget before blaming the
    /// ladder for a composition problem.
    /// </summary>
    public bool HasThrottleableCompletion()
    {
        for (int i = 0; i < _byCompletion.Length; i++)
            if (!_byCompletion[i].ShutIn) return true;
        return false;
    }

    public void ApplyProRataCap(double factor)
    {
        for (int i = 0; i < _byCompletion.Length; i++)
        {
            ref CompletionSolveState state = ref _byCompletion[i];
            if (state.ShutIn) continue;

            double capped = state.Rate * factor;
            if (capped < state.Cap) state.Cap = capped;
            if (state.Rate > capped) state.Rate = capped;
        }
    }

    /// <summary>
    /// §8's attribution, run ONCE on the converged state: the same network with
    /// every cap lifted, so each violated constraint reports what it refused.
    ///
    /// <para>Recording deferrals inside S3 cannot work, and the reason is worth
    /// keeping. S5 will not declare convergence while any capacity is violated,
    /// so at the converged iteration there is no excess left to record — the cap
    /// has already removed it. Accumulating the per-iteration excesses instead
    /// makes the reported volume a function of how many iterations convergence
    /// happened to take, so the same bottleneck reports different numbers under
    /// different damping. Neither is a refusal; both are artefacts.</para>
    ///
    /// <para>Measured against the completions' UNCAPPED targets, the figure is
    /// what the player actually lost to the bottleneck, and it is independent of
    /// the solver's path to the answer — which is what §8 promises the UI when
    /// it says the deferred-by-element projection is used verbatim.</para>
    ///
    /// <para>Conservation is not re-checked here: these transforms already
    /// passed it at the converged rates, and this pass exists to ask a
    /// diagnostic question, not to admit new state.</para>
    /// </summary>
    public void AttributeDeferrals(FlowNetwork network, SegmentContext segment)
    {
        Deferrals.Clear();

        var uncapped = new Dictionary<EntityId<IFlowElement>, TransformResult>();
        double seconds = segment.DurationDays * 86_400.0;

        IReadOnlyList<IFlowElement> ordered = network.Ordered;
        for (int i = 0; i < ordered.Count; i++)
        {
            IFlowElement element = ordered[i];

            int index = IndexOf(element.Id);
            ReservoirRate? rate = index < 0
                ? null
                // A completion the ladder shut is not being refused by a
                // bottleneck — it was withdrawn by the solver, and §8 records
                // that separately as a ForcedShutIn.
                : new ReservoirRate(_byCompletion[index].ShutIn
                    ? 0.0
                    : _byCompletion[index].UncappedTarget);

            var input = new TransformInput(
                InletsFrom(element, network, uncapped), segment, rate);

            uncapped[element.Id] = element.Transform(input);

            IReadOnlyList<ConstraintEvaluation> constraints = element.EvaluateConstraints(input);
            for (int c = 0; c < constraints.Count; c++)
            {
                ConstraintEvaluation constraint = constraints[c];
                if (constraint.Load <= constraint.Capacity) continue;

                RecordDeferral(element.Id, constraint.Kind,
                    new Mass((constraint.Load - constraint.Capacity) * seconds));
            }
        }
    }

    private void RecordDeferral(EntityId<IFlowElement> element, ConstraintKind kind, Mass deferred)
    {
        for (int i = 0; i < Deferrals.Count; i++)
        {
            if (Deferrals[i].Element != element || Deferrals[i].Kind != kind) continue;
            Deferrals[i] = (element, kind,
                new Mass(Deferrals[i].Deferred.Kilograms + deferred.Kilograms));
            return;
        }
        Deferrals.Add((element, kind, deferred));
    }

    /// <summary>
    /// S4, as pinned: with rates fixed, walk from each SINK UPWARD —
    /// <c>P_upstream = P_downstream + ΔP_element(q)</c> — and the pressure
    /// arriving at a completion's downstream neighbour is its new wellhead
    /// backpressure.
    ///
    /// ΔP for an element is what its own transform lost at the current rate
    /// (inlet pressure minus outlet pressure, from the forward pass), so the
    /// solver never needs a hydraulic model of its own — the element already
    /// answered that question and this pass only re-reads the answer.
    ///
    /// Reverse topological order guarantees every element's downstream is
    /// resolved before it is: the same ordering the forward pass used, walked
    /// backwards, so no separate traversal is needed and neither can drift.
    /// </summary>
    public void BackwardPass(FlowNetwork network, double sinkBoundaryPa)
    {
        _inletPressurePa.Clear();

        IReadOnlyList<IFlowElement> ordered = network.Ordered;
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            IFlowElement element = ordered[i];

            // Where this element must deliver to: its downstream neighbour's
            // inlet, or the network boundary if it is a sink.
            double outletPa = sinkBoundaryPa;
            IReadOnlyList<FlowConnection> downstream = network.Downstream(element.Id);
            if (downstream.Count > 0
                && _inletPressurePa.TryGetValue(downstream[0].To, out double demanded))
                outletPa = demanded;

            _inletPressurePa[element.Id] = outletPa + PressureDropOf(element.Id);
        }
    }

    /// <summary>What the element's own transform lost, at the rate just solved.</summary>
    private double PressureDropOf(EntityId<IFlowElement> id)
    {
        if (!_results.TryGetValue(id, out TransformResult? result)) return 0.0;
        if (result.Outlets.Count == 0) return 0.0;
        if (!_inletSeenPa.TryGetValue(id, out double inletPa)) return 0.0;

        double drop = inletPa - result.Outlets[0].P.Pascals;
        return drop > 0.0 ? drop : 0.0;      // a pump raising pressure is not a drop
    }

    /// <summary>
    /// The backpressure a completion now faces: the inlet pressure demanded by
    /// the element it feeds. Returns the size of the change, which S5 tests.
    /// A PRESSURE-DECOUPLED completion keeps its rate and is not updated — that
    /// is why a choked well survives backpressure swings on a shared line.
    /// </summary>
    public double UpdateBackpressures(FlowNetwork network)
    {
        double worst = 0.0;

        for (int i = 0; i < _completions.Count; i++)
        {
            // A PRESSURE-DECOUPLED completion keeps its rate and is not updated:
            // that is why a choked well survives backpressure swings on a shared
            // line, and what damps the oscillation S1's damping alone would not.
            if (_completions[i].IsPressureDecoupled) continue;

            double change = UpdateBackpressure(_completions[i], network);
            if (change > worst) worst = change;
        }

        return worst;
    }

    private double UpdateBackpressure(ICompletion completion, FlowNetwork network)
    {
        int index = IndexOf(completion.Id);
        if (index < 0) return 0.0;

        IReadOnlyList<FlowConnection> downstream = network.Downstream(completion.Id);
        if (downstream.Count == 0) return 0.0;
        if (!_inletPressurePa.TryGetValue(downstream[0].To, out double demandedPa)) return 0.0;

        ref CompletionSolveState state = ref _byCompletion[index];
        double previous = state.Backpressure.Pascals;
        state.Backpressure = new Pressure(demandedPa);
        return Math.Abs(demandedPa - previous);
    }

    private readonly Dictionary<EntityId<IFlowElement>, double> _inletPressurePa = [];
    private readonly Dictionary<EntityId<IFlowElement>, double> _inletSeenPa = [];

    // ------------------------------------------------------------- S6 ladder

    /// <summary>The completion carrying the largest relative residual; ties go to
    /// the lowest id, so the ladder's choice is total (SDD-002 §7 S6).</summary>
    public ICompletion LargestResidual()
    {
        ICompletion? worst = null;
        double worstRate = -1.0;

        for (int i = 0; i < _completions.Count; i++)
        {
            if (_byCompletion[i].ShutIn) continue;
            double rate = _byCompletion[i].Rate;
            if (rate > worstRate)
            {
                worstRate = rate;
                worst = _completions[i];
            }
        }

        return worst ?? throw new InvariantFault("SDD-002 §7", null,
            "the ladder has no completion left to shut, yet the solve has not converged");
    }

    public void ForceShutIn(EntityId<IFlowElement> id)
    {
        int index = IndexOf(id);
        if (index >= 0)
        {
            _byCompletion[index].ShutIn = true;
            _byCompletion[index].Rate = 0.0;
        }
    }

    /// <summary>Caps are lifted for the next ladder round: they were consequences
    /// of a solve that did not converge, and carrying them forward would bias the
    /// retry with numbers from a run that was abandoned.</summary>
    public void ResetForLadderStep()
    {
        for (int i = 0; i < _byCompletion.Length; i++)
        {
            _byCompletion[i].Cap = double.PositiveInfinity;
            if (!_byCompletion[i].ShutIn) _byCompletion[i].Rate = 0.0;
        }
        Deferrals.Clear();
    }

    public List<CompletionState> CompletionStates()
    {
        var states = new List<CompletionState>(_completions.Count);
        for (int i = 0; i < _completions.Count; i++)
            states.Add(new CompletionState(
                _completions[i].Id,
                new ReservoirRate(_byCompletion[i].Rate),
                _byCompletion[i].Backpressure));
        return states;
    }

    private int IndexOf(EntityId<IFlowElement> id)
    {
        for (int i = 0; i < _completions.Count; i++)
            if (_completions[i].Id == id) return i;
        return -1;
    }

    private struct CompletionSolveState
    {
        public double Rate;
        public double UncappedTarget;
        public Pressure Backpressure;
        public double Cap;
        public bool ShutIn;
    }
}
