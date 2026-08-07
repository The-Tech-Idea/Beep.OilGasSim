// R19.1 / R19.2 / R19.3 — the save header, digests and refusals
// (SDD-013 §2, §5, §6).
//
// NO PARTIAL LOAD EXISTS AS A CODE PATH. LoadResult is Loaded | Refused, the
// same shape the content loader has, and for the same reason: a half-loaded
// game is a game whose failure surfaces fifty ticks later as an inexplicable
// number. Every refusal names what is wrong SPECIFICALLY — the module whose
// digest diverged, the mod and version that is missing, the entry that was
// truncated — because "the save is corrupt" tells a player nothing they can act
// on.
//
// PER-MODULE DIGESTS LOCALISE DIVERGENCE. A single whole-file digest would tell
// you that two platforms disagreed; per-module digests tell you WHICH module,
// which is the difference between a bug report and an investigation.

using System.Security.Cryptography;
using System.Text;
using OGSim.Kernel;

namespace OGSim.Persistence;

/// <summary>SDD-013 §2's header.</summary>
public sealed record SaveHeader(
    int SchemaVersion,
    string EngineVersion,
    string ContentVersion,
    IReadOnlyList<ModReference> ActiveMods,
    ulong WorldSeed,
    Tick Tick,
    IReadOnlyDictionary<string, ulong> RngPositions,
    IReadOnlyDictionary<string, string> ModuleDigests,
    string StateDigest);

public sealed record ModReference(string Id, string Version, int Order);

/// <summary>One module's canonical block.</summary>
public sealed record ModuleBlock(string Module, JsonValue State);

public abstract record LoadResult;

public sealed record Loaded(SaveHeader Header, IReadOnlyList<ModuleBlock> Blocks) : LoadResult;

/// <summary>Every reason, named specifically. Never "the save is corrupt".</summary>
public sealed record Refused(IReadOnlyList<string> Reasons) : LoadResult;

/// <summary>
/// SDD-013 §2's digesting and §6's refusal.
/// </summary>
public static class SaveFile
{
    /// <summary>
    /// Per-module SHA-256 over the canonical bytes, plus a state digest over the
    /// module digests concatenated in MODULE-NAME order.
    ///
    /// <para>Name order rather than registration order: registration order is a
    /// composition detail that could legitimately change between builds, and a
    /// digest that moved with it would report divergence where there was none.</para>
    /// </summary>
    public static (IReadOnlyDictionary<string, string> PerModule, string State) Digest(
        IReadOnlyList<ModuleBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var ordered = new List<ModuleBlock>(blocks);
        ordered.Sort((a, b) => string.CompareOrdinal(a.Module, b.Module));

        var perModule = new Dictionary<string, string>(StringComparer.Ordinal);
        var combined = new StringBuilder();

        for (int i = 0; i < ordered.Count; i++)
        {
            if (perModule.ContainsKey(ordered[i].Module))
                throw new SaveDataFault("SDD-013 §2", null,
                    $"module {ordered[i].Module} has two state blocks; a module owns one " +
                    "block and two would make its digest ambiguous");

            string digest = Sha256(CanonicalJson.Write(ordered[i].State));

            perModule.Add(ordered[i].Module, digest);
            combined.Append(ordered[i].Module).Append(':').Append(digest).Append(';');
        }

        return (perModule, Sha256(combined.ToString()));
    }

    /// <summary>
    /// SDD-013 §6. Validates a save and returns <see cref="Loaded"/> or every
    /// reason it was refused.
    ///
    /// <para>ALL reasons at once, like every other refusal in the engine: a
    /// player who fixes a missing mod only to discover a version mismatch has
    /// been made to pay twice for one piece of information.</para>
    /// </summary>
    public static LoadResult Validate(
        SaveHeader header,
        IReadOnlyList<ModuleBlock> blocks,
        int supportedSchemaVersion,
        IReadOnlyList<ModReference> installedMods)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(installedMods);

        var reasons = new List<string>();

        // A save from the FUTURE is refused with both versions named. Migrating
        // forward is possible; migrating backward means guessing what a later
        // version meant, and a wrong guess corrupts a campaign silently.
        if (header.SchemaVersion > supportedSchemaVersion)
            reasons.Add(
                $"save schema version {header.SchemaVersion} is newer than this engine's " +
                $"{supportedSchemaVersion}");

        for (int i = 0; i < header.ActiveMods.Count; i++)
        {
            ModReference required = header.ActiveMods[i];
            ModReference? found = null;

            for (int j = 0; j < installedMods.Count; j++)
                if (string.Equals(installedMods[j].Id, required.Id, StringComparison.Ordinal))
                    found = installedMods[j];

            if (found is null)
                reasons.Add($"mod {required.Id} version {required.Version} is not installed");
            else if (!string.Equals(found.Version, required.Version, StringComparison.Ordinal))
                reasons.Add(
                    $"mod {required.Id} is version {found.Version}; the save needs " +
                    $"{required.Version}");
        }

        // Digests: recomputed and compared PER MODULE, so a mismatch names the
        // module rather than the file.
        (IReadOnlyDictionary<string, string> perModule, string state) = Digest(blocks);

        foreach (string module in OrderedKeys(header.ModuleDigests))
        {
            if (!perModule.TryGetValue(module, out string? actual))
            {
                reasons.Add($"module {module} is in the header but has no state block");
                continue;
            }

            if (!string.Equals(actual, header.ModuleDigests[module], StringComparison.Ordinal))
                reasons.Add($"module {module}'s state digest does not match the header");
        }

        foreach (string module in OrderedKeys(perModule))
            if (!header.ModuleDigests.ContainsKey(module))
                reasons.Add($"module {module} has a state block the header does not list");

        if (reasons.Count == 0 && !string.Equals(state, header.StateDigest, StringComparison.Ordinal))
            reasons.Add("the overall state digest does not match the header");

        return reasons.Count == 0 ? new Loaded(header, blocks) : new Refused(reasons);
    }

    /// <summary>Ordinal-sorted, so the refusal list reads identically every run
    /// and two machines report divergence in the same order (D-5).</summary>
    private static List<string> OrderedKeys(IReadOnlyDictionary<string, string> map)
    {
        var keys = new List<string>(map.Keys);
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }

    private static string Sha256(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

/// <summary>SDD-013 §5. One step of the migration chain.</summary>
public interface IMigrationStep
{
    int From { get; }
    JsonValue Migrate(JsonValue block, string module);
}

/// <summary>
/// SDD-013 §5's chain.
///
/// <para><b>A gap is a composition fault at startup</b>, not a load-time
/// surprise. A chain missing v3→v4 cannot migrate a v3 save, and discovering
/// that when a player opens one is discovering it at the worst possible
/// moment — the engine refuses to start instead.</para>
/// </summary>
public sealed class MigrationChain
{
    private readonly Dictionary<int, IMigrationStep> _steps = [];

    public MigrationChain(IReadOnlyList<IMigrationStep> steps, int oldestSupported, int current)
    {
        ArgumentNullException.ThrowIfNull(steps);

        for (int i = 0; i < steps.Count; i++)
            if (!_steps.TryAdd(steps[i].From, steps[i]))
                throw new SaveDataFault("SDD-013 §5", null,
                    $"two migration steps claim to start from version {steps[i].From}; " +
                    "the chain would be ambiguous");

        var gaps = new List<int>();
        for (int version = oldestSupported; version < current; version++)
            if (!_steps.ContainsKey(version)) gaps.Add(version);

        if (gaps.Count > 0)
            throw new SaveDataFault("SDD-013 §5", null,
                "the migration chain has gaps at version(s) " +
                string.Join(", ", gaps) +
                $"; saves between {oldestSupported} and {current} could not be loaded, and " +
                "finding that out when a player opens one is the worst possible moment");

        OldestSupported = oldestSupported;
        Current = current;
    }

    public int OldestSupported { get; }
    public int Current { get; }

    /// <summary>Runs the chain from a block's version up to current.</summary>
    public JsonValue Migrate(JsonValue block, string module, int from)
    {
        if (from > Current)
            throw new SaveDataFault("SDD-013 §5", null,
                $"block version {from} is newer than the current {Current}");

        if (from < OldestSupported)
            throw new SaveDataFault("SDD-013 §5", null,
                $"block version {from} is older than the oldest supported {OldestSupported}");

        JsonValue migrated = block;
        for (int version = from; version < Current; version++)
            migrated = _steps[version].Migrate(migrated, module);

        return migrated;
    }
}
