// R24.2 / R24.4 — objective evaluation (SDD-014 §1–3).
//
// OBSERVE, NEVER INFLUENCE. Evaluation runs at stage 12 over a SEALED snapshot
// and a SEALED event list, and this assembly holds no reference to the command
// bus at all (R24-V15, architecture-tested). An objective that could act would
// make a scenario a second player, and the game's outcome would stop being the
// player's doing.
//
// AND OBJECTIVES SEE ONLY THE READ MODEL. Every path is resolved against a
// schema registry generated from R21's projections, and a path the registry does
// not know is a CONTENT FAULT AT LOAD. So an objective can never reference data
// the player cannot see — GM4 mechanised rather than promised — and a read-model
// rename breaks content loudly at load rather than silently at runtime.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Objectives;

/// <summary>
/// The stateful nodes' counters, persisted with the objective (SDD-013).
///
/// <para>Not <c>ObjectiveState</c>, which is what an objective has COME TO
/// (SDD-014 §5a). This is what its <see cref="SustainedFor"/>,
/// <see cref="InSequence"/> and <see cref="Never"/> nodes have accumulated on
/// the way — a different concept, and the two meet on one object the moment a
/// scenario runner is written.</para>
/// </summary>
public sealed class PredicateState
{
    private readonly Dictionary<string, int> _sustained = [];
    private readonly Dictionary<string, int> _sequenceStep = [];
    private readonly HashSet<string> _neverBroken = [];

    public int SustainedTicks(string node) => _sustained.GetValueOrDefault(node);

    public void SetSustained(string node, int ticks) => _sustained[node] = ticks;

    public int SequenceStep(string node) => _sequenceStep.GetValueOrDefault(node);

    public void SetSequenceStep(string node, int step) => _sequenceStep[node] = step;

    /// <summary>A `Never` that has been broken STAYS broken — that is what makes
    /// it a failure condition rather than a momentary check.</summary>
    public bool IsBroken(string node) => _neverBroken.Contains(node);

    public void MarkBroken(string node) => _neverBroken.Add(node);
}

/// <summary>
/// SDD-014 §2's registry. A path that is not here is a content fault.
/// </summary>
public sealed class ReadModelSchema
{
    private readonly HashSet<string> _scalars;
    private readonly HashSet<string> _collections;
    private readonly HashSet<string> _itemFields;

    public ReadModelSchema(
        IReadOnlyList<string> scalarPaths,
        IReadOnlyList<string> collectionPaths,
        IReadOnlyList<string> itemFields)
    {
        ArgumentNullException.ThrowIfNull(scalarPaths);
        ArgumentNullException.ThrowIfNull(collectionPaths);
        ArgumentNullException.ThrowIfNull(itemFields);

        _scalars = [.. scalarPaths];
        _collections = [.. collectionPaths];
        _itemFields = [.. itemFields];
    }

    /// <summary>
    /// SDD-014 §2's load-time validation. Walks the whole tree and reports EVERY
    /// unknown path.
    ///
    /// <para>All of them at once, because a content author fixing one typo and
    /// reloading to find the next has been made to pay twice for one piece of
    /// information — the same rule every refusal in this engine follows.</para>
    /// </summary>
    public IReadOnlyList<string> Validate(Predicate predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var unknown = new List<string>();
        Walk(predicate, unknown);
        return unknown;
    }

    private void Walk(Predicate predicate, List<string> unknown)
    {
        switch (predicate)
        {
            case Metric metric:
                if (!_scalars.Contains(metric.Path.Path))
                    unknown.Add($"metric path '{metric.Path.Path}' is not in the read model");
                break;

            case Aggregate aggregate:
                if (!_collections.Contains(aggregate.Collection.Path))
                    unknown.Add(
                        $"collection path '{aggregate.Collection.Path}' is not in the read model");

                if (!_itemFields.Contains(aggregate.ItemField.Path))
                    unknown.Add(
                        $"item field '{aggregate.ItemField.Path}' is not in the read model");
                break;

            case Compare compare:
                Walk(compare.L, unknown);
                Walk(compare.R, unknown);
                break;

            case All all:
                for (int i = 0; i < all.Items.Count; i++) Walk(all.Items[i], unknown);
                break;

            case Any any:
                for (int i = 0; i < any.Items.Count; i++) Walk(any.Items[i], unknown);
                break;

            case CountOf countOf:
                for (int i = 0; i < countOf.Items.Count; i++) Walk(countOf.Items[i], unknown);
                break;

            case InSequence sequence:
                for (int i = 0; i < sequence.Steps.Count; i++) Walk(sequence.Steps[i], unknown);
                break;

            case SustainedFor sustained:
                Walk(sustained.Inner, unknown);
                break;

            case Never never:
                Walk(never.Inner, unknown);
                break;

            case Const or OnEvent:
                break;

            default:
                // The AST is CLOSED (SDD-014 §1). An unrecognised node means the
                // vocabulary grew without this walk growing with it, which is a
                // fault rather than something to skip.
                throw new ModelFault("SDD-014 §1", null,
                    $"unknown predicate {predicate.GetType().Name}; the AST is closed and " +
                    "every node must be validated");
        }
    }
}

/// <summary>
/// SDD-014 §3. Pure over the snapshot.
/// </summary>
public sealed class ObjectiveEvaluator
{
    /// <summary>
    /// Evaluates one objective. <paramref name="state"/> carries the stateful
    /// nodes' counters between ticks and is the ONLY thing mutated.
    /// </summary>
    public bool Evaluate(Objective objective, ObjectiveSnapshot snapshot, PredicateState state)
    {
        ArgumentNullException.ThrowIfNull(objective);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(state);

        return Truth(objective.Condition, snapshot, state, objective.Id.Value);
    }

    private bool Truth(Predicate predicate, ObjectiveSnapshot snapshot, PredicateState state, string node)
    {
        switch (predicate)
        {
            case Compare compare:
                return Apply(
                    Value(compare.L, snapshot), compare.Op, Value(compare.R, snapshot));

            case All all:
                for (int i = 0; i < all.Items.Count; i++)
                    if (!Truth(all.Items[i], snapshot, state, $"{node}.all{i}")) return false;
                return true;

            case Any any:
                for (int i = 0; i < any.Items.Count; i++)
                    if (Truth(any.Items[i], snapshot, state, $"{node}.any{i}")) return true;
                return false;

            case CountOf countOf:
                int met = 0;
                for (int i = 0; i < countOf.Items.Count; i++)
                    if (Truth(countOf.Items[i], snapshot, state, $"{node}.count{i}")) met++;
                return met >= countOf.N;

            case SustainedFor sustained:
                return Sustained(sustained, snapshot, state, node);

            case InSequence sequence:
                return Sequence(sequence, snapshot, state, node);

            case Never never:
                return NeverBroken(never, snapshot, state, node);

            case OnEvent onEvent:
                return Fired(onEvent, snapshot);

            // A bare value used where a truth is wanted: non-zero is true. The
            // grammar allows it because content can and does write
            // `Metric(flag)` for a boolean projection.
            case Metric or Const or Aggregate:
                return Value(predicate, snapshot) != 0.0;

            default:
                throw new ModelFault("SDD-014 §1", null,
                    $"unknown predicate {predicate.GetType().Name}; the AST is closed");
        }
    }

    /// <summary>
    /// A consecutive-true counter. Interrupting the condition RESETS it.
    ///
    /// <para>Resets rather than pauses, because "sustained for twelve months"
    /// means twelve in a row — a counter that merely paused would let a player
    /// satisfy a stability objective with a decade of intermittent
    /// compliance.</para>
    /// </summary>
    private bool Sustained(
        SustainedFor sustained, ObjectiveSnapshot snapshot, PredicateState state, string node)
    {
        string key = $"{node}.sustained";

        if (!Truth(sustained.Inner, snapshot, state, key))
        {
            state.SetSustained(key, 0);
            return false;
        }

        int ticks = state.SustainedTicks(key) + 1;
        state.SetSustained(key, ticks);

        return ticks >= sustained.Ticks;
    }

    /// <summary>
    /// A current-step index. Steps are satisfied IN ORDER and the index never
    /// goes backwards — a sequence is a sequence, and letting an earlier step
    /// un-satisfy would make "drill, then complete, then produce" satisfiable
    /// by producing first.
    /// </summary>
    private bool Sequence(
        InSequence sequence, ObjectiveSnapshot snapshot, PredicateState state, string node)
    {
        string key = $"{node}.sequence";
        int step = state.SequenceStep(key);

        while (step < sequence.Steps.Count
               && Truth(sequence.Steps[step], snapshot, state, $"{key}.{step}"))
            step++;

        state.SetSequenceStep(key, step);
        return step >= sequence.Steps.Count;
    }

    /// <summary>
    /// A failure condition. Once broken it STAYS broken — which is what makes it
    /// a promise about the whole scenario rather than a momentary check.
    /// </summary>
    private bool NeverBroken(
        Never never, ObjectiveSnapshot snapshot, PredicateState state, string node)
    {
        string key = $"{node}.never";

        if (state.IsBroken(key)) return false;

        if (Truth(never.Inner, snapshot, state, key))
        {
            state.MarkBroken(key);
            return false;
        }

        return true;
    }

    /// <summary>True for the tick the event fired, and no longer.</summary>
    private static bool Fired(OnEvent onEvent, ObjectiveSnapshot snapshot)
    {
        for (int i = 0; i < snapshot.Events.Count; i++)
        {
            EngineEvent fired = snapshot.Events[i];

            if (fired.Category != onEvent.Category) continue;

            // An unset filter field does not narrow (SDD-014 §1).
            if (onEvent.Filter.Subject is EntityRef subject && fired.Subject != subject) continue;

            if (onEvent.Filter.MinimumSeverity is Severity minimum
                && fired.Severity < minimum) continue;

            return true;
        }

        return false;
    }

    private double Value(Predicate predicate, ObjectiveSnapshot snapshot) => predicate switch
    {
        Const constant => constant.Value,

        Metric metric => snapshot.Values.TryGetValue(metric.Path.Path, out double value)
            ? value
            // A path the registry accepted but the snapshot does not carry is an
            // engine bug, not a missing value — defaulting to zero would make an
            // objective silently true.
            : throw new InvariantFault("SDD-014 §2", null,
                $"read-model path '{metric.Path.Path}' validated at load but is absent from " +
                "the snapshot; the registry and the projection disagree"),

        Aggregate aggregate => Aggregated(aggregate, snapshot),

        _ => throw new ModelFault("SDD-014 §1", null,
            $"predicate {predicate.GetType().Name} has no value; it is a truth, not a number"),
    };

    /// <summary>
    /// SDD-014 §1's quantifier — the node without which fleet-level objectives
    /// were expressible only one id at a time.
    /// </summary>
    private static double Aggregated(Aggregate aggregate, ObjectiveSnapshot snapshot)
    {
        if (!snapshot.Collections.TryGetValue(aggregate.Collection.Path,
                out IReadOnlyList<IReadOnlyDictionary<string, double>>? items))
            throw new InvariantFault("SDD-014 §2", null,
                $"collection '{aggregate.Collection.Path}' validated at load but is absent " +
                "from the snapshot");

        if (aggregate.Op == AggOp.Count) return items.Count;

        // An EMPTY collection is not zero for Max, Min or All. "The highest
        // water cut across no wells" has no answer, and returning zero would
        // make a fleet objective trivially true before the first well is
        // drilled.
        if (items.Count == 0)
            return aggregate.Op switch
            {
                AggOp.Sum => 0.0,
                AggOp.Any => 0.0,     // nothing satisfies it
                AggOp.All => 1.0,     // vacuously, as in logic
                _ => throw new ModelFault("SDD-014 §1", null,
                    $"{aggregate.Op} over an empty collection '{aggregate.Collection.Path}' " +
                    "has no value; guard the objective with a Count first"),
            };

        double result = aggregate.Op switch
        {
            AggOp.Max => double.NegativeInfinity,
            AggOp.Min => double.PositiveInfinity,
            _ => 0.0,
        };

        bool anyTrue = false, allTrue = true;

        for (int i = 0; i < items.Count; i++)
        {
            if (!items[i].TryGetValue(aggregate.ItemField.Path, out double value))
                throw new InvariantFault("SDD-014 §2", null,
                    $"item field '{aggregate.ItemField.Path}' is absent from item {i} of " +
                    $"'{aggregate.Collection.Path}'");

            switch (aggregate.Op)
            {
                case AggOp.Max: result = Math.Max(result, value); break;
                case AggOp.Min: result = Math.Min(result, value); break;
                case AggOp.Sum: result += value; break;
                case AggOp.Any: anyTrue |= value != 0.0; break;
                case AggOp.All: allTrue &= value != 0.0; break;
            }
        }

        return aggregate.Op switch
        {
            AggOp.Any => anyTrue ? 1.0 : 0.0,
            AggOp.All => allTrue ? 1.0 : 0.0,
            _ => result,
        };
    }

    private static bool Apply(double left, CompareOp op, double right) => op switch
    {
        CompareOp.Lt => left < right,
        CompareOp.Le => left <= right,
        CompareOp.Eq => left == right,
        CompareOp.Ne => left != right,
        CompareOp.Ge => left >= right,
        CompareOp.Gt => left > right,
        _ => throw new ModelFault("SDD-014 §1", null, $"unknown compare {op}"),
    };
}
