# CompanyContracts

Source: `src\OGSim.Contracts\CompanyContracts.cs` · Lines: 60

## File intent

> SDD-011 — companies, licences and rivals.
> 
> ILicence lives HERE as of R16, having sat in InformationContracts.cs since the
> contract layer. SDD-011 §1 records why it was there — IWell.Licence needs it
> from R6, and Information was the file that existed — and records that moving
> it is an R16 task: the type was right and only its address was wrong, and a
> public type is worth moving where the tests that cover it are being written.
> <summary>

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L18` `public interface ILicence`
- `L26` `public sealed record CommitmentItem(`
- `L32` `public sealed record RelinquishmentStep(double Fraction, Tick Due);`
- `L42` `public sealed record LicenceTerms(`

## Accessible members

- `L51` `public bool Equals(LicenceTerms? other) =>`
- `L57` `public override int GetHashCode() =>`

## Imports

- `using OGSim.Kernel;`

