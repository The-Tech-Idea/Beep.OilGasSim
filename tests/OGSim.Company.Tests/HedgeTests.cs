// R13.3 — the hedge (SDD-009 §7's finding-272 amendment).

using OGSim.Company;
using OGSim.Kernel;

namespace OGSim.Company.Tests;

public class HedgeTests
{
    private static HedgeTerms Terms(
        double hedgedFraction = 0.5, long floorCents = 4_000_000L, long capCents = 8_000_000L) =>
        new(hedgedFraction, new Money(floorCents), new Money(capCents));

    [Fact] // A benchmark sitting inside the collar settles nothing
    public void R13V_a_benchmark_inside_the_collar_settles_nothing()
    {
        HedgeSettlement? settlement = Hedge.SettleAt(
            Terms(floorCents: 4_000_000L, capCents: 8_000_000L),
            hedgedTonnes: 1_000.0, benchmark: new Money(6_000_000L));

        Assert.Null(settlement);
    }

    [Fact] // A benchmark AT the floor or cap exactly is not outside it either
    public void R13V_a_benchmark_exactly_at_a_boundary_settles_nothing()
    {
        HedgeTerms terms = Terms(floorCents: 4_000_000L, capCents: 8_000_000L);

        Assert.Null(Hedge.SettleAt(terms, 1_000.0, new Money(4_000_000L)));
        Assert.Null(Hedge.SettleAt(terms, 1_000.0, new Money(8_000_000L)));
    }

    [Fact] // Below the floor, the company RECEIVES the shortfall against it
    public void R13V_a_benchmark_below_the_floor_pays_the_company()
    {
        HedgeSettlement settlement = Assert.IsType<HedgeSettlement>(
            Hedge.SettleAt(
                Terms(floorCents: 4_000_000L, capCents: 8_000_000L),
                hedgedTonnes: 1_000.0, benchmark: new Money(3_000_000L)));

        Assert.True(settlement.CompanyReceives);

        // (floor - benchmark) x tonnes, recomputed independently.
        Assert.Equal(new Money(1_000_000L * 1_000L), settlement.Amount);
    }

    [Fact] // Above the cap, the company PAYS the excess away
    public void R13V_a_benchmark_above_the_cap_costs_the_company()
    {
        HedgeSettlement settlement = Assert.IsType<HedgeSettlement>(
            Hedge.SettleAt(
                Terms(floorCents: 4_000_000L, capCents: 8_000_000L),
                hedgedTonnes: 1_000.0, benchmark: new Money(9_500_000L)));

        Assert.False(settlement.CompanyReceives);

        // (benchmark - cap) x tonnes, recomputed independently.
        Assert.Equal(new Money(1_500_000L * 1_000L), settlement.Amount);
    }

    [Fact] // No tonnage hedged is nothing to settle, whatever the benchmark does
    public void R13V_zero_hedged_tonnage_settles_nothing_even_outside_the_collar()
    {
        HedgeSettlement? settlement = Hedge.SettleAt(
            Terms(floorCents: 4_000_000L, capCents: 8_000_000L),
            hedgedTonnes: 0.0, benchmark: new Money(1_000_000L));

        Assert.Null(settlement);
    }

    [Theory] // Content errors are refused where the terms are still in hand
    [InlineData(0.0, 4_000_000L, 8_000_000L, "fraction")]
    [InlineData(-0.5, 4_000_000L, 8_000_000L, "fraction")]
    [InlineData(1.5, 4_000_000L, 8_000_000L, "fraction")]
    [InlineData(0.5, 0L, 8_000_000L, "floor")]
    [InlineData(0.5, -1L, 8_000_000L, "floor")]
    [InlineData(0.5, 8_000_000L, 8_000_000L, "cap")]
    [InlineData(0.5, 9_000_000L, 8_000_000L, "cap")]
    public void R13V_an_unusable_hedge_is_a_model_fault(
        double hedgedFraction, long floorCents, long capCents, string expected)
    {
        var fault = Assert.Throws<ModelFault>(
            () => Hedge.Validate(new HedgeTerms(
                hedgedFraction, new Money(floorCents), new Money(capCents))));

        Assert.Contains(expected, fault.Fault.Detail);
    }

    [Fact] // A hedged fraction of exactly 1.0 (the whole tick's production) is valid
    public void R13V_a_hedged_fraction_of_one_is_valid()
    {
        Hedge.Validate(Terms(hedgedFraction: 1.0));
    }
}
