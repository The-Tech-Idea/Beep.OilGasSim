// R20d.24 — the flood, as a lever (SDD-003 §3.1d, finding 182).
//
// THE OLDEST DECISION IN RESERVOIR MANAGEMENT, and until now this engine had
// every part of it except the water. Produced water already went back down the
// hole — but a field can only put back what it makes, and early in life it makes
// almost none, which is precisely when support is worth most. Measured on the
// shipped field: 0.0033 pore volumes over forty years, against 0.1–1 for a real
// flood. Reinjection is disposal that happens to help. This is the decision.
//
// WHAT IT BUYS AND WHAT IT COSTS. Water bought today holds the pressure up, so
// the wells flow harder for longer and more of the oil comes out. It is also
// water: it raises the saturation, so the field waters out sooner, and it shares
// the injector with the disposal duty, so it plugs the well faster and the acid
// job comes round more often. Flood hard for recovery, flood little to keep the
// oil dry — and the ratio is where a player says which.
//
// NOT AN ACTIVITY, for the reason a choke is not (R20.4). Everything on the
// scheduled engine is a project: months, a rig, a cost, a chance of failure.
// A voidage replacement target is a number a reservoir engineer changes on a
// Tuesday afternoon, and committing a quarter to it would make the decision
// slower without making it harder. What costs money is the water, charged by the
// cubic metre in the month it is lifted.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// How much of the voidage to replace: 0 is no flood, 1 replaces every reservoir
/// cubic metre the field takes out (SDD-003 §3.1d).
/// </summary>
public sealed record SetVoidageReplacementCommand(double Ratio) : Command(Subject: null);

/// <summary>
/// Pure: it decides whether the number is a ratio and nothing else (R1 §2.5).
/// </summary>
internal sealed class SetVoidageReplacementValidator(ProductionLoop loop)
    : ICommandValidator<SetVoidageReplacementCommand>
{
    public IReadOnlyList<RejectionReason> Validate(SetVoidageReplacementCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!double.IsFinite(command.Ratio) || command.Ratio < 0.0)
            return
            [
                new RejectionReason(
                    "$loc:reject.not-a-ratio",
                    "a voidage replacement ratio is a finite fraction of what the field " +
                    "produced; a negative one would be a reservoir pumping water out to sea"),
            ];

        // NO UPPER BOUND, and that is deliberate rather than an oversight. What
        // limits a flood is the injector: the intake is clamped to the headroom
        // the disposal duty leaves, so an absurd ratio buys exactly the water the
        // well will take and no more. An invented ceiling here would be a second
        // limit that could disagree with the real one.

        // Already where it was asked to be. Refused rather than silently
        // accepted, for the same reason a choke command is: "flood at 1.0" and
        // "you are already flooding at 1.0" are different answers, and a player
        // acting on a stale read model deserves the second.
        if (command.Ratio == loop.VoidageReplacement)
            return
            [
                new RejectionReason(
                    "$loc:reject.voidage-unchanged",
                    "the field is already replacing that much of its voidage"),
            ];

        return [];
    }
}

/// <summary>Cannot fail: everything that could refuse already has (R1 §2.5).</summary>
internal sealed class SetVoidageReplacementApplier(ProductionLoop loop, IAuditTrail audit)
    : ICommandApplier<SetVoidageReplacementCommand>
{
    public Applied Apply(SetVoidageReplacementCommand command, AuditId submission)
    {
        ArgumentNullException.ThrowIfNull(command);

        loop.SetVoidageReplacement(command.Ratio);

        // Audited because it changes what the field costs AND what the reservoir
        // does, on two different timescales — the bill lands next month and the
        // recovery lands in a decade. A player reading back why their late life
        // went the way it did needs the month they decided it.
        audit.Record(
            AuditCategory.StateTransition, subject: null, cause: submission,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["voidage-replacement"] = new(command.Ratio.ToString(
                    "R", System.Globalization.CultureInfo.InvariantCulture)),
            });

        return new Applied(submission, []);
    }
}
