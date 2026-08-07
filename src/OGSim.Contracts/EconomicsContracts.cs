// SDD-009 — the replaceable economics models (design 03 §3.2). Regimes are
// content-selected per licence; the engine calls Assess once per licence per
// tick at stage 8 and books ONLY what comes back — no fiscal math elsewhere.

using OGSim.Kernel;

namespace OGSim.Contracts;

/// <summary>
/// Everything a regime may consider, per licence per tick (SDD-009 §3).
/// CostPoolCarry is the regime's own prior-tick output fed back — the regime
/// is otherwise stateless, which is what makes it swappable mid-campaign.
/// </summary>
public sealed record FiscalInput(
    Money GrossRevenue,
    Money RecoverableOpex,
    Money RecoverableCapex,
    Money Depreciation,
    Money CostPoolCarry,
    double PriorRFactor);

/// <summary>Booked verbatim at stage 8; INV2 checks the split re-adds to gross less costs.</summary>
public sealed record FiscalResult(
    Money Royalty,
    Money Tax,
    Money ContractorTake,
    Money CostPoolCarry);

/// <summary>Concession, PSC, service contract — one implementation each (SDD-009 §3).</summary>
public interface IFiscalRegime
{
    ContentId Id { get; }
    FiscalResult Assess(FiscalInput input);
}

// ---------------------------------------------- reserve-based lending (§5)

/// <summary>What the bank will lend against, and what it costs.</summary>
public sealed record BorrowingTerms(
    Money BorrowingBase,
    double Rate,

    /// <summary>The ESG standing's effect on the rate, carried separately
    /// because a company that cannot see why its debt got more expensive cannot
    /// act on it (design 08 §5).</summary>
    double EsgSpread);

/// <summary>Where a covenant stands. A breach is not a call.</summary>
public enum CovenantState
{
    Clear,

    /// <summary>Debt exceeds the borrowing base and the cure window is running.
    /// <b>The bank never calls instantly</b> — the window is the player's
    /// warning, and removing it would make a price crash unsurvivable rather
    /// than hard (SDD-009 §5).</summary>
    Curing,

    /// <summary>The window closed uncured; forced amortisation is scheduled.</summary>
    Amortising,
}

public sealed record CovenantStatus(CovenantState State, int TicksRemaining);

/// <summary>
/// SDD-009 §5 — <c>borrowingBase = advanceRate · PV(1P after-fiscal cash
/// flows)</c>, redetermined quarterly and immediately on any reserves
/// recomputation.
///
/// <para>This is the mechanism by which a company can over-commit and be
/// disciplined for it: reserves fall, the base falls with them, and debt taken
/// against last year's reserves is suddenly a covenant problem. It reads
/// reserves and standing; it never books a movement — the ledger does that.</para>
/// </summary>
public interface IReserveBasedLending
{
    ContentId Id { get; }

    BorrowingTerms Redetermine(
        SurfaceVolume provedReserves, Money debt, double esgStanding);

    CovenantStatus Assess(BorrowingTerms terms, Money debt, CovenantStatus previous);
}

/// <summary>
/// Benchmark price advance, one call per tick at stage 8, consuming ONLY the
/// Price stream (D-2). OU-in-log-space is the shipped model (SDD-009 §2);
/// the slot exists so mods can replace it.
/// </summary>
public interface IPriceModel
{
    ContentId Id { get; }
    Money Advance(Money current, IRandomStream price);
}
