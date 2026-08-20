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
using OGSim.Contracts;
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

    /// <summary>
    /// §1's audit sidecar — ONE file since R20d.12.22, because the pipeline
    /// prunes at every tick and the engine therefore holds no complete trail to
    /// write beside the retained one.
    /// </summary>
    private const string TrailEntry = "audit/trail.jsonl";

    /// <summary>LF (10), as §3 requires of every byte this writes.</summary>
    private const char Newline = (char)10;

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

            // The date tick zero was, asked of the CLOCK rather than taken from
            // a caller's settings: the clock is what turns this save's tick count
            // into months, so it is the thing that knows. Without it a reload
            // relabels the whole history from whatever epoch a host passed
            // (S013-6).
            engine.Provided.Resolve<SimulationClock>().Epoch,

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

        // THE TRAIL, and it is NOT in the digest (§1b). It is diagnostic rather
        // than simulation state: a container whose trail was truncated is still
        // a playable game, and refusing to load one would be refusing over a
        // record. Digesting it would also make PV1 depend on a file that
        // retention rewrites as the game runs.
        WriteEntry(archive, TrailEntry, Trail(engine));
    }

    /// <summary>
    /// The retained trail as JSONL — one entry per line, each the canonical form
    /// of the same flat object shape the state blocks use.
    ///
    /// <para>Line-per-entry rather than one array, because a trail is appended to
    /// and read in ranges: a reader wanting month 214 should not have to parse
    /// forty years to reach it, and a truncated file should lose its tail rather
    /// than fail to parse at all.</para>
    /// </summary>
    private static string Trail(Engine engine)
    {
        // Everything the trail still holds — retention has already decided what
        // that is (09 §4.4), and asking for a range here would be a second
        // policy competing with it.
        IReadOnlyList<AuditEntry> entries =
            engine.Audit.Query(new AuditQuery(null, null, null, null));

        var text = new System.Text.StringBuilder();

        for (int i = 0; i < entries.Count; i++)
        {
            text.Append(CanonicalJson.Write(Written(entries[i])));
            text.Append(Newline);
        }

        return text.ToString();
    }

    private static JsonValue Written(AuditEntry entry)
    {
        var data = new Dictionary<string, JsonValue>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, AuditValue> pair in entry.Data)
            data[pair.Key] = new JsonString(pair.Value.Value);

        var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal)
        {
            ["id"] = new JsonString(Text(entry.Id.Value)),
            ["tick"] = new JsonInteger(entry.Tick.Value),
            ["category"] = new JsonString(entry.Category.ToString()),
            ["data"] = new JsonObject(data),
        };

        // ABSENT RATHER THAN NULL for the two optionals: a subject-less entry and
        // one whose subject is "nothing" are different claims, and the reader
        // below distinguishes them by the key being there at all.
        if (entry.Subject is EntityRef subject)
        {
            members["subject-kind"] = new JsonString(subject.Kind.ToString());
            members["subject-id"] = new JsonString(Text(subject.Value));
        }

        if (entry.Cause is AuditId cause) members["cause"] = new JsonString(Text(cause.Value));

        return new JsonObject(members);
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

    /// <summary>
    /// Composes a new engine and restores the save into it.
    ///
    /// <para>The SEED comes from the header. Everything else in
    /// <paramref name="settings"/> is a host choice that is not a property of the
    /// saved game — where the log goes, how much audit to retain, whether faults
    /// halt — and those are the caller's to make again.</para>
    ///
    /// <para>Returns composition's own refusal if the module set will not build.
    /// A container that does not validate is <see cref="Read"/>'s answer and is
    /// asked for first, so a caller reports reasons rather than catching.</para>
    /// </summary>
    public static BuildResult Load(Stream source, EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);

        if (Read(source) is not Loaded loaded)
            throw new SaveDataFault("SDD-013 §6", null,
                "the container did not validate; call Read first and report its reasons");

        // THE SEED AND THE EPOCH ARE THE SAVED GAME'S, not the loader's. A host
        // asked to supply either for a game it has not opened yet would be
        // guessing — and getting the epoch wrong relabels the entire history
        // while leaving the simulation identical, which is the worst shape a
        // defect can take: every number right and every date wrong (S013-6).
        BuildResult built = EngineBuilder.BuildAt(
            settings with
            {
                WorldSeed = loaded.Header.WorldSeed,
                Epoch = loaded.Header.Epoch,
            },
            loaded.Header.Tick);

        if (built is not Built ready) return built;

        Restore(ready.Engine, loaded);

        // THE TRAIL, after the state. It is not an owner and not in the digest,
        // so it is restored beside the blocks rather than among them — and after
        // them, because a fresh engine records nothing while being restored and
        // the ids in the file must land in an empty trail (SDD-001 §5).
        source.Position = 0;
        RestoreTrail(ready.Engine, source);

        return ready;
    }

    /// <summary>
    /// Puts a validated save into a freshly composed engine.
    ///
    /// <para>THE ORDER IS A CONTRACT, not an implementation detail (design 11
    /// §2.1). Owners are CAPTURED in state-key order so that composing modules
    /// differently cannot change a byte of the save; they are RESTORED in
    /// dependency order, because a well's gathering line is as long as the
    /// distance from its field to the header and neither number exists until the
    /// world and the subsurface are back.</para>
    ///
    /// <para>The three phases below are that order, and each is here because
    /// something breaks without it:</para>
    ///
    /// <list type="number">
    /// <item>the world and the subsurface, which the field is measured
    /// against;</item>
    /// <item>the FIELD ITSELF, rebuilt by reopening every well the save
    /// records — the step design 11 §2.1 calls the loader and whose absence is
    /// why no save could be read back (finding 194);</item>
    /// <item>every remaining owner, including the wells' own block, which now
    /// finds the completions it is asked to check.</item>
    /// </list>
    ///
    /// <para>Obligations land in the third phase ON PURPOSE: reopening a well
    /// registers an abandonment obligation, exactly as drilling one does, and
    /// the save's own record is the one that must stand — a company that had
    /// already discharged one must not get it back by reloading.</para>
    /// </summary>
    private static void Restore(Engine engine, Loaded loaded)
    {
        Regenerate(engine, loaded);

        // THE ORDER IS DECLARED NOW, not written out here (S013-5, SDD-013 §2b).
        // What stood in this method was three hand-maintained phases, and the
        // middle one was wrong for a while: `world.decisions` sorts after
        // `wells.completions`, so the rebuild measured each reopened well's
        // gathering line against a header that had not arrived and every tieback
        // fell to its floor (finding 201). Nothing could have caught it — a
        // tieback's length is a live flow element in no block, so the digests are
        // blind to it, and a one-field game measures zero metres to its own
        // header either way.
        //
        // `RestoreOrder` is the same owners `Owners` returns, sorted so each
        // follows what it declares, with key order as the tie-break. So the
        // dependency lives in `WellsState` — the file that has the reason — and
        // this walks a list.
        IReadOnlyList<IStateOwner> owners = engine.State.RestoreOrder;

        for (int i = 0; i < owners.Count; i++)
        {
            // THE REBUILD IS PART OF RESTORING THE WELLS, not a phase beside it.
            // Reopening every completion the save records is what materialises
            // the things that block describes — design 11 §2.1 calls it that
            // owner's loader — so it runs here rather than at a point in the
            // sequence someone has to remember. The owner's own Restore then
            // checks what the rebuild just did.
            if (string.Equals(owners[i].Key.Value, WellsKey, StringComparison.Ordinal))
                Rebuild(engine, loaded);

            StateBlock.Restore(owners[i], BlockFor(loaded, owners[i].Key.Value));
        }

        var random = engine.Provided.Resolve<IRandomSource>();

        for (int i = 0; i < Streams.Length; i++)
        {
            string name = Streams[i].ToString();

            if (!loaded.Header.RngPositions.TryGetValue(name, out ulong position))
                throw new SaveDataFault("SDD-013 §2", null,
                    $"the header has no position for the '{name}' stream; resuming it from " +
                    "zero would re-draw values the saved game had already consumed");

            random.Stream(Streams[i]).Seek(position);
        }
    }

    /// <summary>
    /// Puts the saved trail back, so 09 §4.3's "why?" can still be asked about a
    /// month that preceded the save (SDD-013 §1b, S013-4).
    ///
    /// <para>Ids restore verbatim and the counter resumes above the highest, so a
    /// `Cause` written before the save still resolves after it. A container with
    /// no trail loads with none: the sidecar is excluded from the digest
    /// precisely so a truncated or absent trail is a lost record rather than an
    /// unplayable game.</para>
    /// </summary>
    private static void RestoreTrail(Engine engine, Stream source)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        if (archive.GetEntry(TrailEntry) is not ZipArchiveEntry found) return;

        var entries = new List<AuditEntry>();

        foreach (string line in Text(found).Split(Newline))
        {
            if (line.Length == 0) continue;

            entries.Add(EntryFrom(CanonicalJson.Read(line)));
        }

        engine.Provided.Resolve<AuditTrail>().RestoreFrom(entries);
    }

    private static AuditEntry EntryFrom(JsonValue value)
    {
        JsonObject json = Object(value, "an audit entry");

        EntityRef? subject = json.Members.ContainsKey("subject-kind")
            ? new EntityRef(
                Kind(String(Member(json, "subject-kind"), "subject-kind")),
                Seed(String(Member(json, "subject-id"), "subject-id")))
            : null;

        AuditId? cause = json.Members.ContainsKey("cause")
            ? new AuditId(Seed(String(Member(json, "cause"), "cause")))
            : null;

        var data = new Dictionary<string, AuditValue>(StringComparer.Ordinal);
        JsonObject payload = Object(Member(json, "data"), "an audit entry's data");

        foreach (KeyValuePair<string, JsonValue> pair in payload.Members)
            data[pair.Key] = new AuditValue(String(pair.Value, pair.Key));

        return new AuditEntry(
            new AuditId(Seed(String(Member(json, "id"), "id"))),
            new Tick((int)Integer(Member(json, "tick"), "tick")),
            Category(String(Member(json, "category"), "category")),
            subject,
            cause,
            data);
    }

    /// <summary>
    /// Enum members by NAME both ways, as the era is (SDD-010 §4c.1): an ordinal
    /// would re-key silently the day a member is inserted, and a trail is the one
    /// artefact a player is invited to check against the engine's own arithmetic.
    /// </summary>
    private static AuditCategory Category(string name) =>
        System.Enum.GetValues<AuditCategory>() is var all
        && System.Array.Find(all, c => string.Equals(c.ToString(), name, StringComparison.Ordinal))
            is AuditCategory found
        && string.Equals(found.ToString(), name, StringComparison.Ordinal)
            ? found
            : throw new SaveDataFault("SDD-013 §1b", null,
                $"the trail names audit category '{name}', which this build does not declare");

    private static EntityKind Kind(string name) =>
        System.Enum.GetValues<EntityKind>() is var all
        && System.Array.Find(all, k => string.Equals(k.ToString(), name, StringComparison.Ordinal))
            is EntityKind found
        && string.Equals(found.ToString(), name, StringComparison.Ordinal)
            ? found
            : throw new SaveDataFault("SDD-013 §1b", null,
                $"the trail names entity kind '{name}', which this build does not declare");

    /// <summary>
    /// Draws the basin again, from the seed and the parameters the save carries
    /// (SDD-010 §4c.1) — the step whose absence made a generated campaign
    /// unloadable (finding 195).
    ///
    /// <para>FIRST, before anything is restored, because everything after it
    /// assumes the world exists: the subsurface block replaces the compartments
    /// generation just made, the rebuild measures a gathering line from a field
    /// to a header, and the world's own block checks its boundary against the
    /// count standing here.</para>
    ///
    /// <para>The streams are at ZERO at this moment — <c>BuildAt</c> composes a
    /// fresh <c>RandomSource</c> — so the generator consumes `worldGen` from the
    /// same position the original run did and PV7's guarantee applies unchanged.
    /// Seeking every stream to its saved position happens afterwards, which is
    /// the only order in which both are true.</para>
    ///
    /// <para>A world nobody generated regenerates nothing and says so by carrying
    /// no parameters — a hand-placed scenario declared its field outright, and
    /// there is no basin behind it to draw.</para>
    /// </summary>
    private static void Regenerate(Engine engine, Loaded loaded)
    {
        if (WorldState.GeneratedFrom(
                StateBlock.ReaderFor(BlockFor(loaded, WorldKey))) is not WorldParameters from)
            return;

        var world = engine.Provided.Resolve<WorldState>();

        engine.Provided.Resolve<IWorldGenerator>().Generate(
            from,
            new WorldSink(
                engine.Provided.Resolve<FieldControl>(),
                engine.Provided.Resolve<IBeliefStore>(),
                world,
                engine.Provided.Resolve<OGSim.Information.ProspectRisks>()),
            engine.Provided.Resolve<IRandomSource>().Stream(StreamId.WorldGen));

        // SEALED AGAIN, exactly as creation seals it, so the boundary the world's
        // own block is about to be checked against is the one this engine just
        // drew rather than the one the save asserts. A mismatch between them is
        // the refusal that has to survive: it means this build's generator no
        // longer draws the basin the save was played on.
        world.SealGeneration(from);

        // AND WEATHER, THE SAME WAY (SDD-016's R20d.8.10 amendment) — missing
        // this would leave a reloaded game's weather at the unscaled default
        // while the original run's was scaled by ClimateSeverity, which is
        // exactly the "right dice from the wrong place" shape finding 192
        // already found once for the reservoir.
        engine.Provided.Resolve<Environment.WeatherState>()
            .SealGeneration([Defaults.ScaledClimate(from.ClimateSeverity)]);
    }

    /// <summary>
    /// Rebuilds the field the save describes: every well reopened through the
    /// path that drilled it, in the order the save lists them.
    ///
    /// <para>Reading the wells' block WITHOUT restoring it, because the owner
    /// cannot be told what it holds until the things it holds exist. The block's
    /// format stays `WellsState`'s own business — this asks it for the list
    /// rather than spelling the keys a second time (L5).</para>
    /// </summary>
    private static void Rebuild(Engine engine, Loaded loaded)
    {
        IReadOnlyList<OGSim.Wells.SavedWell> wells = OGSim.Wells.WellsState.Saved(
            StateBlock.ReaderFor(BlockFor(loaded, WellsKey)));

        var field = engine.Provided.Resolve<FieldControl>();

        for (int i = 0; i < wells.Count; i++)
            field.Reopen(wells[i].Id, wells[i].Drains, wells[i].TotalDepth);
    }



    /// <summary>
    /// One owner's block, or a fault naming it.
    ///
    /// <para>AN OWNER WITH NO BLOCK IS A FAULT, not a default. A composition that
    /// grew a state owner since the save was written would otherwise load with
    /// that owner silently at its constructed value — the "quietly lost a field"
    /// case <see cref="StateBlock"/> refuses key by key, one level up.</para>
    /// </summary>
    private static JsonValue BlockFor(Loaded loaded, string key)
    {
        for (int i = 0; i < loaded.Blocks.Count; i++)
            if (string.Equals(loaded.Blocks[i].Module, key, StringComparison.Ordinal))
                return loaded.Blocks[i].State;

        throw new SaveDataFault("SDD-013 §6", null,
            $"the save has no block for '{key}'; this engine owns state the container does " +
            "not carry, so restoring it would leave that owner at its constructed value and " +
            "the game would be subtly not the one that was saved");
    }

    // WHAT `world.decisions` CARRIES, and what it deliberately does not
    // (SDD-010 §4c). It holds where the header went up and every prospect the
    // GAME placed. What generation placed is a function of the seed, so it is
    // redrawn by `Regenerate` above rather than stored — a save that carried the
    // terrain would be storing a function of a number it also stores, and every
    // generator change would silently fork old saves from new ones.
    private const string WellsKey = "wells.completions";

    private const string WorldKey = "world.decisions";

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

            // A (year, month) record rather than a date string: §3 bans a format
            // nobody can parse unambiguously twice.
            ["epoch"] = new JsonObject(new Dictionary<string, JsonValue>(StringComparer.Ordinal)
            {
                ["year"] = new JsonInteger(header.Epoch.Year),
                ["month"] = new JsonInteger(header.Epoch.Month),
            }),

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
            EpochFrom(Member(json, "epoch")),
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

    /// <summary>
    /// The epoch, validated by `GameDate` itself — a month outside 1–12 is a
    /// corrupt header and says so here rather than surfacing as a date nobody
    /// can explain forty ticks later.
    /// </summary>
    private static GameDate EpochFrom(JsonValue value)
    {
        JsonObject json = Object(value, "epoch");

        return new GameDate(
            (int)Integer(Member(json, "year"), "epoch.year"),
            (int)Integer(Member(json, "month"), "epoch.month"));
    }

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
