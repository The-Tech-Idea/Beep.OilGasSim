// R12b.10 — abandonment (SDD-007 §6, design 02 §3.4).
//
// THE ENDING. A field's arc is build-up, plateau, decline and then a long tail
// producing almost nothing while the standing charge eats the cash it made — and
// until this a player could watch that happen for thirty years and had no way to
// stop paying for it. Shutting a well in stops what it costs to lift; only
// abandoning it ends the liability and lets the field close.
//
// IT COSTS MONEY TO STOP, and that is the decision. Plugging a well is a real
// job with a real bill, so a company that has run its field into the ground may
// find it cannot afford to leave — which is the authentic and uncomfortable
// shape of late life, and the reason the obligation is registered the moment the
// well is drilled rather than when someone remembers it.
//
// ONLY A COMPLETED ABANDONMENT DISCHARGES (SDD-007 §6). Not shutting in, not
// walking away, not the licence expiring: the plug has to go in the hole.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>Plug and abandon a well.</summary>
public sealed record AbandonWellCommand(
    EntityId<ICompletion> Well) : Command(Subject: null);

internal sealed class AbandonWellActivity(
    ActivityTerms terms,
    FieldControl field,
    IObligationRegistry obligations) : Activity<AbandonWellCommand>(terms)
{
    /// <summary>
    /// NO. It removes an asset rather than leaving one — the money buys the end
    /// of a liability, which is not something the company owns next month
    /// (SDD-009 §1). Capitalising it would let a field improve its balance sheet
    /// by decommissioning itself.
    /// </summary>
    public override bool LeavesAnAsset => false;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(AbandonWellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (Asset(command.Well), NoDepth);
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(AbandonWellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (field.WellNamed(command.Well) is null)
            return
            [
                new RejectionReason(
                    "$loc:reject.no-such-well",
                    $"there is no well {command.Well.Value} to abandon"),
            ];

        // Already plugged. Refused rather than quietly re-run, because the
        // obligation is gone and a second abandonment would be months and money
        // spent discharging nothing.
        //
        // Asked as "does it still cost anything", which is SDD-007 §6's own
        // vocabulary: the registry answers `EstimatedCost` and nothing owed is
        // nothing to pay. A separate "is it outstanding" would be a second way
        // to ask one question.
        if (obligations.EstimatedCost(Asset(command.Well)) == Money.Zero)
            return
            [
                new RejectionReason(
                    "$loc:reject.already-abandoned",
                    $"well {command.Well.Value} carries no abandonment obligation; " +
                    "it has already been plugged"),
            ];

        return [];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A FAILED abandonment leaves the well and the liability exactly where
        // they were. The money and the months are gone and the hole is still
        // open — which is what makes committing to it a decision rather than a
        // formality, and the same shape as a dry hole.
        if (!done.Succeeded) return;

        field.Abandon(new EntityId<ICompletion>(done.Target.Value), done.Cause);
    }

    /// <summary>
    /// A completion, as the obligation registry names it. The registry keys on
    /// assets generally — a facility carries one too — so the well's identity
    /// crosses as an <see cref="EntityRef"/> rather than a completion id.
    /// </summary>
    private static EntityRef Asset(EntityId<ICompletion> well) =>
        new(EntityKind.Completion, well.Value);
}
