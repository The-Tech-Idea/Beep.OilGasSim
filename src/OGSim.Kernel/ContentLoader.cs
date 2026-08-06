// R3.2–R3.6 — the six-stage content loader (SDD-004 §5–§7, design 10 §3.1).
//
// THE LOADER KNOWS NO CONTENT KIND. Every kind arrives as an IContentKind
// registration, which is what makes R3 §3's acceptance criterion structural: if
// a later phase needed a change in this file to add a content kind, R3's design
// would be wrong. Nothing below mentions a material, a tier or a technology.
//
// ALL FILES RUN ALL STAGES AND FAILURES ACCUMULATE (R3-V2). A loader that
// stopped at the first bad file would make fixing a content pack an N-run loop,
// which is the same complaint R1 §2.9 makes about composition and design 09
// §5.1 about content faults: report every problem, not the first.

using System.Text.Json;

namespace OGSim.Kernel;

public sealed class ContentLoader
{
    private readonly Dictionary<string, IContentKind> _kinds;
    private readonly IModuleRegistry _plugins;

    public ContentLoader(IReadOnlyList<IContentKind> kinds, IModuleRegistry plugins)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        ArgumentNullException.ThrowIfNull(plugins);

        _kinds = new Dictionary<string, IContentKind>(StringComparer.Ordinal);
        for (int i = 0; i < kinds.Count; i++)
        {
            IContentKind kind = kinds[i];
            if (!_kinds.TryAdd(kind.Name, kind))
                throw new InvariantFault("L5", null,
                    $"Content kind '{kind.Name}' is registered twice; one kind, one reader.");
        }

        _plugins = plugins;
    }

    public ContentLoadResult LoadAll(IReadOnlyList<IContentSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var failures = new List<LoadFailure>();

        // Source order fixes override precedence (§7); path order fixes
        // diagnostic order. Both are ordinal sorts so neither depends on the
        // machine's culture or on a directory listing's whim (D-8).
        var ordered = new List<IContentSource>(sources);
        ordered.Sort((a, b) =>
        {
            int byOrder = a.DeclaredOrder.CompareTo(b.DeclaredOrder);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Name, b.Name);
        });

        // kind:id -> the winning entry, and who last wrote it.
        var entries = new SortedDictionary<string, LoadedEntry>(StringComparer.Ordinal);

        for (int s = 0; s < ordered.Count; s++)
            ReadSource(ordered[s], entries, failures);

        if (failures.Count > 0) return new ContentFailures(failures);

        ResolveReferences(entries, failures);
        RunConsistency(entries, failures);
        BindPlugins(entries, failures);

        return failures.Count > 0
            ? new ContentFailures(failures)
            : new ContentLoaded(new CatalogueSet(entries));
    }

    // ------------------------------------------------------------- stages 1–3

    private void ReadSource(
        IContentSource source,
        SortedDictionary<string, LoadedEntry> entries,
        List<LoadFailure> failures)
    {
        var files = new List<ContentFile>(source.Files);
        files.Sort((a, b) => string.CompareOrdinal(a.RelativePath, b.RelativePath));

        for (int f = 0; f < files.Count; f++)
        {
            ContentFile file = files[f];

            // ---- stage 1: parse
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(file.Json);
            }
            catch (JsonException json)
            {
                failures.Add(new LoadFailure(source.Name, file.RelativePath,
                    $"$ (byte {json.BytePositionInLine?.ToString(Invariant) ?? "?"})",
                    LoadStage.Parse, json.Message));
                continue;
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (!root.TryGetProperty("kind", out JsonElement kindElement)
                    || kindElement.ValueKind != JsonValueKind.String)
                {
                    failures.Add(new LoadFailure(source.Name, file.RelativePath, "$.kind",
                        LoadStage.Shape, "every entry declares its kind explicitly (10 §3.1)"));
                    continue;
                }

                string kindName = kindElement.GetString()!;
                if (!_kinds.TryGetValue(kindName, out IContentKind? kind))
                {
                    failures.Add(new LoadFailure(source.Name, file.RelativePath, "$.kind",
                        LoadStage.Shape,
                        $"unknown content kind '{kindName}'{NearestKind(kindName)}"));
                    continue;
                }

                // ---- stages 2-3: shape and units, delegated to the kind
                ContentDefinition definition;
                try
                {
                    definition = kind.Read(root);
                }
                catch (ContentUnitFault unit)
                {
                    failures.Add(new LoadFailure(source.Name, file.RelativePath, "$",
                        LoadStage.Units, unit.Reason));
                    continue;
                }
                catch (JsonException json)
                {
                    failures.Add(new LoadFailure(source.Name, file.RelativePath,
                        json.Path ?? "$", LoadStage.Shape, json.Message));
                    continue;
                }

                Record(source, file, kindName, definition, entries, failures);
            }
        }
    }

    /// <summary>
    /// §7's override rules. A later source replaces an earlier one WHOLE — no
    /// partial merge, because merge semantics for a datasheet are unspecifiable
    /// (what would half a head curve mean?). Two sources at the SAME order
    /// claiming one kind:id is a failure naming both, since neither can be said
    /// to win.
    /// </summary>
    private static void Record(
        IContentSource source,
        ContentFile file,
        string kindName,
        ContentDefinition definition,
        SortedDictionary<string, LoadedEntry> entries,
        List<LoadFailure> failures)
    {
        string key = kindName + ":" + definition.Id.Value;

        if (entries.TryGetValue(key, out LoadedEntry existing))
        {
            if (existing.SourceOrder == source.DeclaredOrder)
            {
                failures.Add(new LoadFailure(source.Name, file.RelativePath, "$.id",
                    LoadStage.Consistency,
                    $"'{key}' is also defined by source '{existing.SourceName}' at the same " +
                    $"declared order {source.DeclaredOrder}; neither can override the other"));
                return;
            }
            // Higher declared order wins, replacing the whole entry.
        }

        entries[key] = new LoadedEntry(
            kindName, definition, source.Name, source.DeclaredOrder, file.RelativePath);
    }

    // ------------------------------------------------------------- stage 4

    /// <summary>
    /// Two passes, exactly as §5 says: the index is complete before anything is
    /// resolved, so a forward reference between files is legal and file order
    /// cannot change whether content loads.
    /// </summary>
    private void ResolveReferences(
        SortedDictionary<string, LoadedEntry> entries, List<LoadFailure> failures)
    {
        foreach (KeyValuePair<string, LoadedEntry> pair in entries)
        {
            LoadedEntry entry = pair.Value;
            IContentKind kind = _kinds[entry.Kind];

            IReadOnlyList<ContentReference> references = kind.ReferencesOf(entry.Definition);
            for (int r = 0; r < references.Count; r++)
            {
                ContentReference reference = references[r];
                string key = reference.Kind + ":" + reference.Id.Value;
                if (entries.ContainsKey(key)) continue;

                failures.Add(new LoadFailure(entry.SourceName, entry.File, reference.JsonPath,
                    LoadStage.References, $"dangling reference '{key}'"));
            }
        }
    }

    // ------------------------------------------------------------- stage 5

    private void RunConsistency(
        SortedDictionary<string, LoadedEntry> entries, List<LoadFailure> failures)
    {
        foreach (KeyValuePair<string, LoadedEntry> pair in entries)
        {
            LoadedEntry entry = pair.Value;
            IContentKind kind = _kinds[entry.Kind];

            IReadOnlyList<string> problems = kind.ConsistencyProblems(entry.Definition);
            for (int p = 0; p < problems.Count; p++)
                failures.Add(new LoadFailure(entry.SourceName, entry.File, "$",
                    LoadStage.Consistency, problems[p]));
        }
    }

    // ------------------------------------------------------------- stage 6

    /// <summary>
    /// R3.5. A gated definition naming a model plugin must resolve against the
    /// registry now, at load — not on the tick that first needs it, by which
    /// point the player has a saved game built on content that cannot run.
    /// </summary>
    private void BindPlugins(
        SortedDictionary<string, LoadedEntry> entries, List<LoadFailure> failures)
    {
        foreach (KeyValuePair<string, LoadedEntry> pair in entries)
        {
            LoadedEntry entry = pair.Value;
            if (entry.Definition is not GatedDefinition gated) continue;
            if (gated.Fits != SlotKind.ModelSlot) continue;

            if (!_plugins.CanBind(gated.Id, typeof(object)))
                failures.Add(new LoadFailure(entry.SourceName, entry.File, "$.id",
                    LoadStage.Binding, $"no plugin registered for model slot '{gated.Id}'"));
        }
    }

    // ------------------------------------------------------------- helpers

    private string NearestKind(string unknown)
    {
        var names = new List<string>(_kinds.Keys);
        names.Sort(StringComparer.Ordinal);      // suggestion must not follow hash order

        string? best = null;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < names.Count; i++)
        {
            int distance = Distance(unknown, names[i]);
            if (distance < bestDistance) { bestDistance = distance; best = names[i]; }
        }
        return best is not null && bestDistance <= 3 ? $" — did you mean '{best}'?" : string.Empty;
    }

    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) previous[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
                current[j] = Math.Min(
                    previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1),
                    Math.Min(previous[j] + 1, current[j - 1] + 1));
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    private static System.Globalization.CultureInfo Invariant =>
        System.Globalization.CultureInfo.InvariantCulture;

    private readonly record struct LoadedEntry(
        string Kind,
        ContentDefinition Definition,
        string SourceName,
        int SourceOrder,
        string File);

    // ------------------------------------------------------------- catalogues

    /// <summary>R3.4 — typed catalogues over the loaded entries.</summary>
    private sealed class CatalogueSet : ICatalogSet
    {
        private readonly Dictionary<Type, object> _byType = [];

        public CatalogueSet(SortedDictionary<string, LoadedEntry> entries)
        {
            // Grouped by the definition's RUNTIME type, so a kind's catalogue is
            // addressable without the loader knowing that type exists.
            var grouped = new Dictionary<Type, List<ContentDefinition>>();
            foreach (KeyValuePair<string, LoadedEntry> pair in entries)
            {
                Type type = pair.Value.Definition.GetType();
                if (!grouped.TryGetValue(type, out List<ContentDefinition>? list))
                {
                    list = [];
                    grouped.Add(type, list);
                }
                list.Add(pair.Value.Definition);
            }

            foreach (KeyValuePair<Type, List<ContentDefinition>> pair in grouped)
            {
                Type catalogueType = typeof(Catalogue<>).MakeGenericType(pair.Key);
                _byType.Add(pair.Key, Activator.CreateInstance(catalogueType, pair.Value)!);
            }
        }

        public ICatalog<TDef> Of<TDef>() where TDef : ContentDefinition =>
            _byType.TryGetValue(typeof(TDef), out object? catalogue)
                ? (ICatalog<TDef>)catalogue
                : new Catalogue<TDef>([]);
    }

    private sealed class Catalogue<TDef> : ICatalog<TDef> where TDef : ContentDefinition
    {
        private readonly Dictionary<ContentId, TDef> _byId;

        public Catalogue(IReadOnlyList<ContentDefinition> definitions)
        {
            // Ordinal-sorted by id string — save-stable, and the order ordinals
            // are assigned from (SDD-004 §6).
            var sorted = new List<TDef>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++) sorted.Add((TDef)definitions[i]);
            sorted.Sort((a, b) => a.Id.CompareTo(b.Id));

            All = sorted;
            _byId = new Dictionary<ContentId, TDef>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++) _byId.Add(sorted[i].Id, sorted[i]);
        }

        public IReadOnlyList<TDef> All { get; }

        public TDef this[ContentId id] =>
            _byId.TryGetValue(id, out TDef? definition)
                ? definition
                : throw new SaveDataFault("SDD-004 §6", null,
                    $"No {typeof(TDef).Name} '{id}' in a catalogue of {All.Count}.");

        public bool TryGet(ContentId id, out TDef definition) =>
            _byId.TryGetValue(id, out definition!);
    }
}
