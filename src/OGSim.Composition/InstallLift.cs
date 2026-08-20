// R12b.2 — a well can be given a pump (SDD-003 §6.2's amendment, finding 255).
//
// FOUR LIFT METHODS HAVE WORKED SINCE R7 AND NEVER REACHED A WELL. Every
// completion opens on natural flow (`lift: null`) and stays there — the same
// "real mechanism, joined to nothing" shape as findings 149, 200, 207, 252,
// 253 and 254, on the other half of what a well can be given.
//
// ONE TECHNIQUE, THE SIMPLEST OF THE FOUR. A rod pump: no curve, no power
// draw, a displacement cap alone (§6.2's own "each method fills the fields it
// uses"). ESP, gas lift and PCP stay real, tested and unreachable from a
// command — the same honest gap R12b.7 left for frac.
//
// NO RIG. A workover crew is not the drilling rig, the same call R20d.20 and
// R12b.7 both made for a wellsite intervention that is not a rig job.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Company;

namespace OGSim.Composition;

/// <summary>Install a rod pump on a well that has none.</summary>
public sealed record InstallLiftCommand(
    EntityId<ICompletion> Well) : Command(Subject: null);

internal sealed class InstallLiftActivity(
    ActivityTerms terms,
    FieldControl field,
    OGSim.Wells.RodPumpTier tier,
    SimulationClock clock) : Activity<InstallLiftCommand>(terms)
{
    /// <summary>YES. A pump is equipment the company owns next month, the
    /// same reasoning as any other install (SDD-009 §1).</summary>
    public override bool LeavesAnAsset => true;

    public override MovementCategory Spend => MovementCategory.Development;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallLiftCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (Asset(command.Well), NoDepth);
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallLiftCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (field.WellNamed(command.Well) is not OGSim.Wells.Completion completion)
            return
            [
                new RejectionReason(
                    "$loc:reject.no-such-well",
                    $"there is no well {command.Well.Value} to fit a pump to"),
            ];

        if (field.IsWellAbandoned(command.Well))
            return
            [
                new RejectionReason(
                    "$loc:reject.already-abandoned",
                    $"well {command.Well.Value} is plugged; a pump would lift nothing"),
            ];

        // ALREADY LIFTED. A second pump on the same string is not a decision
        // this mechanic models — the player who wants a bigger one is asking
        // for the ladder R12b.2's own amendment named as out of scope.
        if (completion.Lift is not null)
            return
            [
                new RejectionReason(
                    "$loc:reject.already-lifted",
                    $"well {command.Well.Value} already carries a lift method"),
            ];

        return [];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A FAILED JOB LEAVES THE WELL EXACTLY AS IT WAS — natural flow, the
        // money and the month gone. The same shape as a dry hole.
        if (!done.Succeeded) return;

        var well = new EntityId<ICompletion>(done.Target.Value);

        if (field.WellNamed(well) is not OGSim.Wells.Completion completion)
            throw new InvariantFault("R1 §2.5", null,
                $"well {done.Target.Value} passed validation and no longer exists at completion");

        // THE TUBING GEOMETRY A NEW OUTFLOW MODEL NEEDS is not a fact lost
        // between drilling and here — it is exactly what `WellboreNamed`
        // already reconstructs from the completion's own perforations
        // (`CompletionFor`'s `(totalDepth, totalDepth, 0.0889 m, 4.6e-5)`),
        // read through the same door a host asking for this well's geometry
        // would use, rather than a second derivation of the same fact (L5).
        if (field.WellboreNamed(new EntityId<IWellbore>(well.Value)) is not IWellbore wellbore)
            throw new InvariantFault("R1 §2.5", null,
                $"well {done.Target.Value} has a completion but no wellbore at completion");

        Length totalDepth = wellbore.Path.Stations[^1].Md;

        var lift = new OGSim.Wells.RodPump(
            new EntityId<IWellComponent>(well.Value), tier.Id, tier.Envelope, clock.Date,
            tier.DisplacementCubicMetresPerSecond);

        var outflow = new OGSim.Wells.HydrostaticFrictionOutflowModel(
            new OGSim.Wells.TubingGeometry(totalDepth, totalDepth, new Length(0.0889), 4.6e-5),
            Defaults.SurfaceOilDensity, lift);

        completion.InstallLift(lift, outflow);
    }

    private static EntityRef Asset(EntityId<ICompletion> well) =>
        new(EntityKind.Completion, well.Value);
}
