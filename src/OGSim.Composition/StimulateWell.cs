// R12b.7 — stimulation (SDD-003 §6's amendment, finding 253).
//
// A WELL DRILLED CLEAN CAN STILL BE MADE BETTER. Every completion opens at
// zero skin (SDD-003 §6's CompletionFor) — nothing here models drilling
// damage — so stimulating one is not restoring what was lost, the way
// acidising a plugged injector is; it is a genuine improvement below the
// drilled baseline, which is why it is capitalised rather than expensed
// (SDD-009 §1).
//
// AN ACID JOB, NOT A FRAC. "Stimulate" names three techniques and this ships
// the first: un-gated, matching the injector's own remediation having no
// tech gate either. Frac and multi-stage are E3-gated per this row's own
// text and need a technology this composition's shipped tree does not yet
// map to an activity — building them now would mean inventing that mapping
// rather than reading it.
//
// NO RIG (SDD-007 §1's null case), the same call R20d.20 made for acidising
// an injector, for the same reason: a wireline and pump crew is not the
// drilling rig, so a company can stimulate a well and drill another in the
// same month.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Company;

namespace OGSim.Composition;

/// <summary>Acidise a producing well: reduce its skin below the drilled baseline.</summary>
public sealed record StimulateWellCommand(
    EntityId<ICompletion> Well) : Command(Subject: null);

internal sealed class StimulateWellActivity(
    ActivityTerms terms,
    FieldControl field) : Activity<StimulateWellCommand>(terms)
{
    /// <summary>
    /// YES. Nothing degraded that this restores — every completion opens at
    /// zero skin — so this leaves the well genuinely better than it was
    /// drilled, which SDD-009 §1 books as an asset the same way a bigger
    /// separator is one.
    /// </summary>
    public override bool LeavesAnAsset => true;

    public override MovementCategory Spend => MovementCategory.Development;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(StimulateWellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (Asset(command.Well), NoDepth);
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(StimulateWellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (field.WellNamed(command.Well) is null)
            return
            [
                new RejectionReason(
                    "$loc:reject.no-such-well",
                    $"there is no well {command.Well.Value} to stimulate"),
            ];

        // A PLUGGED WELL IS OUT OF THE NETWORK FOR GOOD. The job would run, the
        // bill would land, and nothing would ever draw against the skin it
        // changed — a player who ordered one deserves to be told rather than
        // invoiced (the same reasoning as remediating a clean injector).
        if (field.IsWellAbandoned(command.Well))
            return
            [
                new RejectionReason(
                    "$loc:reject.already-abandoned",
                    $"well {command.Well.Value} is plugged; stimulating it would improve nothing"),
            ];

        return [];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A FAILED JOB LEAVES THE WELL EXACTLY AS IT WAS. The money and the
        // month are gone and the skin is untouched — the same shape as a dry
        // hole, and what makes committing to it a decision.
        if (!done.Succeeded) return;

        OGSim.Wells.Completion? completion =
            field.WellNamed(new EntityId<ICompletion>(done.Target.Value));

        // Refused before it could run (OwnRefusals), so a null here would be a
        // composition defect rather than a player outcome (R1 §2.5).
        if (completion is null)
            throw new InvariantFault("R1 §2.5", null,
                $"well {done.Target.Value} passed validation and no longer exists at completion");

        completion.Stimulate(Defaults.StimulationSkinReduction);
    }

    private static EntityRef Asset(EntityId<ICompletion> well) =>
        new(EntityKind.Completion, well.Value);
}
