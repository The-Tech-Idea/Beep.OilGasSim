# StepStreams

Source: `src\OGSim.World\StepStreams.cs` · Lines: 107

## File intent

> R15.1 — per-step substreams (SDD-010 §1).
> 
> EDITING STEP 7 MUST NOT SHIFT STEP 9's DRAWS. That is the stream-independence
> principle of design 11 §3.1 applied INSIDE world generation, and it is what
> makes PV7 — regeneration identity — survive development rather than only
> survive a single build.
> 
> Without it, adding one draw to the charge step would change every settlement,

## Namespaces

- `OGSim.World`

## Type declarations

- `L24` `public static class WorldStep`
- `L49` `public sealed class StepStreams`

## Accessible members

- `L26` `public const string Tectonic = "tectonic";`
- `L27` `public const string Stratigraphy = "stratigraphy";`
- `L28` `public const string BurialThermal = "burial-thermal";`
- `L29` `public const string Structure = "structure";`
- `L30` `public const string Traps = "traps";`
- `L31` `public const string Charge = "charge";`
- `L32` `public const string Accumulations = "accumulations";`
- `L33` `public const string PlaysAndClasses = "plays-and-classes";`
- `L34` `public const string Surface = "surface";`
- `L35` `public const string Jurisdictions = "jurisdictions";`
- `L36` `public const string RegionalData = "regional-data";`
- `L39` `public static IReadOnlyList<string> InOrder { get; } =`
- `L51` `private readonly ulong _worldSeed;`
- `L52` `private readonly Dictionary<string, IRandomStream> _streams = [];`
- `L54` `public StepStreams(ulong worldSeed) => _worldSeed = worldSeed;`
- `L61` `public IRandomStream For(string stepName)`
- `L82` `internal static ulong SplitMix64(ulong z)`
- `L93` `internal static ulong Hash(string name)`

## Imports

- `using OGSim.Kernel;`

