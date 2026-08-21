// SDD-007 §4.1's finding-265 amendment — the crew, as a lever a player pulls.
//
// A ONE-TIME INVESTMENT, not a per-operation dial. A company that never trains
// stays cheap and stays exposed — every operation runs 15% slower and every
// barrier the bow-tie samples is weaker for it. A company that trains pays
// once and keeps both improvements for the rest of the game, the same shape a
// technology acquisition already has.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>Trains the crew, once. Refused a second time (SDD-007 §4.1).</summary>
public sealed record TrainCrewCommand() : Command(Subject: null);

/// <summary>Pure: whether the company can afford it and has not already done
/// it (R1 §2.5).</summary>
internal sealed class TrainCrewValidator(OGSim.Company.CompanyState company, CrewState crew)
    : ICommandValidator<TrainCrewCommand>
{
    public IReadOnlyList<RejectionReason> Validate(TrainCrewCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (crew.Trained)
            return
            [
                new RejectionReason(
                    "$loc:reject.crew-already-trained",
                    "this crew has already been trained; there is no second level to buy"),
            ];

        if (company.Ledger.Cash < crew.TrainingCost)
            return
            [
                new RejectionReason(
                    "$loc:reject.insufficient-cash",
                    $"training costs {crew.TrainingCost.Cents} cents and the company holds " +
                    $"{company.Ledger.Cash.Cents}"),
            ];

        return [];
    }
}

internal sealed class TrainCrewApplier(OGSim.Company.CompanyState company, CrewState crew, IAuditTrail audit)
    : ICommandApplier<TrainCrewCommand>
{
    public Applied Apply(TrainCrewCommand command, AuditId submission)
    {
        ArgumentNullException.ThrowIfNull(command);

        // EXPENSED, NOT CAPITALISED (SDD-009 §1's own distinction for a
        // vessel being PP&E) — training buys a permanently better crew, but
        // unlike a vessel it is not a balance-sheet asset in the accounting
        // this engine follows.
        company.Ledger.Post(new Movement(
            new Tick(0), Account.Opex, Account.Cash, crew.TrainingCost,
            MovementCategory.Operating, Asset: null, Cause: submission));

        crew.Train();

        audit.Record(
            AuditCategory.StateTransition, subject: null, cause: submission,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["crew-trained"] = new("true"),
                ["cost"] = new(crew.TrainingCost.Cents.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
            });

        return new Applied(submission, []);
    }
}
