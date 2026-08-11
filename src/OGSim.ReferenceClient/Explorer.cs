// R21.5 — a client that explores (R21 §2.5, SDD-008 §4).
//
// THE OTHER HALF OF THE REFERENCE CLIENT. `Operator` is handed a field and
// develops it, which proves the production surface. Nothing had ever tried to
// play the part BEFORE the field: read a basin's prospects, decide which is
// worth a survey, decide which is worth a hole, be wrong about it, and pay for
// being wrong.
//
// That is the part the whole information layer exists to serve, and until this
// nothing had once tried to consume it — the same shape as every finding this
// project keeps catching. If a decision cannot be taken from `ReadModel` and
// `Commands` alone, it cannot be taken by a host either, and this is where that
// shows up.
//
// IT REMEMBERS WHAT IT DRILLED, and that is a deliberate choice rather than an
// oversight. A host legitimately knows its own history. What it must NOT need is
// knowledge the engine has and the surface does not expose — so everything about
// the WORLD comes off the read model, and only the client's own actions come
// from its own memory.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.ReferenceClient;

/// <summary>
/// What a drilling campaign came to.
///
/// <para>NOT `Campaign`, which is a scenario's chapters (SDD-014). One concept
/// one name, and the other concept had the name first (rule N1).</para>
/// </summary>
public sealed record DrillingSeason(
    ObjectiveState Outcome,
    Tick Ended,
    Money Cash,
    int Surveyed,
    int Drilled,
    int Discoveries,
    int DryHoles);

/// <summary>
/// R21 §2.5's exploring client. One policy, applied every month, taken entirely
/// from the read model.
/// </summary>
public sealed class Explorer
{
    private readonly Engine _engine;
    private readonly double _drillAbove;
    private readonly int _wellTarget;

    // The client's OWN history: which structures it has put a hole in. Not a
    // fact about the world — a fact about this company — so remembering it here
    // is a host doing its job rather than a surface being short of something.
    private readonly HashSet<ulong> _drilled = [];

    private int _surveyed;
    private int _discoveries;
    private int _dryHoles;

    // A hole is down and the answer has not come back yet.
    private bool _awaitingResult;

    /// <summary>
    /// <paramref name="drillAbove"/> is the probability of success at which this
    /// company will commit a rig. Below it, the prospect is worth a survey and
    /// not a hole — which is the decision the five factors exist to inform.
    /// </summary>
    /// <summary>
    /// <paramref name="buildBelow"/> is the cost index above which this company
    /// will not commit capital (SDD-009 §6's ED4). A boom is a good time to
    /// PRODUCE and a bad time to BUILD, and those are different verbs — a client
    /// that cannot tell them apart pays cycle prices for everything it puts up.
    ///
    /// <para>`double.MaxValue` is a company that never looks at the market,
    /// which is the comparison this exists to lose to.</para>
    /// </summary>
    public Explorer(Engine engine, double drillAbove, int wellTarget, double buildBelow)
    {
        ArgumentNullException.ThrowIfNull(engine);

        _engine = engine;
        _drillAbove = drillAbove;
        _wellTarget = wellTarget;
        _buildBelow = buildBelow;
    }

    private readonly double _buildBelow;

    public DrillingSeason Play(int months)
    {
        for (var month = 0; month < months; month++)
        {
            FieldReadModel? seen = _engine.ReadModel;

            if (seen is not null && seen.ActivitiesRunning == 0)
            {
                if (seen.Wells > 0) Develop(seen);
                else Explore(seen);
            }

            var wellsBefore = seen?.Wells ?? 0;

            _engine.Pipeline.AdvanceTick();

            Resolve(wellsBefore);

            if (_engine.ReadModel!.Outcome != ObjectiveState.Pending) break;
        }

        FieldReadModel final = _engine.ReadModel!;

        return new DrillingSeason(
            final.Outcome, final.Tick, final.Cash,
            _surveyed, _drilled.Count, _discoveries, _dryHoles);
    }

    /// <summary>
    /// Survey what is not yet worth drilling; drill what is.
    ///
    /// <para>The threshold is the whole policy. A company that drills everything
    /// spends its money on dry holes; one that surveys everything never puts a
    /// hole down. Both are losing strategies, which is what makes the number a
    /// decision rather than a setting.</para>
    /// </summary>
    private void Explore(FieldReadModel seen)
    {
        ProspectView? best = Best(seen);

        if (best is null) return;

        if (best.ProbabilityOfSuccess >= _drillAbove)
        {
            if (_engine.Commands.Submit(
                    new DrillWellCommand(
                        new EntityId<IProspect>(best.Prospect.Value), WellDepth)) is Accepted)
            {
                _drilled.Add(best.Prospect.Value);
                _awaitingResult = true;
            }

            return;
        }

        // NOT WORTH A HOLE YET. Shoot seismic over it instead — which sharpens
        // the trap factor and tells the company whether its hesitation was
        // justified. A survey that could not change the answer would be money
        // spent on reassurance.
        if (_engine.Commands.Submit(
                new SeismicSurveyCommand(
                    new EntityId<IProspect>(best.Prospect.Value))) is Accepted)
            _surveyed++;
    }

    /// <summary>
    /// The prospect this company would put its rig on: the best odds it has not
    /// already drilled.
    ///
    /// <para>Odds ALONE, not odds against size, and that is a real limitation of
    /// this client rather than of the surface — `ProspectView` carries what a
    /// bigger client would need to weigh the two. A company that drilled purely
    /// by probability would pass over the elephant it was less sure of, which is
    /// exactly the mistake this policy makes.</para>
    /// </summary>
    private ProspectView? Best(FieldReadModel seen)
    {
        ProspectView? best = null;

        for (var i = 0; i < seen.Prospects.Count; i++)
        {
            ProspectView at = seen.Prospects[i];

            if (_drilled.Contains(at.Prospect.Value)) continue;

            if (best is null || at.ProbabilityOfSuccess > best.ProbabilityOfSuccess) best = at;
        }

        return best;
    }

    /// <summary>Once there is a field, it is `Operator`'s game: fill the header,
    /// then answer whatever the chain is refusing.</summary>
    private void Develop(FieldReadModel seen)
    {
        if (seen.Wellbores.Count < _wellTarget)
        {
            // MORE WELLS ON THE FIELD THAT PAID OFF. A discovery is drilled out
            // before the company goes looking again — the rig is one, and a
            // producing field earns while a prospect only costs.
            ProspectView? found = Drilled(seen);

            if (found is not null)
            {
                _engine.Commands.Submit(
                    new DrillWellCommand(new EntityId<IProspect>(found.Prospect.Value), WellDepth));

                return;
            }
        }

        // WAIT FOR THE YARD TO BE QUIET. Plant bought in a boom costs what the
        // boom says it costs, and the read model carries the index that says so
        // — which is the whole reason it is on the surface. A field that is
        // producing loses very little by deferring a vessel a few months, and a
        // company that never defers pays a premium on everything it ever builds.
        if (seen.CostIndex > _buildBelow) return;

        _engine.Commands.Submit(new InstallSeparatorCommand());

        if (seen.Cash > ExportLineWorthBuildingAt)
            _engine.Commands.Submit(new ExpandExportCommand());
    }

    /// <summary>The structure this company found something in — the one worth
    /// another well.</summary>
    private ProspectView? Drilled(FieldReadModel seen)
    {
        for (var i = 0; i < seen.Prospects.Count; i++)
            if (_drilled.Contains(seen.Prospects[i].Prospect.Value)) return seen.Prospects[i];

        return null;
    }

    /// <summary>
    /// Read the result off the surface: a well appeared, or it did not.
    ///
    /// <para>A HOST CANNOT SEE THE ROCK. What it can see is whether it now owns
    /// a wellbore it did not own last month, and that is exactly how a real
    /// company learns the same thing — the news arrives as an asset or as a
    /// bill. Nothing here asks the engine what was down there.</para>
    ///
    /// <para>Read only once the rig is free, because a drill takes months and a
    /// well that has not finished is not yet a dry hole.</para>
    /// </summary>
    private void Resolve(int wellsBefore)
    {
        if (!_awaitingResult) return;
        if (_engine.ReadModel!.ActivitiesRunning > 0) return;

        _awaitingResult = false;

        if (_engine.ReadModel!.Wells > wellsBefore) _discoveries++;
        else _dryHoles++;
    }

    private static Length WellDepth { get; } = new(2000.0);

    private static Money ExportLineWorthBuildingAt { get; } = Money.FromMillions(100.0);
}
