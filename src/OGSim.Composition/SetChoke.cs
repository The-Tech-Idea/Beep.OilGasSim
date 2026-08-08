// R20.4 — shutting a well in (SDD-003 §5.1, design 04 §5 stage 3).
//
// THE LEVER THE ECONOMICS DEMAND. Operating cost scales with the liquid a field
// lifts, and it is charged on water as readily as on oil — so a well at a high
// water cut eventually costs more to produce than it earns. Until this, a player
// could watch that happen and do nothing about it.
//
// NOT AN ACTIVITY. Everything on the scheduled-activity engine is a project: it
// takes months, commits a rig, accrues cost and can go wrong. Turning a valve is
// none of those, and putting it on the engine would make a player wait a quarter
// to stop losing money — which would not be a harder decision, only a slower
// one. A choke tier that needs a site visit is a CONTENT distinction (07 §4b's
// remote choke), not a reason to make every valve turn a project.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>Open a well or shut it in.</summary>
public sealed record SetWellChokeCommand(
    EntityId<ICompletion> Well,
    bool Open) : Command(Subject: null);

/// <summary>
/// Pure: it decides whether the valve can be turned and nothing else
/// (R1 §2.5).
/// </summary>
internal sealed class SetWellChokeValidator(FieldControl field)
    : ICommandValidator<SetWellChokeCommand>
{
    public IReadOnlyList<RejectionReason> Validate(SetWellChokeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (field.WellNamed(command.Well) is not OGSim.Wells.Completion well)
            return
            [
                new RejectionReason(
                    "$loc:reject.no-such-well",
                    $"there is no well {command.Well.Value} to open or shut in"),
            ];

        // Already where it was asked to be. Refused rather than silently
        // accepted, because "shut in the well" and "the well is already shut
        // in" are different answers and a player acting on a stale read model
        // deserves the second one.
        if (well.IsShutIn != command.Open) return
            [
                new RejectionReason(
                    "$loc:reject.choke-unchanged",
                    $"well {command.Well.Value} is already " +
                    (command.Open ? "open" : "shut in")),
            ];

        return [];
    }
}

/// <summary>Cannot fail: everything that could refuse already has (R1 §2.5).</summary>
internal sealed class SetWellChokeApplier(FieldControl field, IAuditTrail audit)
    : ICommandApplier<SetWellChokeCommand>
{
    public Applied Apply(SetWellChokeCommand command, AuditId submission)
    {
        ArgumentNullException.ThrowIfNull(command);

        field.SetChoke(
            command.Well,
            command.Open ? OGSim.Wells.ChokeSetting.Open : OGSim.Wells.ChokeSetting.Closed);

        // Audited because it changes what the field produces, and a month whose
        // production fell for a reason the player chose should say so rather
        // than looking like decline.
        audit.Record(
            AuditCategory.StateTransition, subject: null, cause: submission,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["well"] = new(command.Well.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
                ["choke"] = new(command.Open ? "open" : "closed"),
            });

        return new Applied(submission, []);
    }
}
