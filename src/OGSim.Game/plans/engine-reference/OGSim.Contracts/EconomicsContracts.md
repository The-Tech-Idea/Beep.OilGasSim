# EconomicsContracts

Source: `src\OGSim.Contracts\EconomicsContracts.cs` · Lines: 94

## File intent

> SDD-009 — the replaceable economics models (design 03 §3.2). Regimes are
> content-selected per licence; the engine calls Assess once per licence per
> tick at stage 8 and books ONLY what comes back — no fiscal math elsewhere.
> <summary>
> Everything a regime may consider, per licence per tick (SDD-009 §3).
> CostPoolCarry is the regime's own prior-tick output fed back — the regime
> is otherwise stateless, which is what makes it swappable mid-campaign.
> </summary>

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L14` `public sealed record FiscalInput(`
- `L23` `public sealed record FiscalResult(`
- `L30` `public interface IFiscalRegime`
- `L39` `public sealed record BorrowingTerms(`
- `L49` `public enum CovenantState`
- `L63` `public sealed record CovenantStatus(CovenantState State, int TicksRemaining);`
- `L75` `public interface IReserveBasedLending`
- `L90` `public interface IPriceModel`

## Accessible members

_No public/internal/protected/private member lines matched the extractor._

## Imports

- `using OGSim.Kernel;`

