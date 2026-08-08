// R20c.7 — the wells module's state and its stage (SDD-003 §6, design 03 §6).
//
// This is the half of the loop that makes a reservoir matter: a completion drains
// a compartment, the compartment loses pressure, and next month the same well
// produces less. Neither half is a game on its own.
//
// The completion NEVER reads a compartment. OGSim.Wells cannot see subsurface
// truth and must not — reservoir pressure arrives as a number, pushed in before
// the solve by the composition that owns both sides. That is the same door
// CompletionFluid has documented since R6.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Wells;

/// <summary>Owner of <c>wells.completions</c>.</summary>
public sealed class WellsState : IStateOwner
{
    private readonly List<Completion> _completions = [];
    private readonly Dictionary<EntityId<ICompletion>, Completion> _byId = [];

    // What each completion drains. Held here rather than derived from the
    // perforations at commit time because a perforation names a compartment and
    // this names the ONE the tick's production is credited to (law L5: the
    // allocation has a single owner).
    private readonly Dictionary<EntityId<ICompletion>, EntityId<IReservoirCompartmentEntity>> _drains = [];

    private readonly IFlowElementRegistry _network;

    private ulong _nextId;

    public WellsState(IFlowElementRegistry network)
    {
        ArgumentNullException.ThrowIfNull(network);
        _network = network;
    }

    public StateKey Key { get; } = new("wells.completions");

    public int SchemaVersion => 1;

    public int Count => _completions.Count;

    /// <summary>
    /// Brings a completion online: it becomes a source element in the network
    /// the solver sees, from this tick.
    /// </summary>
    public EntityId<ICompletion> Open(
        Completion completion,
        EntityId<IReservoirCompartmentEntity> drains)
    {
        ArgumentNullException.ThrowIfNull(completion);

        if (!_byId.TryAdd(completion.CompletionId, completion))
            throw new InvariantFault("INV3", null,
                $"completion {completion.CompletionId.Value} is already open");

        _completions.Add(completion);
        _drains[completion.CompletionId] = drains;
        _nextId = Math.Max(_nextId, completion.CompletionId.Value);

        // Registered as a flow element so stage 5 sees it. The registry holds
        // the reference; this list holds the well.
        _network.Add(completion);

        return completion.CompletionId;
    }

    /// <summary>Every open completion, in the order they were opened.</summary>
    public IReadOnlyList<Completion> Completions => _completions;

    /// <summary>
    /// One open completion by id, or null. A player's lever needs to name the
    /// well it is pulled on, and a lookup that faulted would make "shut in a
    /// well that is not there" an engine halt rather than a rejection.
    /// </summary>
    public Completion? Find(EntityId<ICompletion> completion) =>
        _byId.TryGetValue(completion, out Completion? found) ? found : null;

    public EntityId<IReservoirCompartmentEntity> CompartmentOf(EntityId<ICompletion> completion) =>
        _drains.TryGetValue(completion, out EntityId<IReservoirCompartmentEntity> found)
            ? found
            : throw new InvariantFault("INV3", null,
                $"completion {completion.Value} is not open");

    /// <summary>
    /// Stage 3/4: refresh each completion with the compartment pressure it will
    /// solve against this tick.
    ///
    /// <para>The delegate is how the truth boundary is crossed without being
    /// breached — composition holds both modules and supplies a function from a
    /// compartment id to its pressure. Nothing here can name a compartment, and
    /// nothing here keeps the number past the tick.</para>
    /// </summary>
    public void RefreshFromReservoir(
        Func<EntityId<IReservoirCompartmentEntity>, Pressure> pressureOf,
        Func<Pressure, double> solutionGorAt,
        Func<EntityId<IReservoirCompartmentEntity>, double> waterCutOf,
        Temperature reservoirTemperature)
    {
        ArgumentNullException.ThrowIfNull(pressureOf);
        ArgumentNullException.ThrowIfNull(solutionGorAt);
        ArgumentNullException.ThrowIfNull(waterCutOf);

        for (int i = 0; i < _completions.Count; i++)
        {
            Completion completion = _completions[i];
            Pressure reservoir = pressureOf(_drains[completion.CompletionId]);

            // Rs travels with the pressure it is a function of. Two deliveries
            // would let a completion solve at this month's pressure and last
            // month's gas ratio, which is a GOR that lags its own reservoir.
            completion.SetReservoirConditions(
                reservoir, reservoirTemperature, solutionGorAt(reservoir),
                waterCutOf(_drains[completion.CompletionId]));
        }
    }

    // ProduceOver LIVED HERE, and it was the second flow system.
    //
    // It solved every completion against ONE hard-coded backpressure and handed
    // the result straight to stage 6 — no network, no separator, no header, no
    // meter, and no way for anything downstream of a well to affect it. The
    // engine therefore shipped with two ways for material to move: the one flow
    // engine (SDD-002 §7), composed and unused, and this.
    //
    // DELETED rather than left for the day something wants it (R20d G3). A
    // second path that still compiles is a second path someone will call, and
    // the two would drift the moment either learned something the other did not
    // — which is exactly how it came to exist: the loop was built end to end at
    // speed and the solver was one of the subsystems it wrote past (finding 142,
    // the same defect on the drilling side).
    //
    // Material moves one way now: stage 4 plans the segments, stage 5 solves the
    // network, stage 6 commits, stage 7 meters. Adding equipment is adding an
    // element to that network.

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // Which wells are open and what they drain. The completion's own
        // configuration — tubing, choke, lift — is CONTENT, restored by the
        // loader rather than copied into every save (SDD-013 §4).
        writer.WriteInt64("count", _completions.Count);

        for (int i = 0; i < _completions.Count; i++)
        {
            string at = Prefix(i);
            Completion completion = _completions[i];

            writer.WriteInt64(at + "id", (long)completion.CompletionId.Value);
            writer.WriteInt64(at + "drains", (long)_drains[completion.CompletionId].Value);
        }
    }

    /// <summary>
    /// Restores which completions are open. The completions themselves are
    /// rebuilt from content first and handed to <see cref="Open"/>; this checks
    /// the save agrees with what was rebuilt, rather than silently producing a
    /// field with wells missing.
    /// </summary>
    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        long count = reader.ReadInt64("count");

        if (count != _completions.Count)
            throw new SaveDataFault("SDD-013 §2", null,
                $"the save holds {count} open completions and the rebuilt field has " +
                $"{_completions.Count}; a field with wells missing is not the field " +
                "that was saved");

        for (long i = 0; i < count; i++)
        {
            string at = Prefix(i);
            var id = new EntityId<ICompletion>((ulong)reader.ReadInt64(at + "id"));

            if (!_byId.ContainsKey(id))
                throw new SaveDataFault("SDD-013 §2", null,
                    $"the save names completion {id.Value}, which the rebuilt field " +
                    "does not contain");

            _drains[id] = new EntityId<IReservoirCompartmentEntity>(
                (ulong)reader.ReadInt64(at + "drains"));
        }
    }

    private static string Prefix(long index) =>
        "completion." + index.ToString("D6", System.Globalization.CultureInfo.InvariantCulture) + ".";
}
