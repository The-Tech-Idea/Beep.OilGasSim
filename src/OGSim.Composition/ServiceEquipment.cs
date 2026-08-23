// R20d.26.2 — planned work, priced as planned work (SDD-012 §3, finding 187).
//
// UNPLANNED WORK IS DEARER THAN PLANNED WORK, and until these were two
// operations §3 offered one strategy: a single repair-equipment template cost
// the same whether the equipment had failed or was merely worn, so waiting was
// free and run-to-failure dominated on every seed (finding 185). This is the
// planned half — a crew scheduled while the plant still runs, parts ordered
// rather than freighted.
//
// THE ASYMMETRY SHIPS IN MONEY ONLY. The duration half is the lever that
// matters physically — an emergency repair is served while the route law has
// the chain shut in behind the failure — and it was built, measured, and
// reverted: a month of outage costs ~$12M at plateau against a worst-seed
// fifth-year margin of $17M, so the field it was meant to serve could not
// afford it (finding 187). Whether money alone is enough to make preventive
// work a strategy is what the measurement after this decides.
//
// MUTUALLY EXCLUSIVE WITH THE EMERGENCY JOB, in the validators: a player
// cannot buy the planned price for a broken thing, and a crew cannot be sent
// to overhaul what has already stopped — that is the other template, at its
// own price.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Company;

namespace OGSim.Composition;

/// <summary>Planned overhaul of working equipment: condition back to new.</summary>
public sealed record ServiceEquipmentCommand(EntityRef Equipment) : Command(Subject: Equipment);

internal sealed class ServiceEquipmentActivity(
    ActivityTerms terms,
    OGSim.Integrity.AssetIntegrity integrity,
    IFlowElementRegistry network) : Activity<ServiceEquipmentCommand>(terms)
{
    /// <summary>
    /// NO. The money buys back a capability the equipment already had, which is
    /// not a new asset (SDD-009 §1) — the same answer as the emergency repair,
    /// because scheduling the work does not change what the work is.
    /// </summary>
    public override bool LeavesAnAsset => false;

    /// <summary>Planned maintenance (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Operating;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(ServiceEquipmentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (command.Equipment, NoDepth);
    }

    public override string QuantityUnit => "element";

    /// <summary>The standing plant the crew mobilises to (finding 289): a
    /// bigger built chain costs more to keep than a wellhead and a spur, on
    /// every map and at every stage of the game.</summary>
    public override double Quantity(ServiceEquipmentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return network.Registered.Count;
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(ServiceEquipmentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Equipment.Kind != EntityKind.FlowElement)
            return
            [
                new RejectionReason(
                    "$loc:reject.not-equipment",
                    "only a piece of equipment can be serviced"),
            ];

        var element = new EntityId<IFlowElement>(command.Equipment.Value);

        if (!IsRegistered(element))
            return
            [
                new RejectionReason(
                    "$loc:reject.no-such-equipment",
                    $"there is no element {command.Equipment.Value} in this field"),
            ];

        // NOBODY CAN SEE IT (C14, §3's R20d.26.4 amendment). Condition-based
        // maintenance requires the instrument that makes condition a fact a
        // company holds, and this refusal is the second half of that gate: the
        // read model publishes no condition for an unmonitored element, and
        // without this a player could still find the worn ones by submitting
        // services and reading which came back "nothing to overhaul".
        //
        // ASKED BEFORE the failure test on purpose. A company with no kit
        // learns nothing here either way — the answer is the same sentence
        // whether the vessel is worn, as-new, or broken.
        if (!integrity.IsMonitored(element))
            return
            [
                new RejectionReason(
                    "$loc:reject.not-monitored",
                    "nothing is measuring this equipment's condition, so there is no " +
                    "condition to schedule work against; fit a monitoring kit first"),
            ];

        // ALREADY BROKEN. Planned work is planned because the plant is still
        // running; what has failed is an emergency and pays the emergency
        // price. The exclusion is the whole reason two templates exist
        // (SDD-012 §3, R20d.26.2).
        if (integrity.HasFailed(element))
            return
            [
                new RejectionReason(
                    "$loc:reject.already-failed",
                    "the equipment has failed and needs an emergency repair, not a scheduled service"),
            ];

        // NOTHING WRONG WITH IT. Refused rather than run for the money: a crew
        // sent to overhaul equipment that is already as new is a month and a
        // bill for no change, and a player who ordered one deserves to be told.
        if (!integrity.NeedsRepair(element))
            return
            [
                new RejectionReason(
                    "$loc:reject.nothing-to-repair",
                    "the equipment is in as-new condition and has nothing to overhaul"),
            ];

        return [];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A FAILED JOB LEAVES IT EXACTLY AS WORN. The money and the month are
        // gone and the decay curve keeps charging — the same shape as a dry
        // hole, and the same shape as the emergency job going wrong.
        if (!done.Succeeded) return;

        integrity.Repair(new EntityId<IFlowElement>(done.Target.Value));
    }

    private bool IsRegistered(EntityId<IFlowElement> element)
    {
        IReadOnlyList<IFlowElement> registered = network.Registered;

        for (int i = 0; i < registered.Count; i++)
            if (registered[i].Id == element) return true;

        return false;
    }
}
