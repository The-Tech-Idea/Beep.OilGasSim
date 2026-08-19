// R20d.11 — the market (SDD-009 §6).
//
// What is asserted here is that the process is the one SDD-009 §6 pins, and that
// its two defining properties hold: it MOVES, and it comes BACK. A market that
// only did the first is a random walk, which the design does not ship for a good
// reason — given forty years a walk ends at three dollars or three hundred, and
// the game becomes a coin flip taken once.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company.Tests;

public sealed class PriceTests
{
    private static readonly Money Benchmark = Money.FromMillions(0.0004435);

    private static MeanRevertingPrice Market(double reversion = 0.02, double jumpChance = 0.0) =>
        new(Benchmark, reversion, volatility: 0.09, jumpChance, jumpScale: 0.27);

    private static IRandomStream Stream(ulong seed = 7UL) =>
        new RandomSource(seed).Stream(StreamId.Price);

    /// <summary>The price moves. A constant is what this replaced.</summary>
    [Fact]
    public void R20d11V1_the_price_moves()
    {
        IRandomStream draws = Stream();
        Money price = Benchmark;

        var moved = 0;

        for (var month = 0; month < 24; month++)
        {
            Money next = Market().Advance(price, draws);

            if (next != price) moved++;

            price = next;
        }

        Assert.True(moved > 20, $"only {moved} of 24 months moved the price at all");
    }

    /// <summary>
    /// AND IT COMES BACK. Pushed far from the mean, the process walks towards it
    /// — which is the whole difference between a market a company can plan
    /// against and a coin flip. Asserted from BOTH sides, because a process that
    /// only fell would satisfy the first half while being a slow crash.
    /// </summary>
    [Fact]
    public void R20d11V1_a_price_far_from_the_mean_is_pulled_back()
    {
        Money high = Benchmark * 3;
        var low = new Money(Benchmark.Cents / 3);

        // No volatility: this is about the drift, and a noisy sample would only
        // say the drift is bigger than the noise on this particular seed.
        var quiet = new MeanRevertingPrice(
            Benchmark, reversion: 0.02, volatility: 0.0, jumpChance: 0.0, jumpScale: 0.0);

        IRandomStream draws = Stream();

        for (var month = 0; month < 120; month++)
        {
            high = quiet.Advance(high, draws);
            low = quiet.Advance(low, draws);
        }

        Assert.True(high < Benchmark * 3, "a price above the mean did not fall towards it");
        Assert.True(low.Cents > Benchmark.Cents / 3, "a price below the mean did not rise towards it");

        // Ten years of reverting at a three-year half-life gets most of the way.
        Assert.True(high < Benchmark * 2, $"reversion is too weak to matter: {high}");
        Assert.True(low.Cents > Benchmark.Cents / 2, $"reversion is too weak to matter: {low}");
    }

    /// <summary>
    /// IT CANNOT GO NEGATIVE, whatever the draws do, because the process is in
    /// log space. A linear one would eventually cross zero and no amount of
    /// clamping afterwards would make that honest.
    /// </summary>
    [Fact]
    public void R20d11V1_the_price_stays_positive_through_a_violent_market()
    {
        var violent = new MeanRevertingPrice(
            Benchmark, reversion: 0.02, volatility: 0.5, jumpChance: 0.5, jumpScale: 1.0);

        IRandomStream draws = Stream();
        Money price = Benchmark;

        for (var month = 0; month < 600; month++)
        {
            price = violent.Advance(price, draws);

            Assert.True(price > Money.Zero, $"the benchmark reached {price} in month {month}");
        }
    }

    /// <summary>
    /// One seed is one market (PV7). Two runs of a save must price the same
    /// barrels the same way, or a reloaded game is a different game.
    /// </summary>
    [Fact]
    public void R20d11V1_one_seed_is_one_market()
    {
        Assert.Equal(Walk(Stream(11UL)), Walk(Stream(11UL)));
        Assert.NotEqual(Walk(Stream(11UL)), Walk(Stream(12UL)));
    }

    private static Money Walk(IRandomStream draws)
    {
        Money price = Benchmark;

        for (var month = 0; month < 60; month++) price = Market().Advance(price, draws);

        return price;
    }

    /// <summary>
    /// A jump is drawn EVERY month whether or not it fires (SDD-009 §6), so
    /// turning shocks off cannot shift the sequence of ordinary moves. Without
    /// that, a content edit to the jump rate would silently rewrite the whole
    /// price history of a seed.
    /// </summary>
    [Fact]
    public void R20d11V1_shocks_do_not_shift_the_ordinary_moves()
    {
        Money never = Benchmark;
        Money always = Benchmark;

        IRandomStream a = Stream(5UL);
        IRandomStream b = Stream(5UL);

        // jumpScale ZERO, so the jump adds nothing when it fires — the draws are
        // consumed either way, which is the property under test.
        var quiet = new MeanRevertingPrice(Benchmark, 0.02, 0.09, jumpChance: 0.0, jumpScale: 0.0);
        var noisy = new MeanRevertingPrice(Benchmark, 0.02, 0.09, jumpChance: 1.0, jumpScale: 0.0);

        for (var month = 0; month < 36; month++)
        {
            never = quiet.Advance(never, a);
            always = noisy.Advance(always, b);
        }

        Assert.Equal(never, always);
    }

    /// <summary>
    /// A market that could not be reverted to is not a market. Refused at
    /// construction, naming what is wrong, rather than producing a price series
    /// nobody could interpret.
    /// </summary>
    [Fact]
    public void R20d11V1_an_impossible_market_is_refused()
    {
        Assert.Throws<ContentFault>(() => Market(reversion: 0.0));
        Assert.Throws<ContentFault>(() => Market(reversion: 1.5));
        Assert.Throws<ContentFault>(() => Market(jumpChance: 1.5));
    }
}
