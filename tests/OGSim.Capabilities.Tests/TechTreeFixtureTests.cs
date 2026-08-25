// R20c.9 — the registry and the content, checked against each other
// (SDD-004 §8, design 22 §6.1's mechanical coherence check).
//
// TECH_TREE.md is the authoring spec and content/technologies/ is what the
// engine loads. Two documents saying the same thing drift, and the drift is
// silent: a sheet gate that names a node nobody ships fails only when a player
// tries to buy the equipment. This is the plans-side gate check's code-side
// twin — it reads BOTH and asserts they agree.

using System.Runtime.CompilerServices;
using System.Text.Json;
using OGSim.Capabilities;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Capabilities.Tests;

public sealed class TechTreeFixtureTests
{
    private sealed record RegistryNode(string Slug, string DisplayName, Era Era, string[] Prereqs);

    /// <summary>
    /// The registry's node tables, parsed. A row is
    /// <c>| Node | Era | Prereqs | Routes | Opens |</c>; the tables are the ones
    /// under "## Node registry", and nothing else in the file has five columns
    /// whose second cell is an era.
    /// </summary>
    private static IReadOnlyList<RegistryNode> Registry()
    {
        var nodes = new List<RegistryNode>();

        foreach (string line in File.ReadLines(Path.Combine(CatalogDirectory(), "TECH_TREE.md")))
        {
            if (!line.StartsWith('|')) continue;

            string[] cells = line.Split('|', StringSplitOptions.TrimEntries);

            // "| a | b | c | d | e |" splits to 7 with empty ends.
            if (cells.Length != 7) continue;
            if (!Enum.TryParse(cells[2], out Era era)) continue;

            string display = cells[1];
            string[] prereqs = cells[3] is "—" or ""
                ? []
                : cells[3].Split('+', StringSplitOptions.TrimEntries);

            nodes.Add(new RegistryNode(TechnologySlug.Of(display), display, era, prereqs));
        }

        return nodes;
    }

    private static IReadOnlyList<TechnologyDefinition> Shipped()
    {
        var kind = new TechnologyContentKind();
        var shipped = new List<TechnologyDefinition>();

        foreach (string path in Directory
                     .EnumerateFiles(Path.Combine(ContentDirectory(), "technologies"), "*.json")
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            shipped.Add((TechnologyDefinition)kind.Read(document.RootElement));
        }

        return shipped;
    }

    /// <summary>SDD-004 §8 (a): every shipped `tech` id appears in the registry.</summary>
    [Fact]
    public void Every_shipped_technology_appears_in_the_registry()
    {
        var registered = Registry().Select(n => n.Slug).ToHashSet(StringComparer.Ordinal);

        string[] orphans = [.. Shipped()
            .Select(t => t.Id.Value)
            .Where(id => !registered.Contains(id))];

        Assert.True(orphans.Length == 0,
            "shipped technologies absent from TECH_TREE.md: " + string.Join(", ", orphans));
    }

    /// <summary>
    /// The other direction, which design 22 §6.1 also requires: a registry node
    /// nobody ships is a gate the sheets can name and the engine cannot honour.
    /// </summary>
    [Fact]
    public void Every_registry_node_is_shipped_as_content()
    {
        var shipped = Shipped().Select(t => t.Id.Value).ToHashSet(StringComparer.Ordinal);

        string[] missing = [.. Registry()
            .Where(n => !shipped.Contains(n.Slug))
            .Select(n => $"{n.DisplayName} (expected {n.Slug}.json)")];

        Assert.True(missing.Length == 0,
            "TECH_TREE.md nodes with no content file: " + string.Join(", ", missing));
    }

    /// <summary>SDD-004 §8 (c): era claims agree between registry and content.</summary>
    [Fact]
    public void Era_agrees_between_the_registry_and_the_content()
    {
        var byId = Shipped().ToDictionary(t => t.Id.Value, StringComparer.Ordinal);

        var disagreements = new List<string>();
        foreach (RegistryNode node in Registry())
            if (byId.TryGetValue(node.Slug, out TechnologyDefinition? shipped)
                && shipped.AvailableFrom != node.Era)
                disagreements.Add(
                    $"{node.Slug}: registry says {node.Era}, content says {shipped.AvailableFrom}");

        Assert.True(disagreements.Count == 0, string.Join("; ", disagreements));
    }

    /// <summary>
    /// SDD-004 §8 (b): every prerequisite names a node that exists. The registry
    /// abbreviates ("Rotary" for "Rotary drilling"), so the CONTENT is the
    /// subject here — it is what the engine resolves — and each id must be a
    /// shipped technology.
    /// </summary>
    [Fact]
    public void Every_prerequisite_names_a_shipped_technology()
    {
        IReadOnlyList<TechnologyDefinition> shipped = Shipped();
        var ids = shipped.Select(t => t.Id.Value).ToHashSet(StringComparer.Ordinal);

        var dangling = new List<string>();
        foreach (TechnologyDefinition tech in shipped)
            foreach (ContentId prereq in tech.Prerequisites)
                if (!ids.Contains(prereq.Value))
                    dangling.Add($"{tech.Id.Value} requires {prereq.Value}, which nothing ships");

        Assert.True(dangling.Count == 0, string.Join("; ", dangling));
    }

    /// <summary>
    /// A prerequisite must come from an era no later than the node's own, or the
    /// node is unreachable in the era it claims to be available.
    /// </summary>
    [Fact]
    public void No_technology_requires_one_from_a_later_era()
    {
        IReadOnlyList<TechnologyDefinition> shipped = Shipped();
        var byId = shipped.ToDictionary(t => t.Id.Value, StringComparer.Ordinal);

        var backwards = new List<string>();
        foreach (TechnologyDefinition tech in shipped)
            foreach (ContentId prereq in tech.Prerequisites)
                if (byId.TryGetValue(prereq.Value, out TechnologyDefinition? required)
                    && required.AvailableFrom > tech.AvailableFrom)
                    backwards.Add(
                        $"{tech.Id.Value} ({tech.AvailableFrom}) requires " +
                        $"{prereq.Value} ({required.AvailableFrom})");

        Assert.True(backwards.Count == 0, string.Join("; ", backwards));
    }

    /// <summary>The graph the engine builds from shipped content is acyclic and
    /// constructible — the check that the whole tree, not each node, is sound.</summary>
    [Fact]
    public void The_shipped_tree_builds_a_usable_technology_state()
    {
        IReadOnlyList<TechnologyDefinition> shipped = Shipped();

        var state = new TechnologyState([.. shipped.Select(t => new TechnologyNode(
            new TechnologyId(t.Id),
            t.AvailableFrom,
            t.DiffusionLagTicks,
            [.. t.Prerequisites.Select(p => new TechnologyId(p))],
            t.Effects,
            t.GrantsDetectClass,
            t.Routes, t.Licence, t.Research))]);

        // Nothing is held before anything is acquired, and the E4 tip of the
        // longest chain is genuinely out of reach at the start.
        Assert.Empty(state.Acquired);
        Assert.False(state.Has(new TechnologyId(new ContentId("multilateral"))));
    }

    /// <summary>
    /// Finding 128, on the shipped tree: diffusion grants only nodes the
    /// registry gives a D route. Horizontal drilling is `R L S` — a hundred
    /// years of waiting must not produce it.
    /// </summary>
    [Fact]
    public void Diffusion_never_grants_a_technology_with_no_diffusion_route()
    {
        IReadOnlyList<TechnologyDefinition> shipped = Shipped();

        var state = new TechnologyState([.. shipped.Select(t => new TechnologyNode(
            new TechnologyId(t.Id), t.AvailableFrom, t.DiffusionLagTicks,
            [.. t.Prerequisites.Select(p => new TechnologyId(p))],
            t.Effects, t.GrantsDetectClass, t.Routes, t.Licence, t.Research))]);

        // Far past every lag, in the last era, so only the route can withhold.
        state.ApplyDiffusion(Era.E4, new Tick(0), new Tick(12_000));

        var granted = state.Acquired.Select(t => t.Value.Value).ToHashSet(StringComparer.Ordinal);

        string[] wrongly = [.. shipped
            .Where(t => !t.Routes.Contains(AcquisitionRoute.Diffusion))
            .Select(t => t.Id.Value)
            .Where(granted.Contains)];

        Assert.True(wrongly.Length == 0,
            "diffused without a D route: " + string.Join(", ", wrongly));

        // And the check is not vacuous: the baseline nodes DID arrive.
        Assert.Contains("rotary-drilling", granted);
    }

    // The slug rule itself (SDD-004 §8), on the cases that motivated it.
    [Theory]
    [InlineData("CO₂ flood", "co2-flood")]
    [InlineData("Telemetry / SCADA", "telemetry-scada")]
    [InlineData("2-D seismic", "2-d-seismic")]
    [InlineData("High-temp / gassy ESP", "high-temp-gassy-esp")]
    [InlineData("Leak detection (LDAR)", "leak-detection-ldar")]
    [InlineData("Deepwater operations ⚑", "deepwater-operations")]
    [InlineData("Polymer / chemical EOR", "polymer-chemical-eor")]
    public void The_slug_rule_folds_display_names_as_specified(string display, string expected) =>
        Assert.Equal(expected, TechnologySlug.Of(display));

    private static string RepositoryRoot([CallerFilePath] string thisFile = "")
    {
        DirectoryInfo directory = new FileInfo(thisFile).Directory!;   // tests/OGSim.Capabilities.Tests
        return directory.Parent!.Parent!.FullName;
    }

    private static string CatalogDirectory() =>
        Path.Combine(RepositoryRoot(), "plans", "catalog");

    private static string ContentDirectory() => Path.Combine(RepositoryRoot(), "content");
}
