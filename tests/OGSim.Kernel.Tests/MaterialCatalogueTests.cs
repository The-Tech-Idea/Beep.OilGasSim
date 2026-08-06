// R2.1 / R2.2 / R2.4 — property kinds, properties and the material catalogue
// (SDD-002 §2b, SDD-004 §6).
//
// R2-V10 is the one with teeth: a correlation given out-of-range inputs raises a
// model fault and never extrapolates silently. R2-V9 (uncertainty defaults
// monotonic in provenance confidence) belongs to R14, where the defaults are
// assigned — here the ORDER those defaults will read is what is pinned.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class MaterialCatalogueTests
{
    private static readonly GameDate Now = new(1970, 6);

    private static PropertyKind Kind(
        string id, Dimension dimension, double min, double max,
        BeliefSpace space = BeliefSpace.Linear) =>
        new(new ContentId(id), dimension, min, max, space);

    // ------------------------------------------------------------- R2.1

    [Fact] // The range is on the kind, so it is checked once for every consumer
    public void R2V10_a_value_outside_the_kinds_range_is_a_model_fault()
    {
        PropertyKind density = Kind("density", Dimension.Density, 500.0, 1200.0);

        Assert.True(density.IsInRange(850.0));
        Assert.False(density.IsInRange(1400.0));

        var fault = Assert.Throws<ModelFault>(() => density.AssertInRange(1400.0, null));
        Assert.Equal(FaultClass.Model, fault.Fault.Class);
        Assert.Equal("R2-V10", fault.Fault.Rule);

        // The numbers are the bug report.
        Assert.Contains("1400", fault.Fault.Detail);
        Assert.Contains("500", fault.Fault.Detail);
        Assert.Contains("1200", fault.Fault.Detail);
    }

    [Fact] // A log-space kind that permits zero would NaN the first belief update
    public void R2V10_a_log_space_kind_must_be_strictly_positive()
    {
        // Permeability is multiplicative — Log space, and never zero.
        PropertyKind valid = Kind("permeability", Dimension.Permeability,
                                  1e-18, 1e-11, BeliefSpace.Log);
        Assert.Equal(BeliefSpace.Log, valid.Space);

        var fault = Assert.Throws<InvariantFault>(
            () => Kind("bad-perm", Dimension.Permeability, 0.0, 1e-11, BeliefSpace.Log));
        Assert.Contains("strictly positive", fault.Fault.Detail);

        // Linear kinds may legitimately reach zero — water cut starts there.
        PropertyKind waterCut = Kind("water-cut", Dimension.Dimensionless, 0.0, 1.0);
        Assert.True(waterCut.IsInRange(0.0));
    }

    [Fact] // An inverted or NaN range would accept everything or nothing
    public void R2V10_a_malformed_range_is_refused_at_construction()
    {
        Assert.Throws<InvariantFault>(() => Kind("inverted", Dimension.Pressure, 100.0, 10.0));
        Assert.Throws<InvariantFault>(() => Kind("nan", Dimension.Pressure, double.NaN, 10.0));
    }

    // ------------------------------------------------------------- R2.2

    [Fact] // R2 §2.1: a property holds a distribution, and all four parts are required
    public void R2V9_a_property_carries_value_provenance_and_as_of()
    {
        PropertyKind porosity = Kind("porosity", Dimension.Dimensionless, 0.0, 0.5);

        Property measured = Property.Validated(
            porosity, new PointValue(0.22), Provenance.Core, Now);

        Assert.Equal(new ContentId("porosity"), measured.Kind);
        Assert.Equal(Provenance.Core, measured.Source);
        Assert.Equal(Now, measured.AsOf);
        Assert.Equal(0.22, measured.Value.P50, 12);
    }

    [Fact] // The TAILS are validated, not just the centre
    public void R2V10_a_distribution_whose_downside_leaves_the_range_is_refused()
    {
        PropertyKind porosity = Kind("porosity", Dimension.Dimensionless, 0.05, 0.40);

        // Centre is physical; the P90 low case is not.
        var wide = new NormalDistribution(0.10, 0.08);
        Assert.True(porosity.IsInRange(wide.P50));
        Assert.False(porosity.IsInRange(wide.P90));

        Assert.Throws<ModelFault>(
            () => Property.Validated(porosity, wide, Provenance.Seismic, Now));

        // A tighter distribution inside the range is accepted.
        Property ok = Property.Validated(
            porosity, new NormalDistribution(0.22, 0.02), Provenance.Log, Now);
        Assert.Equal(0.22, ok.Value.Mean, 12);
    }

    [Fact] // R2 §2.2: the confidence ordering IS the contract
    public void R2V9_provenance_is_ordered_by_confidence()
    {
        Assert.True(Provenance.Assumed < Provenance.Analogue);
        Assert.True(Provenance.Analogue < Provenance.Seismic);
        Assert.True(Provenance.Seismic < Provenance.Log);
        Assert.True(Provenance.Log < Provenance.WellTest);
        Assert.True(Provenance.WellTest < Provenance.Core);

        // The one that surprises people: dynamic data outranks a core plug,
        // which is why the p/Z deduction is as powerful as it is.
        Assert.True(Provenance.Core < Provenance.ProductionHistory);
        Assert.True(Provenance.ProductionHistory < Provenance.Measured);
    }

    // ------------------------------------------------------------- R2.4

    private static MaterialCatalogue Catalogue(params string[] ids)
    {
        var definitions = new List<(ContentId, PhaseAtStandardConditions, IReadOnlyList<IProperty>)>();
        foreach (string id in ids)
            definitions.Add((new ContentId(id), PhaseAtStandardConditions.Liquid, []));
        return new MaterialCatalogue(definitions);
    }

    [Fact] // SDD-004 §6: ordinal = index into the ID-SORTED list
    public void R2V1_ordinals_come_from_the_id_sort_not_the_declaration_order()
    {
        MaterialCatalogue catalogue = Catalogue("water", "oil", "gas");

        Assert.Equal(3, catalogue.Count);
        Assert.Equal(new ContentId("gas"), catalogue[new MaterialId(0)].Id);
        Assert.Equal(new ContentId("oil"), catalogue[new MaterialId(1)].Id);
        Assert.Equal(new ContentId("water"), catalogue[new MaterialId(2)].Id);

        // Declaring in a different order must produce identical ordinals, or two
        // runs of the same content would index Composition differently.
        MaterialCatalogue other = Catalogue("oil", "water", "gas");
        for (int i = 0; i < catalogue.Count; i++)
            Assert.Equal(catalogue[new MaterialId(i)].Id, other[new MaterialId(i)].Id);
    }

    [Fact] // The ordinal is the catalogue's to assign, and it round-trips
    public void R2V1_a_material_knows_its_own_ordinal()
    {
        MaterialCatalogue catalogue = Catalogue("water", "oil", "gas");

        IMaterial oil = catalogue.Resolve(new ContentId("oil"));
        Assert.Equal(1, oil.Ordinal.Ordinal);
        Assert.Same(oil, catalogue[oil.Ordinal]);
    }

    [Fact] // A missing id is a content fault, never null (SDD-004 §3)
    public void R2V1_resolving_an_unknown_material_faults()
    {
        MaterialCatalogue catalogue = Catalogue("oil", "gas");

        Assert.Throws<SaveDataFault>(() => catalogue.Resolve(new ContentId("helium")));
        Assert.False(catalogue.TryResolve(new ContentId("helium"), out IMaterial? absent));
        Assert.Null(absent);

        Assert.Throws<InvariantFault>(() => catalogue[new MaterialId(9)]);
        Assert.Throws<InvariantFault>(() => catalogue[new MaterialId(-1)]);
    }

    [Fact] // Two definitions of one id would give one material two ordinals
    public void R2V1_a_duplicate_material_id_is_refused()
    {
        Assert.Throws<InvariantFault>(() => Catalogue("oil", "gas", "oil"));
    }

    [Fact] // A Composition's length must match the catalogue that gave its ordinals meaning
    public void R2V1_the_catalogue_sizes_a_zero_composition()
    {
        MaterialCatalogue catalogue = Catalogue("water", "oil", "gas");

        Composition empty = catalogue.ZeroComposition();
        Assert.Equal(3, empty.Length);
        Assert.Equal(0.0, empty.Total.KgPerSecond);

        // And it composes with real streams from the same catalogue.
        Composition produced = Composition.Validated([0.0, 90.0, 10.0]);
        Assert.Equal(100.0, empty.Plus(produced).Total.KgPerSecond, 12);
    }
}
