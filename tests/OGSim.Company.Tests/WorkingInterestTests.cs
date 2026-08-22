// R13.10 — the working-interest sale (SDD-011 §4's finding-275 amendment).

using OGSim.Company;
using OGSim.Kernel;

namespace OGSim.Company.Tests;

public class WorkingInterestTests
{
    private static WorkingInterestTerms Terms(
        double maxSellableFraction = 0.5, double distressDiscount = 0.25) =>
        new(maxSellableFraction, distressDiscount);

    [Fact] // reserveValue x fraction x (1 - discount), recomputed independently
    public void R13V_the_price_is_reserve_value_times_fraction_at_the_discount()
    {
        Money reserveValue = new(10_00_000_000L);   // $10.0M

        Money price = WorkingInterest.Price(Terms(distressDiscount: 0.25), reserveValue, fraction: 0.2);

        Assert.Equal(new Money((long)(10_00_000_000L * 0.2 * 0.75)), price);
    }

    [Fact] // A zero discount sells at the bare pro-rata reserve value
    public void R13V_a_zero_discount_sells_at_the_bare_pro_rata_value()
    {
        Money reserveValue = new(10_00_000_000L);

        Money price = WorkingInterest.Price(Terms(distressDiscount: 0.0), reserveValue, fraction: 0.5);

        Assert.Equal(new Money(5_00_000_000L), price);
    }

    [Fact] // Two sales accumulate rather than replace
    public void R13V_selling_twice_accumulates_the_partner_share()
    {
        var stake = new WorkingInterest();

        stake.Sell(0.1);
        Assert.Equal(0.1, stake.PartnerShare, 9);

        stake.Sell(0.2);
        Assert.Equal(0.3, stake.PartnerShare, 9);
    }

    [Theory] // Content errors are refused where the terms are still in hand
    [InlineData(0.0, 0.25, "sellable cap")]
    [InlineData(-0.1, 0.25, "sellable cap")]
    [InlineData(1.1, 0.25, "sellable cap")]
    [InlineData(0.5, -0.01, "distress discount")]
    [InlineData(0.5, 1.0, "distress discount")]
    [InlineData(0.5, 1.5, "distress discount")]
    public void R13V_unusable_terms_are_a_model_fault(
        double maxSellableFraction, double distressDiscount, string expected)
    {
        var fault = Assert.Throws<ModelFault>(
            () => WorkingInterest.Validate(new WorkingInterestTerms(maxSellableFraction, distressDiscount)));

        Assert.Contains(expected, fault.Fault.Detail);
    }

    [Fact] // The boundaries themselves are usable, not just values inside them
    public void R13V_the_boundary_values_are_valid()
    {
        WorkingInterest.Validate(new WorkingInterestTerms(MaxSellableFraction: 1.0, DistressDiscount: 0.0));
    }
}
