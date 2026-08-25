// R17.3/R17.4 — buying technology, and what holding it costs (design 07 §3,
// SDD-005 §2, finding 293).
//
// Three-quarters of the shipped technology tree offered no diffusion route and
// no other route had a door: forty-eight of sixty-five nodes were permanently
// unreachable, and the two routes a company PAYS for — the interesting ones —
// existed as enum values nothing could choose. These are the doors, and the
// bill: licence a node and it is yours this month and expensive forever; put a
// programme on it and it is a year of budget and then owned outright. A small
// company rents; a major develops — design 07 §3's own sentence, now a
// decision a player actually takes.

using OGSim.Capabilities;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>Take a vendor licence on a technology: granted this month, billed
/// every month after, never owned (design 07 §3).</summary>
public sealed record LicenseTechnologyCommand(ContentId Technology) : Command(Subject: null);

/// <summary>Put an in-house programme on a technology: a sustained monthly
/// budget for the node's stated months, then it is owned outright.</summary>
public sealed record ResearchTechnologyCommand(ContentId Technology) : Command(Subject: null);

/// <summary>
/// The shared refusal set for both procurement doors: the node, the era, the
/// prerequisites and the route — every reason, not the first (R1 §2.5).
/// </summary>
internal static class Procurement
{
    public static List<RejectionReason> Refusals(
        IReadOnlyList<TechnologyNode> registry, CapabilityState capabilities,
        ContentId technology, AcquisitionRoute route)
    {
        var reasons = new List<RejectionReason>();

        TechnologyNode? node = null;

        for (int i = 0; i < registry.Count; i++)
            if (registry[i].Id.Value == technology) { node = registry[i]; break; }

        if (node is null)
        {
            reasons.Add(new RejectionReason(
                "$loc:reject.no-such-technology",
                $"'{technology.Value}' is not a technology this build's tree carries"));

            return reasons;
        }

        if (!node.Routes.Contains(route))
            reasons.Add(new RejectionReason(
                "$loc:reject.route-not-offered",
                $"'{technology.Value}' cannot be acquired by {route}; its routes are " +
                string.Join(", ", node.Routes)));

        if (node.AvailableFrom > capabilities.Era)
            reasons.Add(new RejectionReason(
                "$loc:reject.before-its-era",
                $"'{technology.Value}' arrives with era {node.AvailableFrom}, and it is " +
                $"{capabilities.Era}"));

        for (int i = 0; i < node.Prerequisites.Count; i++)
            if (!capabilities.Technology.Acquired.Contains(node.Prerequisites[i]))
                reasons.Add(new RejectionReason(
                    "$loc:reject.prerequisite-missing",
                    $"'{technology.Value}' requires " +
                    $"'{node.Prerequisites[i].Value.Value}', which is not held"));

        if (capabilities.Technology.Acquired.Contains(new TechnologyId(technology)))
            reasons.Add(new RejectionReason(
                "$loc:reject.already-held",
                $"'{technology.Value}' is already held; a capability is not bought twice"));

        for (int i = 0; i < capabilities.Technology.Researching.Count; i++)
            if (capabilities.Technology.Researching[i].Tech.Value == technology)
                reasons.Add(new RejectionReason(
                    "$loc:reject.already-under-way",
                    $"a programme is already running on '{technology.Value}'"));

        return reasons;
    }
}

internal sealed class LicenseTechnologyValidator(
    IReadOnlyList<TechnologyNode> registry, CapabilityState capabilities)
    : ICommandValidator<LicenseTechnologyCommand>
{
    public IReadOnlyList<RejectionReason> Validate(LicenseTechnologyCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return Procurement.Refusals(
            registry, capabilities, command.Technology, AcquisitionRoute.Licence);
    }
}

internal sealed class LicenseTechnologyApplier(
    CapabilityState capabilities, IAuditTrail audit)
    : ICommandApplier<LicenseTechnologyCommand>
{
    public Applied Apply(LicenseTechnologyCommand command, AuditId submission)
    {
        ArgumentNullException.ThrowIfNull(command);

        capabilities.Technology.Acquire(
            new TechnologyId(command.Technology), capabilities.Era, AcquisitionRoute.Licence);

        audit.Record(
            AuditCategory.StateTransition, subject: null, cause: submission,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["kind"] = new("technology.licensed"),
                ["technology"] = new(command.Technology.Value),
            });

        return new Applied(submission, []);
    }
}

internal sealed class ResearchTechnologyValidator(
    IReadOnlyList<TechnologyNode> registry, CapabilityState capabilities)
    : ICommandValidator<ResearchTechnologyCommand>
{
    public IReadOnlyList<RejectionReason> Validate(ResearchTechnologyCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return Procurement.Refusals(
            registry, capabilities, command.Technology, AcquisitionRoute.Research);
    }
}

internal sealed class ResearchTechnologyApplier(
    CapabilityState capabilities, IAuditTrail audit)
    : ICommandApplier<ResearchTechnologyCommand>
{
    public Applied Apply(ResearchTechnologyCommand command, AuditId submission)
    {
        ArgumentNullException.ThrowIfNull(command);

        capabilities.Technology.StartResearch(
            new TechnologyId(command.Technology), capabilities.Era);

        audit.Record(
            AuditCategory.StateTransition, subject: null, cause: submission,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["kind"] = new("technology.research-started"),
                ["technology"] = new(command.Technology.Value),
            });

        return new Applied(submission, []);
    }
}

/// <summary>
/// Stage 11, after diffusion: what the month's technology holdings COST
/// (design 07 §3.1's "a company can be over-teched for its size", finding
/// 293). Licence fees on everything licensed, this month's budget on every
/// running programme — and the programmes that finished this month land
/// through the same grant every other route uses.
/// </summary>
internal sealed class TechnologyFeesStage(
    CapabilityState capabilities,
    OGSim.Company.CompanyState company,
    IAuditTrail audit) : ITickStage
{
    public StageId Id => StageId.Company;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Money fees = Money.FromMillions(capabilities.Technology.LicenceFeeMillionsThisTick());

        if (fees.Cents > 0)
        {
            AuditId cause = audit.Record(
                AuditCategory.Financial, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["spend"] = new("technology-licence-fees"),
                });

            company.Ledger.Post(new OGSim.Company.Movement(
                context.Tick, OGSim.Company.Account.Opex, OGSim.Company.Account.Cash,
                fees, OGSim.Company.MovementCategory.Operating, Asset: null, Cause: cause));
        }

        (IReadOnlyList<TechnologyId> completed, double spendMillions) =
            capabilities.Technology.AdvanceResearch(capabilities.Era);

        Money spend = Money.FromMillions(spendMillions);

        if (spend.Cents > 0)
        {
            AuditId cause = audit.Record(
                AuditCategory.Financial, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["spend"] = new("technology-research"),
                });

            company.Ledger.Post(new OGSim.Company.Movement(
                context.Tick, OGSim.Company.Account.Opex, OGSim.Company.Account.Cash,
                spend, OGSim.Company.MovementCategory.Operating, Asset: null, Cause: cause));
        }

        for (int i = 0; i < completed.Count; i++)
            audit.Record(
                AuditCategory.StateTransition, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["kind"] = new("technology.researched"),
                    ["technology"] = new(completed[i].Value.Value),
                });
    }
}
