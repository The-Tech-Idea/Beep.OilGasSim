// R12b — run logs in a wellbore (SDD-008 §3, design 05 §4).
//
// Cheap, quick, and the first thing done in any new hole. It reads the rock at
// the wellbore: porosity sharply, permeability only through a transform and
// therefore badly, and the size of the accumulation not at all — a log sees one
// point, and no number of points tells you how far the oil extends.
//
// That last absence is the whole reason seismic exists as a separate activity.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Company;

namespace OGSim.Composition;

/// <summary>Run a logging tool through an existing wellbore.</summary>
public sealed record WirelineLogCommand(
    EntityId<IReservoirCompartmentEntity> Target) : Command(Subject: null);

internal sealed class WirelineLogActivity(
    ActivityTerms terms,
    ContentId source,
    ContentId porosityKind,
    ContentId permeabilityKind,
    FieldControl field,
    OGSim.Subsurface.SubsurfaceState subsurface,
    ObservationDoor door) : Activity<WirelineLogCommand>(terms)
{
    /// <summary>Knowledge is not PP&amp;E (SDD-009 §1).</summary>
    public override bool LeavesAnAsset => false;

    /// <summary>Logging buys knowledge about the hole (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Exploration;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(WirelineLogCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (new EntityRef(EntityKind.Compartment, command.Target.Value), NoDepth);
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(WirelineLogCommand command)
    {
        // A tool is run on a wire, down a hole. There has to be a hole.
        if (field.WellCount > 0) return [];

        return
        [
            new RejectionReason(
                "$loc:reject.no-well-to-log",
                "a logging tool is run in a wellbore, and the company has none"),
        ];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        if (!done.Succeeded) return;

        var target = new EntityId<IReservoirCompartmentEntity>(done.Target.Value);

        door.Deliver(source, porosityKind, done.Target,
                     subsurface.TruePorosityOf(target), Provenance.Log);

        door.Deliver(source, permeabilityKind, done.Target,
                     subsurface.TruePermeabilityOf(target).SquareMetres, Provenance.Log);
    }
}
