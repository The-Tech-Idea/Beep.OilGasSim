// R21.5 — the reference client (R21 §2.5).
//
// IT PLAYS A FULL GAME THROUGH THE PUBLISHED SURFACE AND NOTHING ELSE: a read
// model to look at, a command bus to act on. It holds no reference to a domain
// module, cannot see a compartment, a completion or a separator, and knows only
// what a host would know.
//
// THAT CONSTRAINT IS THE WHOLE POINT. "If it needs anything the surface does not
// offer, the surface is incomplete, and that is discovered here rather than by a
// UI team six months later." Writing it immediately found one: the read model
// reported how MANY wells a company had and never which, so every well-level
// command — shut in, re-open, abandon — was unaimable (SDD-017 §2's R21.5
// amendment).
//
// IT IS NOT AN ADVISOR. R25's Advisor reasons about what a player should do and
// explains itself; this decides as simply as it can, because its job is to prove
// the surface sufficient rather than to play well. Where the two would differ,
// this one does the obvious thing.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.ReferenceClient;

/// <summary>What a run came to, as the client saw it.</summary>
public sealed record Session(
    ObjectiveState Outcome,
    Tick Ended,
    Money Cash,
    int WellsDrilled,
    int WellsAbandoned,
    bool Debottlenecked);

/// <summary>
/// R21 §2.5's headless client. One policy, applied every month.
/// </summary>
public sealed class Operator
{
    private readonly Engine _engine;
    private readonly EntityId<IReservoirCompartmentEntity> _prospect;
    private readonly int _wellTarget;

    /// <summary>
    /// The monthly cash flow below which the field is closed.
    ///
    /// <para>A HURDLE and not zero, because that is how the decision is actually
    /// made: a company with better things to do with its people and its capital
    /// abandons a field that is still marginally positive, and one with nothing
    /// else on will run it to the last barrel. Zero is a legitimate setting and
    /// is simply the most patient operator there is.</para>
    /// </summary>
    private readonly Money _hurdle;

    public Operator(
        Engine engine,
        EntityId<IReservoirCompartmentEntity> prospect,
        int wellTarget,
        Money hurdle)
    {
        ArgumentNullException.ThrowIfNull(engine);

        _engine = engine;
        _prospect = prospect;
        _wellTarget = wellTarget;
        _hurdle = hurdle;
    }

    /// <summary>
    /// Plays until the scenario resolves or the clock runs out.
    ///
    /// <para>Every decision below is taken from the read model alone. Nothing
    /// here asks the engine a question the surface does not answer, which is the
    /// property this client exists to demonstrate.</para>
    /// </summary>
    public Session Play(int months)
    {
        var debottlenecked = false;
        var abandoned = 0;

        for (var month = 0; month < months; month++)
        {
            FieldReadModel? seen = _engine.ReadModel;

            if (seen is not null)
            {
                debottlenecked |= Develop(seen);
                abandoned += CloseWhatIsFinished(seen);
            }

            _engine.Pipeline.AdvanceTick();

            if (_engine.ReadModel!.Outcome != ObjectiveState.Pending) break;
        }

        FieldReadModel final = _engine.ReadModel!;

        return new Session(
            final.Outcome, final.Tick, final.Cash,
            final.Wellbores.Count, abandoned, debottlenecked);
    }

    /// <summary>
    /// Drill while there is a rig free and a field worth developing, and answer
    /// the bottleneck once there is enough production to need the capacity.
    ///
    /// <para>The rig is only ever asked for one well at a time, because the
    /// engine only ever grants one: the company owns a single rig and the
    /// scheduler reserves it for a well's worst case. A client that submitted a
    /// batch would get one well and a stack of refusals — which is exactly what
    /// the win test was doing before this one was written.</para>
    /// </summary>
    private bool Develop(FieldReadModel seen)
    {
        if (seen.ActivitiesRunning == 0 && seen.Wellbores.Count < _wellTarget)
            _engine.Commands.Submit(new DrillWellCommand(_prospect, WellDepth));

        // The vessel is worth buying once a second well is there to fill it —
        // and the read model says when, because the separator appears among the
        // bottlenecks with the mass it is refusing.
        if (Producing(seen) < 2) return false;

        return _engine.Commands.Submit(new InstallSeparatorCommand()) is Accepted;
    }

    /// <summary>
    /// Closes the field once it costs more to run than it earns.
    ///
    /// <para>Judged on MONEY and not on volume, which is what an operator
    /// actually decides on: a field making a lot of water and a little oil can
    /// still be producing impressively while losing money every month, and the
    /// volume would say to keep going. Cash flow is the honest signal and the
    /// read model carries it — the client compares this month's cash against
    /// last month's, which is all a host could do too.</para>
    ///
    /// <para>Judged on the FIELD rather than per well, because the surface
    /// reports production per field. That is a real limit of the current read
    /// model rather than a policy choice, and it is recorded here instead of
    /// worked around: a client inventing a per-well number would be guessing.</para>
    /// </summary>
    private int CloseWhatIsFinished(FieldReadModel seen)
    {
        Money flow = seen.Cash - _lastSeenCash;
        _lastSeenCash = seen.Cash;

        // Still clearing the hurdle: nothing to close. A single bad month is not
        // a reason to plug a field either, so the shortfall has to persist.
        if (flow >= _hurdle)
        {
            _losingMonths = 0;
            return 0;
        }

        if (++_losingMonths < LosingMonthsBeforeClosing) return 0;

        _losingMonths = 0;

        var closed = 0;

        for (int i = 0; i < seen.Wellbores.Count; i++)
        {
            WellStatusView well = seen.Wellbores[i];
            if (well.Status == WellStatus.Abandoned) continue;

            var completion = new EntityId<ICompletion>(well.Well.Value);

            if (_engine.Commands.Submit(new AbandonWellCommand(completion)) is Accepted)
                closed++;
        }

        return closed;
    }

    private static int Producing(FieldReadModel seen)
    {
        var producing = 0;
        for (int i = 0; i < seen.Wellbores.Count; i++)
            if (seen.Wellbores[i].Status == WellStatus.Producing) producing++;

        return producing;
    }

    /// <summary>The one depth this content can drill to. A client that guessed
    /// deeper would simply be refused (`beyond-drilling-envelope`).</summary>
    private static Length WellDepth { get; } = new(2000.0);

    /// <summary>
    /// How long a field has to lose money before it is closed. A quarter: one
    /// bad month is noise — a rig on site, a month a cargo slipped — and
    /// plugging a field is not reversible.
    /// </summary>
    private const int LosingMonthsBeforeClosing = 3;

    private Money _lastSeenCash = Money.Zero;

    private int _losingMonths;
}
