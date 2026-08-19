# Structure

Source: `src\OGSim.World\Structure.cs` · Lines: 295

## File intent

> R15.4 / R15.5 — the structural horizon and the traps on it (SDD-010 §2 steps 4–5).
> 
> WHY A SURFACE AND NOT A DRAW. Until this, a trap's size was a log-normal number
> and its position was a cell picked at random, and the two had nothing to do
> with each other. That is a distribution, not a geology: it cannot say why a
> trap is where it is, it cannot produce two traps that share a spill point, and
> it cannot tell a broad gentle closure from a small sharp one — which is the
> difference between a field worth developing and a field worth drilling once.

## Namespaces

- `OGSim.World`

## Type declarations

- `L29` `internal sealed class StructuralHorizon`
- `L294` `internal sealed record Closure(`

## Accessible members

- `L31` `private readonly double[] _depth;      // metres below datum; larger is deeper`
- `L32` `private readonly int _width;`
- `L33` `private readonly int _height;`
- `L41` `internal StructuralHorizon(int width, int height, ulong seed, double crestDepth, double relief)`
- `L60` `internal double DepthAt(int cell) => _depth[cell];`
- `L62` `internal int Width => _width;`
- `L64` `internal int Count => _depth.Length;`
- `L77` `internal IReadOnlyList<Closure> Traps(double minimumClosureHeight)`
- `L95` `private bool IsCrest(int cell)`
- `L120` `private Closure? Grow(int crest, double minimumClosureHeight)`
- `L182` `private void Consider(int cell, double level, HashSet<int> seen, List<int> next)`
- `L198` `private static double Octaves(int x, int y, ulong seed)`
- `L219` `private static double Value(double x, double y, ulong seed)`
- `L233` `private static double Fade(double t) => t * t * (3.0 - (2.0 * t));`
- `L235` `private static double Lerp(double a, double b, double t) => a + ((b - a) * t);`
- `L243` `private static double Lattice(int x, int y, ulong seed)`
- `L254` `private static ulong Mix(ulong z)`
- `L265` `private const double BaseWavelength = 12.0;`
- `L267` `private const int OctaveCount = 4;`
- `L269` `private const double AmplitudeFalloff = 0.5;`
- `L274` `private const double DipShare = 0.5;`
- `L276` `private const double NoiseShare = 0.5;`
- `L280` `private const double ContourInterval = 10.0;`
- `L287` `private const double MaximumClosureHeight = 300.0;`

## Imports

- `using OGSim.Kernel;`

