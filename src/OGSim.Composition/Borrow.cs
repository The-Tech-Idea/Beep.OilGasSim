// R20d.15 — drawing and repaying (SDD-009 §5).
//
// THE LEVER THAT MAKES A DEVELOPMENT FUNDABLE. A field pays for itself and not
// before it is built, so a company that could only spend what it had would
// develop at the speed its smallest discovery allowed. Borrowing against oil in
// the ground is how the industry actually solves that, and it is a decision
// rather than a free move: the interest is charged whether the field produces or
// not, and the base falls when the market does.
//
// NOT ACTIVITIES. Everything on the scheduled-activity engine is a project that
// takes months, commits a rig and can fail. A drawdown is a phone call. Putting
// it on the engine would make a company wait a quarter for money it already had
// a facility for — which is not a harder decision, only a slower one.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>Draw down against the borrowing base.</summary>
public sealed record BorrowCommand(Money Amount) : Command(Subject: null);

/// <summary>Pay debt back.</summary>
public sealed record RepayCommand(Money Amount) : Command(Subject: null);

/// <summary>Pure: it decides whether the money can be drawn and nothing else.</summary>
internal sealed class BorrowValidator(OGSim.Company.CompanyState company, Bank bank)
    : ICommandValidator<BorrowCommand>
{
    public IReadOnlyList<RejectionReason> Validate(BorrowCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Amount <= Money.Zero)
            return [new RejectionReason("$loc:reject.not-a-drawdown",
                                        "a drawdown is a positive amount")];

        Money drawn = -company.Ledger.BalanceOf(Account.Debt);
        Money headroom = bank.Terms.BorrowingBase - drawn;

        // THE BASE IS THE LIMIT, and it is worth refusing at rather than
        // lending past: a facility a company could overdraw would make the
        // covenant meaningless, because the breach would be the bank's doing.
        if (command.Amount > headroom)
            return [new RejectionReason(
                "$loc:reject.beyond-borrowing-base",
                $"the borrowing base is {bank.Terms.BorrowingBase.Cents} cents and " +
                $"{drawn.Cents} is already drawn; the bank will not advance " +
                $"{command.Amount.Cents} against reserves it has not booked")];

        return [];
    }
}

internal sealed class BorrowApplier(OGSim.Company.CompanyState company, IAuditTrail audit)
    : ICommandApplier<BorrowCommand>
{
    public Applied Apply(BorrowCommand command, AuditId submission)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Cash in, debt up. Not revenue: money borrowed is money owed, and a
        // ledger that let a drawdown look like income would let a company report
        // a record year by taking a loan.
        company.Ledger.Post(new Movement(
            new Tick(0), Account.Cash, Account.Debt, command.Amount,
            MovementCategory.Financing, Asset: null, Cause: submission));

        audit.Record(
            AuditCategory.Financial, subject: null, cause: submission,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["drawdown"] = new(command.Amount.Cents.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
            });

        return new Applied(submission, []);
    }
}

internal sealed class RepayValidator(OGSim.Company.CompanyState company)
    : ICommandValidator<RepayCommand>
{
    public IReadOnlyList<RejectionReason> Validate(RepayCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Amount <= Money.Zero)
            return [new RejectionReason("$loc:reject.not-a-repayment",
                                        "a repayment is a positive amount")];

        if (command.Amount > company.Ledger.Cash)
            return [new RejectionReason(
                "$loc:reject.insufficient-cash",
                $"the company holds {company.Ledger.Cash.Cents} cents and cannot repay " +
                $"{command.Amount.Cents}")];

        // Paying back more than is owed is refused rather than treated as a
        // deposit: this is a facility, not an account, and an over-repayment
        // would leave the ledger carrying negative debt.
        Money drawn = -company.Ledger.BalanceOf(Account.Debt);

        if (command.Amount > drawn)
            return [new RejectionReason(
                "$loc:reject.over-repayment",
                $"{drawn.Cents} cents are drawn and the company offered {command.Amount.Cents}")];

        return [];
    }
}

internal sealed class RepayApplier(OGSim.Company.CompanyState company, IAuditTrail audit)
    : ICommandApplier<RepayCommand>
{
    public Applied Apply(RepayCommand command, AuditId submission)
    {
        ArgumentNullException.ThrowIfNull(command);

        company.Ledger.Post(new Movement(
            new Tick(0), Account.Debt, Account.Cash, command.Amount,
            MovementCategory.Financing, Asset: null, Cause: submission));

        audit.Record(
            AuditCategory.Financial, subject: null, cause: submission,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["repayment"] = new(command.Amount.Cents.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
            });

        return new Applied(submission, []);
    }
}
