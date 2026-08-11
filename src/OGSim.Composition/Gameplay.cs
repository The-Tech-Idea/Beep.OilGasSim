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
/// One well, as a host renders it and a client acts on it (SDD-017 §2's R21.5
/// amendment).
///
/// <para>Narrower than SDD-017's <c>WellView</c>, which carries a site, an
/// operating point and sampled curves the current loop has no source for. What
/// it does carry is the part every well-level command needs: WHICH well, and
/// what state it is in — because a read model that reported only how MANY wells
/// there were could be looked at and not acted on.</para>
/// </summary>
public sealed record WellStatusView(
    EntityRef Well,
    string DisplayId,
    WellStatus Status,
    SurfaceVolume ProducedThisTick);

/// <summary>
/// An undrilled structure and what the company thinks its chances are
/// (SDD-008 §4, SDD-017 §2's R20d.7 amendment).
///
/// <para>THE CHOICE THE EXPLORATION GAME IS MADE OF. A basin offers dozens of
/// these and a company can afford to drill a handful, so the whole of the early
/// game is reading this list and being wrong about it in an informed way.</para>
///
/// <para>POS is the PRODUCT of five factor means, and it is carried alongside
/// them rather than instead of them: "one chance in six" tells a player what to
/// expect and not what to do about it, whereas "one chance in six, and it is the
/// seal we doubt" is the difference between drilling and shooting more
/// seismic.</para>
/// </summary>
public sealed record ProspectView(
    EntityRef Prospect,

    /// <summary>
    /// The petroleum system it draws on. THE FIELD THAT SAYS TWO BETS ARE NOT
    /// INDEPENDENT: source, reservoir and seal are one belief across a play, so
    /// a dry hole on any prospect in it re-prices all the others. A player who
    /// cannot see this is choosing between prospects that look separate and are
    /// not, and cannot reason about spreading risk across plays at all.
    /// </summary>
    ContentId Play,

    Coordinate At,
    Length ToMarket,
    double ProbabilityOfSuccess,
    double Source,
    double Reservoir,
    double Seal,
    double Trap,
    double Timing);

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
    IReadOnlyList<ChainElementView> Chain,

    /// <summary>
    /// Every well the company has, and what state it is in (SDD-017 §2).
    ///
    /// <para>The list a well-level command is aimed with: shutting one in,
    /// re-opening it or abandoning it all name a well, and a count cannot be
    /// named.</para>
    /// </summary>
    IReadOnlyList<WellStatusView> Wellbores,

    /// <summary>
    /// Every structure the world placed that the company has not drilled, with
    /// what it believes about each (SDD-017 §2's R20d.7 amendment).
    ///
    /// <para>Empty for a hand-built field, which is correct rather than missing:
    /// a prospect is something a world GENERATED and a scenario that placed its
    /// reservoir directly has nothing to explore.</para>
    /// </summary>
    IReadOnlyList<ProspectView> Prospects,

    /// <summary>
    /// What a tonne of oil fetches this month (SDD-009 §6).
    ///
    /// <para>A read model that showed a company its cash and not the price it
    /// was earning would let a player watch revenue fall and be unable to tell a
    /// declining field from a falling market — which are the same number and
    /// opposite decisions: one says build, the other says wait.</para>
    /// </summary>
    Money OilPrice,

    /// <summary>
    /// What a day of work costs, against the opening year (SDD-009 §6's ED4).
    ///
    /// <para>Carried because with a moving index the CATALOGUE price is no
    /// longer the price: a player told a well costs eight million and charged
    /// eleven has been misled by the surface, not by the market. A host
    /// multiplies a listed cost by this to show what it would actually be
    /// quoted.</para>
    /// </summary>
    double CostIndex)
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
        && Structural.Equal(Chain, other.Chain)
        && Structural.Equal(Wellbores, other.Wellbores);

    public override int GetHashCode() =>
        HashCode.Combine(Tick, Date, Cash, Wells, ActivitiesRunning, ProducedThisTick,
            HashCode.Combine(Insolvent, Progress, Structural.HashOf(Beliefs),
                             Structural.HashOf(Chain), Structural.HashOf(Wellbores)));
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
    IBeliefStore beliefs,
    WorldState world,
    OGSim.Information.ProspectRisks risks)
{
    public FieldPosition Take(Tick tick, GameDate date, bool insolvent) =>
        new(tick, date, company.Ledger.Cash, field.WellCount, activities.InProgress,
            loop.ProducedThisTick, insolvent);

    public FieldReadModel Publish(FieldPosition position, ScenarioProgress progress) =>
        new(position.Tick, position.Date, position.Cash, position.Wells,
            position.ActivitiesRunning, position.ProducedThisTick, position.Insolvent,
            progress, Project(beliefs), loop.Chain(), field.Wells(), Prospects(),
            loop.Market.OilPrice, loop.Market.CostIndex);

    /// <summary>
    /// The undrilled structures, in the order the world placed them (D-5).
    ///
    /// <para>Rebuilt whole each tick like every other part of the read model,
    /// because POS moves when a well reports and a host holding last month's
    /// list would be choosing against a number the company no longer
    /// believes.</para>
    /// </summary>
    private IReadOnlyList<ProspectView> Prospects()
    {
        var seen = new List<ProspectView>();

        IReadOnlyList<EntityId<IProspect>> prospects = world.Prospects;

        for (int i = 0; i < prospects.Count; i++)
        {
            var at = new EntityRef(EntityKind.Prospect, prospects[i].Value);

            if (!risks.Knows(at)) continue;

            OGSim.Information.ProspectRisk risk = risks.Of(at);

            seen.Add(new ProspectView(
                at,
                risks.PlayOf(at),
                world.PositionOf(prospects[i]),
                world.DistanceToMarket(prospects[i]) ?? new Length(0.0),
                risk.ProbabilityOfSuccess,
                OGSim.Information.ProspectRisk.MeanOf(risk[PosFactor.Source]),
                OGSim.Information.ProspectRisk.MeanOf(risk[PosFactor.Reservoir]),
                OGSim.Information.ProspectRisk.MeanOf(risk[PosFactor.Seal]),
                OGSim.Information.ProspectRisk.MeanOf(risk[PosFactor.Trap]),
                OGSim.Information.ProspectRisk.MeanOf(risk[PosFactor.Timing])));
        }

        return seen;
    }

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
