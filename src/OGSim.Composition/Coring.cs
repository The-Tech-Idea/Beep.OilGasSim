// R12b — cut a core and measure the rock itself (SDD-008 §3, design 05 §4).
//
// The same two properties a log reads, an order of magnitude sharper and several
// times the price, because the laboratory has the rock in its hands instead of a
// tool's inference about it.
//
// Log against core is the cheap-and-fuzzy against dear-and-sharp decision in its
// purest form, and it is a real one: the sigma floor (INV8) means a core cannot
// be replaced by logging the same compartment repeatedly.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Company;

namespace OGSim.Composition;

/// <summary>Cut and analyse a core from an existing wellbore.</summary>
public sealed record CutCoreCommand(
    EntityId<IReservoirCompartmentEntity> Target) : Command(Subject: null);

internal sealed class CoringActivity(
    ActivityTerms terms,
    ContentId source,
    ContentId porosityKind,
    ContentId permeabilityKind,
    FieldControl field,
    OGSim.Subsurface.SubsurfaceState subsurface,
    ObservationDoor door) : Activity<CutCoreCommand>(terms)
{
    /// <summary>Knowledge is not PP&amp;E (SDD-009 §1) — the core itself is a box
    /// of rock in a store, not a producing asset.</summary>
    public override bool LeavesAnAsset => false;

    /// <summary>Cutting core buys knowledge about the rock (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Exploration;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(CutCoreCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (new EntityRef(EntityKind.Compartment, command.Target.Value), NoDepth);
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(CutCoreCommand command)
    {
        if (field.WellCount > 0) return [];

        return
        [
            new RejectionReason(
                "$loc:reject.no-well-to-core",
                "a core is cut from the bottom of a hole, and the company has none"),
        ];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        if (!done.Succeeded) return;

        var target = new EntityId<IReservoirCompartmentEntity>(done.Target.Value);

        door.Deliver(source, porosityKind, done.Target,
                     subsurface.TruePorosityOf(target), Provenance.Core);

        door.Deliver(source, permeabilityKind, done.Target,
                     subsurface.TruePermeabilityOf(target).SquareMetres, Provenance.Core);
    }
}
