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
    WellDesign design,
    OGSim.Information.ProspectRisks risks) : Activity<DrillWellCommand>(terms)
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
        var prospect = new EntityRef(EntityKind.Compartment, done.Target.Value);

        if (!done.Succeeded)
        {
            // WHAT A DRY HOLE SHOULD TEACH, and does not yet (finding 169). A
            // failed well here is a roll on the outcome table, not a report on
            // the rock: the generator emits only CHARGED traps, so truth always
            // says there is oil under this structure and a "dry hole" contradicts
            // it. Attributing the failure to source or seal would therefore write
            // a diagnosis nobody derived from truth, which is exactly what
            // SDD-008 §4 requires ("truth-derived, R14 §2.5") and F-3 forbids
            // inventing.
            //
            // Left recording nothing rather than recording a guess. The fix is
            // for dry structures to exist in the world so a well can genuinely
            // find one — R20d.8's remaining slice, not something to paper over
            // here.
            return;
        }

        var target = new EntityId<IReservoirCompartmentEntity>(done.Target.Value);

        field.OpenWell(design(field.NextWellId(), target, done.Depth), target);

        // A DISCOVERY DE-RISKS THE PLAY (SDD-008 §4). The well proved every
        // element at this location — there was a source, a reservoir, a seal, a
        // trap, and the timing worked, because oil is in the hole. Three of
        // those beliefs belong to the play, so every other prospect drawing on
        // the same petroleum system is worth more than it was this morning.
        //
        // This is the half of exploration a player does not pay for directly and
        // is the reason a first discovery changes a whole campaign.
        if (!risks.Knows(prospect)) return;

        risks.Drilled(prospect, PosFactor.Source, present: true);
        risks.Drilled(prospect, PosFactor.Reservoir, present: true);
        risks.Drilled(prospect, PosFactor.Seal, present: true);
        risks.Drilled(prospect, PosFactor.Trap, present: true);
        risks.Drilled(prospect, PosFactor.Timing, present: true);
    }
}
