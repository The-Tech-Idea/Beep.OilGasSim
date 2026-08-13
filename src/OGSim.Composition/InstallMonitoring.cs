// R20d.26.4 — the instrument that makes a strategy selectable (SDD-012 §3,
// catalogue C14, finding 191).
//
// WEAR IS NOT FREE INFORMATION. Until this, the chain view published every
// element's condition to anyone holding a read model, and `service-equipment`
// acted on it — so the maintenance strategy that pays was the one strategy
// nobody had to buy, and C14's condition-monitoring kit, whose catalogue line
// is literally "enables condition-based maintenance", was content with no
// customer. §3 has said "ConditionBased: requires monitoring tier installed
// (C14)" since it was written; the record that implemented that gate was called
// by its own unit test and nothing else.
//
// PER ASSET, which is what the catalogue specifies ("install per asset") and
// what makes it a recurring decision rather than a one-off unlock: a vessel
// bought in year twenty arrives blind like every other, so a company that keeps
// debottlenecking keeps choosing whether to instrument what it just installed.
//
// CHEAP AGAINST WHAT IT REVEALS AND NOT AGAINST WHAT IT SAVES. The kit is a
// fraction of one emergency repair, because it is a sensor and a wire rather
// than a crew and a crane. What makes it a decision is that a field has a
// dozen elements and instrumenting all of them costs real money before any of
// them has told you anything.
//
// NOT TECH-GATED OR ERA-GATED, deliberately and temporarily. C14 puts this at
// E3 behind a "Condition monitoring" technology, and in this engine nothing can
// acquire a technology and the era never advances (finding 191), so either gate
// would put condition-based maintenance permanently out of reach — a cost with
// no response, which findings 172 and 177 already cost this project twice. The
// gate arrives with R20d.10.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>Fit a condition-monitoring kit to one piece of equipment.</summary>
public sealed record InstallMonitoringCommand(EntityRef Equipment) : Command(Subject: Equipment);

internal sealed class InstallMonitoringActivity(
    ActivityTerms terms,
    OGSim.Integrity.AssetIntegrity integrity,
    IFlowElementRegistry network) : Activity<InstallMonitoringCommand>(terms)
{
    /// <summary>YES. A sensor bolted to a vessel is still there next month, so
    /// it is PP&amp;E and not a survey consumed in the buying (SDD-009 §1) — the
    /// same answer as any other installed hardware.</summary>
    public override bool LeavesAnAsset => true;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallMonitoringCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (command.Equipment, NoDepth);
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallMonitoringCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Equipment.Kind != EntityKind.FlowElement)
            return
            [
                new RejectionReason(
                    "$loc:reject.not-equipment",
                    "only a piece of equipment can be instrumented"),
            ];

        var element = new EntityId<IFlowElement>(command.Equipment.Value);

        if (!IsRegistered(element))
            return
            [
                new RejectionReason(
                    "$loc:reject.no-such-equipment",
                    $"there is no element {command.Equipment.Value} in this field"),
            ];

        // ALREADY WIRED. Refused rather than billed twice, and it is the one
        // refusal here that tells a player nothing they did not already know:
        // the chain view reports a condition for exactly the instrumented
        // elements, so this answer is already on their screen.
        if (integrity.IsMonitored(element))
            return
            [
                new RejectionReason(
                    "$loc:reject.already-monitored",
                    "the equipment already has a condition-monitoring kit fitted"),
            ];

        return [];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A failed install fits nothing and the money is gone — the same shape
        // as every other install, and as a dry hole.
        if (!done.Succeeded) return;

        integrity.FitMonitoring(new EntityId<IFlowElement>(done.Target.Value));
    }

    private bool IsRegistered(EntityId<IFlowElement> element)
    {
        IReadOnlyList<IFlowElement> registered = network.Registered;

        for (int i = 0; i < registered.Count; i++)
            if (registered[i].Id == element) return true;

        return false;
    }
}
