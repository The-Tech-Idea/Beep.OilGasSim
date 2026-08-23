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
/// the scenario's target inside ten years, and until something plays the whole
/// ten years nobody
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

    /// <summary>How many structures to have on the board before drilling one.</summary>
    private const int WantsAChoiceOf = 3;

    /// <summary>Odds below which another block is better value than a hole.</summary>
    /// <remarks>
    /// SET FROM WHAT THE WORLD ACTUALLY OFFERS, not from taste. A freshly
    /// revealed prospect carries the prior — 2-D reconnaissance finds a closure,
    /// it does not sharpen one — and measured on seed 3 the best on the board
    /// runs 14-18%. A floor of 25% is a policy that never drills, which is how
    /// the first version sat on eleven shot blocks for five years.
    /// </remarks>
    private const double DrillAbove = 0.12;

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
        int shot = 0;
        var commissioned = false;

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

            // 2. A DISCOVERY WITH NOWHERE TO GO. Under frontier rules a well
            //    can be drilled before there is a plant and waits shut in
            //    (plans 23), so the month a company has oil in the ground and
            //    no facility is the month it should be building one. Ahead of
            //    the jam because there is no jam yet — there is no chain.
            if (!acted && snapshot.Chain.Count == 0 && snapshot.Wellbores.Count > 0
                && Rich(snapshot) && Accepted(new InstallEarlyProductionFacilityCommand()))
            {
                commissioned = true;
                acted = true;
            }

            // 3. Then the jam, if there is one. A bottleneck is production the
            //    field has already paid to find and cannot get out.
            if (!acted && snapshot.Bottlenecks.Count > 0 && Rich(snapshot)
                && Accepted(Debottleneck(snapshot.Bottlenecks[0].DisplayId)))
            {
                builds++;
                acted = true;
            }

            // 4. NOTHING ON THE BOARD MEANS BUY SOME MAP. With the licence
            //    dark (SDD-010 4b's S1 amendment) a company begins knowing of
            //    no structure at all, so the first move is not a hole — it is
            //    deciding which acreage to look under. A policy that skipped
            //    this would sit on its hands for ten years and report a tidy
            //    Expired, which is the shape of deadlock this harness exists to
            //    catch.
            //    SHOT UNTIL THERE IS A CHOICE, not until there is one name. The
            //    first version stopped at the first prospect it found and then
            //    drilled it whatever the odds were, which is how a company with
            //    a dark map spends $32M on four holes in one barren block. A
            //    board of three is the difference between picking and taking.
            if (!acted && snapshot.ActivitiesRunning == 0
                && snapshot.Prospects.Count < WantsAChoiceOf
                && Rich(snapshot) && Unshot(snapshot) is BlockView block
                && Accepted(new SurveyBlockCommand(new EntityId<IBlock>(block.Block.Value))))
            {
                shot++;
                acted = true;
            }

            // 5. Then exploration, while the rig is free. Cheap information
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
                    // A FLOOR UNDER WHAT IS WORTH A HOLE. Below it the money
                    // buys another block instead — information is cheaper than
                    // a dry hole by a factor of ten, and a policy that drills
                    // whatever is on the board is not choosing at all.
                    // ...UNLESS THERE IS NOTHING LEFT TO LOOK UNDER. A floor
                    // with no alternative is a stall: the first version sat on
                    // eleven shot blocks and 18% odds for five years, drilling
                    // nothing, because every prospect was below the bar and
                    // there was no cheaper information left to buy. When the map
                    // is finished, the best of what you have IS the decision.
                    else if ((aim.ProbabilityOfSuccess >= DrillAbove || Unshot(snapshot) is null)
                             && Accepted(new DrillWellCommand(
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
                 $"{Goal.Line(final)}, outcome {final.Outcome}" +
                 (final.Insolvent ? " (BROKE)" : string.Empty));

        GD.Print($"[play] {shot} blocks shot, {(commissioned ? "a facility" : "NO facility")}, " +
                 $"{drilled} holes, {surveys} surveys, {repairs} repairs, " +
                 $"{builds} units built, " +
                 $"{final.Wells} wells producing {final.ProducedThisTick.CubicMetres:N0} m3 in the last month");
    }

    private static void Report(FieldReadModel snapshot) =>
        GD.Print($"[play] year {(snapshot.Tick.Value / 12).ToString(CultureInfo.InvariantCulture)}: " +
                 $"${snapshot.Cash.Cents / 100.0 / 1e6:N1}M, {snapshot.Wells} wells, " +
                 $"{snapshot.ProducedThisTick.CubicMetres:N0} m3, " +
                 $"oil ${snapshot.OilPrice.Cents / 100.0:N0}/t, " +

                 // THE ODDS ON THE BOARD. Without this a run that drills nothing
                 // looks the same as a run with nothing worth drilling, and they
                 // want opposite fixes — one is the policy, the other the world.
                 $"{snapshot.Prospects.Count} prospects, best POS {Best(snapshot):P0}");

    /// <summary>The best odds the company can see, or zero on a dark map.</summary>
    private static double Best(FieldReadModel snapshot)
    {
        var best = 0.0;

        for (int i = 0; i < snapshot.Prospects.Count; i++)
            if (snapshot.Prospects[i].ProbabilityOfSuccess > best)
                best = snapshot.Prospects[i].ProbabilityOfSuccess;

        return best;
    }

    /// <summary>Enough cash that another commitment is not reckless.</summary>
    private static bool Rich(FieldReadModel snapshot) =>
        snapshot.Cash.Cents / 100.0 / 1e6 > ReserveMillions;

    /// <summary>
    /// The first block of the licence nobody has looked under.
    /// </summary>
    /// <remarks>
    /// In order, which is a policy and a poor one — a company would shoot near
    /// its yard first, and near what it has already found after that. It is
    /// enough to measure whether the loop closes; it is not a strategy the game
    /// ships.
    /// </remarks>
    private static BlockView? Unshot(FieldReadModel snapshot)
    {
        for (int i = 0; i < snapshot.Blocks.Count; i++)
        {
            if (!snapshot.Blocks[i].Surveyed)
                return snapshot.Blocks[i];
        }

        return null;
    }

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
