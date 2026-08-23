// Plans 28 — the catalogue checks that ran as a script become tests.
//
// Check 1 (every reference resolves) is the loader's own stage 4 and is
// asserted by loading. Check 3 (the reverse views are the exact inverse) is
// no longer falsifiable: `unlocks` and `enables` left the files after
// drifting twice, and `RelationGraph` derives them — there is nothing left to
// disagree. The remaining checks are below.

using System.IO;
using System.Runtime.CompilerServices;
using OGSim.Kernel;
using Xunit;

namespace OGSim.Composition.Tests;

public sealed class CatalogueContentTests
{
    /// <summary>
    /// The shipped catalogues load through the real loader, and every
    /// reference — activity to activity, activity to equipment, either to a
    /// technology — resolves. Technologies join the load because the
    /// relations name them; a catalogue-only load would report every tech
    /// gate dangling, which is the facilities-standalone lesson
    /// `28_CONTENT_TREES.md` records.
    /// </summary>
    [Fact]
    public void The_shipped_catalogues_load_and_every_reference_resolves() =>
        Load();

    /// <summary>Check 2 — no activity is stranded: everything is reachable
    /// from work that needs nothing done first.</summary>
    [Fact]
    public void Every_activity_is_reachable_from_a_root()
    {
        ICatalogSet loaded = Load();
        RelationGraph graph = RelationGraph.From(loaded);

        IReadOnlyList<ActivityDefinition> activities = loaded.Of<ActivityDefinition>().All;

        var reached = new HashSet<ContentId>();
        var frontier = new Queue<ContentId>();

        foreach (ActivityDefinition activity in activities)
            if (graph.Requires(activity.Id).Count == 0)
            { reached.Add(activity.Id); frontier.Enqueue(activity.Id); }

        while (frontier.Count > 0)
            foreach (ContentId next in graph.Unlocks(frontier.Dequeue()))
                if (reached.Add(next))
                    frontier.Enqueue(next);

        var stranded = new List<string>();
        foreach (ActivityDefinition activity in activities)
            if (!reached.Contains(activity.Id))
                stranded.Add(activity.Id.Value);

        Assert.True(stranded.Count == 0,
            "unreachable from any root: " + string.Join(", ", stranded));
    }

    /// <summary>Check 4 — every activity is enabled by some kit, so building
    /// the yard is never optional and never pointless.</summary>
    [Fact]
    public void Every_activity_is_enabled_by_some_equipment()
    {
        ICatalogSet loaded = Load();
        RelationGraph graph = RelationGraph.From(loaded);

        var bare = new List<string>();
        foreach (ActivityDefinition activity in loaded.Of<ActivityDefinition>().All)
            if (graph.NeedsEquipment(activity.Id).Count == 0)
                bare.Add(activity.Id.Value);

        Assert.True(bare.Count == 0,
            "activities no equipment enables: " + string.Join(", ", bare));
    }

    /// <summary>
    /// Check 5 — the one worth having: a style may not quietly lose an
    /// activity. Days either enables it or explains its omission in writing.
    /// That every named id EXISTS is the loader's own stage 4 now — the
    /// selection's references resolve or the load refuses by name — so this
    /// asserts only what the loader cannot see across entries: completeness.
    /// </summary>
    [Fact]
    public void The_days_style_enables_or_explains_every_activity()
    {
        ICatalogSet loaded = Load();
        ICatalog<GameStyleSelection> styles = loaded.Of<GameStyleSelection>();

        GameStyleSelection days = styles[new ContentId("days")];
        Assert.False(days.EveryActivity);

        var accounted = new HashSet<ContentId>(days.Activities);
        foreach (StyleOmission omission in days.Omits)
            accounted.Add(omission.Activity);

        var unaccounted = new List<string>();
        foreach (ActivityDefinition activity in loaded.Of<ActivityDefinition>().All)
            if (!accounted.Contains(activity.Id))
                unaccounted.Add(activity.Id.Value);

        Assert.True(unaccounted.Count == 0,
            "neither enabled nor explained by days: " + string.Join(", ", unaccounted));

        // And the full-fidelity style is every node by declaration, so
        // anything added later ships in it the day it exists.
        GameStyleSelection engineer = styles[new ContentId("engineer")];
        Assert.True(engineer.EveryActivity);
        Assert.True(engineer.EveryEquipment);
        Assert.True(engineer.EveryTechnology);
    }

    // ------------------------------------------------------------- loading

    /// <summary>The four directories the checks span, through the real loader.</summary>
    private static ICatalogSet Load()
    {
        var loader = new ContentLoader(
            [
                new ActivityContentKind(),
                new EquipmentContentKind(),
                new RelationContentKind(),
                new GameStyleContentKind(),
                new OGSim.Capabilities.TechnologyContentKind(),
            ],
            new PluginRegistry());

        string root = ContentRoot();

        ContentLoadResult result = loader.LoadAll(
        [
            new DiskSource("base", 0, root, "activities"),
            new DiskSource("base", 0, root, "equipment"),
            new DiskSource("base", 0, root, "relations"),
            new DiskSource("base", 0, root, "game-styles"),
            new DiskSource("base", 0, root, "technologies"),
        ]);

        if (result is ContentFailures failed)
            Assert.Fail(string.Join("; ", failed.Failures.Select(
                f => $"{f.File} {f.JsonPath} [{f.Stage}] {f.Message}")));

        return Assert.IsType<ContentLoaded>(result).Catalogues;
    }

    private static string ContentRoot([CallerFilePath] string thisFile = "")
    {
        DirectoryInfo directory = new FileInfo(thisFile).Directory!;   // tests/OGSim.Composition.Tests
        return Path.Combine(directory.Parent!.Parent!.FullName, "content");
    }

    private sealed class DiskSource(string name, int order, string root, string subdirectory)
        : IContentSource
    {
        public string Name => name;

        public int DeclaredOrder => order;

        public IReadOnlyList<ContentFile> Files { get; } =
            Directory.EnumerateFiles(Path.Combine(root, subdirectory), "*.json")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new ContentFile(
                    subdirectory + "/" + Path.GetFileName(path), File.ReadAllText(path)))
                .ToList();
    }
}
