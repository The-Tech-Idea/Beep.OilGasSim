# Spatial

Source: `src\OGSim.Kernel\Spatial.cs` · Lines: 271

## File intent

> SDD-001 §1.4 — spatial primitives (concept G17). Fictional basins on a flat
> plane (open decision W1): no geodesy, ever. R1.1 implements the algorithms the
> contract stage declared: shoelace area, ray-cast containment, conservative
> overlap. Network distance is NOT here — it lives on the generated transport
> graph (design 06 §5.1a step 9.5) and belongs to OGSim.World.
> 
> Every algorithm is pure +, -, * on doubles with the vertex order fixed by
> construction, so all of it is D-1 safe and bit-identical across platforms.

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L18` `public readonly record struct Coordinate(double X, double Y);`
- `L24` `public readonly record struct Polygon`
- `L263` `public static class Distances`

## Accessible members

- `L26` `public ImmutableArray<Coordinate> Vertices { get; }`
- `L28` `public Polygon(ImmutableArray<Coordinate> vertices)`
- `L82` `public bool Equals(Polygon other) => Structural.Equal(Vertices, other.Vertices);`
- `L84` `public override int GetHashCode() => Structural.HashOf(Vertices);`
- `L87` `public Area Area => new(TwiceSignedArea(Guarded()) * 0.5);`
- `L90` `public Coordinate Centroid`
- `L116` `public bool Contains(Coordinate point)`
- `L139` `public bool Overlaps(in Polygon other)`
- `L171` `private ImmutableArray<Coordinate> Guarded() =>`
- `L177` `private static double TwiceSignedArea(ImmutableArray<Coordinate> vertices)`
- `L189` `private static void Bounds(`
- `L208` `private static double Cross(Coordinate o, Coordinate a, Coordinate b) =>`
- `L211` `private static bool SegmentsIntersect(Coordinate a0, Coordinate a1, Coordinate b0, Coordinate b1)`
- `L230` `private static bool OnSegment(Coordinate from, Coordinate to, Coordinate point) =>`
- `L236` `private static bool HasSelfIntersection(ImmutableArray<Coordinate> vertices)`
- `L265` `public static Length Euclidean(Coordinate a, Coordinate b)`

## Imports

- `using System.Collections.Immutable;`

