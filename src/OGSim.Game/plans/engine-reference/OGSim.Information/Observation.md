# Observation

Source: `src\OGSim.Information\Observation.cs` · Lines: 268

## File intent

> R14.3 / R14.7 — observation sampling and POS (SDD-008 §3–4).
> 
> SOURCES NEVER RETURN TRUTH AND NEVER RETURN BIAS. Sigma honest, centre honest.
> What distinguishes a core from a log is WHICH KINDS it can see and HOW SMALL
> its sigma is — not a fudge applied to the answer. That is what makes the
> player's uncertainty real rather than theatrical, and what makes a survey
> worth paying for rather than a formality.
> 

## Namespaces

- `OGSim.Information`

## Type declarations

- `L23` `public sealed class ObservationSampler`
- `L115` `public sealed class ProspectRisk`

## Accessible members

- `L25` `private readonly IObservationModel _model;`
- `L26` `private readonly IRandomStream _exploration;`
- `L27` `private readonly IRandomStream _measurement;`
- `L28` `private readonly IAuditTrail _audit;`
- `L30` `public ObservationSampler(`
- `L60` `public Observation? Sample(`
- `L102` `private static string Format(double value) =>`
- `L117` `private readonly Dictionary<PosFactor, FactorBelief> _factors = [];`
- `L121` `private static readonly PosFactor[] Factors =`
- `L129` `private readonly HashSet<PosFactor> _shared = [];`
- `L131` `private ProspectRisk? _play;`
- `L133` `public ProspectRisk(FactorBelief prior)`
- `L144` `public FactorBelief this[PosFactor factor] =>`
- `L148` `public static double MeanOf(FactorBelief belief) =>`
- `L152` `public double ProbabilityOfSuccess`
- `L169` `public void Observe(PosFactor factor, bool present) => Observe(factor, present, 1.0);`
- `L178` `public void Observe(PosFactor factor, bool present, double weight)`
- `L212` `public void ShareFrom(ProspectRisk play, PosFactor factor)`
- `L237` `public void Weigh(PosFactor factor, double mean)`
- `L261` `private static void Validate(FactorBelief belief)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

