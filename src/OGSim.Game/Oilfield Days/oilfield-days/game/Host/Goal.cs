#nullable enable

using System.Globalization;
using OGSim.Composition;
using OGSim.Contracts;

namespace OilfieldDays.Host;

/// <summary>
/// The scenario's goal, as the ENGINE states it.
/// </summary>
/// <remarks>
/// <para><b>Nothing here holds a number.</b> Six screens used to spell the
/// target out — the menu, the setup card, the HUD, the results sheet, the
/// auto-player's report — and every one of them still said $600M after the
/// scenario moved to $360M. The game told a player one figure and scored them
/// against another, which is law L5 with a face on it.</para>
///
/// <para>The engine publishes what each visible objective asks for
/// (<see cref="ObjectiveGoal"/>), so the host reads it and formats it. A screen
/// with no engine running has no goal to state and must not invent one — see
/// <see cref="Line"/>.</para>
/// </remarks>
internal static class Goal
{
    /// <summary>The headline goal, or none when no run is loaded.</summary>
    /// <remarks>
    /// The first VISIBLE threshold the scenario declares. A scenario with
    /// several would need the host to choose between them, and choosing would
    /// be the host deciding what the game is about — so the first is taken and
    /// the scenario's declared order is what decides.
    /// </remarks>
    public static ObjectiveGoal? Of(FieldReadModel? snapshot) =>
        snapshot is not null && snapshot.Progress.Goals.Count > 0
            ? snapshot.Progress.Goals[0]
            : null;

    /// <summary>
    /// The metric this host knows how to render.
    /// </summary>
    /// <remarks>
    /// <para><b>A target has no unit of its own.</b> The engine publishes the
    /// number and the METRIC beside it, because 36,000,000,000 is a fortune in
    /// cents and nonsense in cubic metres — and reading it as the wrong one is
    /// exactly what this screen did on its first run, reporting a $360M target
    /// as "$36,000.0M".</para>
    ///
    /// <para>A goal on any other metric renders as "—" rather than as a guess.
    /// Teaching this host a second metric means adding a case here, which is the
    /// point: the conversion is stated once, where it can be seen.</para>
    /// </remarks>
    private static readonly ReadModelPath Cash = new("company.cash");

    /// <summary>How far along, between nothing and done.</summary>
    public static double Fraction(FieldReadModel? snapshot)
    {
        if (Of(snapshot) is not ObjectiveGoal goal || goal.Target <= 0.0)
            return 0.0;

        if (goal.Metric != Cash)
            return 0.0;

        // Both sides in cents, so neither is converted and neither can be
        // converted twice.
        return System.Math.Clamp(snapshot!.Cash.Cents / goal.Target, 0.0, 1.0);
    }

    /// <summary>
    /// "$41.2M of $360M", or just the money when there is no goal to measure it
    /// against.
    /// </summary>
    public static string Line(FieldReadModel? snapshot)
    {
        if (snapshot is null)
            return "no run";

        string reached = Millions(snapshot.Cash.Cents);

        return Of(snapshot) is ObjectiveGoal goal && goal.Metric == Cash
            ? reached + " of " + Millions(goal.Target)
            : reached;
    }

    /// <summary>The target alone, for a caption that already shows the money.</summary>
    public static string Target(FieldReadModel? snapshot) =>
        Of(snapshot) is ObjectiveGoal goal && goal.Metric == Cash
            ? Millions(goal.Target)
            : "—";

    /// <summary>Cents in, millions of dollars out — the one conversion.</summary>
    private static string Millions(double cents) =>
        "$" + (cents / 100.0 / 1_000_000.0).ToString("N1", CultureInfo.InvariantCulture) + "M";
}
