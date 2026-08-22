// R13.3 — insurance, the third of ISalesContract's four kinds (SDD-009 §7's
// finding-273 amendment; design 08 §7's ED6).
//
// TWO INDEPENDENT FACTS, ONE INSTRUMENT. What an incident costs is priced by
// OGSim.Composition/Modules.cs's ThreatStage.ConsequenceLoss (SDD-012 §4b's
// own finding-273 amendment) — a loss in cents, the same "mitigated/
// unmitigated straight line" shape ConsequencePoints already has — and that
// loss happens whether or not a policy exists. This file prices what a
// STANDING policy does about it: a premium every tick, and a claim on
// whichever tick a loss actually lands.

using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>
/// SDD-009 §7's finding-273 amendment — the one policy this composition
/// ships, the same "one contract, not a ladder" shape <see cref="HedgeTerms"/>
/// already has.
/// </summary>
public sealed record InsuranceTerms(
    Money Exposure,          // the worst loss this policy is priced against
    double BaseRatePerYear,  // of Exposure, at a perfect (100) ESG standing
    double EsgRateSpread,    // additional, at the worst (0) standing
    Money Deductible,
    Money Limit);

/// <summary>SDD-009 §7's two formulas, pinned. Both are pure functions of the
/// terms and one input — a standing for the premium, a loss for the claim —
/// so, like <see cref="Hedge"/>, this is a static class rather than an
/// <c>IStateOwner</c>: neither figure depends on any earlier tick's.</summary>
public static class Insurance
{
    /// <summary>
    /// premium(class) = exposure(class) · rate(record) per year (SDD-009 §7),
    /// charged one twelfth per tick (SDD-001 §3's 30/360 month).
    ///
    /// <para>rate(standing) is the SAME base-plus-ESG-spread shape
    /// <c>ReserveBasedLending</c> already prices the borrowing base with
    /// (SDD-009 §5) — a company with a clean record pays the base rate, one
    /// with none pays up to the base plus the whole spread, and the record IS
    /// design 08 §7's own "second economic channel beside ESG" rather than a
    /// second one this amendment invents.</para>
    /// </summary>
    public static Money PremiumThisTick(InsuranceTerms terms, double standing)
    {
        ArgumentNullException.ThrowIfNull(terms);

        if (standing is < 0.0 or > 100.0 || double.IsNaN(standing))
            throw new ModelFault("SDD-009 §7", null,
                $"an ESG standing of {standing} is not on the 0-100 scale the record is kept on");

        double annualRate = terms.BaseRatePerYear
            + (100.0 - standing) / 100.0 * terms.EsgRateSpread;

        return Money.RoundHalfEven(terms.Exposure.Cents * annualRate / 12.0);
    }

    /// <summary>
    /// a claim pays min(loss − deductible, limit) (SDD-009 §7). Null when the
    /// loss does not clear the deductible — the company carries the whole of
    /// a small loss itself, which is what a deductible IS.
    /// </summary>
    public static Money? ClaimFor(InsuranceTerms terms, Money loss)
    {
        ArgumentNullException.ThrowIfNull(terms);

        if (loss.Cents <= terms.Deductible.Cents) return null;

        Money aboveDeductible = loss - terms.Deductible;

        return aboveDeductible.Cents > terms.Limit.Cents ? terms.Limit : aboveDeductible;
    }

    public static void Validate(InsuranceTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        if (terms.Exposure.Cents <= 0)
            throw new ModelFault("SDD-009 §7", null, "insured exposure must be a positive value");

        if (terms.BaseRatePerYear < 0.0)
            throw new ModelFault("SDD-009 §7", null, "a base rate cannot be negative");

        if (terms.EsgRateSpread < 0.0)
            throw new ModelFault("SDD-009 §7", null,
                "an ESG rate spread cannot be negative; a worse record must not price cheaper");

        if (terms.Deductible.Cents < 0)
            throw new ModelFault("SDD-009 §7", null, "a deductible cannot be negative");

        if (terms.Limit.Cents <= 0)
            throw new ModelFault("SDD-009 §7", null, "a policy limit must be a positive value");
    }
}
