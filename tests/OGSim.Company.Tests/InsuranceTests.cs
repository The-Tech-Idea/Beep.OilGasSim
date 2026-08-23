// R13.3 — insurance (SDD-009 §7's finding-273 amendment).

using OGSim.Company;
using OGSim.Kernel;

namespace OGSim.Company.Tests;

public class InsuranceTests
{
    private static InsuranceTerms Terms(
        long exposureCents = 6_00_000_000L, double baseRate = 0.03, double esgSpread = 0.09,
        long deductibleCents = 50_000_000L, long limitCents = 6_00_000_000L) =>
        new(new Money(exposureCents), baseRate, esgSpread,
            new Money(deductibleCents), new Money(limitCents));

    [Fact] // A spotless record pays only the base rate
    public void R13V_a_perfect_standing_prices_the_base_rate_alone()
    {
        Money premium = Insurance.PremiumThisTick(
            Terms(exposureCents: 6_00_000_000L, baseRate: 0.03, esgSpread: 0.09), standing: 100.0);

        // exposure x 0.03 / 12, recomputed independently.
        Assert.Equal(new Money((long)(6_00_000_000L * 0.03 / 12.0)), premium);
    }

    [Fact] // The worst record pays the base rate plus the whole spread
    public void R13V_the_worst_standing_prices_the_base_rate_plus_the_whole_spread()
    {
        Money premium = Insurance.PremiumThisTick(
            Terms(exposureCents: 6_00_000_000L, baseRate: 0.03, esgSpread: 0.09), standing: 0.0);

        // exposure x (0.03 + 0.09) / 12, recomputed independently.
        Assert.Equal(new Money((long)(6_00_000_000L * 0.12 / 12.0)), premium);
    }

    [Fact] // A standing halfway down prices halfway into the spread
    public void R13V_a_standing_halfway_down_prices_halfway_into_the_spread()
    {
        Money premium = Insurance.PremiumThisTick(
            Terms(exposureCents: 6_00_000_000L, baseRate: 0.03, esgSpread: 0.09), standing: 50.0);

        // exposure x (0.03 + 0.045) / 12, recomputed independently.
        Assert.Equal(new Money((long)(6_00_000_000L * 0.075 / 12.0)), premium);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(101.0)]
    public void R13V_a_standing_outside_0_to_100_is_a_model_fault(double standing)
    {
        Assert.Throws<ModelFault>(() => Insurance.PremiumThisTick(Terms(), standing));
    }

    [Fact] // A loss that does not clear the deductible is the company's alone
    public void R13V_a_loss_below_the_deductible_settles_no_claim()
    {
        Money? claim = Insurance.ClaimFor(
            Terms(deductibleCents: 50_000_000L), loss: new Money(30_000_000L));

        Assert.Null(claim);
    }

    [Fact] // A loss exactly at the deductible is not yet ABOVE it
    public void R13V_a_loss_exactly_at_the_deductible_settles_no_claim()
    {
        Money? claim = Insurance.ClaimFor(
            Terms(deductibleCents: 50_000_000L), loss: new Money(50_000_000L));

        Assert.Null(claim);
    }

    [Fact] // Between the deductible and the limit, the claim is the loss less the deductible
    public void R13V_a_loss_between_the_deductible_and_the_limit_pays_the_excess()
    {
        Money claim = Assert.IsType<Money>(Insurance.ClaimFor(
            Terms(deductibleCents: 50_000_000L, limitCents: 6_00_000_000L),
            loss: new Money(2_00_000_000L)));

        Assert.Equal(new Money(1_50_000_000L), claim);
    }

    [Fact] // Above the limit, the claim caps there regardless of how much larger the loss is
    public void R13V_a_loss_above_the_limit_caps_the_claim_at_it()
    {
        Money claim = Assert.IsType<Money>(Insurance.ClaimFor(
            Terms(deductibleCents: 50_000_000L, limitCents: 6_00_000_000L),
            loss: new Money(20_00_000_000L)));

        Assert.Equal(new Money(6_00_000_000L), claim);
    }

    [Theory] // Content errors are refused where the terms are still in hand
    [InlineData(0L, 0.03, 0.09, 50_000_000L, 6_00_000_000L, "exposure")]
    [InlineData(-1L, 0.03, 0.09, 50_000_000L, 6_00_000_000L, "exposure")]
    [InlineData(6_00_000_000L, -0.01, 0.09, 50_000_000L, 6_00_000_000L, "base rate")]
    [InlineData(6_00_000_000L, 0.03, -0.01, 50_000_000L, 6_00_000_000L, "spread")]
    [InlineData(6_00_000_000L, 0.03, 0.09, -1L, 6_00_000_000L, "deductible")]
    [InlineData(6_00_000_000L, 0.03, 0.09, 50_000_000L, 0L, "limit")]
    [InlineData(6_00_000_000L, 0.03, 0.09, 50_000_000L, -1L, "limit")]
    public void R13V_an_unusable_policy_is_a_model_fault(
        long exposureCents, double baseRate, double esgSpread,
        long deductibleCents, long limitCents, string expected)
    {
        var fault = Assert.Throws<ModelFault>(() => Insurance.Validate(new InsuranceTerms(
            new Money(exposureCents), baseRate, esgSpread,
            new Money(deductibleCents), new Money(limitCents))));

        Assert.Contains(expected, fault.Fault.Detail);
    }
}
