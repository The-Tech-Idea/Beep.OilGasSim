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
using OGSim.Company;

namespace OGSim.Composition;

/// <summary>Shoot a seismic survey over a compartment.</summary>
/// <summary>
/// Shoot seismic over a PROSPECT (SDD-010 §4b).
///
/// <para>It named a compartment until R20d.7.5, which meant a company could only
/// survey a field it had already found — backwards, and contradicted by this
/// activity's own reason for existing: a survey needs no wellbore, which is what
/// makes it the FIRST move.</para>
/// </summary>
public sealed record SeismicSurveyCommand(
    EntityId<IProspect> Target) : Command(Subject: null);

internal sealed class SeismicSurveyActivity(
    ActivityTerms terms,
    ContentId source,
    ContentId capacityKind,
    WorldState world,
    OGSim.Information.ProspectRisks risks,
    ObservationDoor door) : Activity<SeismicSurveyCommand>(terms)
{
    /// <summary>
    /// Knowledge is not PP&amp;E (SDD-009 §1). This one matters most of the four:
    /// a survey is bought and consumed in the same month, and capitalising it
    /// would let a company inflate its balance sheet by shooting seismic.
    /// </summary>
    public override bool LeavesAnAsset => false;

    /// <summary>Looking for oil is what the line means (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Exploration;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(SeismicSurveyCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (new EntityRef(EntityKind.Prospect, command.Target.Value), NoDepth);
    }

    public override string QuantityUnit => "square-kilometre";

    /// <summary>The patch of ground a detail shoot covers — the structure's own
    /// block, since blocks are the generated survey grid (finding 289). A
    /// hand-built field has no surface and answers zero; its price is then the
    /// market's quote of nothing, and the shoot still runs, which is exactly
    /// what a scenario that placed its reservoir directly asked for.</summary>
    public override double Quantity(SeismicSurveyCommand command) =>
        (world.BlockWidth * world.BlockHeight).ToSquareKilometres();

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

        var target = new EntityId<IProspect>(done.Target.Value);

        // WHAT THE STRUCTURE COULD HOLD, sharpened. Seismic images a closure far
        // better than a regional gravity pass does, and it images DRY closures
        // exactly as well as charged ones — which is what stops a survey being a
        // way to ask whether there is oil (SDD-010 §4b).
        door.Deliver(source, capacityKind, done.Target,
                     world.CapacityOf(target).CubicMetres, Provenance.Seismic);

        // AND IT MOVES THE RISK IT CAN ACTUALLY SEE (SDD-008 §4). Trap hard,
        // because imaging a closure is close to proving there is one; reservoir
        // soft, because amplitude hints at the rock and no more. Source, seal
        // and timing are untouched — seismic has nothing to say about them, and
        // a survey that quietly improved every factor would make "which survey
        // answers my weakest element?" a question with one answer.
        //
        // This is what makes showing the five factors worth anything. A player
        // reading "one in six, and it is the trap we doubt" can buy something
        // about it; the same player told only "one in six" can only drill.
        EntityRef prospect = done.Target;

        if (!risks.Knows(prospect)) return;

        risks.Learned(prospect, PosFactor.Trap, present: true, weight: HardEvidence);
        risks.Learned(prospect, PosFactor.Reservoir, present: true, weight: SoftEvidence);
    }

    /// <summary>
    /// SDD-008 §4's w_hard — what a measurement aimed straight at an element is
    /// worth. Two wells' conviction: strong, and still short of drilling.
    /// </summary>
    private const double HardEvidence = 2.0;

    /// <summary>w_soft — a hint, worth a quarter of that.</summary>
    private const double SoftEvidence = 0.5;
}
