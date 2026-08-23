// Oilfield Engineer — the operating company's workflow cycle (plans 23 §4's
// 2026-08-23 amendment).
//
// THE SAME ENGINE AS OILFIELD DAYS, COMPOSED AT THE OTHER NAMED MODE. Nothing
// in here is a second simulation: every month is one tick of the same fourteen
// stages, every action is one of the same eighteen commands, and everything this
// client knows it read off the same read model the Godot host reads. What makes
// it Oilfield Engineer is `GameModes.Engineer` and nothing else.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Engineer;

/// <summary>Where a month's work went.</summary>
/// <remarks>
/// Named phases rather than a log line, because the phase is the thing this
/// client exists to make visible: an operating company's month is a CYCLE, and
/// a run that never leaves <see cref="Phase.Appraise"/> is a different problem
/// from one that never leaves <see cref="Phase.Maintain"/>.
/// </remarks>
public enum Phase
{
    /// <summary>Nothing was ordered — the field simply ran.</summary>
    Produce = 0,

    /// <summary>A block was shot, because the licence still holds dark ground.</summary>
    Explore = 1,

    /// <summary>A prospect was sharpened before anyone spent a hole on it.</summary>
    Appraise = 2,

    /// <summary>A well was ordered.</summary>
    Drill = 3,

    /// <summary>The chain was widened where it was refusing throughput.</summary>
    Debottleneck = 4,

    /// <summary>Something broken was repaired.</summary>
    Maintain = 5,

    /// <summary>
    /// A processing train was commissioned, because there was none.
    /// </summary>
    /// <remarks>
    /// Only ever reached on a starting state that opens on bare ground. An
    /// operating company opens holding a plant and never builds a first one —
    /// which is why this phase is absent from an Oilfield Engineer run and
    /// present in an Oilfield Days one, off the same cycle and the same code.
    /// </remarks>
    Build = 6,
}

/// <summary>One month of the cycle, as the client saw it.</summary>
public sealed record CycleMonth(
    Tick Tick,
    GameDate Date,
    Phase Phase,
    Money Cash,
    int Wells,
    SurfaceVolume Produced,
    string Note);

/// <summary>What a run came to.</summary>
public sealed record CycleRun(
    IReadOnlyList<CycleMonth> Months,
    Money Closing,
    int Wells,
    SurfaceVolume Lifetime,
    bool Insolvent,

    /// <summary>
    /// What the company was left holding, element by element and well by well.
    /// </summary>
    /// <remarks>
    /// A run that produces nothing looks identical whether it never found oil,
    /// never built a train, or built one and left every well shut in — three
    /// problems with three different answers. This is what tells them apart.
    /// </remarks>
    IReadOnlyList<string> Standing);

/// <summary>
/// The operating company's month, as one ordered cycle.
/// </summary>
/// <remarks>
/// <para><b>The order is the point, and it is the order an operator works in.</b>
/// Fix what is broken before widening anything, because a repaired train is
/// cheaper than a bigger broken one. Widen before drilling, because a well into
/// a chain that is already refusing throughput buys nothing. Drill what is
/// known before appraising what is not, and explore only when there is nothing
/// better to do with the month.</para>
///
/// <para><b>One action a month.</b> Not a rule of the engine — a company can
/// order several — but a policy, and it is what makes the phase readable: a
/// month has a reason, and the report can name it.</para>
///
/// <para><b>It refuses to know anything the surface does not offer.</b> Same
/// constraint as the reference client (R21 §2.5): if this needs a fact the read
/// model does not carry, the surface is incomplete and that is a finding, not a
/// reason to reach into a module.</para>
/// </remarks>
public sealed class WorkflowCycle
{
    private readonly Engine _engine;

    /// <summary>
    /// The odds below which a prospect is not worth a hole.
    /// </summary>
    /// <remarks>
    /// An operator's number, not a game's: a company with a producing field has
    /// somewhere better to put the money than a one-in-eight chance, which is
    /// exactly the judgement Oilfield Days cannot afford to make because it has
    /// no field yet. The two modes differ here in policy as well as in rules.
    /// </remarks>
    private readonly double _drillAbove;


    /// <summary>
    /// How deep a hole goes.
    /// </summary>
    /// <remarks>
    /// The read model carries a prospect's odds and its position, and not a
    /// depth to drill it to — that is the company's decision, not a fact about
    /// the prospect, and a client that read one off the board would be reading
    /// something nobody has measured yet.
    /// </remarks>
    private static readonly Length TotalDepth = new(2000.0);

    /// <summary>
    /// The cash below which the company stops buying information.
    /// </summary>
    /// <remarks>
    /// <para><b>Information is worthless to a company that cannot act on it.</b>
    /// The first version of this policy spent something every month and went
    /// insolvent in month 25 holding six 3-D surveys and one half-drilled hole —
    /// it had bought a very good picture of ground it could no longer afford to
    /// drill.</para>
    ///
    /// <para><b>It is what is still to be BOUGHT, not a constant.</b> The second
    /// version used one figure for both products and died on bare ground in
    /// month 38, holding a discovery and $0.1M against a $22M plant. A company
    /// that has not built a train yet has to keep the price of one back before
    /// it spends a penny on looking around — which is exactly the arithmetic
    /// recorded in plans 22 §8, arrived at from the other direction.</para>
    /// </remarks>
    private static Money Reserve(FieldReadModel now) =>
        Standing(now) ? HoleAndRunning : HoleAndRunning + Plant;

    /// <summary>A hole and a year of running costs.</summary>
    private static readonly Money HoleAndRunning = Money.FromMillions(14.0);

    /// <summary>An early production facility.</summary>
    private static readonly Money Plant = Money.FromMillions(22.0);

    /// <summary>
    /// Whether a processing train exists.
    /// </summary>
    /// <remarks>
    /// <para><b>Asked for the HEADER, not for an empty chain.</b> "No elements
    /// is no plant" was wrong and cost this policy a run: a well drilled on bare
    /// ground puts its own gathering line in the chain, so the chain is already
    /// one element long before there is anywhere for that line to go. The
    /// company then sized its reserve as though the train were bought and spent
    /// the plant money on seismic.</para>
    ///
    /// <para>The manifold is the head of the train — nothing else can be reached
    /// without it — which is the same thing <c>PlantBuilder.Standing</c> asks
    /// inside the engine, asked here through the only door a client has.</para>
    /// </remarks>
    private static bool Standing(FieldReadModel now)
    {
        for (int i = 0; i < now.Chain.Count; i++)
            if (now.Chain[i].DisplayId == "manifold")
                return true;

        return false;
    }

    /// <summary>Prospects already shot with 3-D.</summary>
    /// <remarks>
    /// A second survey over the same closure re-prices nothing — the belief has
    /// already moved to what the seismic says. Without this the policy shot the
    /// same prospect six times, which is the most expensive way there is to
    /// learn nothing.
    /// </remarks>
    private readonly HashSet<ulong> _appraised = [];

    public WorkflowCycle(Engine engine, double drillAbove)
    {
        ArgumentNullException.ThrowIfNull(engine);

        _engine = engine;
        _drillAbove = drillAbove;
    }

    /// <summary>Run the cycle for this many months, or until it goes insolvent.</summary>
    public CycleRun Run(int months)
    {
        var log = new List<CycleMonth>();
        var lifetime = 0.0;

        for (var month = 0; month < months; month++)
        {
            // THE FIRST MONTH IS A LOOK, NOT A MOVE. A read model is produced
            // BY a tick (design 03 §6's Close), so before the first one there
            // is nothing to decide from — which is also true of a human player
            // opening a new game.
            (Phase phase, string note) = _engine.ReadModel is FieldReadModel now
                ? Decide(now)
                : (Phase.Produce, "opened the books");

            _engine.Pipeline.AdvanceTick();

            FieldReadModel after = Read();

            lifetime += after.ProducedThisTick.CubicMetres;

            log.Add(new CycleMonth(
                after.Tick, after.Date, phase, after.Cash, after.Wells,
                after.ProducedThisTick, note));

            if (after.Insolvent)
                break;
        }

        FieldReadModel end = Read();

        var standing = new List<string>();

        // WITH THROUGHPUT, because that is what says WHERE flow stops. A chain
        // that exists and passes nothing is a different fault from a chain with
        // a hole in it, and the element list alone cannot tell them apart.
        for (int i = 0; i < end.Chain.Count; i++)
            standing.Add(end.Chain[i].DisplayId + "="
                         + end.Chain[i].Throughput.Kilograms.ToString(
                             "F0", System.Globalization.CultureInfo.InvariantCulture));

        for (int i = 0; i < end.Wellbores.Count; i++)
            standing.Add(end.Wellbores[i].DisplayId + ":" + end.Wellbores[i].Status);

        return new CycleRun(
            log, end.Cash, end.Wells, new SurfaceVolume(lifetime), end.Insolvent, standing);
    }

    /// <summary>
    /// The month's one decision, in the order an operator makes it.
    /// </summary>
    private (Phase Phase, string Note) Decide(FieldReadModel now)
    {
        // 1. FIX WHAT IS BROKEN. Everything downstream of a failed element is
        //    shut in, so this is worth more than any amount of new capacity.
        for (int i = 0; i < now.Chain.Count; i++)
        {
            ChainElementView element = now.Chain[i];

            if (element.Failed && Order(new RepairEquipmentCommand(element.Element)))
                return (Phase.Maintain, "repaired the " + element.DisplayId);
        }

        // 2. WIDEN WHAT IS REFUSING. A chain that is throttling turns every
        //    barrel the wells could lift into a barrel nobody sold.
        for (int i = 0; i < now.Chain.Count; i++)
        {
            ChainElementView element = now.Chain[i];

            if (element.IsBottleneck && Order(Wider(element.DisplayId)))
                return (Phase.Debottleneck, "widened the " + element.DisplayId);
        }

        // 3. BUILD A PLANT IF THERE IS NONE AND SOMETHING TO PUT THROUGH IT.
        //    Above the reserve line and above drilling both, because a company
        //    holding a discovery and no train is earning nothing from it: a
        //    second hole into nowhere is worth strictly less than the thing
        //    that makes the first one pay.
        //
        //    ORDERED WHENEVER THERE IS A WELL, and refused by the engine when
        //    a plant already stands. The client holds no copy of that rule: a
        //    second statement of when a facility may be commissioned is a
        //    second thing to get wrong.
        //    ASKED OF THE WELLBORES, not the well COUNT. A well drilled on
        //    ground with no plant is suspended — shut in, because there is
        //    nowhere to send it (plans 23) — and a shut-in well is exactly the
        //    one that justifies a train. Counting only flowing wells would wait
        //    for production that cannot start until the thing is built.
        if (now.Wellbores.Count > 0 && Order(new InstallEarlyProductionFacilityCommand()))
            return (Phase.Build, "commissioned an early production facility");

        // 4. DRILL WHAT IS WORTH DRILLING — OR THE BEST THERE IS, when there
        //    is no longer any way to find out more.
        //
        //    A FLOOR WITH NO ALTERNATIVE IS A STALL. Held on its own, the bar
        //    sat this company on 24% odds for five years buying nothing and
        //    drilling nothing, until the running costs finished it: it could
        //    not afford to look any harder and would not act on what it had.
        //    When information is out of reach, the best on the board IS the
        //    decision. (The Godot client's auto-player met the same wall from
        //    the other side — a finished map rather than an empty account.)
        //    "No longer any way to find out more" is the MAP, not the purse:
        //    measured before this read the blocks, a rich company sat on a
        //    fully surveyed, fully appraised board for a hundred months and
        //    never drilled at all, because the fallback below armed only when
        //    the money ran low.
        bool canLookHarder = now.Cash >= Reserve(now) && StillDark(now);

        if (Best(now) is ProspectView best
            && (best.ProbabilityOfSuccess >= _drillAbove || !canLookHarder))
        {
            var aim = new EntityId<IProspect>(best.Prospect.Value);

            if (Order(new DrillWellCommand(aim, TotalDepth)))
                return (Phase.Drill, $"drilled at {best.ProbabilityOfSuccess:P0}");
        }

        // NOTHING BELOW THE RESERVE IS BOUGHT. Repairs and debottlenecking are
        // above this line on purpose — they are the field it already has, and a
        // company stops exploring long before it stops fixing its own plant.
        if (now.Cash < Reserve(now))
            return (Phase.Produce, Standing(now)
                ? "held the reserve"
                : "held the reserve — a plant still to buy");

        // 5. SHARPEN WHAT IS NOT, ONCE. 3-D moves trap and reservoir, which is
        //    what turns a prospect nobody would drill into one somebody would —
        //    and having moved them, it has nothing left to say.
        if (Best(now) is ProspectView marginal
            && _appraised.Add(marginal.Prospect.Value)
            && Order(new SeismicSurveyCommand(new EntityId<IProspect>(marginal.Prospect.Value))))
            return (Phase.Appraise,
                    $"shot 3-D over a {marginal.ProbabilityOfSuccess:P0} prospect");

        // 6. AND LOOK AT GROUND NOBODY HAS LOOKED AT, when there is nothing
        //    better to do with the month.
        for (int i = 0; i < now.Blocks.Count; i++)
        {
            BlockView block = now.Blocks[i];

            if (!block.Surveyed
                && Order(new SurveyBlockCommand(new EntityId<IBlock>(block.Block.Value))))
                return (Phase.Explore, "shot 2-D over dark ground");
        }

        return (Phase.Produce, "ran the field");
    }

    /// <summary>Whether looking harder is still possible at all — dark
    /// ground left to shoot, or a best prospect 3-D has not moved yet.</summary>
    private bool StillDark(FieldReadModel now)
    {
        for (int i = 0; i < now.Blocks.Count; i++)
            if (!now.Blocks[i].Surveyed)
                return true;

        return Best(now) is ProspectView best
               && !_appraised.Contains(best.Prospect.Value);
    }

    /// <summary>The best prospect on the board, or none.</summary>
    private static ProspectView? Best(FieldReadModel now)
    {
        ProspectView? best = null;

        for (int i = 0; i < now.Prospects.Count; i++)
            if (best is null
                || now.Prospects[i].ProbabilityOfSuccess > best.ProbabilityOfSuccess)
                best = now.Prospects[i];

        return best;
    }

    /// <summary>The next rung for whatever is refusing throughput.</summary>
    private static Command Wider(string element) => element switch
    {
        var e when e.Contains("separator", StringComparison.Ordinal) => new InstallSeparatorCommand(),
        var e when e.Contains("manifold", StringComparison.Ordinal) => new InstallManifoldCommand(),
        var e when e.Contains("gas", StringComparison.Ordinal) => new InstallGasPlantCommand(),
        var e when e.Contains("treater", StringComparison.Ordinal) => new InstallTreaterCommand(),
        var e when e.Contains("tank", StringComparison.Ordinal) => new InstallTankCommand(),
        var e when e.Contains("compressor", StringComparison.Ordinal) => new InstallCompressorCommand(),
        var e when e.Contains("pump", StringComparison.Ordinal) => new InstallLiquidPumpStationCommand(),
        _ => new ExpandExportCommand(),
    };

    private bool Order(Command command) => _engine.Commands.Submit(command) is Accepted;

    private FieldReadModel Read() =>
        _engine.ReadModel ?? throw new InvariantFault("SDD-017 §2", null,
            "the engine has no read model; a client can only act on what it can see");
}
