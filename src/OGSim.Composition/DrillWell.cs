// R12b — drill and complete a well (SDD-007, design 20's D-catalogue).
//
// The first decision a player makes and the one every other decision waits on:
// it is the only activity that leaves an asset behind, the only one that can
// come back dry, and the only one whose failure costs a fortune.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Company;

namespace OGSim.Composition;

/// <summary>Drill and complete a well on a known compartment.</summary>
/// <summary>
/// Drill a PROSPECT — a closed structure, which may or may not hold anything
/// (SDD-010 §4b).
///
/// <para>It named a compartment until R20d.7.4, and a compartment is oil that is
/// already known to be there. Aiming at one meant every well was drilled into a
/// discovery that had already happened, which is why probability of success had
/// nothing to be wrong about.</para>
/// </summary>
public sealed record DrillWellCommand(
    EntityId<IProspect> Target,
    Length TotalDepth) : Command(Subject: null);

/// <summary>
/// The well a successful hole becomes. Content in a finished game (R20c.9): a
/// completion design is a catalogue entry, not a rule, which is why it arrives
/// as an argument rather than being read off a static.
/// </summary>
internal delegate OGSim.Wells.Completion WellDesign(
    ulong id,
    EntityId<IReservoirCompartmentEntity> compartment,
    Length totalDepth,

    /// <summary>
    /// The rock this well is in (SDD-008 §2c). Passed rather than read off a
    /// static, because a well's productivity is a fact about ITS compartment —
    /// and a design that supplied its own would be stating a physical fact twice
    /// (finding 170).
    /// </summary>
    OGSim.Wells.InflowConditions rock);

internal sealed class DrillWellActivity(
    ActivityTerms terms,
    Length maximumDepth,
    FieldControl field,
    OGSim.Information.ProspectRisks risks,
    WorldState world,
    IBeliefStore beliefs,
    OGSim.Subsurface.SubsurfaceState subsurface,
    ObservationDoor door,
    IDrillingRule rule) : Activity<DrillWellCommand>(terms)
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
    /// <summary>A well is the asset a field is developed with (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool OnePerTarget => false;

    public override (EntityRef Target, Length Depth) Aim(DrillWellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (new EntityRef(EntityKind.Prospect, command.Target.Value), command.TotalDepth);
    }

    public override string QuantityUnit => "metre";

    /// <summary>The hole the player chose to make (finding 289): a shallow
    /// well is a cheaper, quicker bet than a deep one, on every map.</summary>
    public override double Quantity(DrillWellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.TotalDepth.Metres;
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

        // A STRUCTURE DRILLED DRY IS SETTLED (finding 286). This engine's
        // truth is fixed at generation — a trap the charge never reached is
        // empty forever — so a second hole into it cannot find anything the
        // first did not, and would re-count the same source evidence against
        // the play a second time. Refused by name, the way an already-shot
        // block refuses a second pass.
        if (world.Condemned(command.Target))
            reasons.Add(new RejectionReason(
                "$loc:reject.prospect-condemned",
                "this structure was drilled and found empty; a second hole into it " +
                "cannot find anything the first did not"));

        // WHERE A WELL MAY GO, asked of the rule set the run is played under
        // (plans 23). An operator drills into a plant that can take the
        // production; a frontier company drills to find out whether a plant is
        // worth building, and its well waits shut in until one is. Checked
        // BEFORE the money moves either way — a player told after four months of
        // rig time has been charged for a hole that could never have produced.
        reasons.AddRange(rule.Refusals(field));

        // A LOST LICENCE REFUSES FURTHER DRILLING (SDD-011 §1's R20d.9
        // amendment). Nothing already producing stops — this is checked at
        // TENURE IS NOT THIS ACTIVITY'S BUSINESS. The licence check used to sit
        // here, which made drilling the one verb of thirty-one that knew about
        // it — so a company that had forfeited its acreage could still order a
        // separator onto it. It lives in `ActivityOrders` now, with every other
        // refusal that applies to any activity.
        //
        // The licence is still held here, and for the one thing that IS this
        // activity's business: recording the well that discharges a work
        // commitment (see Complete).

        return reasons;
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A dry hole opens nothing. The money is spent, the months are gone, and
        // what the player has bought is knowledge — which is the whole of
        // exploration economics and the reason drilling is a decision rather
        // than a button.
        var target = new EntityId<IProspect>(done.Target.Value);
        var prospect = new EntityRef(EntityKind.Prospect, done.Target.Value);

        if (!done.Succeeded)
        {
            // THE JOB WAS LOST, which is not the same as the rock being empty.
            // The outcome table decides whether the hole was drilled — on time,
            // late, over budget, or abandoned mechanically — and says nothing
            // about what was under it. So a mechanical failure teaches the
            // company nothing about the petroleum system, and recording a
            // geological diagnosis here would invent one (SDD-008 §4 requires
            // the diagnosis be truth-derived).
            return;
        }

        // THE HOLE WAS DRILLED. What it found is TRUTH — whether charge ever
        // reached this structure — and not a roll (SDD-010 §4b, finding 169).
        EntityId<IReservoirCompartmentEntity>? found = world.Beneath(target);

        if (found is null)
        {
            // A DRY HOLE. The money is spent, the months are gone, and what the
            // company bought is knowledge: this generator leaves a trap empty
            // for exactly one reason — the charge ran out before it migrated
            // this far — so the well disproved SOURCE at this location. That is
            // derived from how the world was made, which is what SDD-008 §4
            // means by a truth-derived diagnosis.
            //
            // Source is play-shared, so the news is bad for every prospect
            // drawing on the same system. That is the whole of "the play died",
            // and it is the moment exploration stops being a formality.
            if (risks.Knows(prospect)) risks.Drilled(prospect, PosFactor.Source, present: false);

            // AND THE STRUCTURE ITSELF IS SETTLED (finding 286): it leaves the
            // board, refuses a second hole, and the save remembers — the
            // Settlers geologist's "nothing here" flag, permanent on the map.
            world.Condemn(target);

            return;
        }

        EntityId<IReservoirCompartmentEntity> reservoir = found.Value;

        field.Drill(reservoir, done.Depth);

        // THE COMMITMENT IS TO A WELL THAT STANDS (SDD-011 §1's R20d.9
        // amendment), not to the money spent trying: a dry hole (the branch
        // above) delivers nothing. Matched by CONTENT ID — `Terms.Template` is
        // the same id `CommitmentItem.Kind` names — the same convention a
        // facility rung's `requiresTech` matches a registry node's id by.
        // THE DELIVERY IS RECORDED BY THE FIELD, where a well comes into
        // existence — so a well placed by a scenario or by world generation
        // discharges the commitment too, which it manifestly should and used
        // not to.

        // AND THE HOLE ITSELF TELLS THE COMPANY WHAT IT FOUND. A discovery
        // well penetrates the column, logs it and samples the rock, so a company
        // walks away from a strike knowing roughly how much is down there —
        // which is a different and far sharper question than how big the trap
        // was, and the one a development decision is actually taken on.
        //
        // Through the observation door like everything else (SDD-008 §3), so it
        // carries a sigma and a provenance and can be argued with. Wide, because
        // one hole has seen one point of a field and the other kilometre of it
        // is inference.
        door.Deliver(
            Defaults.DiscoverySource, Defaults.OilInPlaceKind,
            new EntityRef(EntityKind.Compartment, reservoir.Value),
            subsurface.TrueOilInPlaceOf(reservoir).CubicMetres,
            Provenance.WellTest);

        // THE PROSPECT BECOMES A FIELD, and the company keeps what it paid for
        // (SDD-008 §4). Seismic bought a belief about this structure's size;
        // drilling it did not make that knowledge wrong, it made the structure
        // an accumulation. The belief follows the thing it was always about,
        // with the same mean, sigma, provenance and as-of date — nothing new was
        // learned by the entity changing name.
        //
        // Without this a discovery stranded everything: the survey's belief
        // stayed on a prospect nobody would look at again, and the field it
        // described was a compartment the company knew nothing about.
        beliefs.ReKey(prospect, new EntityRef(EntityKind.Compartment, reservoir.Value));

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
