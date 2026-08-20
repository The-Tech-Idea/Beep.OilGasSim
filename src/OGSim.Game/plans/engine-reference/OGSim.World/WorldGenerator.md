# WorldGenerator

Source: `src\OGSim.World\WorldGenerator.cs` · Lines: 590

## File intent

> R15.1 / R15.5 / R15.9 — the generation pipeline (SDD-010 §2–4).
> 
> THE HANDOFF IS TYPED. The generator's only output channel is IWorldSink, so it
> never sees a module store and truth never travels sideways. That is what makes
> the slot moddable (design 03 §3.2) without opening the truth wall — a
> third-party generator can produce a world and still cannot reach into
> anybody's beliefs.
> 

## Namespaces

- `OGSim.World`

## Type declarations

- `L28` `public sealed class BasinWorldGenerator : IWorldGenerator`

## Accessible members

- `L33` `private const double RegionalSigmaLog = 1.2;`
- `L41` `private const double ChargeFractionOfCapacity = 0.35;`
- `L43` `private const double MinimumPorosity = 0.12;`
- `L45` `private const double PorositySpread = 0.15;`
- `L47` `public ContentId Id { get; } = new("basin-generator");`
- `L49` `public void Generate(WorldParameters parameters, IWorldSink sink, IRandomStream worldGen)`
- `L83` `private static IReadOnlyList<GeneratedAccumulation> GenerateGeology(`
- `L204` `private static IReadOnlyList<Closure> MigrationOrder(IReadOnlyList<Closure> closures)`
- `L217` `private static double CapacityOf(Closure closure, double porosity) =>`
- `L220` `private static double TotalCapacity(IReadOnlyList<Closure> closures, double porosity)`
- `L233` `private static AccessRequirements AccessFor(Length depth, WaterDepthClass water) =>`
- `L253` `private static WaterDepthClass WaterDepthAt(GeneratedTerrain terrain, int cell)`
- `L278` `private static Polygon FootprintOf(Closure closure, int cell, int width)`
- `L295` `private const double CellSizeMetres = 1000.0;`
- `L299` `private const double ShallowestHorizonMetres = 1200.0;`
- `L303` `private const double HorizonReliefMetres = 3500.0;`
- `L310` `private const double MinimumClosureHeightMetres = 20.0;`
- `L314` `private static GeneratedSurface GenerateSurface(`
- `L359` `private static IReadOnlyList<Harbour> PlaceHarbours(GeneratedTerrain terrain)`
- `L409` `private static IReadOnlyList<Settlement> PlaceSettlements(`
- `L448` `private static bool TooClose(`
- `L466` `private const double MinimumSpacingCells = 5.0;`
- `L468` `private const int MaxSettlements = 8;`
- `L471` `private const double FirstTownLogPopulation = 11.0;`
- `L473` `private const double RankDecay = 0.55;`
- `L475` `private const double PopulationSigmaLog = 0.35;`
- `L477` `private static Jurisdiction GenerateJurisdiction(`
- `L505` `private static void DeliverRegionalData(`
- `L550` `private static void Validate(WorldParameters parameters)`
- `L577` `private static Polygon SquareAround(int index)`
- `L588` `private static string Format(double value) =>`

## Imports

- `using System.Collections.Immutable;`
- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

