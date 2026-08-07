// Finding 131 — contract records that carry collections compare by VALUE.
//
// A record's generated equality compares an ImmutableArray or an IReadOnlyList
// member by reference, so two records built from identical values compare
// unequal and two built from different values compare equal when they share an
// array. Finding 123 caught it on Polygon, where PV7 reported two regenerations
// of one seed as different worlds; these are the contract records that carried
// the same trap.
//
// Each test builds its two operands from SEPARATE arrays on purpose. Reusing one
// would make every assertion below pass by reference and prove nothing.

using System.Collections.Immutable;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Contracts.Tests;

public sealed class StructuralEqualityTests
{
    private static Coordinate At(double x, double y) => new(x, y);

    [Fact]
    public void Two_heightfields_with_the_same_elevations_are_equal()
    {
        var left = new Heightfield(new Length(100.0), 2, 2, [1.0, 2.0, 3.0, 4.0]);
        var right = new Heightfield(new Length(100.0), 2, 2, [1.0, 2.0, 3.0, 4.0]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Two_heightfields_differing_in_one_cell_are_not_equal()
    {
        var left = new Heightfield(new Length(100.0), 2, 2, [1.0, 2.0, 3.0, 4.0]);
        var right = new Heightfield(new Length(100.0), 2, 2, [1.0, 2.0, 3.0, 4.5]);

        Assert.NotEqual(left, right);
    }

    /// <summary>The scalar members must still count — a structural override that
    /// forgot them would make two different grids equal.</summary>
    [Fact]
    public void Heightfields_of_different_shape_are_not_equal()
    {
        var left = new Heightfield(new Length(100.0), 4, 1, [1.0, 2.0, 3.0, 4.0]);
        var right = new Heightfield(new Length(100.0), 2, 2, [1.0, 2.0, 3.0, 4.0]);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void Two_rivers_on_the_same_path_are_equal()
    {
        var left = new River([At(0, 0), At(1, 1)]);
        var right = new River([At(0, 0), At(1, 1)]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// The whole point of PV2: a regenerated terrain must BE the terrain it was
    /// generated from. This nests every collection kind — an ImmutableArray of
    /// doubles inside a Heightfield, one of ints, and three lists of records.
    /// </summary>
    [Fact]
    public void Two_terrains_generated_alike_are_equal()
    {
        static GeneratedTerrain Build() => new(
            new Heightfield(new Length(50.0), 2, 1, [10.0, -5.0]),
            [0, 1],
            [new ContentId("plain"), new ContentId("marsh")],
            [new River([At(0, 0), At(1, 0)])],
            [new Polygon([At(0, 0), At(1, 0), At(1, 1)])]);

        Assert.Equal(Build(), Build());
        Assert.Equal(Build().GetHashCode(), Build().GetHashCode());
    }

    [Fact]
    public void Two_terrains_differing_in_a_nested_river_are_not_equal()
    {
        static GeneratedTerrain Build(double lastX) => new(
            new Heightfield(new Length(50.0), 2, 1, [10.0, -5.0]),
            [0, 1],
            [new ContentId("plain"), new ContentId("marsh")],
            [new River([At(0, 0), At(lastX, 0)])],
            []);

        Assert.NotEqual(Build(1.0), Build(2.0));
    }

    [Fact]
    public void Two_accumulations_with_the_same_compartments_are_equal()
    {
        static GeneratedAccumulation Build() => new(
            new ContentId("deltaic"),
            new Polygon([At(0, 0), At(1, 0), At(1, 1)]),
            DetectClass.D1,
            new AccessRequirements(DepthClass.Standard, WaterDepthClass.Onshore, false, false, false),
            FluidForm.BlackOil,
            [new GeneratedCompartment(
                new ReservoirVolume(1e6), 0.2, 0.7,
                new Pressure(30e6), Temperature.FromCelsius(90.0), new Length(2000.0))]);

        Assert.Equal(Build(), Build());
    }

    [Fact]
    public void Two_component_splits_with_the_same_fractions_are_equal()
    {
        ComponentSplit left = ComponentSplit.Validated(0.7, 0.1, 0.1, 0.05, 0.05);
        ComponentSplit right = ComponentSplit.Validated(0.7, 0.1, 0.1, 0.05, 0.05);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Two_component_splits_that_differ_are_not_equal()
    {
        ComponentSplit left = ComponentSplit.Validated(0.7, 0.1, 0.1, 0.05, 0.05);
        ComponentSplit right = ComponentSplit.Validated(0.6, 0.2, 0.1, 0.05, 0.05);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void Two_recovery_sets_with_the_same_fractions_are_equal() =>
        Assert.Equal(
            NglRecovery.Validated(0.0, 0.4, 0.9, 0.95, 0.99),
            NglRecovery.Validated(0.0, 0.4, 0.9, 0.95, 0.99));

    [Fact]
    public void Two_specifications_with_the_same_limits_are_equal()
    {
        var left = new Specification([new SpecLimit(SpecProperty.H2SFraction, 4e-6)]);
        var right = new Specification([new SpecLimit(SpecProperty.H2SFraction, 4e-6)]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Two_specifications_with_different_limits_are_not_equal()
    {
        var left = new Specification([new SpecLimit(SpecProperty.H2SFraction, 4e-6)]);
        var right = new Specification([new SpecLimit(SpecProperty.H2SFraction, 8e-6)]);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void Two_trajectories_through_the_same_stations_are_equal()
    {
        var left = new Trajectory([new TrajectoryStation(new Length(0.0), new Length(0.0), At(0, 0))]);
        var right = new Trajectory([new TrajectoryStation(new Length(0.0), new Length(0.0), At(0, 0))]);

        Assert.Equal(left, right);
    }

    [Fact]
    public void Two_phase_splits_with_the_same_fractions_are_equal()
    {
        static PhaseSplit Build() => new([(new MaterialId(0), 1.0, 0.0, 0.0)]);

        Assert.Equal(Build(), Build());
    }

    /// <summary>
    /// A polygon carried the first instance of this defect (finding 123) and now
    /// shares the one implementation. Kept here so the consolidation cannot
    /// regress it silently.
    /// </summary>
    [Fact]
    public void Two_polygons_on_the_same_vertices_are_equal()
    {
        var left = new Polygon([At(0, 0), At(1, 0), At(1, 1)]);
        var right = new Polygon([At(0, 0), At(1, 0), At(1, 1)]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// A default ImmutableArray is not an empty one — it has no array at all.
    /// Treating them alike would make an uninitialised record equal to a
    /// deliberately empty one.
    /// </summary>
    [Fact]
    public void A_default_array_is_not_an_empty_array()
    {
        Assert.False(Structural.Equal(default(ImmutableArray<double>), []));
        Assert.True(Structural.Equal(default(ImmutableArray<double>), default));
    }
}
