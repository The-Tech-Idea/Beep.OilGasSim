// R3.2–R3.6 — the six-stage loader (SDD-004 §5–§7).
//
// R3-V2 is the one with teeth: all files run all stages and failures accumulate.
// The fixture kind below is deliberately a TEST kind — if any assertion here
// needed a change in ContentLoader.cs to accommodate a new content kind, R3's
// design would be wrong (R3 §3's acceptance criterion), so proving the loader
// can carry a kind it has never heard of IS the test.

using System.Text.Json;
using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class ContentLoaderTests
{
    // ------------------------------------------------------------- fixtures

    private sealed record RockTypeDefinition(
        ContentId Id, double Porosity, ContentId? FluidSystem) : ContentDefinition(Id);

    /// <summary>A kind the loader has never heard of, registered from outside.</summary>
    private sealed class RockTypeKind : IContentKind
    {
        public string Name => "rock-type";

        public ContentDefinition Read(JsonElement element)
        {
            var id = new ContentId(element.GetProperty("id").GetString()!);

            // Stage 3: a quantity property goes through the unit grammar, so a
            // wrong dimension fails here rather than at first use.
            double porosity = element.TryGetProperty("porosity", out JsonElement p)
                ? UnitGrammar.ParseToSi(p.GetString()!, Dimension.Dimensionless)
                : throw new JsonException("missing 'porosity'");

            ContentId? fluid = element.TryGetProperty("fluidSystem", out JsonElement f)
                ? new ContentId(f.GetString()!)
                : null;

            return new RockTypeDefinition(id, porosity, fluid);
        }

        public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) =>
            definition is RockTypeDefinition { FluidSystem: ContentId fluid }
                ? [new ContentReference("fluid-system", fluid, "$.fluidSystem")]
                : [];

        public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
        {
            var problems = new List<string>();
            if (definition is RockTypeDefinition rock)
            {
                if (rock.Porosity <= 0.0) problems.Add("porosity must be positive");
                if (rock.Porosity >= 1.0) problems.Add("porosity must be below 1");
            }
            return problems;
        }
    }

    private sealed record FluidSystemDefinition(ContentId Id) : ContentDefinition(Id);

    private sealed class FluidSystemKind : IContentKind
    {
        public string Name => "fluid-system";
        public ContentDefinition Read(JsonElement element) =>
            new FluidSystemDefinition(new ContentId(element.GetProperty("id").GetString()!));
        public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition d) => [];
        public IReadOnlyList<string> ConsistencyProblems(ContentDefinition d) => [];
    }

    private sealed class Source(string name, int order, params (string Path, string Json)[] files)
        : IContentSource
    {
        public string Name => name;
        public int DeclaredOrder => order;
        public IReadOnlyList<ContentFile> Files { get; } =
            [.. files.Select(f => new ContentFile(f.Path, f.Json))];
    }

    private sealed class NoPlugins : IModuleRegistry
    {
        public bool CanBind(ContentId plugin, Type contract) => true;
        public T Bind<T>(ContentId plugin) where T : class =>
            throw new InvariantFault("test", null, "not used");
    }

    private static ContentLoader Loader() =>
        new([new RockTypeKind(), new FluidSystemKind()], new NoPlugins());

    private const string GoodFluid =
        """{ "kind": "fluid-system", "id": "black-oil-a" }""";

    private static string Rock(string id, string porosity = "22 pct", string? fluid = "black-oil-a") =>
        fluid is null
            ? $$"""{ "kind": "rock-type", "id": "{{id}}", "porosity": "{{porosity}}" }"""
            : $$"""{ "kind": "rock-type", "id": "{{id}}", "porosity": "{{porosity}}", "fluidSystem": "{{fluid}}" }""";

    // ------------------------------------------------------------- success

    [Fact] // The whole path, and a kind the loader has never heard of
    public void R3V1_a_valid_pack_loads_into_typed_catalogues()
    {
        ContentLoadResult result = Loader().LoadAll(
        [
            new Source("base", 0,
                ("fluids/black-oil-a.json", GoodFluid),
                ("rocks/sandstone.json", Rock("sandstone")),
                ("rocks/limestone.json", Rock("limestone", "14 pct"))),
        ]);

        var loaded = Assert.IsType<ContentLoaded>(result);
        ICatalog<RockTypeDefinition> rocks = loaded.Catalogues.Of<RockTypeDefinition>();

        Assert.Equal(2, rocks.All.Count);
        // Ordinal-sorted by id, save-stable (SDD-004 §6) — not file order.
        Assert.Equal(new ContentId("limestone"), rocks.All[0].Id);
        Assert.Equal(new ContentId("sandstone"), rocks.All[1].Id);

        Assert.Equal(0.22, rocks[new ContentId("sandstone")].Porosity, 12);
        Assert.True(rocks.TryGet(new ContentId("limestone"), out RockTypeDefinition? lime));
        Assert.Equal(0.14, lime!.Porosity, 12);

        Assert.Throws<SaveDataFault>(() => rocks[new ContentId("chalk")]);
    }

    [Fact] // Stage 4 is TWO passes, so a forward reference is legal
    public void R3V1_a_reference_may_point_at_a_file_loaded_later()
    {
        // The rock names the fluid; the fluid file sorts AFTER it by path.
        ContentLoadResult result = Loader().LoadAll(
        [
            new Source("base", 0,
                ("a-rock.json", Rock("sandstone")),
                ("z-fluid.json", GoodFluid)),
        ]);

        Assert.IsType<ContentLoaded>(result);
    }

    // ------------------------------------------------------------- R3-V2

    [Fact] // R3-V2: EVERY malformed file is reported, never just the first
    public void R3V2_all_files_run_the_per_file_stages_and_failures_accumulate()
    {
        ContentLoadResult result = Loader().LoadAll(
        [
            new Source("base", 0,
                ("broken.json", "{ this is not json"),
                ("nokind.json", """{ "id": "x" }"""),
                ("unknown.json", """{ "kind": "rock-typo", "id": "x" }"""),
                ("badunit.json", Rock("bad-unit", "22 psi"))),
        ]);

        var failures = Assert.IsType<ContentFailures>(result).Failures;

        // Four broken files, four reports — not one report and a stop.
        Assert.Equal(4, failures.Count);
        Assert.Contains(failures, f => f.Stage == LoadStage.Parse);
        Assert.Contains(failures, f => f.Stage == LoadStage.Shape && f.JsonPath == "$.kind");
        Assert.Contains(failures, f => f.Stage == LoadStage.Units);

        // Every failure names its file, so a fix is one edit away.
        Assert.All(failures, f => Assert.False(string.IsNullOrEmpty(f.File)));
        Assert.All(failures, f => Assert.Equal("base", f.Source));
    }

    /// <summary>
    /// SDD-004 §5's R3.2 refinement: stages 1–3 are per-file and unconditional;
    /// stages 4–6 are cross-file and need a complete index. A file that failed to
    /// parse leaves no entry, so running reference resolution anyway would report
    /// a dangling reference to it — a cascade of spurious failures burying the one
    /// real error. So the cross-file stages wait for a clean index.
    /// </summary>
    [Fact]
    public void R3V2_cross_file_stages_wait_for_a_complete_index()
    {
        // Round one: a malformed file AND a dangling reference. Only the
        // malformed file is reported — the reference cannot be judged yet.
        var withParseError = Assert.IsType<ContentFailures>(Loader().LoadAll(
        [
            new Source("base", 0,
                ("broken.json", "{ not json"),
                ("orphan.json", Rock("orphan", fluid: "no-such-fluid"))),
        ])).Failures;

        Assert.Equal(LoadStage.Parse, Assert.Single(withParseError).Stage);

        // Round two: the malformed file fixed. NOW the reference and consistency
        // stages run, and report everything they can see.
        var crossFile = Assert.IsType<ContentFailures>(Loader().LoadAll(
        [
            new Source("base", 0,
                ("fluids/f.json", GoodFluid),
                ("orphan.json", Rock("orphan", fluid: "no-such-fluid")),
                ("badrange.json", Rock("bad-range", "150 pct"))),
        ])).Failures;

        Assert.Equal(2, crossFile.Count);
        Assert.Contains(crossFile, f => f.Stage == LoadStage.References
                                     && f.Message.Contains("no-such-fluid"));
        Assert.Contains(crossFile, f => f.Stage == LoadStage.Consistency
                                     && f.Message.Contains("below 1"));
    }

    [Fact] // Catalogues on success, failures otherwise — NEVER both
    public void R3V2_one_bad_file_means_no_catalogues_at_all()
    {
        ContentLoadResult result = Loader().LoadAll(
        [
            new Source("base", 0,
                ("fluids/ok.json", GoodFluid),
                ("rocks/ok.json", Rock("sandstone")),
                ("rocks/bad.json", Rock("bad", "150 pct"))),
        ]);

        // The engine does not start with bad content (10 §3, G2): there is no
        // partial catalogue to be tempted by.
        Assert.IsType<ContentFailures>(result);
    }

    [Fact] // A typo'd kind says what it probably meant
    public void R3V2_an_unknown_kind_suggests_the_nearest_registered_one()
    {
        var failures = Assert.IsType<ContentFailures>(Loader().LoadAll(
            [new Source("base", 0, ("x.json", """{ "kind": "rock-typ", "id": "x" }"""))])).Failures;

        Assert.Contains(failures, f => f.Message.Contains("did you mean 'rock-type'"));
    }

    // ------------------------------------------------------------- R3.6 mods

    [Fact] // A later source replaces an earlier entry WHOLE
    public void R3V11_a_mod_overrides_base_content_by_declared_order()
    {
        ContentLoadResult result = Loader().LoadAll(
        [
            new Source("mod", 10, ("rocks/sandstone.json", Rock("sandstone", "35 pct"))),
            new Source("base", 0,
                ("fluids/f.json", GoodFluid),
                ("rocks/sandstone.json", Rock("sandstone", "22 pct"))),
        ]);

        var loaded = Assert.IsType<ContentLoaded>(result);
        ICatalog<RockTypeDefinition> rocks = loaded.Catalogues.Of<RockTypeDefinition>();

        Assert.Single(rocks.All);
        Assert.Equal(0.35, rocks[new ContentId("sandstone")].Porosity, 12);
    }

    [Fact] // Two mods at the same order: neither can be said to win
    public void R3V11_two_sources_overriding_at_the_same_order_is_a_failure_naming_both()
    {
        ContentLoadResult result = Loader().LoadAll(
        [
            new Source("base", 0, ("f.json", GoodFluid), ("r.json", Rock("sandstone"))),
            new Source("mod-a", 10, ("r.json", Rock("sandstone", "30 pct"))),
            new Source("mod-b", 10, ("r.json", Rock("sandstone", "40 pct"))),
        ]);

        var failures = Assert.IsType<ContentFailures>(result).Failures;
        LoadFailure clash = Assert.Single(failures);

        Assert.Equal(LoadStage.Consistency, clash.Stage);
        Assert.Contains("mod-a", clash.Message);
        Assert.Contains("same declared order", clash.Message);
    }

    [Fact] // Load order must not depend on how the host enumerated the sources
    public void R3V11_source_order_is_by_declared_order_not_argument_order()
    {
        static double Porosity(params IContentSource[] sources)
        {
            var loaded = Assert.IsType<ContentLoaded>(Loader().LoadAll(sources));
            return loaded.Catalogues.Of<RockTypeDefinition>()[new ContentId("sandstone")].Porosity;
        }

        IContentSource baseContent = new Source("base", 0,
            ("f.json", GoodFluid), ("r.json", Rock("sandstone", "22 pct")));
        IContentSource mod = new Source("mod", 5, ("r.json", Rock("sandstone", "31 pct")));

        Assert.Equal(0.31, Porosity(baseContent, mod), 12);
        Assert.Equal(0.31, Porosity(mod, baseContent), 12);
    }

    // ------------------------------------------------------------- registration

    [Fact] // One kind, one reader (law L5 at the content boundary)
    public void L5_a_kind_cannot_be_registered_twice()
    {
        Assert.Throws<InvariantFault>(
            () => new ContentLoader([new RockTypeKind(), new RockTypeKind()], new NoPlugins()));
    }

    [Fact] // An empty pack is valid and produces empty catalogues, not a fault
    public void R3V1_an_empty_source_set_loads_to_empty_catalogues()
    {
        var loaded = Assert.IsType<ContentLoaded>(Loader().LoadAll([new Source("base", 0)]));
        Assert.Empty(loaded.Catalogues.Of<RockTypeDefinition>().All);
    }
}
