# SaveFile

Source: `src\OGSim.Persistence\SaveFile.cs` · Lines: 265

## File intent

> R19.1 / R19.2 / R19.3 — the save header, digests and refusals
> (SDD-013 §2, §5, §6).
> 
> NO PARTIAL LOAD EXISTS AS A CODE PATH. LoadResult is Loaded | Refused, the
> same shape the content loader has, and for the same reason: a half-loaded
> game is a game whose failure surfaces fifty ticks later as an inexplicable
> number. Every refusal names what is wrong SPECIFICALLY — the module whose
> digest diverged, the mod and version that is missing, the entry that was

## Namespaces

- `OGSim.Persistence`

## Type declarations

- `L23` `public sealed record SaveHeader(`
- `L51` `public sealed record ModReference(string Id, string Version, int Order);`
- `L54` `public sealed record ModuleBlock(string Module, JsonValue State);`
- `L56` `public abstract record LoadResult;`
- `L58` `public sealed record Loaded(SaveHeader Header, IReadOnlyList<ModuleBlock> Blocks) : LoadResult`
- `L68` `public sealed record Refused(IReadOnlyList<string> Reasons) : LoadResult`
- `L80` `public static class SaveFile`
- `L202` `public interface IMigrationStep`
- `L216` `public sealed class MigrationChain`

## Accessible members

- `L35` `public bool Equals(SaveHeader? other) =>`
- `L45` `public override int GetHashCode() =>`
- `L61` `public bool Equals(Loaded? other) =>`
- `L64` `public override int GetHashCode() => HashCode.Combine(Header, Structural.HashOf(Blocks));`
- `L71` `public bool Equals(Refused? other) =>`
- `L74` `public override int GetHashCode() => Structural.HashOf(Reasons);`
- `L90` `public static (IReadOnlyDictionary<string, string> PerModule, string State) Digest(`
- `L125` `public static LoadResult Validate(`
- `L190` `private static List<string> OrderedKeys(IReadOnlyDictionary<string, string> map)`
- `L197` `private static string Sha256(string text) =>`
- `L218` `private readonly Dictionary<int, IMigrationStep> _steps = [];`
- `L220` `public MigrationChain(IReadOnlyList<IMigrationStep> steps, int oldestSupported, int current)`
- `L245` `public int OldestSupported { get; }`
- `L246` `public int Current { get; }`
- `L249` `public JsonValue Migrate(JsonValue block, string module, int from)`

## Imports

- `using System.Security.Cryptography;`
- `using System.Text;`
- `using OGSim.Kernel;`

