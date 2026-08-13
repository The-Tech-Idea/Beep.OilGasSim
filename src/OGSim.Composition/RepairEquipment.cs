// R20d.22 — putting it back in service (SDD-012 §2–§3, finding 180).
//
// A FAILURE WITHOUT A REPAIR IS NOT A MECHANIC, IT IS AN ENDING. The hazard pass
// takes an element out of the network and the route law shuts in everything
// behind it, so the first unlucky draw stops the field — permanently, if there
// is nothing here. That is the third time this project has built a cost with no
// response (findings 172, 177), and the reason this ships in the same commit as
// the failure it answers rather than in the next one.
//
// AN ORDINARY OPERATION, which SDD-012 §3 insists on in as many words: "all
// three produce ordinary SDD-007 operations — no special execution path". It
// costs money, it takes a month, and it can fail, so the field is down for two
// months when it does. Nothing about a repair needs the scheduler to know it is
// a repair.
//
// NO RIG. A maintenance crew is not the drilling rig, so a company can repair a
// separator and drill a well in the same month — they compete for money, not for
// iron. The same call R20d.20 made for acidising, for the same reason.
//
// EMERGENCY WORK ONLY, since R20d.26.2. This template shipped preventive as
// well as corrective, and priced them identically — so waiting was free and
// run-to-failure dominated on every seed (finding 185). Unplanned work is
// dearer than planned work everywhere maintenance is a profession: parts are
// freighted rather than stocked, a crew is mobilised rather than scheduled.
// Equipment that still works is the OTHER template, service-equipment, at the
// planned price — and the validators make the two mutually exclusive.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>Emergency repair of failed equipment: condition back to new.</summary>
public sealed record RepairEquipmentCommand(EntityRef Equipment) : Command(Subject: Equipment);

internal sealed class RepairEquipmentActivity(
    ActivityTerms terms,
    OGSim.Integrity.AssetIntegrity integrity,
    IFlowElementRegistry network) : Activity<RepairEquipmentCommand>(terms)
{
    /// <summary>
    /// NO. The money buys back a capability the equipment already had, which is
    /// not a new asset (SDD-009 §1) — the same reasoning as acidising an
    /// injector, and the reason a company cannot improve its balance sheet by
    /// letting things break and mending them.
    /// </summary>
    public override bool LeavesAnAsset => false;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(RepairEquipmentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (command.Equipment, NoDepth);
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(RepairEquipmentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Equipment.Kind != EntityKind.FlowElement)
            return
            [
                new RejectionReason(
                    "$loc:reject.not-equipment",
                    "only a piece of equipment can be repaired"),
            ];

        var element = new EntityId<IFlowElement>(command.Equipment.Value);

        if (!IsRegistered(element))
            return
            [
                new RejectionReason(
                    "$loc:reject.no-such-equipment",
                    $"there is no element {command.Equipment.Value} in this field"),
            ];

        // STILL WORKING. An emergency crew for equipment that has not failed is
        // the planned job bought at the emergency price, and refusing it is what
        // keeps SDD-012 §3's two operations two prices rather than one
        // (R20d.26.2): worn-but-working equipment is service-equipment's
        // subject, not this one's.
        if (!integrity.HasFailed(element))
            return
            [
                new RejectionReason(
                    "$loc:reject.not-failed",
                    "the equipment is still working; order a scheduled service, not an emergency repair"),
            ];

        return [];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A FAILED JOB LEAVES IT EXACTLY AS BROKEN. The money and the month are
        // gone and the field is still down — which is what makes a company hold
        // a spare rather than a plan, and the same shape as a dry hole.
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
