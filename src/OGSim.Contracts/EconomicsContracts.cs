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
