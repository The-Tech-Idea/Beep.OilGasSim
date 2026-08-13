// R20d.12 — the walk that turns nine state owners into a save (SDD-013 §1–§2,
// §6, finding 188).
//
// EVERY PART OF THIS EXISTED AND NOTHING ASSEMBLED THEM. `StateBlock` captured
// an owner, `CanonicalJson` wrote the bytes, `SaveFile` digested and refused,
// `MigrationChain` migrated, `StateRegistry.Owners` returned owners in
// state-key order for exactly this walk, `IRandomStream` carried Position and
// Seek "saved and restored exactly", and `SimulationClock.RestoreTo` said it
// was for load. Each had a unit test. The composition of them had nothing,
// which is the one thing a unit test cannot stand in for — so every
// `IStateOwner` in this engine was verified by itself alone, including the
// imported-water history souring depends on and the monitoring kits that decide
// which maintenance strategy a company can play.
//
// LOADING COMPOSES A NEW ENGINE (SDD-017 §1b, PV2). Restoring into a live one
// would mutate a graph whose dependencies were wired against the old values —
// "restored as a value, not as a live dependency", which is the failure class
// SDD-013 §4 opens with. A loaded game is a freshly composed engine that has
// been told what it was, and the seed comes from the SAVE rather than from the
// caller: a host asked to supply the seed of a game it has not opened yet would
// be guessing.
//
// THE AUDIT SIDECAR IS NOT HERE. §1's container has four entries and this
// writes two — the manifest and the state blocks. The trail has its own
// retention policy (design 09 §4.4) and its own open item (S013-4); shipping
// the half that carries the GAME and saying so beats shipping neither.

using System.IO.Compression;
using OGSim.Kernel;
using OGSim.Persistence;

namespace OGSim.Composition;

/// <summary>
/// Writes and reads SDD-013 §1's container for a composed engine.
///
/// <para>Against composition's <see cref="Engine"/> rather than
/// <see cref="OGSim.Contracts.IEngine"/>, deliberately: that interface's
/// <c>ReadModel</c> is SDD-017 §2's fifteen-projection record and eleven of
/// those projections have no source until R20d wires their subsystems in, so
/// implementing it today would mean fabricating them. A save needs none of it
/// (finding 188's two halves, separated at R20d.12.0).</para>
/// </summary>
public static class SaveGame
{
    /// <summary>
    /// The container's own version, which is not any owner's schema version:
    /// blocks carry theirs individually (<see cref="StateBlock"/> stamps it), and
    /// this one moves when the CONTAINER's shape changes.
    /// </summary>
    public const int SchemaVersion = 1;

    /// <summary>
    /// The eight named streams (SDD-001 §4), listed rather than reflected over,
    /// so the header's field order is a property of this file and adding a
    /// stream is a visible edit here.
    /// </summary>
    private static readonly StreamId[] Streams =
    [
        StreamId.WorldGen, StreamId.Exploration, StreamId.Measurement, StreamId.Hazard,
        StreamId.Weather, StreamId.Price, StreamId.Market, StreamId.Operations,
    ];

    /// <summary>
    /// Declared, not stamped. There is no release process to read a version from
    /// yet, and a save that refused to name one would be worse than a save that
    /// names an honest constant — S013-3 replaces both the moment there is a
    /// pipeline to ask.
    /// </summary>
    private const string EngineVersion = "0.20d.12";

    private const string ContentVersion = "shipped";

    private const string ManifestEntry = "manifest.json";

    private const string StatePrefix = "state/";

    private const string StateSuffix = ".json";

    // ------------------------------------------------------------- writing

    /// <summary>
    /// Writes the whole engine to a container.
    ///
    /// <para>The stream is left OPEN — the caller owns it, which is R19 §5's
    /// split: the engine owns the payload and the host owns slots, paths and
    /// file I/O.</para>
    /// </summary>
    public static void Write(Engine engine, ulong worldSeed, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(destination);

        IReadOnlyList<ModuleBlock> blocks = Capture(engine);

        (IReadOnlyDictionary<string, string> perModule, string state) = SaveFile.Digest(blocks);

        var header = new SaveHeader(
            SchemaVersion,
            EngineVersion,
            ContentVersion,

            // EMPTY AND HONESTLY SO: there is no mod system, so there are no
            // active mods to name. A load checks this list against what is
            // installed and an empty list checks out trivially, which is the
            // right answer rather than an absent one.
            ActiveMods: [],

            worldSeed,
            engine.Pipeline.CurrentTick,
            RngPositions(engine),
            perModule,
            state);

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        WriteEntry(archive, ManifestEntry, CanonicalJson.Write(Manifest(header)));

        for (int i = 0; i < blocks.Count; i++)
            WriteEntry(archive,
                StatePrefix + blocks[i].Module + StateSuffix,
                CanonicalJson.Write(blocks[i].State));
    }

    /// <summary>
    /// Every owner, in state-key order — which <see cref="StateRegistry.Owners"/>
    /// guarantees precisely so that composing modules in a different order cannot
    /// change a byte of the save.
    /// </summary>
    private static IReadOnlyList<ModuleBlock> Capture(Engine engine)
    {
        IReadOnlyList<IStateOwner> owners = engine.State.Owners;
        var blocks = new List<ModuleBlock>(owners.Count);

        for (int i = 0; i < owners.Count; i++)
            blocks.Add(new ModuleBlock(
                owners[i].Key.Value, StateBlock.Capture(owners[i]).Written()));

        return blocks;
    }

    private static IReadOnlyDictionary<string, ulong> RngPositions(Engine engine)
    {
        var source = engine.Provided.Resolve<IRandomSource>();
        var positions = new Dictionary<string, ulong>(StringComparer.Ordinal);

        for (int i = 0; i < Streams.Length; i++)
            positions.Add(Streams[i].ToString(), source.Stream(Streams[i]).Position);

        return positions;
    }

    private static void WriteEntry(ZipArchive archive, string name, string text)
    {
        // No compression level choice and no timestamps: a ZIP entry carries a
        // last-write time, and writing a real one would make two saves of one
        // game differ byte for byte (D-6 bans the clock outright). The default
        // entry time is whatever the BCL stamps, so the DIGEST rather than the
        // file is what PV1 compares — which is what §2 specifies anyway.
        using Stream entry = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        using var writer = new StreamWriter(entry, new System.Text.UTF8Encoding(false));

        writer.Write(text);
    }

    // ------------------------------------------------------------- reading

    /// <summary>
    /// Reads and VALIDATES a container without composing anything (SDD-013 §6).
    ///
    /// <para>Separate from <see cref="Load"/> because a host wants to show a save
    /// list — tick, seed, whether it will open — without building an engine for
    /// each row.</para>
    /// </summary>
    public static LoadResult Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        ZipArchiveEntry? manifest = archive.GetEntry(ManifestEntry);

        if (manifest is null)
            return new Refused([$"the container has no {ManifestEntry}"]);

        SaveHeader header = HeaderFrom(CanonicalJson.Read(Text(manifest)));

        // ORDINAL ORDER, not the archive's. A ZIP lists entries in whatever
        // order they were written, and a digest computed over that order would
        // depend on the writer rather than on the state (D-5).
        var names = new List<string>();
        for (int i = 0; i < archive.Entries.Count; i++)
        {
            string name = archive.Entries[i].FullName;

            if (name.StartsWith(StatePrefix, StringComparison.Ordinal)
                && name.EndsWith(StateSuffix, StringComparison.Ordinal))
                names.Add(name);
        }

        names.Sort(StringComparer.Ordinal);

        var blocks = new List<ModuleBlock>(names.Count);

        for (int i = 0; i < names.Count; i++)
        {
            string module = names[i][StatePrefix.Length..^StateSuffix.Length];
            blocks.Add(new ModuleBlock(module, CanonicalJson.Read(Text(archive.GetEntry(names[i])!))));
        }

        return SaveFile.Validate(header, blocks, SchemaVersion, installedMods: []);
    }

    // THERE IS NO Load HERE YET, AND THE REASON IS A DESIGN GAP RATHER THAN
    // UNWRITTEN CODE (finding 194).
    //
    // The walk that reads a container back into an engine was written, run, and
    // found three defects in two owners — two of them fixed here (findings 192
    // and 193) and the third structural. `WellsState.Restore` says in its own
    // documentation that "the completions themselves are rebuilt from content
    // first and handed to Open; this checks the save agrees with what was
    // rebuilt". **Nothing rebuilds them.** A loaded engine composes an empty
    // field, and which wells a company drilled is not content — it is the whole
    // history of the game, recorded in the save as ids and treated on the way
    // back in as a checksum against a rebuild that does not happen.
    //
    // So a save can be WRITTEN — every owner captured, digested and validated,
    // which is what this file does and what the tests exercise — and cannot yet
    // be read back into a running field. Shipping a `Load` that throws on every
    // real save would be shipping a member that cannot do its job (L3), and
    // shipping one that silently produced a field with no wells would be worse.
    // The rebuild step, and where it belongs, is R20d.12's remaining half.

    // ------------------------------------------------------- the manifest

    private static JsonValue Manifest(SaveHeader header)
    {
        var mods = new List<JsonValue>(header.ActiveMods.Count);

        for (int i = 0; i < header.ActiveMods.Count; i++)
            mods.Add(new JsonObject(new Dictionary<string, JsonValue>(StringComparer.Ordinal)
            {
                ["id"] = new JsonString(header.ActiveMods[i].Id),
                ["version"] = new JsonString(header.ActiveMods[i].Version),
                ["order"] = new JsonInteger(header.ActiveMods[i].Order),
            }));

        return new JsonObject(new Dictionary<string, JsonValue>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = new JsonInteger(header.SchemaVersion),
            ["engineVersion"] = new JsonString(header.EngineVersion),
            ["contentVersion"] = new JsonString(header.ContentVersion),
            ["activeMods"] = new JsonArray(mods),
            ["worldSeed"] = new JsonString(Text(header.WorldSeed)),
            ["tick"] = new JsonInteger(header.Tick.Value),
            ["rngPositions"] = Map(header.RngPositions),
            ["moduleDigests"] = Map(header.ModuleDigests),
            ["stateDigest"] = new JsonString(header.StateDigest),
        });
    }

    private static SaveHeader HeaderFrom(JsonValue value)
    {
        JsonObject json = Object(value, "the manifest");

        var mods = new List<ModReference>();
        JsonArray active = Array(Member(json, "activeMods"), "activeMods");

        for (int i = 0; i < active.Items.Count; i++)
        {
            JsonObject mod = Object(active.Items[i], "an activeMods entry");

            mods.Add(new ModReference(
                String(Member(mod, "id"), "id"),
                String(Member(mod, "version"), "version"),
                (int)Integer(Member(mod, "order"), "order")));
        }

        return new SaveHeader(
            (int)Integer(Member(json, "schemaVersion"), "schemaVersion"),
            String(Member(json, "engineVersion"), "engineVersion"),
            String(Member(json, "contentVersion"), "contentVersion"),
            mods,
            Seed(String(Member(json, "worldSeed"), "worldSeed")),
            new Tick((int)Integer(Member(json, "tick"), "tick")),
            Positions(Member(json, "rngPositions")),
            Digests(Member(json, "moduleDigests")),
            String(Member(json, "stateDigest"), "stateDigest"));
    }

    /// <summary>
    /// A STRING, because a seed is a <c>ulong</c> and the canonical form's
    /// integer is a <c>long</c> — a seed past <c>long.MaxValue</c> would round
    /// trip as a negative number and silently open a different world.
    /// </summary>
    private static string Text(ulong value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static ulong Seed(string text) =>
        ulong.TryParse(text, System.Globalization.NumberStyles.None,
                       System.Globalization.CultureInfo.InvariantCulture, out ulong seed)
            ? seed
            : throw new SaveDataFault("SDD-013 §2", null,
                $"the manifest's world seed '{text}' is not a number");

    private static JsonValue Map(IReadOnlyDictionary<string, ulong> values)
    {
        var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, ulong> pair in values)
            members[pair.Key] = new JsonString(Text(pair.Value));

        return new JsonObject(members);
    }

    private static JsonValue Map(IReadOnlyDictionary<string, string> values)
    {
        var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> pair in values)
            members[pair.Key] = new JsonString(pair.Value);

        return new JsonObject(members);
    }

    private static IReadOnlyDictionary<string, ulong> Positions(JsonValue value)
    {
        JsonObject json = Object(value, "rngPositions");
        var positions = new Dictionary<string, ulong>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonValue> pair in json.Members)
            positions[pair.Key] = Seed(String(pair.Value, pair.Key));

        return positions;
    }

    private static IReadOnlyDictionary<string, string> Digests(JsonValue value)
    {
        JsonObject json = Object(value, "moduleDigests");
        var digests = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonValue> pair in json.Members)
            digests[pair.Key] = String(pair.Value, pair.Key);

        return digests;
    }

    private static string Text(ZipArchiveEntry entry)
    {
        using Stream open = entry.Open();
        using var reader = new StreamReader(open, new System.Text.UTF8Encoding(false));

        return reader.ReadToEnd();
    }

    // Every accessor names the field it failed on, because "the save is corrupt"
    // tells a player nothing they can act on (SDD-013 §6).

    private static JsonValue Member(JsonObject json, string key) =>
        json.Members.TryGetValue(key, out JsonValue? value)
            ? value
            : throw new SaveDataFault("SDD-013 §2", null,
                $"the manifest has no '{key}'");

    private static JsonObject Object(JsonValue value, string what) =>
        value as JsonObject
        ?? throw new SaveDataFault("SDD-013 §2", null, $"{what} is not an object");

    private static JsonArray Array(JsonValue value, string what) =>
        value as JsonArray
        ?? throw new SaveDataFault("SDD-013 §2", null, $"{what} is not an array");

    private static string String(JsonValue value, string what) =>
        (value as JsonString)?.Value
        ?? throw new SaveDataFault("SDD-013 §2", null, $"'{what}' is not a string");

    private static long Integer(JsonValue value, string what) =>
        (value as JsonInteger)?.Value
        ?? throw new SaveDataFault("SDD-013 §2", null, $"'{what}' is not an integer");
}
