# Aquifer

Source: `src\OGSim.Subsurface\Aquifer.cs` · Lines: 94

## File intent

> R5.5 — the aquifer (SDD-003 §3.3, Fetkovich form).
> 
> An aquifer IS a water compartment, so the influx it reports is a reservoir
> volume like any other withdrawal or injection — it enters §3.1's balance as
> We and nowhere else. That is what keeps the water drive testable independently
> of the aquifer producing the water (R5-V8).
> <summary>
> Fetkovich: influx is proportional to the pressure difference across the

## Namespaces

- `OGSim.Subsurface`

## Type declarations

- `L25` `internal sealed class FetkovichAquifer : IAquiferModel`

## Accessible members

- `L27` `public ContentId Id { get; } = new("fetkovich-aquifer");`
- `L29` `private readonly double _productivityIndex;    // J_aq, m³/s/Pa`
- `L30` `private readonly double _initialPressurePa;`
- `L31` `private readonly double _maximumInfluxM3;      // W_ei: total expansion available`
- `L33` `private double _cumulativeInfluxM3;`
- `L35` `public FetkovichAquifer(`
- `L57` `public ReservoirVolume CumulativeInflux => new(_cumulativeInfluxM3);`
- `L66` `public Pressure AquiferPressure =>`
- `L69` `public ReservoirVolume InfluxDuring(Pressure reservoirPressure, Duration duration)`
- `L92` `private static string Format(double value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

