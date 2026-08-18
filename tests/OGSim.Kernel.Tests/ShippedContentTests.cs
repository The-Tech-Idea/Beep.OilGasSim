// R3.7 — the shipped catalogues, loaded from the real content/ directory.
//
// This is the first test that runs the actual pipeline over actual files rather
// than fixtures, and it is the one that would catch a shipped file going stale:
// if someone edits content/materials/crude-oil.json into something the loader
// rejects, this fails, naming the file.
//
// It also proves the R2.4 material catalogue can be BUILT FROM CONTENT rather
// than constructed in a test — the join R2 and R3 exist either side of.

using System.Runtime.CompilerServices;
using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class ShippedContentTests
{
    /// <summary>Anchored to this file's compile-time path, so the suite finds
    /// content/ from any working directory or runner (same trick as EngineCorpus).</summary>
    private static string ContentRoot([CallerFilePath] string thisFile = "")
    {
        DirectoryInfo directory = new FileInfo(thisFile).Directory!;   // tests/OGSim.Kernel.Tests
        return Path.Combine(directory.Parent!.Parent!.FullName, "content");
    }

    private sealed class DiskSource(string name, int order, string root, string subdirectory)
        : IContentSource
    {
        public string Name => name;
        public int DeclaredOrder => order;

        public IReadOnlyList<ContentFile> Files { get; } =
        [
            .. Directory.EnumerateFiles(Path.Combine(root, subdirectory), "*.json")
                        .OrderBy(p => p, StringComparer.Ordinal)
                        .Select(p => new ContentFile(
                            subdirectory + "/" + Path.GetFileName(p), File.ReadAllText(p)))
        ];
    }

    private sealed class NoPlugins : IModuleRegistry
    {
        public bool CanBind(ContentId plugin, Type contract) => true;
        public T Bind<T>(ContentId plugin) where T : class =>
            throw new InvariantFault("test", null, "no plugins in shipped content yet");
    }

    /// <summary>
    /// SDD-004 §5's bootstrap rule: property kinds are the vocabulary everything
    /// else is written in, so they load first and supply the dimension map that
    /// stage 3 binds material quantities against.
    /// </summary>
    private static (ICatalogSet Kinds, ICatalogSet Loaded) LoadShipped()
    {
        string root = ContentRoot();
        Assert.True(Directory.Exists(root), $"content/ not found at {root}");

        var bootstrap = new ContentLoader([new PropertyKindContentKind()], new NoPlugins());
        var kindResult = bootstrap.LoadAll([new DiskSource("base", 0, root, "property-kinds")]);
        ICatalogSet kinds = Assert.IsType<ContentLoaded>(kindResult).Catalogues;

        var dimensions = new Dictionary<ContentId, Dimension>();
        foreach (PropertyKindDefinition kind in kinds.Of<PropertyKindDefinition>().All)
            dimensions.Add(kind.Id, kind.Dimension);

        var loader = new ContentLoader(
            [new PropertyKindContentKind(), new MaterialContentKind(dimensions),
             new RockTypeContentKind()],
            new NoPlugins());

        var result = loader.LoadAll(
        [
            new DiskSource("base", 0, root, "property-kinds"),
            new DiskSource("base-materials", 0, root, "materials"),
            new DiskSource("base-rocks", 0, root, "rock-types"),
        ]);

        // If this fails the message names the offending file and stage.
        if (result is ContentFailures failed)
            Assert.Fail("shipped content did not load:\n  " + string.Join("\n  ",
                failed.Failures.Select(f => $"{f.File} [{f.Stage}] {f.Message}")));

        return (kinds, ((ContentLoaded)result).Catalogues);
    }

    [Fact] // The shipped pack loads clean — the regression guard on content/
    public void R3V7_the_shipped_content_loads()
    {
        (ICatalogSet kinds, ICatalogSet all) = LoadShipped();

        Assert.NotEmpty(kinds.Of<PropertyKindDefinition>().All);
        Assert.NotEmpty(all.Of<MaterialDefinition>().All);
        Assert.NotEmpty(all.Of<RockTypeDefinition>().All);
    }

    [Fact] // research §5's product list, as data rather than an enum
    public void R3V7_the_material_set_covers_the_ppdm_product_list()
    {
        (_, ICatalogSet all) = LoadShipped();
        ICatalog<MaterialDefinition> materials = all.Of<MaterialDefinition>();

        foreach (string id in new[]
        {
            "crude-oil", "condensate", "natural-gas", "sales-gas",
            "produced-water", "carbon-dioxide", "hydrogen-sulphide",
            "nitrogen", "sulphur",
        })
            Assert.True(materials.TryGet(new ContentId(id), out _), $"missing material '{id}'");

        // Phases are declared, not inferred from the name.
        Assert.Equal(PhaseAtStandardConditions.Liquid,
                     materials[new ContentId("crude-oil")].Phase);
        Assert.Equal(PhaseAtStandardConditions.Gas,
                     materials[new ContentId("sales-gas")].Phase);
        Assert.Equal(PhaseAtStandardConditions.Aqueous,
                     materials[new ContentId("produced-water")].Phase);
        Assert.Equal(PhaseAtStandardConditions.Solid,
                     materials[new ContentId("sulphur")].Phase);

        // Sellability is a property of the material, and produced water is the
        // one that costs money rather than earning it (research §5).
        Assert.True(materials[new ContentId("crude-oil")].IsSold);
        Assert.False(materials[new ContentId("produced-water")].IsSold);
    }

    [Fact] // Quantities in content arrive as canonical SI, not as the authored unit
    public void R3V7_authored_units_are_bound_to_si()
    {
        (_, ICatalogSet all) = LoadShipped();
        MaterialDefinition oil = all.Of<MaterialDefinition>()[new ContentId("crude-oil")];

        MaterialPropertyEntry density = oil.Properties
            .Single(p => p.Kind == new ContentId("density"));
        Assert.Equal(850.0, density.SiValue, 9);

        // "3.2 cP" authored; 3.2e-3 Pa·s stored.
        MaterialPropertyEntry viscosity = oil.Properties
            .Single(p => p.Kind == new ContentId("viscosity"));
        Assert.Equal(3.2e-3, viscosity.SiValue, 12);

        // "42.7 MJ/kg" authored; joules per kilogram stored.
        MaterialPropertyEntry heating = oil.Properties
            .Single(p => p.Kind == new ContentId("heating-value"));
        Assert.Equal(42.7e6, heating.SiValue, 3);
    }

    [Fact] // R2.4 meets R3: the catalogue is BUILT from content, not from a test
    public void R3V7_the_material_catalogue_is_built_from_shipped_content()
    {
        (ICatalogSet kinds, ICatalogSet all) = LoadShipped();

        var propertyKinds = new Dictionary<ContentId, PropertyKind>();
        foreach (PropertyKindDefinition definition in kinds.Of<PropertyKindDefinition>().All)
            propertyKinds.Add(definition.Id, new PropertyKind(
                definition.Id, definition.Dimension,
                definition.MinimumValid, definition.MaximumValid, definition.Space));

        var asOf = new GameDate(1965, 1);
        var definitions =
            new List<(ContentId, PhaseAtStandardConditions, IReadOnlyList<IProperty>)>();

        foreach (MaterialDefinition material in all.Of<MaterialDefinition>().All)
        {
            var properties = new List<IProperty>();
            foreach (MaterialPropertyEntry entry in material.Properties)
            {
                // Property.Validated re-checks every shipped value against its
                // kind's range — so a bad number in content/ fails HERE, not on
                // the tick that first read it.
                PropertyKind kind = propertyKinds[entry.Kind];
                properties.Add(Property.Validated(
                    kind, new PointValue(entry.SiValue), entry.Source, asOf));
            }
            definitions.Add((material.Id, material.Phase, properties));
        }

        var catalogue = new MaterialCatalogue(definitions);

        Assert.Equal(all.Of<MaterialDefinition>().All.Count, catalogue.Count);

        // Ordinals come from the id sort, so the catalogue and the content
        // agree on ordering without either being told (SDD-004 §6).
        Assert.Equal(new ContentId("carbon-dioxide"), catalogue[new MaterialId(0)].Id);

        // And a Composition sized to it is ready for the flow engine.
        Assert.Equal(catalogue.Count, catalogue.ZeroComposition().Length);
    }

    [Fact] // Every shipped value sits inside its kind's declared validity range
    public void R3V7_no_shipped_property_is_outside_its_kinds_range()
    {
        (ICatalogSet kinds, ICatalogSet all) = LoadShipped();

        var ranges = new Dictionary<ContentId, PropertyKindDefinition>();
        foreach (PropertyKindDefinition definition in kinds.Of<PropertyKindDefinition>().All)
            ranges.Add(definition.Id, definition);

        foreach (MaterialDefinition material in all.Of<MaterialDefinition>().All)
            foreach (MaterialPropertyEntry entry in material.Properties)
            {
                PropertyKindDefinition range = ranges[entry.Kind];
                Assert.True(
                    entry.SiValue >= range.MinimumValid && entry.SiValue <= range.MaximumValid,
                    $"{material.Id}.{entry.Kind} = {entry.SiValue} is outside " +
                    $"[{range.MinimumValid}, {range.MaximumValid}]");
            }
    }

    [Fact] // Rock types load with their lithology ranges intact
    public void R3V7_shipped_rock_types_are_physical()
    {
        (_, ICatalogSet all) = LoadShipped();
        ICatalog<RockTypeDefinition> rocks = all.Of<RockTypeDefinition>();

        RockTypeDefinition sandstone = rocks[new ContentId("sandstone")];
        Assert.Equal(0.22, sandstone.TypicalPorosity, 9);
        Assert.Equal(1.0, sandstone.TypicalPermeability / (250.0 * 9.869233e-16), 9);

        // Shale is tight and sandstone is not — orders of magnitude, from content.
        RockTypeDefinition shale = rocks[new ContentId("shale")];
        Assert.True(sandstone.TypicalPermeability / shale.TypicalPermeability > 1e4);
    }

    /// <summary>
    /// R20c.9. <b>The shipped facility sheets load, and they carry the numbers
    /// the C# ladders carried</b> — value by value, because this is a
    /// TRANSCRIPTION and a transcription's only real risk is a digit.
    ///
    /// <para>Pinned against the literals rather than against
    /// <c>Defaults.SeparatorLadder</c>: comparing content to the thing it
    /// replaces passes trivially once the ladder is built FROM the content, and
    /// then stops being evidence about anything. These are the values as they
    /// stood before the move, written out here so that a later rebalance is a
    /// visible edit to this test as well as to the sheet.</para>
    ///
    /// <para>Two of them were nearly lost in the move and are the reason this
    /// test asserts every field: <c>waterIntoLiquid</c>, without which a vessel
    /// produces dry crude at every tier and the sales spec cannot be breached
    /// (finding 173), and <c>operatingPressure</c>, which the vessel IMPOSES on
    /// the network upstream (finding 157). Both sit on records long enough that
    /// the first transcription of the datasheet omitted them.</para>
    /// </summary>
    [Fact]
    public void R20cV9_the_shipped_facility_sheets_carry_the_ladders_they_replaced()
    {
        ICatalogSet loaded = LoadFacilities();

        SeparatorDefinition first = Facility<SeparatorDefinition>(loaded, "separator-3phase-e1");

        Assert.Equal(0, first.Rung);
        Assert.Equal(50.0, first.GasCapacityKgPerSecond, precision: 9);
        Assert.Equal(12.0, first.LiquidCapacityKgPerSecond, precision: 9);
        Assert.Equal(30.0, first.VolumeCubicMetres, precision: 9);
        Assert.Equal(0.0, first.LiquidFromGas, precision: 9);
        Assert.Equal(0.0, first.GasFromLiquid, precision: 9);
        Assert.Equal(0.0, first.WaterFromLiquid, precision: 9);
        Assert.Equal(0.07, first.WaterIntoLiquid, precision: 9);
        Assert.Equal(0.05, first.DesignRateCubicMetresPerSecond, precision: 9);
        Assert.Equal(15.0e5, first.OperatingPressurePascals, precision: 6);

        SeparatorDefinition second = Facility<SeparatorDefinition>(loaded, "separator-3phase-e2");

        Assert.Equal(1, second.Rung);
        Assert.Equal(150.0, second.GasCapacityKgPerSecond, precision: 9);
        Assert.Equal(40.0, second.LiquidCapacityKgPerSecond, precision: 9);
        Assert.Equal(90.0, second.VolumeCubicMetres, precision: 9);
        Assert.Equal(0.15, second.DesignRateCubicMetresPerSecond, precision: 9);

        Assert.Equal(0.0, Facility<GasPlantDefinition>(loaded, "gas-plant-none").CapacityKgPerSecond);
        Assert.Equal(3.0, Facility<GasPlantDefinition>(loaded, "gas-plant-e1").CapacityKgPerSecond, 9);
        Assert.Equal(8.0, Facility<GasPlantDefinition>(loaded, "gas-plant-e2").CapacityKgPerSecond, 9);

        Assert.Equal(20.0, Facility<ExportLineDefinition>(loaded, "export-line-e1").OfftakeKgPerSecond, 9);
        Assert.Equal(40.0, Facility<ExportLineDefinition>(loaded, "export-line-e2").OfftakeKgPerSecond, 9);
        Assert.Equal(80.0, Facility<ExportLineDefinition>(loaded, "export-line-e3").OfftakeKgPerSecond, 9);

        Assert.Equal(0.0, Facility<TreaterDefinition>(loaded, "treater-none").WaterRemoved);
        Assert.Equal(0.90, Facility<TreaterDefinition>(loaded, "heater-treater").WaterRemoved, 9);
        Assert.Equal(0.98, Facility<TreaterDefinition>(loaded, "heater-treater-desalter").WaterRemoved, 9);

        TankDefinition tank = Facility<TankDefinition>(loaded, "tank-terminal-e1");

        Assert.Equal(150.0e6, tank.CapacityKilograms, precision: 3);
        Assert.Equal(0.001, tank.VapourLossRatePerTick, precision: 9);
        Assert.Equal(300.0e6, Facility<TankDefinition>(loaded, "tank-farm-e2").CapacityKilograms, 3);

        Assert.Equal(8, Facility<ManifoldDefinition>(loaded, "manifold-fixed-8").Slots);
        Assert.Equal(16, Facility<ManifoldDefinition>(loaded, "manifold-16slot").Slots);
    }

    /// <summary>
    /// Every ladder's rungs are 0..n-1 with no gap and no repeat, per kind.
    ///
    /// <para>A duplicate rung is the defect that would not otherwise show: two
    /// definitions claiming one position sort by whatever the sort is stable
    /// against, so the ladder a player climbs would depend on file order. A gap
    /// is milder and still wrong — the install command walks one rung at a time.</para>
    /// </summary>
    [Fact]
    public void The_shipped_ladders_are_dense_and_start_at_the_absent_rung()
    {
        ICatalogSet loaded = LoadFacilities();

        Dense<SeparatorDefinition>(loaded);
        Dense<TankDefinition>(loaded);
        Dense<TreaterDefinition>(loaded);
        Dense<GasPlantDefinition>(loaded);
        Dense<ExportLineDefinition>(loaded);
        Dense<ManifoldDefinition>(loaded);
    }

    private static void Dense<TDefinition>(ICatalogSet loaded)
        where TDefinition : FacilityUnitDefinition
    {
        var rungs = new List<int>();
        foreach (TDefinition unit in loaded.Of<TDefinition>().All) rungs.Add(unit.Rung);

        rungs.Sort();

        Assert.NotEmpty(rungs);

        for (int i = 0; i < rungs.Count; i++)
            Assert.Equal(i, rungs[i]);
    }

    /// <summary>Catalogues are keyed by the CONCRETE definition type — the
    /// loader groups on what it built, so there is no catalogue of the shared
    /// base and each ladder is addressed by its own kind.</summary>
    private static TDefinition Facility<TDefinition>(ICatalogSet loaded, string id)
        where TDefinition : FacilityUnitDefinition =>
        loaded.Of<TDefinition>()[new ContentId(id)];

    /// <summary>The facility sheets through the real loader, from the real
    /// directory — the same route the engine takes.</summary>
    private static ICatalogSet LoadFacilities()
    {
        string root = ContentRoot();

        var loader = new ContentLoader(
            [
                new SeparatorContentKind(), new TankContentKind(), new TreaterContentKind(),
                new GasPlantContentKind(), new ExportLineContentKind(), new ManifoldContentKind(),
            ],
            new NoPlugins());

        ContentLoadResult result =
            loader.LoadAll([new DiskSource("base", 0, root, "facilities")]);

        if (result is ContentFailures failed)
            Assert.Fail(string.Join("; ", failed.Failures.Select(
                f => $"{f.File} {f.JsonPath} [{f.Stage}] {f.Message}")));

        return Assert.IsType<ContentLoaded>(result).Catalogues;
    }
}
