// SDD-001 §9–10 — composition, the tick pipeline, segments, state.
// Composition either fully succeeds or refuses to start with EVERY unmet
// requirement named (design 03 §3.1).

namespace OGSim.Kernel;

public readonly record struct ModuleName(string Value);

/// <summary>
/// A module's state block name. Comparable so registries can order by it:
/// capture and restore must visit owners in a fixed sequence or two runs
/// produce different bytes and the PV1 digest is meaningless (R1.11).
/// </summary>
public readonly record struct StateKey(string Value) : IComparable<StateKey>
{
    public int CompareTo(StateKey other) => string.CompareOrdinal(Value, other.Value);
}

/// <summary>
/// Where in the tick a module works, and in what position within that stage.
///
/// R1.10: <c>Order</c> was added because two modules acting in the same stage
/// need a FIXED relative order or the tick is non-deterministic, and design 03
/// §6 requires the order be declared rather than emergent. Ordering by module
/// name instead would make execution order a consequence of spelling.
/// </summary>
public sealed record StageParticipation(StageId Stage, int Order);

/// <summary>Design 03 §3.1 — what a module declares before it is constructed.</summary>
public sealed record ModuleManifest(
    ModuleName Name,
    IReadOnlyList<Type> Provides,
    IReadOnlyList<Type> Requires,
    IReadOnlyList<StateKey> OwnsState,
    IReadOnlyList<StageParticipation> Stages,
    IReadOnlyList<Type> Commands)
{
    // Finding 131. A manifest is compared when a scenario asks whether its
    // module set matches the one a save was written under.
    public bool Equals(ModuleManifest? other) =>
        other is not null && Name == other.Name
        && Structural.Equal(Provides, other.Provides)
        && Structural.Equal(Requires, other.Requires)
        && Structural.Equal(OwnsState, other.OwnsState)
        && Structural.Equal(Stages, other.Stages)
        && Structural.Equal(Commands, other.Commands);

    public override int GetHashCode() =>
        HashCode.Combine(Name,
            Structural.HashOf(Provides), Structural.HashOf(Requires),
            Structural.HashOf(OwnsState), Structural.HashOf(Stages),
            Structural.HashOf(Commands));
}

/// <summary>Resolution happens AFTER validation of the whole module set.</summary>
public interface IModuleComposition
{
    void Provide<T>(T implementation) where T : class;
    T Require<T>() where T : class;

    /// <summary>
    /// The work for a declared slot. <c>work.Id</c> says which stage and
    /// <paramref name="order"/> where within it; together they must match a
    /// <see cref="StageParticipation"/> this module's own manifest declared, so
    /// a module cannot act in a stage it never named (SDD-001 §9, finding 125).
    /// </summary>
    void Contribute(int order, ITickStage work);

    /// <summary>
    /// The owner of a declared fact. <c>state.Key</c> must appear in this
    /// module's own <c>OwnsState</c>, and every key it declares must receive an
    /// owner — the same declaration-must-have-behaviour rule as
    /// <see cref="Contribute"/>, on the state side (SDD-001 §9, finding 127).
    /// </summary>
    void Own(IStateOwner state);

    /// <summary>
    /// The two halves of a declared command: the validator that may refuse it
    /// and the applier that cannot (SDD-001 §7).
    ///
    /// <para><typeparamref name="TCommand"/> must appear in this module's own
    /// <c>Commands</c>, and every command it declares must be handled — the same
    /// rule again, on the command side (finding 139). Registering commands
    /// anywhere but here would put the engine's entire input surface outside the
    /// set the composer validates: a module could declare a command nothing
    /// handles, and a host would discover it by submitting one and being told
    /// nothing is listening.</para>
    /// </summary>
    void HandleCommand<TCommand>(
        ICommandValidator<TCommand> validator,
        ICommandApplier<TCommand> applier)
        where TCommand : Command;
}

/// <summary>Model-plugin binding for content stage 6 (SDD-004 §5): a content
/// entry naming a plugin resolves here or the load fails naming the entry.</summary>
public interface IModuleRegistry
{
    bool CanBind(ContentId plugin, Type contract);
    T Bind<T>(ContentId plugin) where T : class;
}

public interface IModule
{
    ModuleManifest Manifest { get; }
    void Compose(IModuleComposition composition);
}

/// <summary>A tick stage — the 14 of design 03 §6, executed in declared order.</summary>
public interface ITickStage
{
    StageId Id { get; }
    void Execute(TickContext context);
}

/// <summary>
/// Per-tick execution context. Stage read isolation (design 21 §4 / I-V5) is
/// asserted at runtime pending open item S001-4's interface-per-stage decision.
/// </summary>
public sealed class TickContext
{
    public required Tick Tick { get; init; }
    public required GameDate Date { get; init; }

    /// <summary>Null before stage 4 builds it; set exactly once by Availability;
    /// reading it earlier is a stage-isolation violation (I-V5).</summary>
    public SegmentPlan? Segments { get; set; }
}

// ---------------------------------------------------------------- segments

/// <summary>
/// A within-tick interval of constant availability, on the /30ths day grid —
/// integer days, so INV9 ("durations sum to exactly one tick") is integer
/// arithmetic (SDD-001 §9). Budget: 4 per tick; merges are audited, ranked by
/// last-committed throughput × remaining duration (the pinned estimator).
/// </summary>
public sealed record Segment(
    int StartDay,
    int DurationDays,
    IReadOnlyCollection<EntityRef> Available)
{
    // Finding 131.
    public bool Equals(Segment? other) =>
        other is not null && StartDay == other.StartDay && DurationDays == other.DurationDays
        && Structural.Equal(Available, other.Available);

    public override int GetHashCode() =>
        HashCode.Combine(StartDay, DurationDays, Structural.HashOf(Available));
}

public sealed record SegmentPlan(IReadOnlyList<Segment> Segments)
{
    // Finding 131.
    public bool Equals(SegmentPlan? other) =>
        other is not null && Structural.Equal(Segments, other.Segments);

    public override int GetHashCode() => Structural.HashOf(Segments);
}

// ---------------------------------------------------------------- state

/// <summary>Ordered key/value writer in the canonical form of SDD-013 §3.</summary>
public interface IStateWriter
{
    void WriteString(string key, string value);
    void WriteInt64(string key, long value);
    void WriteDouble(string key, double value);   // canonical shortest round-trip on disk
}

/// <summary>A missing or unreadable value is a SaveDataFault — never a default (SDD-001 §10).</summary>
public interface IStateReader
{
    string ReadString(string key);
    long ReadInt64(string key);
    double ReadDouble(string key);
}

public interface IStateOwner
{
    StateKey Key { get; }
    int SchemaVersion { get; }
    void Capture(IStateWriter writer);
    void Restore(IStateReader reader);
}
