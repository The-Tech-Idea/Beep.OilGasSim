# Streams

Source: `src\OGSim.Kernel\Streams.cs` · Lines: 477

## File intent

> SDD-002 §2–4 — the one currency every module trades in.
> 
> R2.5/R2.6 implement the algebra the contract stage declared as data shapes.
> Two properties do all the work downstream: mass is what the engine conserves
> (04 §2.1), so the arithmetic here is the arithmetic INV1 checks; and
> provenance rides along with it, so allocation, royalty and depletion never
> need a separate subsystem to reconstruct where a barrel came from.
> <summary>Catalogue-ordinal material index. Ordinals NEVER persist (SDD-004 §6).</summary>

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L14` `public readonly record struct MaterialId(int Ordinal);`
- `L25` `public readonly record struct Composition(ImmutableArray<double> KgPerSecondByOrdinal)`
- `L180` `public readonly record struct Allocation(`
- `L317` `public readonly record struct MaterialStream(`
- `L350` `public readonly record struct MaterialInventory(ImmutableArray<double> KilogramsByOrdinal)`

## Accessible members

- `L30` `public MassRate this[MaterialId material]`
- `L43` `public int Length => Guarded().Length;`
- `L45` `public MassRate Total`
- `L64` `public static Composition Validated(ImmutableArray<double> kgPerSecondByOrdinal)`
- `L83` `public static Composition Zero(int materialCount) =>`
- `L89` `public Composition Plus(in Composition other)`
- `L109` `public Composition Scaled(double factor)`
- `L127` `public (Composition A, Composition B) Split(double fractionToA)`
- `L147` `public bool Equals(Composition other)`
- `L157` `public override int GetHashCode()`
- `L165` `private ImmutableArray<double> Guarded() =>`
- `L183` `private const double SumTolerance = 1e-12;`
- `L184` `private const double RenormalisationLimit = 1e-9;`
- `L192` `public static Allocation Validated(ImmutableArray<(EntityRef Compartment, double Fraction)> shares)`
- `L226` `public static Allocation FromSingle(EntityRef compartment) =>`
- `L234` `public static Allocation Blend(ReadOnlySpan<(Allocation Part, Mass Weight)> parts)`
- `L281` `public bool Equals(Allocation other)`
- `L292` `public override int GetHashCode()`
- `L304` `private ImmutableArray<(EntityRef Compartment, double Fraction)> Guarded() =>`
- `L328` `public (MaterialStream A, MaterialStream B) Split(double fractionToA)`
- `L355` `public bool Equals(MaterialInventory other) =>`
- `L358` `public override int GetHashCode() => Structural.HashOf(KilogramsByOrdinal);`
- `L360` `public static MaterialInventory Empty(int materialCount) =>`
- `L363` `public static MaterialInventory Of(params double[] kilogramsByOrdinal)`
- `L385` `public static MaterialInventory From(Composition rate, Duration duration)`
- `L399` `public int MaterialCount => KilogramsByOrdinal.Length;`
- `L401` `public Mass this[MaterialId material] => new(KilogramsByOrdinal[material.Ordinal]);`
- `L403` `public Mass Total`
- `L413` `public MaterialInventory Plus(MaterialInventory other)`
- `L430` `public (MaterialInventory Taken, MaterialInventory Left) Split(double fraction)`
- `L452` `public MaterialInventory Less(MaterialInventory taken)`

## Imports

- `using System.Collections.Immutable;`

