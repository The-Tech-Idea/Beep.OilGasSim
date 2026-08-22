// R13.10 — selling working interest, the second of three player-side
// restructuring levers (SDD-011 §4's finding-275 amendment).
//
// A RESTRUCTURING LEVER, NOT A ROUTINE FINANCING TOOL. Refused unless the
// company is already in trouble — the same reason a drawdown is unlimited
// but this is not: a healthy company selling off its own field for cash
// would not be a decision this command exists to represent.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>Sell a fraction of the field's future economics to a partner
/// for cash now.</summary>
public sealed record SellWorkingInterestCommand(double Fraction) : Command(Subject: null);

internal sealed class SellWorkingInterestValidator(
    OGSim.Company.CompanyState company,
    Bank bank,
    WorkingInterest stake,
    WorkingInterestTerms terms)
    : ICommandValidator<SellWorkingInterestCommand>
{
    public IReadOnlyList<RejectionReason> Validate(SellWorkingInterestCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Fraction <= 0.0)
            return [new RejectionReason("$loc:reject.not-a-sale",
                                        "a working-interest sale is a positive fraction")];

        // DISTRESS, READ THE SAME WAY `ObjectiveStage.Insolvent` DERIVES IT —
        // cash below zero — rather than depending on that stage's own
        // persisted latch, which exists for the scenario's verdict and not
        // for gating a command (and would make this module require a stage
        // that itself requires this module's own CompanyState).
        bool distressed = bank.Covenant.State is CovenantState.Curing or CovenantState.Amortising
            || company.Ledger.Cash.Cents < 0;

        if (!distressed)
            return [new RejectionReason("$loc:reject.not-distressed",
                "a working-interest sale is a restructuring lever; the covenant reads Clear " +
                "and cash is positive")];

        double after = stake.PartnerShare + command.Fraction;

        if (after > terms.MaxSellableFraction)
            return [new RejectionReason("$loc:reject.beyond-sellable-cap",
                $"selling {command.Fraction} would take the partner's share to {after}, past " +
                $"the {terms.MaxSellableFraction} ceiling — the company must keep operatorship")];

        return [];
    }
}

internal sealed class SellWorkingInterestApplier(
    OGSim.Company.CompanyState company,
    Bank bank,
    WorkingInterest stake,
    WorkingInterestTerms terms,
    IAuditTrail audit)
    : ICommandApplier<SellWorkingInterestCommand>
{
    public Applied Apply(SellWorkingInterestCommand command, AuditId submission)
    {
        ArgumentNullException.ThrowIfNull(command);

        // PRICED OFF THE SAME DCF WALK THE BANK ALREADY RAN THIS TICK
        // (SDD-011 §4) — `bank.Terms.ReserveValue`, not a second one.
        Money price = WorkingInterest.Price(terms, bank.Terms.ReserveValue, command.Fraction);

        // A CAPITAL TRANSACTION, NOT REVENUE — the company is not selling a
        // barrel, it is selling a share of itself, the same distinction the
        // opening balance draws between Cash and Equity (SDD-009 §1's own
        // opening entry).
        if (price > Money.Zero)
            company.Ledger.Post(new Movement(
                new Tick(0), Account.Cash, Account.Equity, price,
                MovementCategory.Financing, Asset: null, Cause: submission));

        stake.Sell(command.Fraction);

        audit.Record(
            AuditCategory.Financial, subject: null, cause: submission,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["working-interest-sold"] = new(command.Fraction.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
                ["price-cents"] = new(price.Cents.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
                ["partner-share-after"] = new(stake.PartnerShare.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
            });

        return new Applied(submission, []);
    }
}
