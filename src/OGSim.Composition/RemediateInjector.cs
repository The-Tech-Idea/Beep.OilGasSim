// R20d.20 — clearing a plugged injector (SDD-003 §3.1d, R10-V4).
//
// R20d.18 GAVE THE DISPOSAL WELL SOMETHING TO DO AND NO WAY TO RECOVER. Every
// cubic metre of water put down it adds skin, the skin lowers what it will
// accept, and the injector constrains the field exactly as a separator does — so
// a company that waterfloods for twenty years is throttled by a well it cannot
// unplug. That is finding 172's shape a second time, and made by the same hand.
//
// AN ACID JOB IS A REAL JOB. It costs money, it takes a month, and it can fail —
// so leaving the well to plug a while longer is a genuine choice against
// spending now, which is what makes the decline a decision rather than a clock.
//
// NO RIG (SDD-007 §1's null case). A wireline and pump crew is not the drilling
// rig, so a company can clear an injector and drill in the same month — the two
// compete for money, not for iron.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>Acidise the disposal well and clear what has built up in it.</summary>
public sealed record RemediateInjectorCommand() : Command(Subject: null);

internal sealed class RemediateInjectorActivity(
    ActivityTerms terms,
    OGSim.Wells.Injector injector) : Activity<RemediateInjectorCommand>(terms)
{
    /// <summary>
    /// NO. The money buys back a capability the well already had — it restores
    /// what plugging took, and a company cannot improve its balance sheet by
    /// cleaning something (SDD-009 §1). The same reasoning as an abandonment,
    /// with the sign of the outcome reversed.
    /// </summary>
    public override bool LeavesAnAsset => false;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(RemediateInjectorCommand command) =>
        (new EntityRef(EntityKind.FlowElement, injector.Id.Value), NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(RemediateInjectorCommand command)
    {
        // NOTHING TO CLEAR. Refused rather than run for the money: a clean well
        // acidised is a month and a bill for no change at all, and a player who
        // ordered one deserves to be told rather than invoiced.
        if (injector.CumulativeInjected.CubicMetres <= 0.0)
            return
            [
                new RejectionReason(
                    "$loc:reject.nothing-to-remediate",
                    "the disposal well has taken no water and has nothing plugging it"),
            ];

        return [];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A FAILED JOB LEAVES THE WELL EXACTLY AS PLUGGED AS IT WAS. The money
        // and the month are gone, which is what makes committing to it a
        // decision rather than a formality — the same shape as a dry hole.
        if (!done.Succeeded) return;

        injector.Remediate();
    }
}
