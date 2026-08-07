// SDD-001 §7 — the only way anything changes. Two-phase inside: validation is
// pure, application cannot fail (R1 §2.5). §7.1: commands are named
// VerbNounCommand, declared per module, and the REQUIRED set derives from the
// 61-decision catalogue via the PD1 fixture.

namespace OGSim.Kernel;

public abstract record Command(EntityRef? Subject);

/// <summary>
/// A domain-typed rejection reason, renderable directly as player-facing text
/// (R21-V5). LocId is a localisation key (SDD-004 §2); Detail is diagnostic.
/// </summary>
public sealed record RejectionReason(string LocId, string Detail);

public abstract record CommandResult;

public sealed record Accepted(
    AuditId Audit,
    IReadOnlyList<EngineEvent> Immediate) : CommandResult
{
    // Finding 131 — a record carrying a collection compares it element by
    // element, or it compares array identities and always answers "different".
    public bool Equals(Accepted? other) =>
        other is not null && Audit == other.Audit
        && Structural.Equal(Immediate, other.Immediate);

    public override int GetHashCode() => HashCode.Combine(Audit, Structural.HashOf(Immediate));
}

/// <summary>ALL reasons, not the first.</summary>
public sealed record Rejected(
    IReadOnlyList<RejectionReason> Reasons) : CommandResult
{
    // Finding 131.
    public bool Equals(Rejected? other) =>
        other is not null && Structural.Equal(Reasons, other.Reasons);

    public override int GetHashCode() => Structural.HashOf(Reasons);
}

public interface ICommandBus
{
    CommandResult Submit(Command command);
}

/// <summary>Pure — may not mutate. Empty list means valid (R1 §2.5).</summary>
public interface ICommandValidator<in TCommand> where TCommand : Command
{
    IReadOnlyList<RejectionReason> Validate(TCommand command);
}

/// <summary>
/// What application produced: the audit entry recording it, and the events it
/// raised. The events come BACK rather than being published by the applier, so
/// publication sits on the bus exactly where design 03 §5 draws it, and an
/// applier needs no IEventBus of its own (R1.9).
/// </summary>
public sealed record Applied(AuditId Audit, IReadOnlyList<EngineEvent> Raised)
{
    // Finding 131.
    public bool Equals(Applied? other) =>
        other is not null && Audit == other.Audit && Structural.Equal(Raised, other.Raised);

    public override int GetHashCode() => HashCode.Combine(Audit, Structural.HashOf(Raised));
}

/// <summary>
/// May not fail — a command that reaches application has been proven applicable
/// (R1 §2.5).
///
/// <paramref name="submission"/> is the bus's accepted-audit entry. It is passed
/// in because the applier must have a cause id to stamp on the events it raises:
/// without one, any Critical or Decision event a command produces would be
/// unpublishable under INV12.
/// </summary>
public interface ICommandApplier<in TCommand> where TCommand : Command
{
    Applied Apply(TCommand command, AuditId submission);
}
