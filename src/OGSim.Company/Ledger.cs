// R13.1 — the cost ledger (SDD-009 §1, INV2).
//
// DOUBLE-ENTRY, ALWAYS, AND EXACT. INV2 is the trial balance — Σ debits =
// Σ credits per tick, and Cash equals opening plus movements — in integers, with
// NO TOLERANCE TERM. A one-cent error is a bug by definition, which is only a
// meaningful statement because the double→Money boundary is pinned:
//
//   Physical quantities are doubles. The ledger is integer cents. Every crossing
//   rounds half-even, EXACTLY ONCE, at the Movement that enters the ledger —
//   never upstream, never twice. Inside the ledger the arithmetic is pure
//   integer, and the only doubles it ever sees are already rounded.
//
// Without that pin, "exact to the cent" is a slogan. With it, the invariant has
// teeth: Money's own arithmetic is checked and overflows rather than wrapping,
// so an impossible balance throws instead of quietly drifting.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>SDD-009 §1's accounts.</summary>
public enum Account
{
    Cash, Debt, Equity, Revenue, Opex, Capex_PPE, Depreciation,
    Royalty, Tax, AbandonmentProvision, Inventory, PartnerPayable,
    InsurancePremium, Penalty,
}

public enum MovementCategory
{
    Production, Development, Exploration, Operating, Financing,
    Fiscal, Abandonment, Insurance, Contractual,
}

/// <summary>SDD-009 §1. One double-entry movement.</summary>
public sealed record Movement(
    Tick Tick,
    Account Debit,
    Account Credit,
    Money Amount,
    MovementCategory Category,
    EntityRef? Asset,
    AuditId Cause);

/// <summary>
/// SDD-009 §1's ledger. Every posting is double-entry and INV2 is checked on
/// demand and at every close.
/// </summary>
public sealed class CostLedger
{
    private readonly List<Movement> _movements = [];
    private readonly Dictionary<Account, long> _balances = [];

    /// <summary>
    /// REVENUE MAY BE CREDITED ONLY WITH A CUSTODY-TRANSFER CAUSE (R13-V2).
    ///
    /// <para>One rule, and several consequences follow free: inventory is
    /// capital rather than revenue, off-spec material is worthless until
    /// treated, and "where do I sell this?" becomes a real question. Enforced
    /// here rather than trusted, because the whole economic model rests on
    /// revenue having exactly one origin.</para>
    /// </summary>
    private readonly Func<AuditId, bool> _isCustodyTransfer;

    public CostLedger(Money openingCash, Func<AuditId, bool> isCustodyTransfer)
    {
        ArgumentNullException.ThrowIfNull(isCustodyTransfer);

        if (openingCash.Cents < 0)
            throw new InvariantFault("SDD-009 §1", null,
                "a company cannot open with negative cash; that is debt, and debt is an account");

        _isCustodyTransfer = isCustodyTransfer;
        OpeningCash = openingCash;

        // The opening entry is a transaction like any other: cash DEBITED,
        // equity CREDITED. Signing both the same way is what INV2 exists to
        // catch, and it caught it here on the first run — the trial balance was
        // out by exactly twice the opening cash.
        //
        // Credit-balance accounts (equity, debt, revenue) therefore carry
        // NEGATIVE balances in this signed convention. That is ordinary
        // double-entry, not an error: a presentation layer flips the sign, and
        // the invariant needs one convention to sum to zero under.
        _balances[Account.Cash] = openingCash.Cents;
        _balances[Account.Equity] = -openingCash.Cents;
    }

    public Money OpeningCash { get; }

    public IReadOnlyList<Movement> Movements => _movements;

    public Money BalanceOf(Account account) =>
        new(_balances.GetValueOrDefault(account));

    public Money Cash => BalanceOf(Account.Cash);

    /// <summary>
    /// Posts one movement. Debit and credit are the SAME amount by construction
    /// — the type makes an unbalanced entry unspellable, which is a stronger
    /// guarantee than checking for one afterwards.
    /// </summary>
    public void Post(Movement movement) => Enter(movement, originating: true);

    /// <summary>
    /// Rebuilds one movement from a save (SDD-009 §1's R20d.12 amendment).
    ///
    /// <para>SAME ARITHMETIC, NO ORIGINATION CHECK. The custody rule below is
    /// about the moment revenue is CREATED, and a replay creates nothing — it
    /// reconstructs an entry that was already checked when it happened. Asking
    /// it again is not a stricter test, it is an unanswerable one: a freshly
    /// composed engine has an empty audit trail, and even a running one retains
    /// detail on a WINDOW (design 09 §4.4), so a movement from tick 5 in a game
    /// saved at tick 60 cites a cause that has already been summarised away.</para>
    ///
    /// <para>What still runs is everything that is a property of the NUMBERS —
    /// the unsigned amount, the two distinct accounts, and INV2 at the end of the
    /// replay. Those are repeatable, and they are what would actually catch a
    /// corrupted ledger.</para>
    /// </summary>
    public void Replay(Movement movement) => Enter(movement, originating: false);

    private void Enter(Movement movement, bool originating)
    {
        ArgumentNullException.ThrowIfNull(movement);

        if (movement.Amount.Cents < 0)
            throw new InvariantFault("SDD-009 §1", null,
                "a movement's amount is unsigned; reverse the accounts instead of negating it");

        if (movement.Debit == movement.Credit)
            throw new InvariantFault("SDD-009 §1", null,
                $"movement debits and credits the same account ({movement.Debit}); " +
                "that is not a transaction");

        if (originating
            && movement.Credit == Account.Revenue && !_isCustodyTransfer(movement.Cause))
            throw new InvariantFault("SDD-009 §1", null,
                $"revenue was credited with cause {movement.Cause.Value}, which is not a " +
                "custody transfer. Revenue originates at exactly one kind of event " +
                "(R13-V2), and inventory is capital until it is metered and sold");

        _movements.Add(movement);

        // Debit increases assets and expenses; credit increases liabilities,
        // equity and revenue. Cash is an asset, so a payment DEBITS the expense
        // and CREDITS cash — and the sign convention below is what makes the
        // trial balance a real check rather than a tautology.
        Add(movement.Debit, movement.Amount.Cents);
        Add(movement.Credit, -movement.Amount.Cents);
    }

    /// <summary>
    /// INV2. Σ debits = Σ credits, exactly, with no tolerance.
    ///
    /// <para>Every movement contributes <c>+amount</c> to its debit and
    /// <c>−amount</c> to its credit, so the balances must sum to the opening
    /// entry and nothing else. A non-zero residue means a posting escaped the
    /// double-entry path, which is a bug rather than a rounding artefact —
    /// the ledger has seen no doubles at all.</para>
    /// </summary>
    public void AssertBalanced()
    {
        long residue = 0;

        // Walked in a fixed order (D-5): the sum is exact in integers, so order
        // cannot change the answer, but a Dictionary enumeration would still be
        // a source of run-to-run difference in the FAULT MESSAGE.
        foreach (Account account in AllAccounts)
            residue += _balances.GetValueOrDefault(account);

        if (residue != 0)
            throw new InvariantFault("INV2", null,
                $"trial balance is out by {residue} cents. INV2 has no tolerance term: " +
                "the ledger is integer throughout and every crossing from double rounds " +
                "once, at the movement, so any residue is a posting that escaped " +
                "double-entry");
    }

    /// <summary>
    /// Cash equals opening plus every movement that touched it — recomputed from
    /// the movement list rather than read from the running balance, so the two
    /// have to agree.
    /// </summary>
    public void AssertCashReconciles()
    {
        long cash = OpeningCash.Cents;

        for (int i = 0; i < _movements.Count; i++)
        {
            Movement movement = _movements[i];
            if (movement.Debit == Account.Cash) cash += movement.Amount.Cents;
            if (movement.Credit == Account.Cash) cash -= movement.Amount.Cents;
        }

        if (cash != _balances.GetValueOrDefault(Account.Cash))
            throw new InvariantFault("INV2", null,
                $"cash from movements is {cash} cents but the balance says " +
                $"{_balances.GetValueOrDefault(Account.Cash)}");
    }

    private void Add(Account account, long cents) =>
        _balances[account] = checked(_balances.GetValueOrDefault(account) + cents);

    /// <summary>Declared order, so the residue is summed identically every run.</summary>
    private static readonly Account[] AllAccounts =
    [
        Account.Cash, Account.Debt, Account.Equity, Account.Revenue, Account.Opex,
        Account.Capex_PPE, Account.Depreciation, Account.Royalty, Account.Tax,
        Account.AbandonmentProvision, Account.Inventory, Account.PartnerPayable,
        Account.InsurancePremium, Account.Penalty,
    ];
}
