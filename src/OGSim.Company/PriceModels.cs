// R20d.11 — the oil price moves (SDD-009 §6, design 03 §3.2).
//
// IT WAS A CONSTANT, and two of the kernel's eight named RNG streams — `price`
// and `market` — existed for a market that never moved. `IPriceModel` was
// declared in the contract layer and implemented by nobody.
//
// A FIXED PRICE MAKES EVERY ECONOMIC DECISION DETERMINISTIC. Whether a marginal
// field is worth developing, whether to abandon now or run one more year,
// whether to spend on a bigger export line — all of them have one right answer
// computable in advance, which is another way of saying they are not decisions.
// A field that was uneconomic last year and pays this year is the whole reason
// an operator waits, and the reason a company that developed at the top of a
// cycle regrets it.
//
// MEAN-REVERTING, NOT A RANDOM WALK (SDD-009 §6's pinned form). A walk wanders
// off and never comes back: given forty years it will end at three dollars or
// three hundred, and the game becomes a coin flip taken once. Oil reverts —
// high prices bring on supply and low ones shut it in — so the interesting
// question is never "where will it end up" but "how long can I sit through
// this", which is a question about a company's balance sheet rather than about
// luck.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>
/// SDD-009 §6's Ornstein–Uhlenbeck process in log space, with jumps.
///
/// <para><c>ln P(t+1) = ln P(t) + κ·(ln μ − ln P(t)) + σ·ε</c> — in LOG space so
/// the price cannot go negative however hard the draw pushes, which a linear
/// process would eventually do and which no amount of clamping makes
/// honest.</para>
/// </summary>
public sealed class MeanRevertingPrice : IPriceModel
{
    private readonly double _logMean;
    private readonly double _reversion;
    private readonly double _volatility;
    private readonly double _jumpChance;
    private readonly double _jumpScale;

    public MeanRevertingPrice(
        Money longRunMean,
        double reversion,
        double volatility,
        double jumpChance,
        double jumpScale)
    {
        if (longRunMean <= Money.Zero)
            throw new ContentFault("SDD-009 §6", null,
                "a benchmark reverts to a positive price; a mean of zero would drag every " +
                "barrel ever sold towards worthlessness");

        if (reversion is <= 0.0 or > 1.0)
            throw new ContentFault("SDD-009 §6", null,
                "reversion is a per-tick fraction in (0, 1]; zero is a random walk, which " +
                "SDD-009 §6 does not ship, and above one overshoots the mean every month");

        if (volatility < 0.0 || !double.IsFinite(volatility))
            throw new ContentFault("SDD-009 §6", null, "volatility is a finite non-negative");

        if (jumpChance is < 0.0 or > 1.0)
            throw new ContentFault("SDD-009 §6", null, "a jump probability is in [0, 1]");

        if (jumpScale < 0.0 || !double.IsFinite(jumpScale))
            throw new ContentFault("SDD-009 §6", null, "jump scale is a finite non-negative");

        _logMean = DetMath.Ln(longRunMean.Cents);
        _reversion = reversion;
        _volatility = volatility;
        _jumpChance = jumpChance;
        _jumpScale = jumpScale;
    }

    public ContentId Id { get; } = new("mean-reverting-benchmark");

    /// <summary>
    /// One month's move. The stream is handed IN and never held (SDD-009 §6):
    /// a model that owned a source could quietly draw from the wrong one, and
    /// the whole point of eight named streams is that adding a draw here cannot
    /// shift what the weather does.
    /// </summary>
    public Money Advance(Money current, IRandomStream price)
    {
        ArgumentNullException.ThrowIfNull(price);

        if (current <= Money.Zero)
            throw new InvariantFault("SDD-009 §6", null,
                "the benchmark reached zero or below; a log-space process cannot start from " +
                "a price that has no logarithm, and a market that free is not a market");

        double log = DetMath.Ln(current.Cents);

        log += _reversion * (_logMean - log);
        log += _volatility * price.NextNormal();

        // A SHOCK. BOTH draws are taken every month, whether or not the jump
        // fires, and that is the whole point rather than a wasteful detail:
        // drawing the size only when the roll succeeds makes the number of draws
        // depend on the jump rate, so a content edit to that rate would silently
        // rewrite the entire price history of every seed. Written the obvious
        // way first, and the test for exactly this caught it.
        //
        // An embargo and a glut are the same mechanism with the sign reversed.
        double roll = price.NextUnit();
        double shock = price.NextNormal();

        if (roll < _jumpChance) log += _jumpScale * shock;

        // Rounded once, here, at the boundary where a real number becomes money
        // — half-even, the one double→Money rule there is (SDD-001 §3).
        return Money.RoundHalfEven(DetMath.Exp(log));
    }
}
