// R20d.15 — the facility, as the engine holds it (SDD-009 §5).
//
// THE MODEL COMPUTES AND THE LEDGER BOOKS — SDD-009 §5's own words. This is the
// piece between them: it asks the lender what the terms are, keeps the covenant
// clock, and charges the interest. `Assess` returns a status and never posts a
// movement, so the cure-window pin stays a statement about state rather than
// about money.
//
// REDETERMINED EVERY TICK rather than quarterly, and that is a simplification
// worth naming: §5 says quarterly and immediately on a reserves recomputation,
// and reserves here are recomputed every tick because they are derived rather
// than stored. Quarterly would mean holding a stale base for two months out of
// three, which is a worse lie than the one it avoids.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Persistence;

namespace OGSim.Composition;

/// <summary>The company's borrowing facility.</summary>
public sealed class Bank : IStateOwner
{
    private readonly IReserveBasedLending _lender;
    private readonly ReservesBook _reserves;
    private readonly OGSim.Company.CompanyState _company;
    private readonly IAuditTrail _audit;
    private readonly Func<SurfaceVolume> _produced;

    public Bank(
        IReserveBasedLending lender,
        ReservesBook reserves,
        OGSim.Company.CompanyState company,
        IAuditTrail audit,
        Func<SurfaceVolume> produced)
    {
        ArgumentNullException.ThrowIfNull(lender);
        ArgumentNullException.ThrowIfNull(reserves);
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(produced);

        _lender = lender;
        _reserves = reserves;
        _company = company;
        _audit = audit;
        _produced = produced;
    }

    /// <summary>What the bank will lend today, and at what price.</summary>
    public BorrowingTerms Terms { get; private set; } =
        new(Money.Zero, Rate: 0.0, EsgSpread: 0.0);

    /// <summary>Where the company stands against its covenant.</summary>
    public CovenantStatus Covenant { get; private set; } = new(CovenantState.Clear, 0);

    /// <summary>How much is drawn. Debt is a credit balance, so it is the
    /// negation (SDD-009 §1's sign convention).</summary>
    public Money Drawn => -_company.Ledger.BalanceOf(Account.Debt);

    /// <summary>
    /// Stage 8: re-price the facility, charge the interest, and see where the
    /// covenant stands.
    ///
    /// <para>In that order. Interest is charged on what was drawn under the old
    /// terms, and the covenant is tested against a base that already reflects
    /// today's reserves — which is the sequence that makes a price crash arrive
    /// as a warning rather than as a surprise bill.</para>
    /// </summary>
    public void Settle(Tick tick, double esgStanding)
    {
        Money owed = Drawn;

        Terms = _lender.Redetermine(
            _reserves.Remaining(_produced()).Proved, owed, esgStanding);

        if (owed > Money.Zero) ChargeInterest(tick, owed);

        Covenant = _lender.Assess(Terms, Drawn, Covenant);
    }

    /// <summary>
    /// A month's interest, paid in cash. Charged whether or not the field
    /// produced, which is the whole difference between debt and equity and the
    /// reason a leveraged company cannot simply wait out a bad market.
    /// </summary>
    private void ChargeInterest(Tick tick, Money owed)
    {
        Money interest = Money.RoundHalfEven(owed.Cents * Terms.Rate / MonthsPerYear);

        if (interest == Money.Zero) return;

        _company.Ledger.Post(new Movement(
            tick, Account.Opex, Account.Cash, interest,
            MovementCategory.Financing, Asset: null, Cause: _audit.Record(
                AuditCategory.Financial, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["interest"] = new(interest.Cents.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                    ["rate"] = new(Terms.Rate.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                })));
    }

    private const double MonthsPerYear = 12.0;

    // ------------------------------------------------------- SDD-013 §4

    /// <summary>The company's standing with its lender (SDD-013 §4's R20d.12.33
    /// amendment). <see cref="Terms"/> is NOT here: `Settle` recomputes it from
    /// restored state before reading it, so saving it would shadow its own
    /// inputs.</summary>
    public StateKey Key { get; } = new("company.facility");

    public int SchemaVersion => 1;

    /// <summary>Nothing: two scalars that depend on no other owner.</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("covenant-state", (long)Covenant.State);
        writer.WriteInt64("covenant-ticks-remaining", Covenant.TicksRemaining);
    }

    /// <summary>
    /// A breach is a CLOCK, so both halves restore together or the clock resets:
    /// `Assess` reads its own previous value, and a company handed back
    /// <c>Curing</c> with a fresh window has had its cure period silently
    /// extended (finding 210).
    /// </summary>
    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        Covenant = new CovenantStatus(
            (CovenantState)reader.ReadInt64("covenant-state"),
            (int)reader.ReadInt64("covenant-ticks-remaining"));
    }
}
