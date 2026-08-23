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
/// <summary>
/// One block of the licence, and whether the company has looked under it
/// (SDD-010 §4b's S1 amendment).
///
/// <para>Published from the first tick, unlike the structures inside it: a
/// company knows the shape of the acreage it holds. What it does not know is
/// what is under any of it, and that is what <see cref="Surveyed"/> tracks.</para>
/// </summary>
public sealed record BlockView(
    EntityRef Block,
    Coordinate Centre,
    Length Wide,
    Length Tall,

    /// <summary>Whether this block has been shot.</summary>
    bool Surveyed,

    /// <summary>
    /// How many structures the company knows of inside it.
    ///
    /// <para><b>Zero means two different things and the flag beside it tells
    /// them apart.</b> Unsurveyed and zero is ignorance; surveyed and zero is a
    /// result — ground the company has paid to rule out, and the reason a
    /// barren block should look settled rather than untouched.</para>
    /// </summary>
    int Structures);

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
/// <summary>
/// What the waterflood is doing (SDD-003 §3.1d, SDD-017 §2).
///
/// <para><c>Headroom</c> is the actionable one: it is what the injector will
/// still take this month once the produced water has had its share, so a flood
/// asking for more than that is being clamped and the answer is to unplug the
/// well rather than to raise the target.</para>
/// </summary>
/// <summary>
/// The weather a region just had, and what the process says about the week
/// ahead (SDD-016 §1, §4).
///
/// <para>Severity and temperature are two content curves over ONE state, which
/// is why a rough day is also a cold one here rather than by coincidence.</para>
///
/// <para><c>Forecast</c> is the analytic predictive distribution of the same
/// AR(1) — <c>Expected</c> is ρ^h·x and <c>Sigma</c> grows with the horizon
/// because ρ^h falls. There is no second forecast model to drift from the
/// generator, so a host cannot show an optimism the engine does not share.</para>
/// </summary>
public sealed record WeatherView(
    double Severity,
    Temperature Ambient,
    OGSim.Environment.Forecast Forecast,

    /// <summary>
    /// Months left before the access window shuts, 0 when it is already shut
    /// (R21.6's coverage record, "access windows with time remaining").
    ///
    /// <para>`WeatherState.MonthsUntilAccessCloses` was built and documented
    /// at R22.6 and had no reader anywhere in `OGSim.Composition` — the same
    /// shape this project keeps finding (findings 149, 200, 207, 252): a real
    /// mechanism with a real unit test, joined to nothing.</para>
    /// </summary>
    int MonthsUntilAccessCloses);

public sealed record WaterFloodView(
    double Target,
    ReservoirVolume Imported,
    ReservoirVolume Headroom,

    /// <summary>
    /// How sour the field has become, 0..1 against the souring reference
    /// (SDD-012 §5).
    ///
    /// <para>ON THE FLOOD VIEW, because that is what caused it. Sea water
    /// carries the sulphate the bacteria eat, so souring is the delayed half of
    /// the price of a flood — and a player who saw only "equipment is failing
    /// more" would be watching a symptom with no cause on the screen beside
    /// it.</para>
    ///
    /// <para>A BELIEF-SIDE number in the making. It is truth today, like the
    /// rest of this view, and becomes a produced-fluid sample the day
    /// observations carry composition (SDD-012 §5).</para>
    /// </summary>
    double Sourness);

/// <summary>
/// R21 §2.4b — what the field holds and what room is left (R21.6).
///
/// <para>Both, because either alone is unreadable: a mass held says nothing
/// about whether the field is about to shut itself in, and ullage alone says
/// nothing about what is waiting to be sold.</para>
/// </summary>
public readonly record struct StorageView(Mass Held, Mass Ullage);

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
    /// The licence, block by block (SDD-010 §4b's S1 amendment).
    ///
    /// <para>What the opening move is aimed with. A player who can see prospects
    /// and not blocks can only choose between structures already found, which is
    /// the game that skipped exploration; this is the list that answers "where
    /// do I look first".</para>
    /// </summary>
    IReadOnlyList<BlockView> Blocks,

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
    double CostIndex,

    /// <summary>
    /// What the company can still get out and sell at a profit (SDD-009 §4) —
    /// the number an oil company is actually valued on.
    ///
    /// <para>Cash and production between them cannot say this. A field run to
    /// exhaustion improves both right up to the month it stops, and a player
    /// watching only those would see a business getting healthier as it ran
    /// out.</para>
    ///
    /// <para>It moves with the MARKET as well as with the rock, because reserves
    /// end where production stops paying: a crash raises the economic limit,
    /// the tail of the decline falls below it, and the barrels beyond stop being
    /// reserves without having gone anywhere.</para>
    /// </summary>
    /// <summary>
    /// What the weather is doing, and what it is expected to do (SDD-016 §1, §4).
    ///
    /// <para>Published rather than held, because a player deciding whether to
    /// start a month-long job in November needs the same number the engine will
    /// use. The forecast is the analytic predictive distribution of the same
    /// process, so a host cannot be shown an optimism the engine does not
    /// share.</para>
    /// </summary>
    WeatherView Weather,

    ReservesEstimate Reserves,

    /// <summary>
    /// Additions over the trailing twelve months against what was produced in
    /// them — IR2's standing indicator for the liquidation spiral (SDD-009 §4).
    ///
    /// <para>Reported on PROVED, because that is what the company's own lender
    /// redetermines the borrowing base from: a ratio on 2P would include volumes
    /// no bank will lend against, so a company could show replacement above one
    /// while its facility shrank every quarter.</para>
    ///
    /// <para>NULL means not defined rather than zero — under twelve months of
    /// history there is no window to measure, and a company that produced
    /// nothing has an empty denominator. Reporting 0.0 in either case would
    /// state a replacement failure that did not happen.</para>
    /// </summary>
    double? ReserveReplacementRatio,

    /// <summary>
    /// What the bank will lend and at what price (SDD-009 §5). A company that
    /// could not see its own borrowing base could not tell whether a development
    /// was fundable, which is the decision the facility exists to enable.
    /// </summary>
    BorrowingTerms Borrowing,

    /// <summary>
    /// Where the company stands against its covenant. The CURE WINDOW is the
    /// point: a breach starts a clock rather than calling the loan, and a player
    /// who could not see the clock would experience the amortisation as an
    /// ambush rather than as the consequence of ignoring a warning.
    /// </summary>
    CovenantStatus Covenant,

    /// <summary>How much is drawn.</summary>
    Money Debt,

    /// <summary>
    /// Everything this company has ever burned, and its record (SDD-012 §4).
    ///
    /// <para>Both, because one without the other cannot be acted on. The mass
    /// says what is happening; the standing says what it is costing, and a
    /// player charged a spread they could not trace to a cause would be paying a
    /// penalty they had no way to answer — which is the whole of finding
    /// 172.</para>
    /// </summary>
    Mass Flared,

    double EsgStanding,

    /// <summary>
    /// The flood: what was asked for, what was bought, and what room is left
    /// (SDD-003 §3.1d's R20d.24 amendment).
    ///
    /// <para>All three, because a target MET and a target CLAMPED are different
    /// situations with opposite answers — the first says the flood is working
    /// and the second says the injector is full, and what to do about the second
    /// is an acid job rather than a bigger number. A player shown only the
    /// target could not tell which they had.</para>
    /// </summary>
    WaterFloodView Flood,

    /// <summary>
    /// What the field is HOLDING and what room is left to hold it — R21 §2.4b's
    /// "tank levels and ullage", which that table has required since it was
    /// written and nothing published (R21.6).
    ///
    /// <para>A cash balance says what the company has been PAID; this says what
    /// it has produced and not yet sold. The two are different questions and
    /// only the first was answerable, so a host could not distinguish a company
    /// that is poor from one that is merely illiquid — which is the same gap
    /// finding 190 records from the cash-flow side, and the reason any mechanic
    /// that defers revenue currently reads as a failure (SDD-006 §7a.2).</para>
    ///
    /// <para>Ullage rather than capacity, because what a player acts on is the
    /// room left: a tank at 90% and a tank at 10% of a vessel ten times the size
    /// hold the same oil and are in completely different trouble.</para>
    /// </summary>
    StorageView Storage,

    /// <summary>
    /// WHERE THE MONEY WENT THIS MONTH, by cause — R21 §2.4b's "cost and revenue
    /// by cause for the period", and finding 190's other half.
    ///
    /// <para>The surface published a cash BALANCE and nothing about the period,
    /// so nothing could tell a month of investment from a month of decline. The
    /// reference client plugged fields for being under repair, and could not
    /// have known better: falling cash looks the same whether a company is
    /// dying or building.</para>
    ///
    /// <para>In `CostLedger.Causes` order, one entry per cause including the
    /// zeroes — a host renders a row that reads nothing this month, and an
    /// absent row would be indistinguishable from a cause that does not
    /// exist.</para>
    /// </summary>
    IReadOnlyList<Money> CashByCause,

    /// <summary>
    /// WHAT THE COMPANY IS DOING — R21 §2.4b row 15, using SDD-017 §2's
    /// `OperationView`, which has been specified since that document was written
    /// and filled by nothing (R21.6).
    ///
    /// <para>`ActivitiesRunning` counted them. A count cannot answer the question
    /// the row exists for — WHICH operations, how far along, and what each has
    /// cost so far — so a player could see the rig was busy and not whether a
    /// well was nearly down or barely started, and could not tell an operation
    /// that had stalled from one progressing normally.</para>
    /// </summary>
    IReadOnlyList<OperationView> Operations)
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
        && Structural.Equal(Wellbores, other.Wellbores)
        && Structural.Equal(CashByCause, other.CashByCause)
        && Structural.Equal(Operations, other.Operations);

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
    OGSim.Information.ProspectRisks risks,
    ReservesBook reserves,
    Bank bank,
    ReserveHistory history,
    OGSim.Environment.WeatherState weather,
    EsgAssessment esg)
{
    /// <summary>The one region this world has (SDD-016 §1).</summary>
    private const int FieldRegion = 0;

    /// <summary>The last day of the month just closed: the projection is built at
    /// the close, so "today" is day 29 of the thirty just simulated.</summary>
    private const int DayJustEnded = (int)Duration.DaysPerTick - 1;

    /// <summary>A week ahead — long enough to matter to a decision about starting
    /// a job, short enough that ρ^h has not collapsed to the seasonal mean.</summary>
    private const int ForecastHorizonDays = 7;

    public FieldPosition Take(Tick tick, GameDate date, bool insolvent) =>
        new(tick, date, company.Ledger.Cash, field.WellCount, activities.InProgress,
            loop.ProducedThisTick, insolvent);

    public FieldReadModel Publish(FieldPosition position, ScenarioProgress progress) =>
        new(position.Tick, position.Date, position.Cash, position.Wells,
            position.ActivitiesRunning, position.ProducedThisTick, position.Insolvent,
            progress, Project(beliefs), loop.Chain(), field.Wells(), Prospects(), Blocks(),
            loop.Market.OilPrice, loop.Market.CostIndex,
            // WHAT THE FIELD RAN AT, taken from the loop rather than recomputed
            // here. This asked `TemperatureOn(lastDayOfTheMonth)` while the solve
            // used a per-segment mean, so a host was shown a temperature the
            // field never ran at — one fact with two owners (L5), and the kind
            // that never fails a test because both answers are plausible.
            new WeatherView(
                loop.SeverityThisTick,
                loop.AmbientThisTick,
                weather.Look(FieldRegion, ForecastHorizonDays),
                weather.MonthsUntilAccessCloses(FieldRegion, position.Date)),
            reserves.Remaining(loop.CumulativeProduced),
            history.Ratio(
                reserves.Remaining(loop.CumulativeProduced).Proved,
                loop.CumulativeProduced),
            bank.Terms, bank.Covenant, bank.Drawn,
            loop.CumulativeFlared,
            esg.Of(),
            new WaterFloodView(
                loop.VoidageReplacement, loop.ImportedThisTick, loop.InjectionHeadroom,
                loop.SourFraction),
            loop.Storage,
            company.Ledger.CashByCause(position.Tick),
            activities.Operations());

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
    /// The licence's blocks, and what the company has found under each.
    /// </summary>
    /// <remarks>
    /// Derived every tick rather than stored (law L5): the grid is a function of
    /// the ground and the count is a function of what is known, so neither can
    /// drift from what it describes.
    /// </remarks>
    private IReadOnlyList<BlockView> Blocks()
    {
        var blocks = new List<BlockView>();

        Length wide = world.BlockWidth;
        Length tall = world.BlockHeight;
        int count = world.BlockCount;

        for (int i = 0; i < count; i++)
        {
            EntityId<IBlock> block = world.BlockAt(i);
            Coordinate centre = world.CentreOf(block);

            blocks.Add(new BlockView(
                new EntityRef(EntityKind.Block, block.Value),
                centre,
                wide,
                tall,
                world.WasShot(block),
                Structures(centre, wide, tall)));
        }

        return blocks;
    }

    /// <summary>
    /// How many KNOWN structures sit inside a patch.
    /// </summary>
    /// <remarks>
    /// Counted from belief and not from truth, which is the whole discipline of
    /// this projection: counting what the world placed would tell a player how
    /// many structures an unshot block holds, and that is the answer the survey
    /// is being sold.
    /// </remarks>
    private int Structures(Coordinate centre, Length wide, Length tall)
    {
        double left = centre.X - (wide.Metres * 0.5);
        double right = centre.X + (wide.Metres * 0.5);
        double bottom = centre.Y - (tall.Metres * 0.5);
        double top = centre.Y + (tall.Metres * 0.5);

        IReadOnlyList<EntityId<IProspect>> prospects = world.Prospects;
        int found = 0;

        for (int i = 0; i < prospects.Count; i++)
        {
            if (!risks.Knows(new EntityRef(EntityKind.Prospect, prospects[i].Value)))
                continue;

            Coordinate at = world.PositionOf(prospects[i]);

            if (at.X < left || at.X >= right) continue;
            if (at.Y < bottom || at.Y >= top) continue;

            found++;
        }

        return found;
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
