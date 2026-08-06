// R1.1 — spatial primitives (SDD-001 §1.4). Licence blocks, prospect outlines
// and facility footprints all run through these, so the areas are pinned against
// hand-computable shapes rather than against another implementation.

using System.Collections.Immutable;
using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class SpatialTests
{
    private static Polygon Ring(params (double X, double Y)[] points) =>
        new([.. points.Select(p => new Coordinate(p.X, p.Y))]);

    /// <summary>A 2 km × 3 km block, counter-clockwise.</summary>
    private static Polygon Block() => Ring((0, 0), (2000, 0), (2000, 3000), (0, 3000));

    // ------------------------------------------------------------- area

    [Fact] // Shoelace against a shape whose area is arithmetic
    public void R1V1_polygon_area_is_the_shoelace_area()
    {
        Assert.Equal(6.0e6, Block().Area.SquareMetres, 9);

        // Right triangle, base 4 height 3 — area 6.
        Assert.Equal(6.0, Ring((0, 0), (4, 0), (0, 3)).Area.SquareMetres, 9);
    }

    [Fact] // Area is a quantity, so it converts like one
    public void R1V1_polygon_area_converts_to_field_units()
    {
        Area block = Block().Area;
        Assert.Equal(6.0, block.ToSquareKilometres(), 9);
        Assert.Equal(6.0e6 / 4046.8564224, block.ToAcres(), 6);
    }

    [Fact] // The area centroid, not the vertex mean — they differ for an L-shape
    public void R1V1_centroid_is_the_area_centroid()
    {
        Coordinate centre = Block().Centroid;
        Assert.Equal(1000.0, centre.X, 9);
        Assert.Equal(1500.0, centre.Y, 9);

        // An L-shape: the vertex mean is not the centroid, and the area
        // centroid is the one that is physically meaningful.
        Polygon lShape = Ring((0, 0), (2, 0), (2, 1), (1, 1), (1, 2), (0, 2));
        Coordinate lCentre = lShape.Centroid;
        double vertexMeanX = (0 + 2 + 2 + 1 + 1 + 0) / 6.0;
        Assert.Equal(3.0, lShape.Area.SquareMetres, 9);
        Assert.Equal(5.0 / 6.0, lCentre.X, 9);
        Assert.NotEqual(vertexMeanX, lCentre.X, 6);
    }

    // ------------------------------------------------------------- containment

    [Fact] // Ray-cast containment, including the awkward cases
    public void R1V1_contains_answers_inside_outside_and_vertex_aligned_points()
    {
        Polygon block = Block();
        Assert.True(block.Contains(new Coordinate(1000, 1500)));
        Assert.False(block.Contains(new Coordinate(-1, 1500)));
        Assert.False(block.Contains(new Coordinate(2001, 1500)));
        Assert.False(block.Contains(new Coordinate(1000, 3001)));

        // A ray passing exactly through vertex height must not double-count:
        // the half-open rule is what makes this deterministic.
        Assert.True(block.Contains(new Coordinate(1000, 0.0 + double.Epsilon)));
        Assert.False(block.Contains(new Coordinate(-5, 0)));
        Assert.False(block.Contains(new Coordinate(-5, 3000)));
    }

    [Fact] // A concave shape is where a naive convex test would be wrong
    public void R1V1_contains_handles_a_concave_ring()
    {
        Polygon lShape = Ring((0, 0), (2, 0), (2, 1), (1, 1), (1, 2), (0, 2));
        Assert.True(lShape.Contains(new Coordinate(0.5, 1.5)));   // in the arm
        Assert.True(lShape.Contains(new Coordinate(1.5, 0.5)));   // in the foot
        Assert.False(lShape.Contains(new Coordinate(1.5, 1.5)));  // in the notch
    }

    // ------------------------------------------------------------- overlap

    [Fact] // Overlap: crossing, nested, and disjoint all answer correctly
    public void R1V1_overlaps_detects_crossing_nested_and_disjoint_rings()
    {
        Polygon block = Block();

        Polygon crossing = Ring((1000, 1000), (3000, 1000), (3000, 2000), (1000, 2000));
        Assert.True(block.Overlaps(crossing));
        Assert.True(crossing.Overlaps(block));

        // Fully nested — crosses no edge, so only the containment fallback finds it.
        Polygon nested = Ring((500, 500), (600, 500), (600, 600), (500, 600));
        Assert.True(block.Overlaps(nested));
        Assert.True(nested.Overlaps(block));

        Polygon disjoint = Ring((10_000, 10_000), (11_000, 10_000), (11_000, 11_000), (10_000, 11_000));
        Assert.False(block.Overlaps(disjoint));
        Assert.False(disjoint.Overlaps(block));
    }

    [Fact] // §1.4 licenses a CONSERVATIVE test: touching counts as overlapping
    public void R1V1_overlaps_is_conservative_at_a_shared_edge()
    {
        Polygon block = Block();
        Polygon adjacent = Ring((2000, 0), (4000, 0), (4000, 3000), (2000, 3000));
        Assert.True(block.Overlaps(adjacent));
    }

    // ------------------------------------------------------------- validation

    [Fact] // Orientation is part of the type: a clockwise ring would negate every area
    public void R1V1_polygon_rejects_a_clockwise_ring()
    {
        var clockwise = Assert.Throws<ArgumentException>(
            () => Ring((0, 0), (0, 3000), (2000, 3000), (2000, 0)));
        Assert.Contains("counter-clockwise", clockwise.Message);
    }

    [Fact] // Degenerate input is refused at construction, not tolerated downstream
    public void R1V1_polygon_rejects_degenerate_rings()
    {
        Assert.Throws<ArgumentException>(() => Ring((0, 0), (1, 0)));                    // too few
        Assert.Throws<ArgumentException>(() => Ring((0, 0), (1, 0), (1, 0), (0, 1)));    // zero-length edge
        Assert.Throws<ArgumentException>(() => Ring((0, 0), (1, 0), (2, 0)));            // collinear, no area
        Assert.Throws<ArgumentException>(() => new Polygon(default));                     // no vertices at all
    }

    [Fact] // A bow-tie has a positive shoelace value but is not a simple ring
    public void R1V1_polygon_rejects_a_self_intersecting_ring()
    {
        var bowTie = Assert.Throws<ArgumentException>(
            () => Ring((0, 0), (2, 2), (2, 0), (0, 2)));
        Assert.Contains("self-intersecting", bowTie.Message);
    }

    [Fact] // default(Polygon) skips the constructor — it must fail loudly, not read as empty
    public void R1V1_default_polygon_faults_rather_than_reporting_zero_area()
    {
        Polygon uninitialised = default;
        var fault = Assert.Throws<InvariantFault>(() => uninitialised.Area);
        Assert.Equal(FaultClass.Invariant, fault.Fault.Class);
        Assert.Throws<InvariantFault>(() => uninitialised.Contains(new Coordinate(0, 0)));
    }

    // ------------------------------------------------------------- distance

    [Fact] // Euclidean only: no geodesy, by decision W1
    public void R1V1_euclidean_distance_is_a_length()
    {
        Length d = Distances.Euclidean(new Coordinate(0, 0), new Coordinate(3000, 4000));
        Assert.Equal(5000.0, d.Metres, 9);
        Assert.Equal(0.0, Distances.Euclidean(new Coordinate(7, 7), new Coordinate(7, 7)).Metres);
    }
}
