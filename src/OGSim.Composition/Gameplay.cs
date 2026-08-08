// R21 (first slice) — what makes this a game rather than a simulation that runs:
// the player can act, can see, and can lose.
//
// AGENCY. Drilling a well is a command, validated then applied, and the
// validation is where the decision has weight: a company that cannot afford the
// well is told so in a reason it can render, not silently allowed to go
// bankrupt. Every rejection is domain-typed (SDD-001 §7) so the host never
// invents an explanation.
//
// VISIBILITY. The read model is rebuilt at stage 13 from what the player is
// entitled to know. It is not a view onto engine state — it is a copy taken at
// the close, so nothing a host holds can change under it mid-tick, and nothing
// truth-side can leak through it.
//
// CONSEQUENCE. A company whose cash runs out is finished. Without that the
// player's decisions cost nothing and none of the rest matters.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Wells;

namespace OGSim.Composition;

// ------------------------------------------------------------- the read model

/// <summary>
/// One belief, as a host renders it (SDD-017 §2, SDD-008 §8).
///
/// <para><b>P90 is the LOW case and P10 the high</b> — the probability of
/// exceeding. This is where the petroleum convention faces a host, and reading
/// the two the statistical way round would show a possible case as a proved
/// one.</para>
///
/// <para>Mu and sigma are deliberately absent. A host given the parameters could
/// quote any quantile it liked, including ones SDD-008 has not pinned; the three
/// that ship are the three the design asks a player to decide on.</para>
/// </summary>
public sealed record BeliefEntryView(
    EntityRef Subject,
    ContentId PropertyKind,
    double P10,
    double P50,
    double P90,
    Provenance BestSource,
    GameDate AsOf);

/// <summary>
/// The month's facts, as an objective reads them and the read model reports
/// them.
///
/// <para>Separate from <see cref="FieldReadModel"/> because the read model also
/// carries the scenario's VERDICT on the month, and an objective that could read
/// its own verdict would be evaluating a circle. What is true is taken once, at
/// stage 12; what it means is decided from it; both are published together at
/// stage 13.</para>
/// </summary>
public sealed record FieldPosition(
    Tick Tick,
    GameDate Date,
    Money Cash,
    int Wells,
    int ActivitiesRunning,
    SurfaceVolume ProducedThisTick,
    bool Insolvent);

/// <summary>
/// What the player can see, rebuilt at the close of every tick.
///
/// <para>Deliberately NOT the full SDD-017 read model — that is R21's whole
/// phase, sixteen projections wide. This is the subset the current loop can
/// honestly fill, and every field in it is a number the player is entitled to:
/// their own cash, their own well count, what they sold. Reservoir pressure is
/// absent because it is truth, and it reaches a host through beliefs or not at
/// all.</para>
///
/// <para><see cref="Beliefs"/> is that "or not at all" answered: it is the only
/// route by which anything the company has LEARNED about the rock reaches a
/// screen, and every entry in it was paid for by an activity that completed
/// (R20d.7).</para>
/// </summary>
public sealed record FieldReadModel(
    Tick Tick,
    GameDate Date,
    Money Cash,
    int Wells,
    int ActivitiesRunning,
    SurfaceVolume ProducedThisTick,
    bool Insolvent,
    ScenarioProgress Progress,
    IReadOnlyList<BeliefEntryView> Beliefs,

    /// <summary>
    /// The chain, element by element, in the order material crosses it
    /// (SDD-017 §2).
    ///
    /// <para>What a production-chain game is watched through: how much went
    /// through each thing, and which thing is refusing. A player who can see
    /// cash and barrels and not this can tell that the field is underperforming
    /// and not why.</para>
    /// </summary>
    IReadOnlyList<ChainElementView> Chain)
{
    /// <summary>Where the chain is jammed, if anywhere — the elements that
    /// refused production this tick.</summary>
    public IReadOnlyList<ChainElementView> Bottlenecks
    {
        get
        {
            var jammed = new List<ChainElementView>();

            for (int i = 0; i < Chain.Count; i++)
                if (Chain[i].IsBottleneck) jammed.Add(Chain[i]);

            return jammed;
        }
    }

    /// <summary>How the run stands (SDD-014 §5a). <c>Pending</c> until the
    /// scenario says otherwise.</summary>
    public ObjectiveState Outcome => Progress.Overall;

    // Finding 131: a record carrying a collection compares it by reference.
    public bool Equals(FieldReadModel? other) =>
        other is not null && Tick == other.Tick && Date == other.Date
        && Cash == other.Cash && Wells == other.Wells
        && ActivitiesRunning == other.ActivitiesRunning
        && ProducedThisTick == other.ProducedThisTick
        && Insolvent == other.Insolvent && Progress == other.Progress
        && Structural.Equal(Beliefs, other.Beliefs)
        && Structural.Equal(Chain, other.Chain);

    public override int GetHashCode() =>
        HashCode.Combine(Tick, Date, Cash, Wells, ActivitiesRunning, ProducedThisTick,
            HashCode.Combine(Insolvent, Progress, Structural.HashOf(Beliefs),
                             Structural.HashOf(Chain)));
}

/// <summary>
/// The one projection from live state to what a player sees.
///
/// <para>Stage 12 <see cref="Take"/>s the month's facts to judge them; stage 13
/// <see cref="Publish"/>es those same facts with the verdict attached. One owner,
/// so the number an objective was measured against and the number the host is
/// shown are the same number (law L5) rather than two reads of a field that
/// nothing stops drifting apart.</para>
/// </summary>
internal sealed class FieldProjection(
    ProductionLoop loop,
    CompanyState company,
    FieldControl field,
    ActivityState activities,
    IBeliefStore beliefs)
{
    public FieldPosition Take(Tick tick, GameDate date, bool insolvent) =>
        new(tick, date, company.Ledger.Cash, field.WellCount, activities.InProgress,
            loop.ProducedThisTick, insolvent);

    public FieldReadModel Publish(FieldPosition position, ScenarioProgress progress) =>
        new(position.Tick, position.Date, position.Cash, position.Wells,
            position.ActivitiesRunning, position.ProducedThisTick, position.Insolvent,
            progress, Project(beliefs), loop.Chain());

    /// <summary>
    /// SDD-008 §8's projection, at the close: everything the company has learned,
    /// as three quantiles and how it came to know them.
    ///
    /// <para>Rebuilt whole rather than diffed (SDD-017 §2's AD2), and a COPY —
    /// the store's own list would go on changing under a host that held last
    /// month's snapshot, which is the mutable handle R21-V1 exists to refuse.</para>
    ///
    /// <para>The quantiles come from <see cref="OGSim.Information.Quantiles"/>
    /// rather than being computed here: the log-space exponentiation and the
    /// P90-is-low convention are one implementation in the module that owns the
    /// distribution, and a second copy at the projection would be free to drift
    /// from it (law L5).</para>
    /// </summary>
    private static IReadOnlyList<BeliefEntryView> Project(IBeliefStore beliefs)
    {
        IReadOnlyList<HeldBelief> held = beliefs.Held;
        var entries = new List<BeliefEntryView>(held.Count);

        for (int i = 0; i < held.Count; i++)
        {
            HeldBelief entry = held[i];

            entries.Add(new BeliefEntryView(
                entry.Subject,
                entry.PropertyKind,
                P10: OGSim.Information.Quantiles.P10(entry.Belief),
                P50: OGSim.Information.Quantiles.P50(entry.Belief),
                P90: OGSim.Information.Quantiles.P90(entry.Belief),
                entry.Belief.BestSource,
                entry.Belief.AsOf));
        }

        return entries;
    }
}

// -------------------------------------------------------------- losing

// Design 09s failure condition, at its simplest true form: a company that
// cannot pay is finished. It is recorded as an AUDIT entry and surfaced on the
// read model rather than as an EngineEvent — an event carries a loop role and a
// player-visibility flag (SDD-001 §8) and those are R21s to decide, not
// something to guess at here.

/// <summary>
/// Stage 13. Publishes the read model and decides whether the company is still
/// playing.
/// </summary>
internal sealed class CloseStage(
    FieldProjection projection,
    ObjectiveStage objectives) : ITickStage
{
    public StageId Id => StageId.Close;

    /// <summary>The tick just closed, as the host reads it.</summary>
    public FieldReadModel? Published { get; private set; }

    public bool Insolvent => objectives.Insolvent;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Stage 12 took the month's facts to judge them; this publishes the same
        // facts with the verdict attached. Re-reading the field here would be a
        // second projection, and a host could be shown a cash figure the
        // objectives were never measured against.
        if (objectives.Position is not FieldPosition position)
            throw new InvariantFault("design 03 §6", null,
                "stage 13 ran without stage 12 having taken the month's position; the " +
                "objectives stage owns the projection and must run first");

        Published = projection.Publish(position, objectives.Progress);
    }
}
