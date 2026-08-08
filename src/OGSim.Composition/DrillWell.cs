// R12b — drill and complete a well (SDD-007, design 20's D-catalogue).
//
// The first decision a player makes and the one every other decision waits on:
// it is the only activity that leaves an asset behind, the only one that can
// come back dry, and the only one whose failure costs a fortune.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>Drill and complete a well on a known compartment.</summary>
public sealed record DrillWellCommand(
    EntityId<IReservoirCompartmentEntity> Target,
    Length TotalDepth) : Command(Subject: null);

/// <summary>
/// The well a successful hole becomes. Content in a finished game (R20c.9): a
/// completion design is a catalogue entry, not a rule, which is why it arrives
/// as an argument rather than being read off a static.
/// </summary>
internal delegate OGSim.Wells.Completion WellDesign(
    ulong id, EntityId<IReservoirCompartmentEntity> compartment, Length totalDepth);

internal sealed class DrillWellActivity(
    ActivityTerms terms,
    Length maximumDepth,
    FieldControl field,
    WellDesign design) : Activity<DrillWellCommand>(terms)
{
    /// <summary>A well is PP&amp;E: the money buys something the company still
    /// owns next month (SDD-009 §1).</summary>
    public override bool LeavesAnAsset => true;

    /// <summary>
    /// False, and deliberately. Two rigs drilling two wells into one compartment
    /// is ordinary field development, not a mistake — what stops it today is that
    /// the company owns one rig, which is a decision about equipment rather than
    /// a rule about drilling.
    /// </summary>
    public override bool OnePerTarget => false;

    public override (EntityRef Target, Length Depth) Aim(DrillWellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (new EntityRef(EntityKind.Compartment, command.Target.Value), command.TotalDepth);
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(DrillWellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reasons = new List<RejectionReason>();

        if (command.TotalDepth.Metres > maximumDepth.Metres)
            reasons.Add(new RejectionReason(
                "$loc:reject.beyond-drilling-envelope",
                $"{command.TotalDepth.Metres} m is past the {maximumDepth.Metres} m the " +
                "company can currently drill"));

        if (command.TotalDepth.Metres <= 0.0)
            reasons.Add(new RejectionReason(
                "$loc:reject.invalid-depth", "a well must have a positive depth"));

        // A well needs somewhere to tie in (SDD-006 §1b). Checked HERE, before
        // the money moves: the header is full or it is not, and a player told so
        // after four months of rig time has been charged for a hole that could
        // never have produced.
        if (!field.HasFreeSlot)
            reasons.Add(new RejectionReason(
                "$loc:reject.no-manifold-slot",
                "every slot on the manifold is taken; a well with nowhere to tie in " +
                "cannot flow, and a bigger header has to be installed first"));

        return reasons;
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A dry hole opens nothing. The money is spent, the months are gone, and
        // what the player has bought is knowledge — which is the whole of
        // exploration economics and the reason drilling is a decision rather
        // than a button.
        if (!done.Succeeded) return;

        var target = new EntityId<IReservoirCompartmentEntity>(done.Target.Value);

        field.OpenWell(design(field.NextWellId(), target, done.Depth), target);
    }
}
