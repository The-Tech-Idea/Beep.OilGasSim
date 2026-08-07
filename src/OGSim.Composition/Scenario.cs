// R21c — the scenario: what the player is trying to do, and when they stop.
//
// A game that can only be lost is not a game. Until now the arc ended one way:
// keep going until the money runs out. A goal gives the other end — something to
// be measured against, a date it has to happen by, and a verdict.
//
// OBJECTIVES OBSERVE AND NEVER ACT (design 03 §6 stage 12, R24-V15). The goal
// reads the position and returns a verdict; it cannot spend, drill, or change a
// number. A scenario that could act would be a second player, and the outcome
// would stop being the player's doing.

using OGSim.Company;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>How a game ends, or that it has not.</summary>
public enum Outcome
{
    /// <summary>Still being played.</summary>
    Playing,

    /// <summary>The goal was met, on time.</summary>
    Won,

    /// <summary>The deadline passed with the goal unmet, or the money ran out.</summary>
    Lost,
}

/// <summary>
/// What this scenario asks for (SDD-014 §3). Cash by a date — the simplest
/// honest goal for a company whose only lever is drilling, and the one that
/// makes every decision so far count for something.
/// </summary>
public sealed record ScenarioGoal(Money TargetCash, Tick Deadline);

/// <summary>
/// Stage 12. Reads the sealed position and returns a verdict.
///
/// <para>Before stage 13, so the read model the host publishes carries the
/// outcome of the month it describes rather than of the one before it.</para>
/// </summary>
internal sealed class ObjectiveStage(
    CompanyState company,
    ScenarioGoal goal,
    IAuditTrail audit) : ITickStage
{
    public StageId Id => StageId.Objectives;

    /// <summary>
    /// Once true, always true: a company does not recover from having been wound
    /// up, and a later month's revenue must not quietly un-fail it.
    ///
    /// <para>The latch lives HERE rather than in the close stage because losing
    /// and being insolvent are one fact and law L5 wants one owner. The close
    /// stage reads it; nothing else decides it.</para>
    /// </summary>
    public bool Insolvent { get; private set; }

    /// <summary>
    /// Once decided, never revisited. A player who hit the target in month 90
    /// has won, and a bad month 91 does not take it back — the verdict is about
    /// what they achieved, not about where they happened to stop.
    /// </summary>
    public Outcome Outcome { get; private set; } = Outcome.Playing;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (company.Ledger.Cash.Cents < 0) Insolvent = true;

        if (Outcome != Outcome.Playing) return;

        // Insolvency first: a company that cannot pay has lost, even in the
        // month it would otherwise have hit the target — the money is gone
        // before the goal is measured, which is the order it happens in.
        if (Insolvent) Decide(Outcome.Lost, "insolvent", context.Tick);
        else if (company.Ledger.Cash >= goal.TargetCash) Decide(Outcome.Won, "target-met", context.Tick);
        else if (context.Tick.Value >= goal.Deadline.Value) Decide(Outcome.Lost, "deadline", context.Tick);
    }

    private void Decide(Outcome outcome, string because, Tick tick)
    {
        Outcome = outcome;

        audit.Record(
            AuditCategory.StateTransition, subject: null, cause: null,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["outcome"] = new(outcome.ToString()),
                ["because"] = new(because),
                ["tick"] = new(tick.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
            });
    }
}
