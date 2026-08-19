# FiscalRegimes

Source: `src\OGSim.Company\FiscalRegimes.cs` · Lines: 252

## File intent

> R13.4 — fiscal regimes (SDD-009 §3).
> 
> The engine calls Assess once per licence per tick at stage 8 and books ONLY
> what comes back. There is no fiscal arithmetic anywhere else — which is why
> four regimes over one field give four materially different answers without a
> single branch outside these classes (R13-V4).
> 
> Every regime is a PURE FUNCTION of its input. That is why CostPoolCarry

## Namespaces

- `OGSim.Company`

## Type declarations

- `L28` `public sealed class RoyaltyTaxRegime : IFiscalRegime`
- `L88` `public sealed record ProfitTranche(double From, double ContractorShare);`
- `L104` `public sealed class ProductionSharingRegime : IFiscalRegime`
- `L214` `public sealed class ServiceContractRegime : IFiscalRegime`

## Accessible members

- `L30` `private readonly double _royaltyRate;`
- `L31` `private readonly double _taxRate;`
- `L33` `private Money _lossCarry = Money.Zero;`
- `L35` `public RoyaltyTaxRegime(ContentId id, double royaltyRate, double taxRate)`
- `L45` `public ContentId Id { get; }`
- `L49` `public Money LossCarry => _lossCarry;`
- `L51` `public FiscalResult Assess(FiscalInput input)`
- `L76` `internal static void Validate(double rate, string name)`
- `L106` `private readonly double _royaltyRate;`
- `L107` `private readonly double _costOilCapFraction;`
- `L108` `private readonly double _taxRate;`
- `L109` `private readonly bool _taxesProfitOil;`
- `L110` `private readonly IReadOnlyList<ProfitTranche> _tranches;`
- `L112` `public ProductionSharingRegime(`
- `L144` `public ContentId Id { get; }`
- `L146` `public FiscalResult Assess(FiscalInput input)`
- `L191` `public double ContractorShareAt(double rFactor)`
- `L201` `private static string Format(double value) =>`
- `L216` `private readonly Money _feePerUnit;`
- `L218` `public ServiceContractRegime(ContentId id, Money feePerUnit)`
- `L228` `public ContentId Id { get; }`
- `L232` `public double DeliveredUnits { get; set; }`
- `L235` `public double CostIndex { get; set; } = 1.0;`
- `L237` `public FiscalResult Assess(FiscalInput input)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

