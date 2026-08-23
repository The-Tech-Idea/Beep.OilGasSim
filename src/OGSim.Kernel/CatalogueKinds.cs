// Plans 28 — the game catalogues as content the loader validates.
//
// The activity, equipment and relation trees were authored as JSON and loaded
// by nothing, which made them design data wearing content's clothes: a broken
// reference or a cycle would have shipped silently. These kinds put them
// through the same six-stage loader every other content kind passes, so the
// catalogues refuse by name at load — the plug-and-play law made enforceable.
//
// RELATIONS LIVE IN THEIR OWN FILES (plans 28 §3): a node describes itself and
// a relation file describes the graph, one edge kind per file. The reverse
// views (`unlocks`, `enables`) are DERIVED by `RelationGraph` — after two
// measured drifts they are no longer representable in content at all.

using System.Text.Json;

namespace OGSim.Kernel;

// ------------------------------------------------------------------ activity

/// <summary>One template of player-ordered work, as the catalogue states it.</summary>
public sealed record ActivityDefinition(
    ContentId Id, string NameKey, ContentId Group,
    double CostMillions, int DurationTurns,
    double WeatherLimit, bool RequiresAccess, bool Develops, string Note)
    : ContentDefinition(Id);

public sealed class ActivityContentKind : IContentKind
{
    public string Name => "activity";

    public ContentDefinition Read(JsonElement element) => new ActivityDefinition(
        new ContentId(PropertyKindContentKind.Required(element, "id").GetString()!),
        PropertyKindContentKind.Required(element, "name").GetString()!,
        new ContentId(PropertyKindContentKind.Required(element, "group").GetString()!),
        PropertyKindContentKind.Required(element, "costMillions").GetDouble(),
        PropertyKindContentKind.Required(element, "durationTurns").GetInt32(),
        PropertyKindContentKind.Required(element, "weatherLimit").GetDouble(),
        PropertyKindContentKind.Required(element, "requiresAccess").GetBoolean(),
        PropertyKindContentKind.Required(element, "develops").GetBoolean(),
        PropertyKindContentKind.Required(element, "note").GetString()!);

    /// <summary>The activity's own edges live in <c>content/relations/</c>;
    /// the node references nothing (plans 28 §3).</summary>
    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not ActivityDefinition activity) return problems;

        if (activity.CostMillions <= 0.0)
            problems.Add($"cost {activity.CostMillions} must be positive; free work is not a decision");

        if (activity.DurationTurns < 1)
            problems.Add($"duration {activity.DurationTurns} turns must be at least one; nothing finishes before it starts");

        if (activity.WeatherLimit <= 0.0)
            problems.Add($"weather limit {activity.WeatherLimit} must be positive, or no day is calm enough to work");

        return problems;
    }

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];
}

// ----------------------------------------------------------------- equipment

/// <summary>One buildable unit of yard kit, as the catalogue states it.</summary>
public sealed record EquipmentDefinition(
    ContentId Id, string NameKey, ContentId Group,
    double CostMillions, int BuildTurns, string Note)
    : ContentDefinition(Id);

public sealed class EquipmentContentKind : IContentKind
{
    public string Name => "equipment";

    public ContentDefinition Read(JsonElement element) => new EquipmentDefinition(
        new ContentId(PropertyKindContentKind.Required(element, "id").GetString()!),
        PropertyKindContentKind.Required(element, "name").GetString()!,
        new ContentId(PropertyKindContentKind.Required(element, "group").GetString()!),
        PropertyKindContentKind.Required(element, "costMillions").GetDouble(),
        PropertyKindContentKind.Required(element, "buildTurns").GetInt32(),
        PropertyKindContentKind.Required(element, "note").GetString()!);

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not EquipmentDefinition equipment) return problems;

        if (equipment.CostMillions <= 0.0)
            problems.Add($"cost {equipment.CostMillions} must be positive; free kit is not a purchase");

        if (equipment.BuildTurns < 1)
            problems.Add($"build time {equipment.BuildTurns} turns must be at least one");

        return problems;
    }

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];
}

// ------------------------------------------------------------------ relation

/// <summary>One directed edge of the catalogue graph.</summary>
public readonly record struct RelationEdge(ContentId From, ContentId To);

/// <summary>
/// Every edge of one kind, in one file — `activity-requires-activity` and its
/// four siblings. The entry's id IS its edge name.
/// </summary>
public sealed record RelationDefinition(
    ContentId Id, IReadOnlyList<RelationEdge> Edges)
    : ContentDefinition(Id)
{
    public bool Equals(RelationDefinition? other) =>
        other is not null && Id == other.Id && Structural.Equal(Edges, other.Edges);

    public override int GetHashCode() =>
        HashCode.Combine(Id, Structural.HashOf(Edges));
}

public sealed class RelationContentKind : IContentKind
{
    public string Name => "relation";

    /// <summary>
    /// The edge kinds this build knows — one row per kind, its ends beside its
    /// name so the two can never disagree; ordinal-sorted, which is also the
    /// refusal message's order (D-5). An unknown edge name is a refusal naming
    /// these — the same closed-set rule game styles and reality profiles
    /// already follow.
    /// </summary>
    internal static readonly IReadOnlyList<(string Edge, string From, string To)> EdgeKinds =
    [
        ("activity-needs-equipment", "activity", "equipment"),
        ("activity-needs-tech", "activity", "tech"),
        ("activity-requires-activity", "activity", "activity"),
        ("equipment-needs-equipment", "equipment", "equipment"),
        ("equipment-needs-tech", "equipment", "tech"),
    ];

    /// <summary>The same rows as a lookup — the list above owns the facts and
    /// the order; this owns nothing but the O(1) access.</summary>
    private static readonly IReadOnlyDictionary<string, (string From, string To)> Ends =
        LookupOf(EdgeKinds);

    private static Dictionary<string, (string, string)> LookupOf(
        IReadOnlyList<(string Edge, string From, string To)> rows)
    {
        var ends = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        foreach ((string edge, string from, string to) in rows)
            ends.Add(edge, (from, to));
        return ends;
    }

    public ContentDefinition Read(JsonElement element)
    {
        string edge = PropertyKindContentKind.Required(element, "edge").GetString()!;

        var edges = new List<RelationEdge>();
        foreach (JsonElement entry in PropertyKindContentKind.Required(element, "edges").EnumerateArray())
            edges.Add(new RelationEdge(
                new ContentId(PropertyKindContentKind.Required(entry, "from").GetString()!),
                new ContentId(PropertyKindContentKind.Required(entry, "to").GetString()!)));

        return new RelationDefinition(new ContentId(edge), edges);
    }

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition)
    {
        if (definition is not RelationDefinition relation
            || !Ends.TryGetValue(relation.Id.Value, out (string From, string To) ends))
            return [];

        var references = new List<ContentReference>(relation.Edges.Count * 2);

        for (int i = 0; i < relation.Edges.Count; i++)
        {
            references.Add(new ContentReference(ends.From, relation.Edges[i].From, $"$.edges[{i}].from"));
            references.Add(new ContentReference(ends.To, relation.Edges[i].To, $"$.edges[{i}].to"));
        }

        return references;
    }

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not RelationDefinition relation) return problems;

        if (!Ends.TryGetValue(relation.Id.Value, out (string From, string To) ends))
        {
            problems.Add(
                $"'{relation.Id.Value}' is not an edge kind this build knows; it knows " +
                string.Join(", ", EdgeKinds.Select(k => $"'{k.Edge}'")));
            return problems;
        }

        var seen = new HashSet<RelationEdge>();

        foreach (RelationEdge edge in relation.Edges)
        {
            if (!seen.Add(edge))
                problems.Add($"edge {edge.From.Value} -> {edge.To.Value} is stated twice");

            if (edge.From == edge.To)
                problems.Add($"'{edge.From.Value}' cannot require itself");
        }

        // A CYCLE IS REFUSED BY NAME, within the one edge kind whose ends share
        // a type. A requires B requires A would make both unreachable forever,
        // and the file that states the whole edge kind is the one place a cycle
        // is visible without loading anything else.
        if (ends.From == ends.To)
            foreach (string cycle in Cycles(relation.Edges))
                problems.Add($"cycle: {cycle}");

        return problems;
    }

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];

    private static IEnumerable<string> Cycles(IReadOnlyList<RelationEdge> stated)
    {
        var next = new Dictionary<ContentId, List<ContentId>>();
        var roots = new List<ContentId>();

        // Iteration order comes from the FILE's own edge order, deduplicated -
        // never from a hash-ordered collection (rule D-5).
        foreach (RelationEdge edge in stated)
        {
            if (!next.TryGetValue(edge.From, out List<ContentId>? list))
            {
                next[edge.From] = list = [];
                roots.Add(edge.From);
            }

            list.Add(edge.To);
        }

        var grey = new List<ContentId>();
        var black = new HashSet<ContentId>();

        // ONE CYCLE PER TANGLE, EVERY TANGLE NAMED (finding 284). The loader's
        // law is "every problem, never just the first", and this stopped at
        // the first cycle it met — an author told one of two cycles fixed one
        // of two. The stated boundary: overlapping cycles through SHARED nodes
        // still surface one at a time (the found cycle's nodes are retired
        // before the walk continues, so a second cycle woven through them is
        // named on the load after its predecessor is fixed) — naming every
        // elementary cycle at once is a different algorithm this refusal does
        // not need.
        foreach (ContentId start in roots)
        {
            if (black.Contains(start)) continue;

            string? found = Visit(start);
            if (found is null) continue;

            yield return found;

            // Visit returned early, leaving the stack dirty; retire what it
            // walked so the next root neither re-reports this cycle nor
            // inherits a stack it never pushed.
            foreach (ContentId walked in grey) black.Add(walked);
            grey.Clear();
        }

        string? Visit(ContentId node)
        {
            grey.Add(node);

            if (next.TryGetValue(node, out List<ContentId>? targets))
                foreach (ContentId target in targets)
                {
                    int at = grey.IndexOf(target);
                    if (at >= 0)
                        return string.Join(" -> ",
                            grey.Skip(at).Select(g => g.Value).Append(target.Value));

                    if (!black.Contains(target) && Visit(target) is string cycle)
                        return cycle;
                }

            grey.RemoveAt(grey.Count - 1);
            black.Add(node);
            return null;
        }
    }
}

// ------------------------------------------------------------- starting-state

/// <summary>
/// What a company opens with — the balance and whether the plant already
/// stands. Designer numbers, so they live here and nowhere else: the engine
/// used to hold both as literals (`OpeningCashFor`'s $50M/$72M and the
/// facilities module's starting-state comparison), which was the exact
/// "unreachable by any style, profile or content" defect the module review
/// catalogued.
/// </summary>
public sealed record StartingStateDefinition(
    ContentId Id, double OpeningCashMillions, bool HoldsPlant, string Note)
    : ContentDefinition(Id);

public sealed class StartingStateContentKind : IContentKind
{
    public string Name => "starting-state";

    public ContentDefinition Read(JsonElement element) => new StartingStateDefinition(
        new ContentId(PropertyKindContentKind.Required(element, "id").GetString()!),
        PropertyKindContentKind.Required(element, "openingCashMillions").GetDouble(),
        PropertyKindContentKind.Required(element, "holdsPlant").GetBoolean(),
        PropertyKindContentKind.Required(element, "note").GetString()!);

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not StartingStateDefinition state) return problems;

        if (state.OpeningCashMillions <= 0.0)
            problems.Add($"opening cash {state.OpeningCashMillions} must be positive; " +
                         "a company that opens broke has already lost");

        return problems;
    }

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];
}

// ---------------------------------------------------------------- game-style

/// <summary>One activity a style leaves out, with the written reason the
/// completeness test demands — an omission must be stated, never silent.</summary>
public readonly record struct StyleOmission(ContentId Activity, string Reason);

/// <summary>
/// A style's catalogue selection — the ONE fact the style files own. The
/// style's identity, axes, terms and mechanic presences live in code
/// (`GameStyles`), where the composer refuses unknown ids by name; stating
/// them in the file as well was the duplication this kind ended.
/// </summary>
public sealed record GameStyleSelection(
    ContentId Id,
    bool EveryActivity, IReadOnlyList<ContentId> Activities,
    bool EveryEquipment, IReadOnlyList<ContentId> Equipment,
    bool EveryTechnology, IReadOnlyList<ContentId> Technologies,
    IReadOnlyList<StyleOmission> Omits)
    : ContentDefinition(Id)
{
    public bool Equals(GameStyleSelection? other) =>
        other is not null && Id == other.Id
        && EveryActivity == other.EveryActivity
        && EveryEquipment == other.EveryEquipment
        && EveryTechnology == other.EveryTechnology
        && Structural.Equal(Activities, other.Activities)
        && Structural.Equal(Equipment, other.Equipment)
        && Structural.Equal(Technologies, other.Technologies)
        && Structural.Equal(Omits, other.Omits);

    public override int GetHashCode() =>
        HashCode.Combine(Id, EveryActivity, EveryEquipment, EveryTechnology,
                         Structural.HashOf(Activities), Structural.HashOf(Equipment),
                         Structural.HashOf(Technologies), Structural.HashOf(Omits));
}

public sealed class GameStyleContentKind : IContentKind
{
    public string Name => "game-style";

    public ContentDefinition Read(JsonElement element)
    {
        var id = new ContentId(PropertyKindContentKind.Required(element, "id").GetString()!);

        (bool every, IReadOnlyList<ContentId> ids) activities = Selection(element, "activities");
        (bool every, IReadOnlyList<ContentId> ids) equipment = Selection(element, "equipment");
        (bool every, IReadOnlyList<ContentId> ids) technologies = Selection(element, "technologies");

        var omits = new List<StyleOmission>();
        if (element.TryGetProperty("omits", out JsonElement stated))
            foreach (JsonProperty omitted in stated.EnumerateObject())
                omits.Add(new StyleOmission(
                    new ContentId(omitted.Name), omitted.Value.GetString() ?? string.Empty));

        return new GameStyleSelection(
            id, activities.every, activities.ids, equipment.every, equipment.ids,
            technologies.every, technologies.ids, omits);
    }

    /// <summary>A list of ids, or the one wildcard: <c>["*"]</c> means every
    /// node — so anything added later is in the product the day it exists,
    /// not the day somebody remembers to list it.</summary>
    private static (bool Every, IReadOnlyList<ContentId> Ids) Selection(
        JsonElement element, string name)
    {
        var raw = new List<string>();
        foreach (JsonElement entry in PropertyKindContentKind.Required(element, name).EnumerateArray())
            raw.Add(entry.GetString()!);

        if (raw.Contains("*"))
        {
            if (raw.Count != 1)
                throw new System.Text.Json.JsonException(
                    $"'{name}' mixes '*' with named ids; the wildcard means every node and stands alone");
            return (true, []);
        }

        var ids = new List<ContentId>(raw.Count);
        foreach (string entry in raw) ids.Add(new ContentId(entry));
        return (false, ids);
    }

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition)
    {
        if (definition is not GameStyleSelection style) return [];

        var references = new List<ContentReference>();

        for (int i = 0; i < style.Activities.Count; i++)
            references.Add(new ContentReference("activity", style.Activities[i], $"$.activities[{i}]"));
        for (int i = 0; i < style.Equipment.Count; i++)
            references.Add(new ContentReference("equipment", style.Equipment[i], $"$.equipment[{i}]"));
        for (int i = 0; i < style.Technologies.Count; i++)
            references.Add(new ContentReference("tech", style.Technologies[i], $"$.technologies[{i}]"));
        for (int i = 0; i < style.Omits.Count; i++)
            references.Add(new ContentReference("activity", style.Omits[i].Activity, $"$.omits"));

        return references;
    }

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not GameStyleSelection style) return problems;

        var enabled = new HashSet<ContentId>();
        foreach (ContentId id in style.Activities)
            if (!enabled.Add(id))
                problems.Add($"'{id.Value}' is enabled twice");

        var omitted = new HashSet<ContentId>();
        foreach (StyleOmission omission in style.Omits)
        {
            if (!omitted.Add(omission.Activity))
                problems.Add($"'{omission.Activity.Value}' is omitted twice");

            if (enabled.Contains(omission.Activity))
                problems.Add($"'{omission.Activity.Value}' is both enabled and omitted");

            if (string.IsNullOrWhiteSpace(omission.Reason))
                problems.Add($"'{omission.Activity.Value}' is omitted with no written reason");
        }

        if (style.EveryActivity && style.Omits.Count > 0)
            problems.Add("a style enabling every activity has nothing to omit");

        return problems;
    }

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];
}

// ------------------------------------------------------------ relation graph

/// <summary>
/// The catalogue graph, one queryable object over the loaded relations —
/// including the DERIVED reverse views (`Unlocks`, `Enables`) that plans 28
/// removed from content after they drifted twice.
/// </summary>
public sealed class RelationGraph
{
    private readonly Dictionary<string, IReadOnlyList<RelationEdge>> _edges;

    // The same names as an ORDERED list (rule D-5, the PluginRegistry shape):
    // the dictionary owns the lookup, this owns the enumeration order.
    private readonly IReadOnlyList<string> _names;

    private RelationGraph(
        Dictionary<string, IReadOnlyList<RelationEdge>> edges, IReadOnlyList<string> names)
    {
        _edges = edges;
        _names = names;
    }

    /// <summary>Built from what the loader accepted, never from files directly —
    /// the loader is the one door content comes through.</summary>
    public static RelationGraph From(ICatalogSet loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);

        var edges = new Dictionary<string, IReadOnlyList<RelationEdge>>(StringComparer.Ordinal);

        var names = new List<string>();

        // `All` is already ordinal-sorted by id (ICatalog's own contract).
        foreach (RelationDefinition relation in loaded.Of<RelationDefinition>().All)
        {
            edges[relation.Id.Value] = relation.Edges;
            names.Add(relation.Id.Value);
        }

        return new RelationGraph(edges, names);
    }

    /// <summary>Every edge of one kind, or a refusal naming what is on offer.</summary>
    public IReadOnlyList<RelationEdge> Edges(string kind)
    {
        if (_edges.TryGetValue(kind, out IReadOnlyList<RelationEdge>? found)) return found;

        throw new InvariantFault("plans 28 §3", null,
            $"'{kind}' is not an edge kind this graph holds; it holds " +
            string.Join(", ", _names.Select(k => $"'{k}'")));
    }

    /// <summary>What must exist before this activity may be ordered.</summary>
    public IReadOnlyList<ContentId> Requires(ContentId activity) =>
        Outgoing("activity-requires-activity", activity);

    /// <summary>DERIVED inverse of <see cref="Requires"/> — completing this
    /// activity is a prerequisite of these.</summary>
    public IReadOnlyList<ContentId> Unlocks(ContentId activity) =>
        Incoming("activity-requires-activity", activity);

    /// <summary>The kit this activity needs standing in the yard.</summary>
    public IReadOnlyList<ContentId> NeedsEquipment(ContentId activity) =>
        Outgoing("activity-needs-equipment", activity);

    /// <summary>DERIVED inverse of <see cref="NeedsEquipment"/> — building this
    /// kit is what makes these activities orderable.</summary>
    public IReadOnlyList<ContentId> Enables(ContentId equipment) =>
        Incoming("activity-needs-equipment", equipment);

    private IReadOnlyList<ContentId> Outgoing(string kind, ContentId from)
    {
        var found = new List<ContentId>();
        foreach (RelationEdge edge in Edges(kind))
            if (edge.From == from) found.Add(edge.To);
        return found;
    }

    private IReadOnlyList<ContentId> Incoming(string kind, ContentId to)
    {
        var found = new List<ContentId>();
        foreach (RelationEdge edge in Edges(kind))
            if (edge.To == to) found.Add(edge.From);
        return found;
    }
}
