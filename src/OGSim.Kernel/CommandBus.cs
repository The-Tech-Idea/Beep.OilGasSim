// R1.9 — the command bus (SDD-001 §7, design 03 §5). Commands are the ONLY way
// anything changes, which is what makes "seed + command sequence reproduces the
// game exactly" true, and what makes every mutation auditable.
//
// Two phases with a hard rule (R1 §2.5): validation may not mutate, and
// application may not fail. A command that reaches application has already been
// proven applicable, which is what makes a half-applied command structurally
// impossible rather than merely avoided.

namespace OGSim.Kernel;

public sealed class CommandBus : ICommandBus
{
    private readonly IAuditTrail _audit;
    private readonly IEventBus _events;
    private readonly Dictionary<Type, Handler> _handlers = [];

    public CommandBus(IAuditTrail audit, IEventBus events)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(events);
        _audit = audit;
        _events = events;
    }

    /// <summary>
    /// Modules register their handlers at composition time. This is on the
    /// concrete bus rather than ICommandBus so that a module holding the
    /// interface can submit but cannot re-point another module's handler.
    /// </summary>
    public void Register<TCommand>(
        ICommandValidator<TCommand> validator,
        ICommandApplier<TCommand> applier)
        where TCommand : Command
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(applier);

        // Two modules claiming one command type is the command-level form of a
        // duplicate provider, and design 03 §3.1 refuses that outright.
        if (_handlers.ContainsKey(typeof(TCommand)))
            throw new InvariantFault("L5", null,
                $"Command {typeof(TCommand).Name} already has a handler; " +
                "two modules cannot own one command.");

        _handlers.Add(typeof(TCommand), new Handler(
            command => validator.Validate((TCommand)command),
            (command, submission) => applier.Apply((TCommand)command, submission)));
    }

    public CommandResult Submit(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_handlers.TryGetValue(command.GetType(), out Handler? handler))
            throw new InvariantFault("SDD-001 §7", command.Subject,
                $"No handler registered for {command.GetType().Name}; " +
                "composition should have refused to start.");

        // Phase one: pure. Nothing below this line has mutated anything.
        IReadOnlyList<RejectionReason> reasons = handler.Validate(command);

        if (reasons.Count > 0)
        {
            // Design 09 §5.1 C3: a rejected command is NOT a failure — it is the
            // simulation correctly saying no, and the player gets the reason. It
            // is audited so R1-V13 holds: state and audit are byte-identical
            // apart from this entry.
            _audit.Record(AuditCategory.Rejection, command.Subject, null,
                RejectionData(command, reasons));
            return new Rejected(reasons);
        }

        AuditId submission = _audit.Record(AuditCategory.Command, command.Subject, null,
            new Dictionary<string, AuditValue>
            {
                ["command"] = new(command.GetType().Name),
                ["outcome"] = new("accepted"),
            });

        // Phase two: cannot fail. The applier returns what it did rather than
        // publishing it, so publication happens here — one place, in order.
        Applied applied = handler.Apply(command, submission);

        for (int i = 0; i < applied.Raised.Count; i++)
            _events.Publish(applied.Raised[i]);

        return new Accepted(applied.Audit, applied.Raised);
    }

    /// <summary>Every reason, indexed — a rejection that reported only its first
    /// cause would send the player round the loop once per problem.</summary>
    private static Dictionary<string, AuditValue> RejectionData(
        Command command, IReadOnlyList<RejectionReason> reasons)
    {
        var data = new Dictionary<string, AuditValue>
        {
            ["command"] = new(command.GetType().Name),
            ["outcome"] = new("rejected"),
            ["reasons"] = new(reasons.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        for (int i = 0; i < reasons.Count; i++)
        {
            string ordinal = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            data["reason." + ordinal] = new AuditValue(reasons[i].LocId);
            data["detail." + ordinal] = new AuditValue(reasons[i].Detail);
        }
        return data;
    }

    private sealed record Handler(
        Func<Command, IReadOnlyList<RejectionReason>> Validate,
        Func<Command, AuditId, Applied> Apply);
}
