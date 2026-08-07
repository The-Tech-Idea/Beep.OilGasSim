// R13's verification suite (R13 §4, SDD-009).
//
// The fiscal tests are HAND-COMPUTED fixtures, as SDD-009 §3.2 requires: each
// expected figure below is worked out in the comment from the stated rates, not
// read back from the implementation. A fixture that called the code twice would
// verify only that the arithmetic is repeatable.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company.Tests;

public class LedgerTests
{
    private static readonly AuditId Custody = new(100);
    private static readonly AuditId Other = new(200);

    private static CostLedger New(double openingMillions = 100.0) =>
        new(Money.FromMillions(openingMillions), cause => cause == Custody);

    private static Movement Post(
        Account debit, Account credit, double millions,
        AuditId? cause = null, MovementCategory category = MovementCategory.Operating) =>
        new(new Tick(1), debit, credit, Money.FromMillions(millions),
            category, null, cause ?? Other);

    // ------------------------------------------------------------ INV2

    [Fact] // R13-V1 / INV2: the trial balance holds, exactly
    public void R13V1_the_trial_balance_holds_with_no_tolerance()
    {
        CostLedger ledger = New();

        ledger.Post(Post(Account.Opex, Account.Cash, 5.0));
        ledger.Post(Post(Account.Capex_PPE, Account.Cash, 20.0));
        ledger.Post(Post(Account.Cash, Account.Revenue, 30.0, Custody));

        ledger.AssertBalanced();
        ledger.AssertCashReconciles();

        // 100 − 5 − 20 + 30.
        Assert.Equal(Money.FromMillions(105.0), ledger.Cash);
    }

    [Fact] // Cash is recomputed from the movements, so the two must agree
    public void R13V1_cash_reconciles_against_the_movement_list()
    {
        CostLedger ledger = New(50.0);

        for (int i = 0; i < 100; i++)
        {
            ledger.Post(Post(Account.Opex, Account.Cash, 0.01));
            ledger.Post(Post(Account.Cash, Account.Revenue, 0.02, Custody));
        }

        ledger.AssertCashReconciles();
        ledger.AssertBalanced();

        // 50 − 1 + 2, to the cent: the ledger has seen no doubles at all.
        Assert.Equal(Money.FromMillions(51.0), ledger.Cash);
    }

    [Fact] // INV2 has no tolerance because the ledger is INTEGER throughout
    public void R13V1_a_thousand_awkward_amounts_still_balance_to_the_cent()
    {
        CostLedger ledger = New();

        // Amounts that would not survive a double-based ledger: thirds of a
        // cent, rounded once at the boundary and never again.
        for (int i = 1; i <= 1000; i++)
        {
            ledger.Post(new Movement(
                new Tick(i), Account.Opex, Account.Cash,
                Money.RoundHalfEven(i / 3.0), MovementCategory.Operating, null, Other));
        }

        ledger.AssertBalanced();
        ledger.AssertCashReconciles();
    }

    // ------------------------------------------------------------ R13-V2

    [Fact] // R13-V2: revenue originates ONLY at a custody transfer
    public void R13V2_revenue_requires_a_custody_transfer_cause()
    {
        CostLedger ledger = New();

        var fault = Assert.Throws<InvariantFault>(
            () => ledger.Post(Post(Account.Cash, Account.Revenue, 10.0, Other)));

        Assert.Contains("custody transfer", fault.Fault.Detail);

        // One rule, several consequences free: inventory is capital rather than
        // revenue, and off-spec material is worthless until treated.
        ledger.Post(Post(Account.Inventory, Account.Cash, 10.0, Other));
        ledger.AssertBalanced();
    }

    [Fact] // A movement debiting and crediting one account is not a transaction
    public void R13V1_a_self_posting_movement_is_refused()
    {
        var fault = Assert.Throws<InvariantFault>(
            () => New().Post(Post(Account.Cash, Account.Cash, 1.0)));

        Assert.Contains("not a transaction", fault.Fault.Detail);
    }

    [Fact] // Amounts are unsigned; reverse the accounts instead
    public void R13V1_a_negative_amount_is_refused()
    {
        CostLedger ledger = New();

        var fault = Assert.Throws<InvariantFault>(() => ledger.Post(new Movement(
            new Tick(1), Account.Opex, Account.Cash, new Money(-500),
            MovementCategory.Operating, null, Other)));

        Assert.Contains("unsigned", fault.Fault.Detail);
    }
}

public class RoyaltyTaxTests
{
    private static RoyaltyTaxRegime Regime(double royalty = 0.125, double tax = 0.40) =>
        new(new ContentId("concession-uk"), royalty, tax);

    private static FiscalInput Input(
        double grossM, double opexM = 0.0, double depreciationM = 0.0) =>
        new(Money.FromMillions(grossM), Money.FromMillions(opexM), Money.Zero,
            Money.FromMillions(depreciationM), Money.Zero, 0.0);

    [Fact] // R13-V4: hand-computed, from the rates alone
    public void R13V4_royalty_and_tax_match_the_hand_calculation()
    {
        FiscalResult result = Regime().Assess(Input(grossM: 100.0, opexM: 30.0, depreciationM: 20.0));

        // royalty  = 12.5% × 100      = 12.5
        // taxable  = 100 − 12.5 − 30 − 20 = 37.5
        // tax      = 40% × 37.5       = 15.0
        // take     = 100 − 12.5 − 15  = 72.5
        Assert.Equal(Money.FromMillions(12.5), result.Royalty);
        Assert.Equal(Money.FromMillions(15.0), result.Tax);
        Assert.Equal(Money.FromMillions(72.5), result.ContractorTake);
    }

    [Fact] // Design 08 §4: ROYALTY IS DUE EVEN AT A LOSS
    public void R13V4_royalty_is_due_at_a_loss_and_tax_is_not()
    {
        FiscalResult result = Regime().Assess(Input(grossM: 100.0, opexM: 200.0));

        // Royalty is a share of PRODUCTION, not of profit — which is exactly
        // what makes a marginal field marginal.
        Assert.Equal(Money.FromMillions(12.5), result.Royalty);
        Assert.Equal(Money.Zero, result.Tax);
    }

    [Fact] // A loss carries forward indefinitely, with no uplift
    public void R13V4_a_loss_shelters_the_following_period()
    {
        RoyaltyTaxRegime regime = Regime();

        // taxable = 100 − 12.5 − 150 = −62.5, so 62.5 carries forward.
        regime.Assess(Input(grossM: 100.0, opexM: 150.0));
        Assert.Equal(Money.FromMillions(62.5), regime.LossCarry);

        // Next period: taxable = 100 − 12.5 − 20 − 62.5 = 5.0, tax = 2.0.
        FiscalResult sheltered = regime.Assess(Input(grossM: 100.0, opexM: 20.0));
        Assert.Equal(Money.FromMillions(2.0), sheltered.Tax);

        // And the shelter is USED UP — a period that spent it has none left.
        Assert.Equal(Money.Zero, regime.LossCarry);
    }
}

public class ProductionSharingTests
{
    private static ProductionSharingRegime Regime(
        double royalty = 0.0, double cap = 0.50, double tax = 0.30, bool taxesProfit = true) =>
        new(new ContentId("psc-generic"), royalty, cap, tax, taxesProfit,
            [
                new ProfitTranche(From: 0.0, ContractorShare: 0.60),
                new ProfitTranche(From: 1.5, ContractorShare: 0.40),
                new ProfitTranche(From: 2.5, ContractorShare: 0.25),
            ]);

    private static FiscalInput Input(
        double grossM, double opexM = 0.0, double capexM = 0.0,
        double carryM = 0.0, double rFactor = 0.0) =>
        new(Money.FromMillions(grossM), Money.FromMillions(opexM), Money.FromMillions(capexM),
            Money.Zero, Money.FromMillions(carryM), rFactor);

    // ------------------------------------------------------------ R13-V5

    [Fact] // R13-V5: the cost-oil cap behaves, hand-computed
    public void R13V5_the_cost_oil_cap_limits_recovery()
    {
        FiscalResult result = Regime().Assess(Input(grossM: 100.0, opexM: 20.0, capexM: 60.0));

        // royalty  = 0
        // cap      = 50% × 100        = 50   ← the pool is 80, so it BINDS
        // costOil  = min(80, 50)      = 50
        // carry    = 80 − 50          = 30   ← step 5
        // profit   = 100 − 0 − 50     = 50
        // share    = 60% (R = 0)      → 30
        // tax      = 30% × 30         = 9
        // take     = 50 + 30 − 9      = 71
        Assert.Equal(Money.FromMillions(30.0), result.CostPoolCarry);
        Assert.Equal(Money.FromMillions(9.0), result.Tax);
        Assert.Equal(Money.FromMillions(71.0), result.ContractorTake);
    }

    [Fact] // R13-V5: THE CARRYFORWARD — in full, no interest, forever
    public void R13V5_under_recovered_cost_carries_forward_in_full()
    {
        ProductionSharingRegime regime = Regime();

        // Period 1: pool 80, cap 50, so 30 carries.
        FiscalResult first = regime.Assess(Input(grossM: 100.0, opexM: 20.0, capexM: 60.0));
        Assert.Equal(Money.FromMillions(30.0), first.CostPoolCarry);

        // Period 2: the carry joins this period's costs — pool = 30 + 10 = 40,
        // under the cap of 50, so it is ALL recovered and nothing carries.
        FiscalResult second = regime.Assess(
            Input(grossM: 100.0, opexM: 10.0, carryM: first.CostPoolCarry.Cents / 100_000_000.0));

        Assert.Equal(Money.Zero, second.CostPoolCarry);

        // costOil  = 40
        // profit   = 100 − 40         = 60
        // share    = 60%              → 36
        // tax      = 30% × 36         = 10.8
        // take     = 40 + 36 − 10.8   = 65.2
        Assert.Equal(Money.FromMillions(65.2), second.ContractorTake);

        // A regime that wrote the 30 off would have handed it to the state, and
        // no test of a single profitable period would have noticed.
    }

    [Fact] // R13-V5: the carry survives many under-recovered periods
    public void R13V5_the_carry_accumulates_across_repeated_under_recovery()
    {
        ProductionSharingRegime regime = Regime();

        Money carry = Money.Zero;
        for (int period = 0; period < 5; period++)
        {
            FiscalResult result = regime.Assess(new FiscalInput(
                Money.FromMillions(100.0), Money.FromMillions(20.0), Money.FromMillions(60.0),
                Money.Zero, carry, 0.0));

            carry = result.CostPoolCarry;
        }

        // Each period adds 80 and recovers 50, so 30 accumulates five times.
        Assert.Equal(Money.FromMillions(150.0), carry);
    }

    // ------------------------------------------------------------ tranches

    [Fact] // The R-factor tranche is a STEP function
    public void R13V4_the_contractor_share_steps_at_the_declared_r_factors()
    {
        ProductionSharingRegime regime = Regime();

        Assert.Equal(0.60, regime.ContractorShareAt(0.0), 12);
        Assert.Equal(0.60, regime.ContractorShareAt(1.49), 12);

        // Crossing is an EVENT the contractor can see coming and time
        // development around — an interpolation would blur exactly that.
        Assert.Equal(0.40, regime.ContractorShareAt(1.5), 12);
        Assert.Equal(0.40, regime.ContractorShareAt(2.49), 12);
        Assert.Equal(0.25, regime.ContractorShareAt(2.5), 12);
        Assert.Equal(0.25, regime.ContractorShareAt(10.0), 12);
    }

    [Fact] // R13-V4: crossing a tranche materially cuts the take
    public void R13V4_crossing_a_tranche_cuts_the_contractor_take()
    {
        ProductionSharingRegime regime = Regime();

        FiscalResult early = regime.Assess(Input(grossM: 100.0, opexM: 20.0, rFactor: 0.5));
        FiscalResult mature = regime.Assess(Input(grossM: 100.0, opexM: 20.0, rFactor: 3.0));

        Assert.True(mature.ContractorTake < early.ContractorTake);

        // early:  costOil 20, profit 80, share 60% → 48, tax 14.4, take 53.6
        // mature: costOil 20, profit 80, share 25% → 20, tax  6.0, take 34.0
        Assert.Equal(Money.FromMillions(53.6), early.ContractorTake);
        Assert.Equal(Money.FromMillions(34.0), mature.ContractorTake);
    }

    // ------------------------------------------------------------ R13-V4

    [Fact] // R13-V4: four regimes over one field give four different answers
    public void R13V4_the_same_field_under_four_regimes_differs_materially()
    {
        var input = new FiscalInput(
            Money.FromMillions(100.0), Money.FromMillions(20.0), Money.FromMillions(30.0),
            Money.FromMillions(10.0), Money.Zero, 1.0);

        Money concession = new RoyaltyTaxRegime(new ContentId("a"), 0.125, 0.40)
            .Assess(input).ContractorTake;

        Money lightTax = new RoyaltyTaxRegime(new ContentId("b"), 0.05, 0.20)
            .Assess(input).ContractorTake;

        Money psc = Regime().Assess(input).ContractorTake;

        var service = new ServiceContractRegime(new ContentId("d"), Money.FromMillions(0.001));
        service.DeliveredUnits = 10_000.0;
        Money fee = service.Assess(input).ContractorTake;

        var takes = new[] { concession, lightTax, psc, fee };

        // Four materially different answers, from four content entries and no
        // branch outside the regime classes.
        Assert.Equal(4, takes.Distinct().Count());

        // And the service contract is the outlier by DESIGN: the fee does not
        // move with the oil price at all, so the state carries the price risk.
        Assert.Equal(Money.FromMillions(10.0), fee);
    }

    [Fact] // A service fee is untouched by a price crash — the asymmetry is the point
    public void R13V4_a_service_fee_does_not_move_with_revenue()
    {
        var service = new ServiceContractRegime(new ContentId("d"), Money.FromMillions(0.001))
        {
            DeliveredUnits = 10_000.0,
        };

        Money boom = service.Assess(new FiscalInput(
            Money.FromMillions(200.0), Money.Zero, Money.Zero, Money.Zero, Money.Zero, 0.0))
            .ContractorTake;

        Money crash = service.Assess(new FiscalInput(
            Money.FromMillions(40.0), Money.Zero, Money.Zero, Money.Zero, Money.Zero, 0.0))
            .ContractorTake;

        Assert.Equal(boom, crash);
    }

    [Fact] // Content errors are refused where the datasheet is in hand
    public void R13V4_unusable_regime_content_is_a_model_fault()
    {
        Assert.Throws<ModelFault>(() => new ProductionSharingRegime(
            new ContentId("bad"), 0.0, 0.5, 0.3, true, []));

        var unordered = Assert.Throws<ModelFault>(() => new ProductionSharingRegime(
            new ContentId("bad"), 0.0, 0.5, 0.3, true,
            [new ProfitTranche(2.0, 0.5), new ProfitTranche(1.0, 0.4)]));

        Assert.Contains("must ascend", unordered.Fault.Detail);

        Assert.Throws<ModelFault>(() => new RoyaltyTaxRegime(new ContentId("bad"), 1.5, 0.4));
    }
}
