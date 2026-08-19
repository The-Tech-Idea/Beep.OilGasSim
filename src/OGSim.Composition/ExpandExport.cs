// R20d.8 — expanding export (SDD-006 §7b, SDD-007 §5).
//
// THE VERB THAT MAKES A BIG FIELD WORTH MORE THAN A SMALL ONE. Until this, the
// export line took what it took and the accumulation underneath was irrelevant:
// ten times the oil earned the same money over twenty years, because both fields
// spent every month against one constant.
//
// A JUDGEMENT MADE ON BELIEFS, which is the whole reason this is a command and
// not a derivation. The capacity a field justifies depends on how much is down
// there, and a company only ever has an estimate of that. Overbuild against a
// prospect that disappointed and the capital is spent on a line that will never
// be full; underbuild an elephant and it produces for decades at a rate that
// leaves most of its value in the ground. Sizing the plant from truth would hand
// the player the answer to the question the information layer exists to make
// them guess (SDD-006 §7b).

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Company;

namespace OGSim.Composition;

/// <summary>
/// Lay the next export line up.
///
/// <para>Names no tier, for the reason <see cref="InstallSeparatorCommand"/>
/// names none: the catalogue describes a ladder, so "expand export" has one
/// meaning, and a chosen-tier parameter needs the block R12b.17 exists to
/// specify.</para>
/// </summary>
public sealed record ExpandExportCommand() : Command(Subject: null);

internal sealed class ExpandExportActivity(
    ActivityTerms terms,
    OGSim.Facilities.ExportTerminal terminal,
    IReadOnlyList<OGSim.Facilities.ExportTier> ladder) : Activity<ExpandExportCommand>(terms)
{
    /// <summary>A pipeline is PP&amp;E (SDD-009 §1).</summary>
    public override bool LeavesAnAsset => true;

    /// <summary>Capacity bought (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(ExpandExportCommand command) =>
        (terminal.Id, NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(ExpandExportCommand command)
    {
        if (NextRung() is not null) return [];

        return
        [
            new RejectionReason(
                "$loc:reject.top-of-the-ladder",
                $"'{terminal.Tier.Id.Value}' is the largest export line in the catalogue; " +
                "the field cannot sell more than it already can"),
        ];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A failed expansion leaves the old line. The money and the months are
        // gone — the same shape as a dry hole, and the reason committing to a
        // big capital item is a decision rather than a formality.
        if (!done.Succeeded) return;

        // Read at COMPLETION, from what is laid now: an expansion that finished
        // second fits the rung above wherever the field actually got to.
        if (NextRung() is OGSim.Facilities.ExportTier next) terminal.Fit(next);
    }

    /// <summary>The rung above what is laid, or null at the top. Walked in the
    /// ladder's declared order (D-5).</summary>
    private OGSim.Facilities.ExportTier? NextRung()
    {
        for (int i = 0; i < ladder.Count - 1; i++)
            if (ladder[i].Id == terminal.Tier.Id) return ladder[i + 1];

        return null;
    }
}
