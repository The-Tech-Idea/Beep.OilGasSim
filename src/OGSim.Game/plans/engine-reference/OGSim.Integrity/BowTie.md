# BowTie

Source: `src\OGSim.Integrity\BowTie.cs` · Lines: 293

## File intent

> R23.1 / R23.2 / R23.4 — the bow-tie (SDD-012 §4b, design 14 §2).
> 
> THREATS → PREVENTIVE BARRIERS → TOP EVENT → MITIGATING BARRIERS →
> CONSEQUENCES. It is how the industry actually reasons, it gives the player
> things to BUY AND MAINTAIN rather than a probability to endure, and it
> produces a fat tail they control — which is what makes safety investment feel
> like the insurance it is.
> 

## Namespaces

- `OGSim.Integrity`

## Type declarations

- `L32` `public sealed record Barrier(`
- `L78` `public enum ThreatOutcome`
- `L93` `public sealed record ThreatResolution(`
- `L113` `public sealed class BowTie`
- `L240` `public sealed class EsgStanding`

## Accessible members

- `L38` `public bool Equals(Barrier? other) =>`
- `L42` `public override int GetHashCode() =>`
- `L45` `public double StrengthGiven(`
- `L67` `private void Validate(double value, string name)`
- `L100` `public bool Equals(ThreatResolution? other) =>`
- `L105` `public override int GetHashCode() =>`
- `L115` `private readonly IRandomStream _hazard;`
- `L116` `private readonly IAuditTrail _audit;`
- `L118` `public BowTie(IRandomStream hazardStream, IAuditTrail audit)`
- `L139` `public ThreatResolution Resolve(`
- `L192` `private int SampleMitigating(IReadOnlyList<Barrier> barriers, Func<Barrier, double> strengthOf)`
- `L206` `private void Audit(`
- `L228` `private static string Format(int value) =>`
- `L242` `private readonly double _halfLifeTicks;`
- `L243` `private double _incidentPoints;`
- `L245` `public EsgStanding(double halfLifeTicks)`
- `L255` `public double IncidentPoints => _incidentPoints;`
- `L258` `public void RecordIncident(double points)`
- `L268` `public void Age(Duration dt)`
- `L283` `public double Standing(IReadOnlyList<(double Weight, double BandScore)> intensities)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

