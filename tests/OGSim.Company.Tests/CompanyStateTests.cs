// R20c.6 — the ledger survives a save (SDD-001 §10, SDD-009 §1).
//
// The one thing these tests are about: money is exact. INV2 reconciles cash to
// the cent, so a ledger that came back approximately right is a ledger that came
// back wrong.

using OGSim.Company;
using OGSim.Kernel;
using OGSim.Persistence;

namespace OGSim.Company.Tests;

public sealed class CompanyStateTests
{
    // Custody transfers are what revenue must be caused by (SDD-009 §1). The
    // test declares which ids are sales, so the rule is exercised rather than
    // bypassed.
    private static readonly HashSet<ulong> Sales = [7];

    private static CompanyState Fresh() =>
        new(Money.FromMillions(10.0), cause => Sales.Contains(cause.Value));

    private static Movement Spend(int tick, long cents, Account to) =>
        new(new Tick(tick), to, Account.Cash, new Money(cents),
            MovementCategory.Operating, null, new AuditId(1));

    [Fact]
    public void A_ledger_of_movements_restores_to_the_same_position()
    {
        CompanyState captured = Fresh();

        captured.Ledger.Post(Spend(1, 250_000, Account.Opex));
        captured.Ledger.Post(Spend(2, 1_000_000, Account.Capex_PPE));
        captured.Ledger.Post(new Movement(
            new Tick(3), Account.Cash, Account.Revenue, new Money(4_321_099),
            MovementCategory.Production,
            new EntityRef(EntityKind.Well, 12), new AuditId(7)));

        JsonValue written = StateBlock.Capture(captured).Written();

        CompanyState restored = Fresh();
        StateBlock.Restore(restored, written);

        Assert.Equal(captured.Ledger.Cash, restored.Ledger.Cash);
        Assert.Equal(captured.Ledger.BalanceOf(Account.Opex), restored.Ledger.BalanceOf(Account.Opex));
        Assert.Equal(captured.Ledger.BalanceOf(Account.Revenue), restored.Ledger.BalanceOf(Account.Revenue));
        Assert.Equal(captured.Ledger.Movements.Count, restored.Ledger.Movements.Count);

        // The invariants hold on the restored ledger for the same reason they
        // held on the original: it was rebuilt by replaying through Post.
        restored.Ledger.AssertBalanced();
        restored.Ledger.AssertCashReconciles();
    }

    /// <summary>
    /// Every field of every movement, not just the balances — an asset
    /// reference or a cause that did not survive would be invisible in the
    /// totals and would break the audit trail that explains them.
    /// </summary>
    [Fact]
    public void Every_movement_field_survives_the_round_trip()
    {
        CompanyState captured = Fresh();

        var original = new Movement(
            new Tick(11), Account.Cash, Account.Revenue, new Money(999_999),
            MovementCategory.Production, new EntityRef(EntityKind.Tank, 3), new AuditId(7));

        captured.Ledger.Post(original);

        CompanyState restored = Fresh();
        StateBlock.Restore(restored, StateBlock.Capture(captured).Written());

        Movement roundTripped = restored.Ledger.Movements[^1];

        Assert.Equal(original, roundTripped);
    }

    /// <summary>
    /// A movement with no asset must come back with no asset. Written as a flag
    /// rather than a sentinel id, because no id value means "none".
    /// </summary>
    [Fact]
    public void A_movement_without_an_asset_restores_without_one()
    {
        CompanyState captured = Fresh();
        captured.Ledger.Post(Spend(1, 100, Account.Opex));

        CompanyState restored = Fresh();
        StateBlock.Restore(restored, StateBlock.Capture(captured).Written());

        Assert.Null(restored.Ledger.Movements[^1].Asset);
    }

    /// <summary>
    /// Cents, never doubles. An amount that no double can represent exactly
    /// must still come back to the cent — this is the value that a
    /// double-valued save would round.
    /// </summary>
    [Fact]
    public void An_amount_no_double_holds_exactly_survives_to_the_cent()
    {
        const long awkward = 12_345_678_901_234;

        CompanyState captured = Fresh();
        captured.Ledger.Post(new Movement(
            new Tick(1), Account.Capex_PPE, Account.Debt, new Money(awkward),
            MovementCategory.Financing, null, new AuditId(1)));

        CompanyState restored = Fresh();
        StateBlock.Restore(restored, StateBlock.Capture(captured).Written());

        Assert.Equal(awkward, restored.Ledger.Movements[^1].Amount.Cents);
    }

    /// <summary>
    /// A REPLAY IS NOT AN ORIGINATION (SDD-009 §1's R20d.12 amendment), and this
    /// test used to say the opposite.
    ///
    /// <para>It asserted that restoring a saved revenue credit re-asks the audit
    /// trail whether its cause was a custody transfer, and it passed by handing
    /// one predicate at capture and a stricter one at restore. **The real engine
    /// cannot produce that pair.** It asks
    /// <c>cause => IsCustodyTransfer(audit, cause)</c>, and a freshly composed
    /// engine's trail is EMPTY — so every save with revenue in it was
    /// unrestorable, and the test that should have caught it was instead pinning
    /// the behaviour that caused it (finding 168's shape, finding 193).</para>
    ///
    /// <para>It could not be repaired by saving the trail either: detail is
    /// retained on a window (design 09 §4.4), so a movement from tick 5 in a game
    /// saved at tick 60 cites a cause the engine has already summarised away.
    /// The check is unrepeatable by construction.</para>
    ///
    /// <para>What protects a restored ledger instead is what is repeatable: the
    /// container's per-module DIGEST refuses an edited block, and INV2 refuses a
    /// ledger whose numbers do not balance. Origination is checked where it
    /// happens — at posting, on live entries.</para>
    /// </summary>
    [Fact]
    public void A_saved_revenue_credit_is_replayed_without_re_asking_its_origin()
    {
        CompanyState captured = Fresh();
        captured.Ledger.Post(new Movement(
            new Tick(1), Account.Cash, Account.Revenue, new Money(500),
            MovementCategory.Production, null, new AuditId(7)));

        JsonValue written = StateBlock.Capture(captured).Written();

        // The engine every load actually gets: one whose trail has never heard
        // of cause 7, because it has never heard of anything.
        var reloaded = new CompanyState(Money.FromMillions(10.0), _ => false);

        StateBlock.Restore(reloaded, written);

        Assert.Equal(500L, reloaded.Ledger.BalanceOf(Account.Revenue).Cents * -1L);
        Assert.Equal(new Money(500), reloaded.Ledger.Movements[^1].Amount);
    }

    /// <summary>
    /// AND POSTING STILL REFUSES IT. Removing the check from the replay path did
    /// not remove it — revenue that cites something other than a sale is still
    /// unspellable on a live ledger, which is where the rule was always about.
    /// </summary>
    [Fact]
    public void Revenue_still_cannot_be_posted_without_a_custody_transfer()
    {
        var ledger = new CostLedger(Money.FromMillions(10.0), _ => false);

        Assert.Throws<InvariantFault>(() => ledger.Post(new Movement(
            new Tick(1), Account.Cash, Account.Revenue, new Money(500),
            MovementCategory.Production, null, new AuditId(7))));
    }

    /// <summary>Movement order is preserved past the point where ordinal key
    /// sorting and numeric order would disagree — index 10 versus index 2.</summary>
    [Fact]
    public void More_than_ten_movements_keep_their_order()
    {
        CompanyState captured = Fresh();
        for (int i = 1; i <= 12; i++) captured.Ledger.Post(Spend(i, i * 100, Account.Opex));

        CompanyState restored = Fresh();
        StateBlock.Restore(restored, StateBlock.Capture(captured).Written());

        IReadOnlyList<Movement> movements = restored.Ledger.Movements;
        Assert.Equal(12, movements.Count);

        for (int i = 0; i < movements.Count; i++)
            Assert.Equal((i + 1) * 100, movements[i].Amount.Cents);
    }
}
