# Licence

Source: `src\OGSim.Company\Licence.cs` · Lines: 181

## File intent

> R16.2 / R16.3 — licences, commitments and rounds (SDD-011 §1).
> 
> A licence is a CLOCK and a PROMISE. The clock is the term and the
> relinquishment schedule; the promise is the work commitment, and failing it
> forfeits the bond and loses the acreage. Commitment tracking is mechanical —
> qualifying operations decrement items, and at the deadline what is left
> undone is what costs.
> 

## Namespaces

- `OGSim.Company`

## Type declarations

- `L20` `public sealed record CommitmentProgress(CommitmentItem Item, double Delivered)`
- `L28` `public sealed record CommitmentAssessment(`
- `L43` `public sealed class Licence : ILicence`

## Accessible members

- `L22` `public double Outstanding => Math.Max(0.0, Item.Quantity - Delivered);`
- `L24` `public bool Met => Outstanding <= 0.0;`
- `L34` `public bool Equals(CommitmentAssessment? other) =>`
- `L38` `public override int GetHashCode() =>`
- `L45` `private readonly List<CommitmentProgress> _progress = [];`
- `L47` `public Licence(EntityId<ILicence> id, LicenceTerms terms, Tick granted)`
- `L61` `public EntityId<ILicence> Id { get; }`
- `L62` `public LicenceTerms Terms { get; }`
- `L63` `public Tick Granted { get; }`
- `L64` `public Tick Expiry { get; }`
- `L66` `public ContentId FiscalRegime => Terms.FiscalRegime;`
- `L68` `public IReadOnlyList<CommitmentProgress> Progress => _progress;`
- `L71` `public bool IsLive { get; private set; } = true;`
- `L82` `public void RecordDelivery(ContentId kind, double quantity)`
- `L106` `public CommitmentAssessment AssessAt(Tick now)`
- `L133` `public double RelinquishedFractionDueBy(Tick now)`
- `L146` `public bool HasExpiredBy(Tick now) => now.Value >= Expiry.Value;`
- `L148` `private static void Validate(LicenceTerms terms)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

