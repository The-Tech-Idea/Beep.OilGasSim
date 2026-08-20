#nullable enable

using System;
using System.Globalization;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OilfieldDays.Host;

namespace OilfieldDays;

/// <summary>
/// A development-only player: run the shipped scenario end to end with a plain
/// strategy, and report where it lands.
///
/// <para>It exists to answer a question the build cannot answer by looking at
/// screens — <b>is the run winnable, and by how much?</b> The scenario asks for
/// $600M inside ten years, and until something plays the whole ten years nobody
/// knows whether that target is generous, brutal, or unreachable.</para>
///
/// <para><b>It is not an AI and it is not the game.</b> It submits the same
/// commands a player would, in an order a reasonable player would choose, and it
/// reads only the published snapshot. Nothing here is a strategy the game ships
/// or a difficulty the engine knows about. What it produces is a measurement,
/// and a measurement of one policy on one seed at that.</para>
///
/// <code>Godot.exe --path &lt;project&gt; -- --play=120 --seed=3</code>
/// </summary>
public static class DevAutoPlayer
{
    private static readonly Length WellDepth = new(2000.0);

    /// <summary>Shoot a structure before drilling it if its odds are this poor.</summary>
    private const double SurveyBelow = 0.30;

    /// <summary>Keep this much cash back rather than spending to the floor.</summary>
    private const double ReserveMillions = 12.0;

    /// <summary>Play the run, a month at a time, and report every year.</summary>
    public static void Play(int months)
    {
        Complained.Clear();

        var surveyed = new System.Collections.Generic.HashSet<ulong>();
        int drilled = 0;
        int surveys = 0;
        int repairs = 0;
        int builds = 0;

        for (int month = 0; month < months; month++)
        {
            FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

            if (snapshot is null || snapshot.Insolvent)
                break;

            // THE BRANCH IS TAKEN ON A COMMAND BEING ACCEPTED, NOT ON A
            // CONDITION BEING TRUE. The first version tested "is something
            // broken" and stopped there, so a repair the engine refused every
            // month wedged the whole policy: on two of eight seeds it drilled
            // nothing, surveyed nothing and repaired nothing for ten years while
            // reporting a tidy Expired. A policy that cannot fall through is a
            // policy that measures its own deadlock.
            bool acted = false;

            // 1. Anything stopped comes first. A failed element shuts in
            //    everything behind it, so a month spent on it is never wasted.
            if (Broken(snapshot) is ChainElementView broken && Accepted(new RepairEquipmentCommand(broken.Element)))
            {
                repairs++;
                acted = true;
            }

            // 2. Then the jam, if there is one. A bottleneck is production the
            //    field has already paid to find and cannot get out.
            if (!acted && snapshot.Bottlenecks.Count > 0 && Rich(snapshot)
                && Accepted(Debottleneck(snapshot.Bottlenecks[0].DisplayId)))
            {
                builds++;
                acted = true;
            }

            // 3. Then exploration, while the rig is free. Cheap information
            //    before an expensive hole: a survey on a poor structure is worth
            //    more than the hole it stops being drilled.
            if (!acted && snapshot.ActivitiesRunning == 0 && snapshot.Prospects.Count > 0 && Rich(snapshot))
            {
                ProspectView? target = EngineHost.Instance.Drilled.BestUndrilled(snapshot);

                if (target is ProspectView aim)
                {
                    bool worthSurveying =
                        aim.ProbabilityOfSuccess < SurveyBelow && surveyed.Add(aim.Prospect.Value);

                    if (worthSurveying)
                    {
                        if (Accepted(new SeismicSurveyCommand(new EntityId<IProspect>(aim.Prospect.Value))))
                            surveys++;
                    }
                    else if (Accepted(new DrillWellCommand(
                                 new EntityId<IProspect>(aim.Prospect.Value), WellDepth)))
                    {
                        EngineHost.Instance.Drilled.Record(aim);
                        drilled++;
                    }
                }
            }

            if (EngineHost.Instance.AdvanceMonth() is not TickCompleted)
                break;

            FieldReadModel? after = EngineHost.Instance.Snapshot;

            if (after is not null && after.Tick.Value % 12 == 0)
                Report(after);
        }

        FieldReadModel? final = EngineHost.Instance.Snapshot;

        if (final is null)
            return;

        GD.Print($"[play] FINISHED month {final.Tick.Value}: " +
                 $"${final.Cash.Cents / 100.0 / 1e6:N1}M of $600M, outcome {final.Outcome}" +
                 (final.Insolvent ? " (BROKE)" : string.Empty));

        GD.Print($"[play] {drilled} holes, {surveys} surveys, {repairs} repairs, {builds} units built, " +
                 $"{final.Wells} wells producing {final.ProducedThisTick.CubicMetres:N0} m3 in the last month");
    }

    private static void Report(FieldReadModel snapshot) =>
        GD.Print($"[play] year {(snapshot.Tick.Value / 12).ToString(CultureInfo.InvariantCulture)}: " +
                 $"${snapshot.Cash.Cents / 100.0 / 1e6:N1}M, {snapshot.Wells} wells, " +
                 $"{snapshot.ProducedThisTick.CubicMetres:N0} m3, " +
                 $"oil ${snapshot.OilPrice.Cents / 100.0:N0}/t");

    /// <summary>Enough cash that another commitment is not reckless.</summary>
    private static bool Rich(FieldReadModel snapshot) =>
        snapshot.Cash.Cents / 100.0 / 1e6 > ReserveMillions;

    private static ChainElementView? Broken(FieldReadModel snapshot)
    {
        for (int i = 0; i < snapshot.Chain.Count; i++)
        {
            if (snapshot.Chain[i].Failed)
                return snapshot.Chain[i];
        }

        return null;
    }

    /// <summary>
    /// The unit that answers a named jam.
    /// </summary>
    /// <remarks>
    /// Matched on the element's display id, which is the only handle a host has
    /// on which part of the chain is short. Anything unrecognised gets export
    /// capacity, because the far end of the chain is the one constraint that
    /// binds every element behind it.
    /// </remarks>
    private static Command Debottleneck(string element) => element switch
    {
        var e when e.Contains("separator", StringComparison.Ordinal) => new InstallSeparatorCommand(),
        var e when e.Contains("manifold", StringComparison.Ordinal) => new InstallManifoldCommand(),
        var e when e.Contains("gas", StringComparison.Ordinal) => new InstallGasPlantCommand(),
        var e when e.Contains("treater", StringComparison.Ordinal) => new InstallTreaterCommand(),
        var e when e.Contains("tank", StringComparison.Ordinal) => new InstallTankCommand(),
        _ => new ExpandExportCommand(),
    };

    /// <summary>
    /// Submit, and say so when the engine says no.
    /// </summary>
    /// <remarks>
    /// A harness that swallows refusals measures its own silence. The first
    /// version returned a bool and dropped the reasons, and two seeds reported a
    /// tidy ten-year Expired while every command they issued had been turned
    /// down — which looked exactly like a balance result and was not one.
    /// </remarks>
    private static bool Accepted(Command command)
    {
        CommandResult result = EngineHost.Instance.Submit(command);

        if (result is Accepted)
            return true;

        if (result is Rejected rejected && !Complained.Contains(command.GetType().Name))
        {
            Complained.Add(command.GetType().Name);

            for (int i = 0; i < rejected.Reasons.Count; i++)
                GD.Print($"[play] {command.GetType().Name} refused: {rejected.Reasons[i].Detail}");
        }

        return false;
    }

    /// <summary>One complaint per command kind: the log is evidence, not noise.</summary>
    private static readonly System.Collections.Generic.HashSet<string> Complained = new();
}
