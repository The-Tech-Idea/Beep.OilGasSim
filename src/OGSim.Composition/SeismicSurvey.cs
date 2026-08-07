// R12b — shoot 3-D seismic over a compartment (SDD-008 §3, design 05 §2).
//
// THE ONLY ACTIVITY A COMPANY WITH NOTHING DRILLED CAN ORDER, and the reason the
// exploration game has a first move at all. It needs no wellbore and no rig, so
// it can be shot while the rig is turning elsewhere; what it buys is the one
// thing no downhole measurement can reach — how big the accumulation is.
//
// It is also blunt. σ is wide, and the sigma floor (INV8) means it stays wide no
// matter how many times it is re-shot: a survey narrows a prospect down to worth
// drilling or not, and then the player has to drill.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>Shoot a seismic survey over a compartment.</summary>
public sealed record SeismicSurveyCommand(
    EntityId<IReservoirCompartmentEntity> Target) : Command(Subject: null);

internal sealed class SeismicSurveyActivity(
    ActivityTerms terms,
    ContentId source,
    ContentId oilInPlaceKind,
    OGSim.Subsurface.SubsurfaceState subsurface,
    ObservationDoor door) : Activity<SeismicSurveyCommand>(terms)
{
    /// <summary>
    /// Knowledge is not PP&amp;E (SDD-009 §1). This one matters most of the four:
    /// a survey is bought and consumed in the same month, and capitalising it
    /// would let a company inflate its balance sheet by shooting seismic.
    /// </summary>
    public override bool LeavesAnAsset => false;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(SeismicSurveyCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (new EntityRef(EntityKind.Compartment, command.Target.Value), NoDepth);
    }

    /// <summary>
    /// Nothing beyond the shared refusals, and that absence is the point: a
    /// survey needs no wellbore, which is what makes it the first move rather
    /// than a follow-up. What still refuses it is the cash and the one-at-a-time
    /// rule every measurement is under.
    /// </summary>
    public override IReadOnlyList<RejectionReason> OwnRefusals(SeismicSurveyCommand command) => [];

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        if (!done.Succeeded) return;

        var target = new EntityId<IReservoirCompartmentEntity>(done.Target.Value);

        // Oil in place as the accumulation WAS, not what is left of it — the
        // door reads initial conditions, so a company cannot deduce its own
        // cumulative offtake by re-shooting (SDD-008 §3).
        door.Deliver(source, oilInPlaceKind, done.Target,
                     subsurface.TrueOilInPlaceOf(target).CubicMetres, Provenance.Seismic);
    }
}
