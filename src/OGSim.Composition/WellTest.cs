// R12b — flow a well and watch the pressure build back up (SDD-008 §3, design 06 §3).
//
// The sharpest measurement of a compartment there is: it watches the reservoir
// answer for itself over days, which is why it beats a log on permeability and
// is the only source that can see pressure at all. It is also the reason a player
// would ever stop producing on purpose — the well is shut in for the build-up, so
// the test costs the month's oil as well as its own price.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>Shut a well in and measure the compartment behind it.</summary>
public sealed record WellTestCommand(
    EntityId<IReservoirCompartmentEntity> Target) : Command(Subject: null);

internal sealed class WellTestActivity(
    ActivityTerms terms,
    ContentId source,
    ContentId pressureKind,
    ContentId permeabilityKind,
    FieldControl field,
    OGSim.Subsurface.SubsurfaceState subsurface,
    ObservationDoor door) : Activity<WellTestCommand>(terms)
{
    /// <summary>Knowledge is not PP&amp;E (SDD-009 §1).</summary>
    public override bool LeavesAnAsset => false;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(WellTestCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (new EntityRef(EntityKind.Compartment, command.Target.Value), NoDepth);
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(WellTestCommand command)
    {
        // A test needs a wellbore to be measured in. Letting a company buy a
        // pressure survey of a compartment it has never penetrated would hand
        // over the one measurement that makes drilling worth doing, for the price
        // of not drilling.
        if (field.WellCount > 0) return [];

        return
        [
            new RejectionReason(
                "$loc:reject.no-well-to-test",
                "a build-up is measured in a wellbore, and the company has none"),
        ];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A failed test is the honest bad outcome: the money is gone and the
        // company knows nothing new, which is what makes buying information a
        // decision rather than a formality.
        if (!done.Succeeded) return;

        var target = new EntityId<IReservoirCompartmentEntity>(done.Target.Value);

        door.Deliver(source, pressureKind, done.Target,
                     subsurface.TruePressureOf(target).Pascals, Provenance.WellTest);

        // kh, from the same build-up. A test sees the permeability the RESERVOIR
        // flows at over days rather than the plug a core cut out of it, and that
        // is the number the inflow model actually needs.
        door.Deliver(source, permeabilityKind, done.Target,
                     subsurface.TruePermeabilityOf(target).SquareMetres, Provenance.WellTest);
    }
}
