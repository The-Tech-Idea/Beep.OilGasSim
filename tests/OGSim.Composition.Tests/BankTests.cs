// R13.10 — the cash sweep (SDD-009 §5's finding-274 amendment). `Bank` has
// no other unit test in the tree: everything else exercises it through a
// full 40-year fixture. These pin the sweep's own arithmetic directly, with
// a fake lender that says exactly what the covenant is doing so a breach
// does not have to be produced from real reserves and a real price crash.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Information;
using OGSim.Kernel;
using Xunit;

namespace OGSim.Composition.Tests;

public sealed class BankTests
{
    /// <summary>Fully test-controlled: <see cref="Bank"/>'s own job is to call
    /// this and act on what comes back, not to decide it.</summary>
    private sealed class FakeLender : IReserveBasedLending
    {
        public ContentId Id => new("fake-lender");

        public Func<SurfaceVolume, Money, double, BorrowingTerms> OnRedetermine { get; set; } =
            (_, _, _) => new BorrowingTerms(Money.Zero, Money.Zero, Rate: 0.0, EsgSpread: 0.0);

        public Func<BorrowingTerms, Money, CovenantStatus, CovenantStatus> OnAssess { get; set; } =
            (_, _, previous) => previous;

        public List<Money> AssessedAgainst { get; } = [];

        public BorrowingTerms Redetermine(SurfaceVolume provedReserves, Money debt, double esgStanding) =>
            OnRedetermine(provedReserves, debt, esgStanding);

        public CovenantStatus Assess(BorrowingTerms terms, Money debt, CovenantStatus previous)
        {
            AssessedAgainst.Add(debt);
            return OnAssess(terms, debt, previous);
        }
    }

    private static (Bank Bank, OGSim.Company.CompanyState Company, FakeLender Lender) Fixture(
        Money openingCash, Money drawn)
    {
        var clock = new SimulationClock(new GameDate(1970, 1));
        var trail = new AuditTrail(clock, new AuditRetention(2000));
        var beliefs = new BeliefStore(trail, _ => 0.0, () => new GameDate(1970, 1));
        var market = new MarketState(
            Defaults.Economics.OilPricePerTonne, Defaults.CostElasticity, Defaults.CostDrift);
        var reserves = new ReservesBook(beliefs, market, Defaults.TypeCurve);

        var company = new OGSim.Company.CompanyState(openingCash, _ => false);

        // Debited against Capex_PPE, not Cash: by the time a covenant is
        // breached the drawn money is long since spent on the field it
        // funded, which is the whole scenario a sweep exists for. Debiting
        // Cash here would hand the fixture back money the company never
        // still has.
        if (drawn > Money.Zero)
            company.Ledger.Post(new Movement(
                new Tick(0), Account.Capex_PPE, Account.Debt, drawn,
                MovementCategory.Development, Asset: null, Cause: trail.Record(
                    AuditCategory.Financial, subject: null, cause: null,
                    new Dictionary<string, AuditValue>(StringComparer.Ordinal))));

        var lender = new FakeLender();
        var bank = new Bank(lender, reserves, company, trail, () => new SurfaceVolume(0.0));

        return (bank, company, lender);
    }

    [Fact]
    public void The_sweep_is_exactly_five_percent_of_drawn_while_amortising()
    {
        Money drawn = Money.FromMillions(2.0);
        (Bank bank, OGSim.Company.CompanyState company, FakeLender lender) =
            Fixture(openingCash: Money.FromMillions(10.0), drawn);

        // Always Amortising, regardless of the resulting balance — isolates
        // the sweep's own arithmetic from the "clears the covenant" behaviour.
        lender.OnAssess = (_, _, previous) => new CovenantStatus(CovenantState.Amortising, previous.TicksRemaining);

        Money cashBefore = company.Ledger.Cash;

        bank.Settle(new Tick(1), esgStanding: 0.0);

        Money expectedSwept = Money.RoundHalfEven(drawn.Cents * 0.05);

        Assert.Equal(drawn.Cents - expectedSwept.Cents, bank.Drawn.Cents);
        Assert.Equal(cashBefore.Cents - expectedSwept.Cents, company.Ledger.Cash.Cents);
    }

    [Fact]
    public void A_swept_company_that_clears_the_covenant_stops_being_swept()
    {
        Money drawn = Money.FromMillions(2.0);
        Money borrowingBase = Money.FromMillions(1.95);   // between pre- and post-sweep drawn

        (Bank bank, OGSim.Company.CompanyState company, FakeLender lender) =
            Fixture(openingCash: Money.FromMillions(10.0), drawn);

        lender.OnRedetermine = (_, _, _) =>
            new BorrowingTerms(borrowingBase, ReserveValue: Money.Zero, Rate: 0.0, EsgSpread: 0.0);
        lender.OnAssess = (terms, debt, previous) => debt <= terms.BorrowingBase
            ? new CovenantStatus(CovenantState.Clear, 0)
            : new CovenantStatus(CovenantState.Amortising, previous.TicksRemaining);

        bank.Settle(new Tick(1), esgStanding: 0.0);

        Money afterFirstSweep = Money.FromMillions(1.9);   // 2.0M less 5% = 1.9M, under the base
        Assert.Equal(CovenantState.Clear, bank.Covenant.State);
        Assert.Equal(afterFirstSweep.Cents, bank.Drawn.Cents);

        // A second tick, already clear: no further sweep.
        bank.Settle(new Tick(2), esgStanding: 0.0);

        Assert.Equal(CovenantState.Clear, bank.Covenant.State);
        Assert.Equal(afterFirstSweep.Cents, bank.Drawn.Cents);
    }

    [Fact]
    public void A_company_with_insufficient_cash_sweeps_only_what_it_has()
    {
        Money drawn = Money.FromMillions(2.0);
        Money openingCash = new(3_000_000L);   // $30,000 — less than 5% of drawn ($100,000)

        (Bank bank, OGSim.Company.CompanyState company, FakeLender lender) = Fixture(openingCash, drawn);

        lender.OnAssess = (_, _, previous) => new CovenantStatus(CovenantState.Amortising, previous.TicksRemaining);

        bank.Settle(new Tick(1), esgStanding: 0.0);

        // Every cent went to the sweep, and none was manufactured: cash is
        // exhausted, never negative, and Drawn fell by exactly that much.
        Assert.Equal(Money.Zero, company.Ledger.Cash);
        Assert.Equal(drawn.Cents - openingCash.Cents, bank.Drawn.Cents);
    }

    [Fact]
    public void The_covenant_reassessment_after_a_sweep_reads_the_post_sweep_balance()
    {
        Money drawn = Money.FromMillions(2.0);
        (Bank bank, OGSim.Company.CompanyState company, FakeLender lender) =
            Fixture(openingCash: Money.FromMillions(10.0), drawn);

        lender.OnAssess = (_, _, previous) => new CovenantStatus(CovenantState.Amortising, previous.TicksRemaining);

        bank.Settle(new Tick(1), esgStanding: 0.0);

        // `Settle` asks the covenant twice while Amortising: once before the
        // sweep and once after. The second ask must see the swept-down debt,
        // not the balance the first ask saw.
        Assert.Equal(2, lender.AssessedAgainst.Count);
        Assert.Equal(drawn.Cents, lender.AssessedAgainst[0].Cents);

        Money expectedSwept = Money.RoundHalfEven(drawn.Cents * 0.05);
        Assert.Equal(drawn.Cents - expectedSwept.Cents, lender.AssessedAgainst[1].Cents);
        Assert.NotEqual(lender.AssessedAgainst[0].Cents, lender.AssessedAgainst[1].Cents);
    }
}
