# BeliefStore

Source: `src\OGSim.Information\BeliefStore.cs` · Lines: 281

## File intent

> R14.2 / R14.3 — beliefs and the one update rule (SDD-008 §2–3).
> 
> APPLY IS THE ONLY WRITER. There is deliberately no Set, no seed-from-truth and
> no bulk import: world generation delivers initial beliefs through this same
> door (R15-V10), so there is no belief-copy path for truth to leak down. The
> absence is the enforcement — a method that does not exist cannot be called by
> mistake, reached by reflection in a hurry, or added "just for the loader".
> 

## Namespaces

- `OGSim.Information`

## Type declarations

- `L22` `public sealed class BeliefStore : IBeliefStore`
- `L267` `public static class Quantiles`

## Accessible members

- `L27` `private readonly List<HeldBelief> _held = [];`
- `L28` `private readonly Dictionary<(EntityRef Subject, ContentId Kind), int> _at = [];`
- `L30` `private readonly IAuditTrail _audit;`
- `L31` `private readonly Func<ContentId, double> _sigmaFloorFor;`
- `L32` `private readonly Func<GameDate> _now;`
- `L34` `public BeliefStore(`
- `L55` `public void Apply(Observation observation)`
- `L111` `public Belief? Get(EntityRef subject, ContentId propertyKind) =>`
- `L123` `public IReadOnlyList<HeldBelief> Held => _held;`
- `L128` `public void ReKey(EntityRef from, EntityRef to)`
- `L172` `public void Age(ContentId propertyKind, double driftPerYear, double years)`
- `L196` `private Belief FromFirstObservation(Observation observation) =>`
- `L203` `private Belief Combine(Belief prior, Observation observation)`
- `L243` `private double FlooredSigma(ContentId kind, double sigma, Provenance source)`
- `L255` `private static string Format(double value) =>`
- `L269` `public static double P50(Belief belief) => Transform(belief, belief.Mu);`
- `L272` `public static double P90(Belief belief) =>`
- `L276` `public static double P10(Belief belief) =>`
- `L279` `private static double Transform(Belief belief, double value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

