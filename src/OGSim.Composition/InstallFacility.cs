// S2 step 3 — commission an early production facility (plans 22 §4).
//
// THE DECISION THAT STARTS A FIELD. A company that has found oil and drilled it
// still has nothing until there is somewhere for the fluid to go, and this is
// what it buys. It is the largest single commitment in the early game by design:
// bigger than a hole, and the thing a first discovery has to be worth.
//
// ONE PURCHASE RATHER THAN TEN, because the separator has three outlets and each
// needs a destination — so a company building strictly piece by piece would buy
// ten elements and see no oil until the tenth. Nine purchases with no visible
// effect is a shopping list, not a decision. A packaged EPF is also what the
// industry actually does with a small field (19, "early production facility").

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Company;

namespace OGSim.Composition;

/// <summary>Commission the plant this field's production will run through.</summary>
public sealed record InstallEarlyProductionFacilityCommand() : Command(Subject: null);

internal sealed class InstallEarlyProductionFacilityActivity(
    ActivityTerms terms,
    SurfaceChain plant,
    PlantBuilder works,
    FieldControl field,

    /// <summary>Where the spur's route is read from (finding 289): a plant on
    /// a coastal find is a short, cheap tie-in and one across the basin is
    /// not, which generation decided when it placed the field.</summary>
    WorldState world) : Activity<InstallEarlyProductionFacilityCommand>(terms)
{
    /// <summary>Steel the company still owns next month (SDD-009 §1).</summary>
    public override bool LeavesAnAsset => true;

    /// <summary>Building the means of production (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    /// <summary>
    /// One at a time, and only ever one at all — two crews commissioning two
    /// plants would each register the same element ids and the second would be
    /// refused by the network mid-tick rather than at the order.
    /// </summary>
    public override bool OnePerTarget => true;

    /// <summary>
    /// Aimed at the FIELD, because that is what gets a plant.
    /// </summary>
    /// <remarks>
    /// Every other install names the element it upgrades, which this one cannot:
    /// none of them exist yet, and the whole point is that it makes them.
    /// </remarks>
    public override (EntityRef Target, Length Depth) Aim(
        InstallEarlyProductionFacilityCommand command) =>
        (new EntityRef(EntityKind.Field, 1), NoDepth);

    public override string QuantityUnit => "kilometre";

    /// <summary>
    /// The route the spur will run (finding 289): the nearest discovered
    /// field's generated distance to market — the same distance the flowline
    /// is laid over at commissioning. A hand-built field no world placed has
    /// no distance and answers zero, which is the composed route it keeps.
    /// </summary>
    public override double Quantity(InstallEarlyProductionFacilityCommand command)
    {
        var nearest = 0.0;

        IReadOnlyList<EntityId<IProspect>> prospects = world.Prospects;

        for (int i = 0; i < prospects.Count; i++)
        {
            if (world.Beneath(prospects[i]) is null) continue;
            if (world.DistanceToMarket(prospects[i]) is not Length away) continue;

            if (nearest == 0.0 || away.ToKilometres() < nearest)
                nearest = away.ToKilometres();
        }

        return nearest;
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(
        InstallEarlyProductionFacilityCommand command)
    {
        if (!PlantBuilder.Standing(plant))
            return [];

        return
        [
            new RejectionReason(
                "$loc:reject.plant-already-standing",
                "this field already has a plant; more capacity is bought one vessel at a " +
                "time from here, not by commissioning a second facility"),
        ];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A FAILED COMMISSIONING LEAVES BARE GROUND. The money and the months
        // are gone and there is no plant — the same shape as a dry hole, and the
        // reason committing to one is a decision rather than a formality.
        if (!done.Succeeded) return;

        // Checked again at COMPLETION rather than trusted from the order: two
        // commissionings ordered in the same month would both have passed the
        // refusal above, and the second must not register ids the first already
        // has.
        if (PlantBuilder.Standing(plant)) return;

        works.Commission(plant);

        // AND THE WELLS THAT WERE WAITING FOR IT COME ON (plans 23). A company
        // that drilled on frontier acreage and then built a plant expects the
        // holes it already paid for to start producing — asking it to re-order
        // something it has already bought would be a worse game and a worse
        // simulation.
        field.TieInWaiting();
    }
}
