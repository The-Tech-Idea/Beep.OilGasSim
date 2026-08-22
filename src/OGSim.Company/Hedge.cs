// R13.3 — the hedge, the second of ISalesContract's four kinds finding 250
// deliberately left open (SDD-009 §7's finding-272 amendment).
//
// A COLLAR, settling fresh every tick against that tick's own production and
// that tick's own benchmark price — unlike take-or-pay, it carries no window
// and no accumulator: nothing about this month's settlement depends on any
// other month's, so there is nothing to persist between them.

using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>
/// SDD-009 §7's collar terms — the one hedge this composition ships, the same
/// "one contract, not a ladder" shape <see cref="TakeOrPayTerms"/> already has.
/// </summary>
public sealed record HedgeTerms(
    double HedgedFraction,   // of the tick's own production, 0..1
    Money Floor,             // per tonne
    Money Cap);              // per tonne

/// <summary>What one tick's settlement found.</summary>
public sealed record HedgeSettlement(Money Amount, bool CompanyReceives);

/// <summary>
/// SDD-009 §7's collar formula, pinned: <c>settle = hedgedVolume ×
/// (clamp(benchmark, floor, cap) − benchmark)</c>. A pure function of the
/// terms, the tick's own hedged tonnage and the tick's own benchmark — no
/// state to own, which is why this is a static method rather than a class.
/// </summary>
public static class Hedge
{
    /// <summary>Null when there is nothing to settle — no tonnage hedged, or
    /// the benchmark already sits inside the collar.</summary>
    public static HedgeSettlement? SettleAt(HedgeTerms terms, double hedgedTonnes, Money benchmark)
    {
        ArgumentNullException.ThrowIfNull(terms);

        if (hedgedTonnes <= 0.0) return null;

        long clampedCents = Math.Clamp(benchmark.Cents, terms.Floor.Cents, terms.Cap.Cents);
        long diffCents = clampedCents - benchmark.Cents;

        if (diffCents == 0) return null;

        Money amount = Money.RoundHalfEven(Math.Abs(diffCents) * hedgedTonnes);

        if (amount.Cents == 0) return null;

        return new HedgeSettlement(amount, CompanyReceives: diffCents > 0);
    }

    public static void Validate(HedgeTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        if (terms.HedgedFraction is <= 0.0 or > 1.0)
            throw new ModelFault("SDD-009 §7", null,
                $"hedged fraction {terms.HedgedFraction} must be in (0, 1]");

        if (terms.Floor.Cents <= 0)
            throw new ModelFault("SDD-009 §7", null, "a hedge floor must be a positive price");

        if (terms.Cap.Cents <= terms.Floor.Cents)
            throw new ModelFault("SDD-009 §7", null,
                $"hedge cap ({terms.Cap.Cents}c) must exceed its own floor ({terms.Floor.Cents}c)");
    }
}
