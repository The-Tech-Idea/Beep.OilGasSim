// R20d.15 — reserve-based lending (SDD-009 §5).
//
// Two properties make this a decision rather than free money: what the bank will
// lend follows the VALUE of the oil rather than its volume, and a breach opens a
// window instead of calling the loan. The second is the one the design pins
// explicitly, and it is what turns a price crash into something to survive.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company.Tests;

public sealed class LendingTests
{
    private static ArpsReserves Curve() => new(0.18, 0.5, 0.35);

    private static ReserveBasedLending Bank(double netbackPerCubicMetre = 0.003) =>
        new(advanceRate: 0.60, discountPerYear: 0.10, years: 15,
            baseRate: 0.08, esgSpreadAtWorst: 0.04, Curve(),
            () => Money.FromMillions(netbackPerCubicMetre));

    private static readonly CovenantStatus Clear = new(CovenantState.Clear, 0);

    /// <summary>More oil, more credit — but discounted, so never the whole of
    /// what it will one day be worth.</summary>
    [Fact]
    public void R20d15V1_the_base_follows_the_reserves()
    {
        Money small = Bank().Redetermine(new SurfaceVolume(1.0e6), Money.Zero, 1.0).BorrowingBase;
        Money large = Bank().Redetermine(new SurfaceVolume(5.0e6), Money.Zero, 1.0).BorrowingBase;

        Assert.True(small > Money.Zero, "a field with reserves supported no borrowing at all");
        Assert.True(large > small, "five times the reserves did not lend more");

        // Discounted and advanced against: never the undiscounted value.
        Money undiscounted = Money.RoundHalfEven(
            5.0e6 * Money.FromMillions(0.003).Cents);

        Assert.True(large < undiscounted,
            "the bank advanced the full nominal value of oil it will not see for fifteen years");
    }

    /// <summary>
    /// A COMPANY NOBODY WANTS TO LEND TO PAYS MORE, and can see why. The spread
    /// is carried separately from the rate — a penalty that only appeared inside
    /// the total would be one nobody could act on.
    /// </summary>
    [Fact]
    public void R20d15V1_a_worse_standing_is_a_dearer_loan()
    {
        BorrowingTerms good = Bank().Redetermine(new SurfaceVolume(2.0e6), Money.Zero, 1.0);
        BorrowingTerms bad = Bank().Redetermine(new SurfaceVolume(2.0e6), Money.Zero, 0.0);

        Assert.True(bad.Rate > good.Rate);
        Assert.Equal(0.0, good.EsgSpread, precision: 12);
        Assert.True(bad.EsgSpread > 0.0);

        // And the base itself is unchanged: standing prices the loan, it does
        // not decide how much oil is in the ground.
        Assert.Equal(good.BorrowingBase, bad.BorrowingBase);
    }

    /// <summary>
    /// A FIELD WORTH NOTHING SUPPORTS NOTHING. No reserves, or a netback the
    /// market has taken away, and there is no security to lend against — which
    /// is the moment a leveraged company discovers what its debt was resting on.
    /// </summary>
    [Fact]
    public void R20d15V1_no_value_no_credit()
    {
        Assert.Equal(
            Money.Zero,
            Bank().Redetermine(new SurfaceVolume(0.0), Money.Zero, 1.0).BorrowingBase);

        Assert.Equal(
            Money.Zero,
            Bank(netbackPerCubicMetre: 0.0)
                .Redetermine(new SurfaceVolume(5.0e6), Money.Zero, 1.0).BorrowingBase);
    }

    /// <summary>
    /// THE BANK NEVER CALLS INSTANTLY (SDD-009 §5's pin). A breach starts a
    /// clock, the clock runs, and only when it expires does the facility go to
    /// an amortisation schedule.
    ///
    /// <para>That window is the player's warning. Called on the month reserves
    /// were written down, a price crash would be an instant loss rather than
    /// something to survive — and surviving it is the game.</para>
    /// </summary>
    [Fact]
    public void R20d15V1_a_breach_opens_a_window_before_it_bites()
    {
        ReserveBasedLending bank = Bank();
        var terms = new BorrowingTerms(Money.FromMillions(10.0), Money.FromMillions(10.0), 0.08, 0.0);
        Money debt = Money.FromMillions(15.0);

        CovenantStatus status = bank.Assess(terms, debt, Clear);

        Assert.Equal(CovenantState.Curing, status.State);
        Assert.True(status.TicksRemaining > 0);

        var months = 0;

        while (status.State == CovenantState.Curing)
        {
            status = bank.Assess(terms, debt, status);

            Assert.True(++months < 24, "the cure window never expired");
        }

        Assert.Equal(CovenantState.Amortising, status.State);
        Assert.True(months > 1, "the window expired immediately; that is calling the loan");
    }

    /// <summary>
    /// AND CURING ENDS WHEN THE BREACH DOES, whichever side moved. A company
    /// that paid debt down and one whose reserves recovered are both back inside
    /// the covenant — it tests a ratio and does not care which way it was
    /// mended.
    /// </summary>
    [Fact]
    public void R20d15V1_the_clock_stops_when_the_breach_ends()
    {
        ReserveBasedLending bank = Bank();
        var tight = new BorrowingTerms(Money.FromMillions(10.0), Money.FromMillions(10.0), 0.08, 0.0);

        CovenantStatus curing = bank.Assess(tight, Money.FromMillions(15.0), Clear);
        Assert.Equal(CovenantState.Curing, curing.State);

        // Paid down.
        Assert.Equal(
            CovenantState.Clear,
            bank.Assess(tight, Money.FromMillions(9.0), curing).State);

        // Or the base recovered.
        Assert.Equal(
            CovenantState.Clear,
            bank.Assess(
                new BorrowingTerms(Money.FromMillions(20.0), Money.FromMillions(20.0), 0.08, 0.0),
                Money.FromMillions(15.0), curing).State);
    }

    /// <summary>
    /// A facility nobody could be refused by is not a facility. Content that
    /// would advance the full present value, or discount at nothing, is refused
    /// at construction.
    /// </summary>
    [Fact]
    public void R20d15V1_an_impossible_facility_is_refused()
    {
        Assert.Throws<ContentFault>(() =>
            new ReserveBasedLending(1.0, 0.1, 15, 0.08, 0.04, Curve(), () => Money.Zero));

        Assert.Throws<ContentFault>(() =>
            new ReserveBasedLending(0.6, 0.0, 15, 0.08, 0.04, Curve(), () => Money.Zero));

        Assert.Throws<ContentFault>(() =>
            new ReserveBasedLending(0.6, 0.1, 0, 0.08, 0.04, Curve(), () => Money.Zero));
    }

    /// <summary>
    /// SDD-009 §5's finding-262 amendment: <c>ReserveValue</c> is what the
    /// reserves are worth, not what the bank will lend against them — so it
    /// must not carry the advance rate at all. Two banks that agree on
    /// everything except the advance rate must still agree on this.
    /// </summary>
    [Fact]
    public void The_reserve_value_does_not_carry_the_advance_rate()
    {
        SurfaceVolume proved = new(2.0e6);

        Money low = new ReserveBasedLending(
                0.20, 0.10, 15, 0.08, 0.04, Curve(), () => Money.FromMillions(0.003))
            .Redetermine(proved, Money.Zero, 1.0).ReserveValue;
        Money high = new ReserveBasedLending(
                0.90, 0.10, 15, 0.08, 0.04, Curve(), () => Money.FromMillions(0.003))
            .Redetermine(proved, Money.Zero, 1.0).ReserveValue;

        Assert.Equal(low, high);
    }

    /// <summary>
    /// AND IT IS ALWAYS WORTH MORE THAN THE BANK WILL ADVANCE — the advance
    /// rate is a fraction in (0, 1) (§5), so the haircut can only shrink what
    /// the bank lends against, never grow it past what the oil is worth.
    /// </summary>
    [Fact]
    public void The_reserve_value_is_never_less_than_the_borrowing_base()
    {
        BorrowingTerms terms = Bank().Redetermine(new SurfaceVolume(2.0e6), Money.Zero, 1.0);

        Assert.True(terms.ReserveValue > Money.Zero, "a field with reserves was worth nothing");
        Assert.True(terms.ReserveValue > terms.BorrowingBase,
            "the bank advanced the full value of the reserves rather than a haircut of it");
    }
}
