// SDD-001 §1.4 — spatial primitives (concept G17). Fictional basins on a flat
// plane (open decision W1): no geodesy, ever. R1.1 implements the algorithms the
// contract stage declared: shoelace area, ray-cast containment, conservative
// overlap. Network distance is NOT here — it lives on the generated transport
// graph (design 06 §5.1a step 9.5) and belongs to OGSim.World.
//
// Every algorithm is pure +, -, * on doubles with the vertex order fixed by
// construction, so all of it is D-1 safe and bit-identical across platforms.
// No epsilon-tuned geometry kernel: per §1.4 an overlap test may be
// conservative provided it is deterministic, and blocks never need robust
// intersection.

using System.Collections.Immutable;

namespace OGSim.Kernel;

/// <summary>Metres on the world plane.</summary>
public readonly record struct Coordinate(double X, double Y);

/// <summary>
/// An immutable ring: counter-clockwise, non-self-intersecting, validated at
/// construction so no downstream consumer has to re-check.
/// </summary>
public readonly record struct Polygon
{
    public ImmutableArray<Coordinate> Vertices { get; }

    public Polygon(ImmutableArray<Coordinate> vertices)
    {
        if (vertices.IsDefault)
            throw new ArgumentException("Polygon requires vertices (SDD-001 §1.4).", nameof(vertices));
        if (vertices.Length < 3)
            throw new ArgumentException(
                $"Polygon requires at least 3 vertices; got {vertices.Length} (SDD-001 §1.4).", nameof(vertices));

        for (int i = 0; i < vertices.Length; i++)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Length];
            if (current.X == next.X && current.Y == next.Y)
                throw new ArgumentException(
                    $"Polygon has a zero-length edge at vertex {i} (SDD-001 §1.4).", nameof(vertices));
        }

        // Simplicity is checked BEFORE orientation, because a self-intersecting
        // ring has no orientation to check: a bow-tie's two lobes cancel to a
        // signed area of zero, so an orientation-first order would report it as
        // "not counter-clockwise" and hide the actual defect.
        if (HasSelfIntersection(vertices))
            throw new ArgumentException(
                "Polygon is self-intersecting (SDD-001 §1.4).", nameof(vertices));

        // Orientation is part of the type's meaning, not a convention consumers
        // must remember: a clockwise ring would silently negate every area.
        double twiceSigned = TwiceSignedArea(vertices);
        if (twiceSigned <= 0.0)
            throw new ArgumentException(
                "Polygon must be counter-clockwise and non-degenerate; the signed area is not positive " +
                "(SDD-001 §1.4).", nameof(vertices));

        Vertices = vertices;
    }

    /// <summary>
    /// STRUCTURAL equality over the vertices (R15.0 finding 123).
    ///
    /// <para>A record's generated equality compares an <see cref="ImmutableArray{T}"/>
    /// member by the underlying array REFERENCE, so two polygons with identical
    /// vertices compared unequal. Found by PV7, which regenerated a world from
    /// the same seed and reported the two as different — the invariant was
    /// right and the comparison was wrong, and the same defect would have made
    /// a save round-trip check, a dedup or a read-model diff silently wrong in
    /// the other direction.</para>
    ///
    /// <para><see cref="Allocation"/> already carried this override for the same
    /// reason; the geometry types did not.</para>
    /// </summary>
    /// <para>Written out by hand when finding 123 caught it here first; it now
    /// shares <see cref="Structural"/> with every other record that carries a
    /// collection, so the rule cannot be applied one way here and another way
    /// there (finding 131).</para>
    public bool Equals(Polygon other) => Structural.Equal(Vertices, other.Vertices);

    public override int GetHashCode() => Structural.HashOf(Vertices);

    /// <summary>Shoelace, summed in vertex order so the result is reproducible.</summary>
    public Area Area => new(TwiceSignedArea(Guarded()) * 0.5);

    /// <summary>The area centroid — not the vertex mean, which is not the same point.</summary>
    public Coordinate Centroid
    {
        get
        {
            var vertices = Guarded();
            double twiceSigned = TwiceSignedArea(vertices);
            double sumX = 0.0;
            double sumY = 0.0;
            for (int i = 0; i < vertices.Length; i++)
            {
                var current = vertices[i];
                var next = vertices[(i + 1) % vertices.Length];
                double cross = current.X * next.Y - next.X * current.Y;
                sumX += (current.X + next.X) * cross;
                sumY += (current.Y + next.Y) * cross;
            }
            double scale = 1.0 / (3.0 * twiceSigned);
            return new Coordinate(sumX * scale, sumY * scale);
        }
    }

    /// <summary>
    /// Ray-cast containment. The half-open rule — an edge counts when exactly one
    /// endpoint is strictly above the test point — is what stops a ray through a
    /// shared vertex being counted twice.
    /// </summary>
    public bool Contains(Coordinate point)
    {
        var vertices = Guarded();
        bool inside = false;
        for (int i = 0; i < vertices.Length; i++)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Length];
            if ((current.Y > point.Y) != (next.Y > point.Y))
            {
                double t = (point.Y - current.Y) / (next.Y - current.Y);
                double crossingX = current.X + t * (next.X - current.X);
                if (point.X < crossingX) inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// Conservative overlap (§1.4): bounding-box prefilter, then edge crossing,
    /// then containment either way for the nested case that crosses no edge.
    /// Touching counts as overlapping — the conservative direction.
    /// </summary>
    public bool Overlaps(in Polygon other)
    {
        var mine = Guarded();
        var theirs = other.Guarded();

        Bounds(mine, out double minX, out double minY, out double maxX, out double maxY);
        Bounds(theirs, out double otherMinX, out double otherMinY, out double otherMaxX, out double otherMaxY);
        if (maxX < otherMinX || otherMaxX < minX || maxY < otherMinY || otherMaxY < minY)
            return false;

        for (int i = 0; i < mine.Length; i++)
        {
            var a0 = mine[i];
            var a1 = mine[(i + 1) % mine.Length];
            for (int j = 0; j < theirs.Length; j++)
            {
                var b0 = theirs[j];
                var b1 = theirs[(j + 1) % theirs.Length];
                if (SegmentsIntersect(a0, a1, b0, b1)) return true;
            }
        }

        return Contains(theirs[0]) || other.Contains(mine[0]);
    }

    // ------------------------------------------------------------- internals

    /// <summary>
    /// `default(Polygon)` bypasses the constructor, so every member checks. The
    /// result is a loud classified failure rather than a ring with no vertices
    /// quietly reporting zero area (law L3's spirit at the value-type boundary).
    /// </summary>
    private ImmutableArray<Coordinate> Guarded() =>
        Vertices.IsDefault
            ? throw new InvariantFault("SDD-001 §1.4", null,
                "default(Polygon) has no vertices; a polygon must be constructed.")
            : Vertices;

    private static double TwiceSignedArea(ImmutableArray<Coordinate> vertices)
    {
        double total = 0.0;
        for (int i = 0; i < vertices.Length; i++)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Length];
            total += current.X * next.Y - next.X * current.Y;
        }
        return total;
    }

    private static void Bounds(
        ImmutableArray<Coordinate> vertices,
        out double minX, out double minY, out double maxX, out double maxY)
    {
        minX = vertices[0].X;
        maxX = vertices[0].X;
        minY = vertices[0].Y;
        maxY = vertices[0].Y;
        for (int i = 1; i < vertices.Length; i++)
        {
            var v = vertices[i];
            if (v.X < minX) minX = v.X;
            if (v.X > maxX) maxX = v.X;
            if (v.Y < minY) minY = v.Y;
            if (v.Y > maxY) maxY = v.Y;
        }
    }

    /// <summary>Sign of the turn o→a→b: positive left, negative right, zero collinear.</summary>
    private static double Cross(Coordinate o, Coordinate a, Coordinate b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    private static bool SegmentsIntersect(Coordinate a0, Coordinate a1, Coordinate b0, Coordinate b1)
    {
        double d0 = Cross(a0, a1, b0);
        double d1 = Cross(a0, a1, b1);
        double d2 = Cross(b0, b1, a0);
        double d3 = Cross(b0, b1, a1);

        // Strictly opposite sides on both segments: a proper crossing.
        if (((d0 > 0.0 && d1 < 0.0) || (d0 < 0.0 && d1 > 0.0)) &&
            ((d2 > 0.0 && d3 < 0.0) || (d2 < 0.0 && d3 > 0.0)))
            return true;

        // Collinear touching — conservative, per §1.4.
        return (d0 == 0.0 && OnSegment(a0, a1, b0))
            || (d1 == 0.0 && OnSegment(a0, a1, b1))
            || (d2 == 0.0 && OnSegment(b0, b1, a0))
            || (d3 == 0.0 && OnSegment(b0, b1, a1));
    }

    private static bool OnSegment(Coordinate from, Coordinate to, Coordinate point) =>
        point.X >= (from.X < to.X ? from.X : to.X) &&
        point.X <= (from.X > to.X ? from.X : to.X) &&
        point.Y >= (from.Y < to.Y ? from.Y : to.Y) &&
        point.Y <= (from.Y > to.Y ? from.Y : to.Y);

    private static bool HasSelfIntersection(ImmutableArray<Coordinate> vertices)
    {
        int count = vertices.Length;
        for (int i = 0; i < count; i++)
        {
            var a0 = vertices[i];
            var a1 = vertices[(i + 1) % count];
            for (int j = i + 1; j < count; j++)
            {
                // Adjacent edges legitimately share an endpoint; the ring's last
                // and first edge are adjacent too.
                if (j == i) continue;
                if ((j + 1) % count == i || (i + 1) % count == j) continue;

                var b0 = vertices[j];
                var b1 = vertices[(j + 1) % count];
                if (SegmentsIntersect(a0, a1, b0, b1)) return true;
            }
        }
        return false;
    }
}

/// <summary>
/// Straight-line distance on the world plane. Network distance (remoteness) is
/// deliberately absent — it is a property of the generated transport graph.
/// </summary>
public static class Distances
{
    public static Length Euclidean(Coordinate a, Coordinate b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return new Length(DetMath.Sqrt(dx * dx + dy * dy));
    }
}
