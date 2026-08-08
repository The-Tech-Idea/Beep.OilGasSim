// R15.4 / R15.5 — the structural horizon and the traps on it (SDD-010 §2 steps 4–5).
//
// WHY A SURFACE AND NOT A DRAW. Until this, a trap's size was a log-normal number
// and its position was a cell picked at random, and the two had nothing to do
// with each other. That is a distribution, not a geology: it cannot say why a
// trap is where it is, it cannot produce two traps that share a spill point, and
// it cannot tell a broad gentle closure from a small sharp one — which is the
// difference between a field worth developing and a field worth drilling once.
//
// A HORIZON MAKES THE ANSWERS FALL OUT. Traps are the local highs of a buried
// surface; a trap's capacity is the rock between its crest and the depth at which
// oil would spill out of it sideways; a big trap is big because the structure is
// broad, not because a number said so. Everything downstream — how much is there,
// how far apart two prospects are, which one is worth the rig first — is then a
// consequence of one generated surface.
//
// VALUE NOISE FROM HASHED INTEGER COORDINATES (SDD-010 §2 step 4's pinned
// choice). No external noise library, and no System.Math transcendentals: the
// lattice values come from an integer hash and the interpolation is polynomial,
// so the same seed gives the same surface on every platform (rule D-3).

using OGSim.Kernel;

namespace OGSim.World;

/// <summary>
/// The depth to the reservoir horizon at each cell, and the traps found on it.
/// </summary>
internal sealed class StructuralHorizon
{
    private readonly double[] _depth;      // metres below datum; larger is deeper
    private readonly int _width;
    private readonly int _height;

    /// <summary>
    /// Regional dip plus layered value noise (SDD-010 §2 step 4). The dip is what
    /// makes a basin have a deep side and a shallow one — without it the noise
    /// alone would scatter traps evenly, and there would be no such thing as
    /// migrating up-dip.
    /// </summary>
    internal StructuralHorizon(int width, int height, ulong seed, double crestDepth, double relief)
    {
        _width = width;
        _height = height;
        _depth = new double[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Deeper towards one corner: the basin's axis.
                double dip = ((x + y) / (double)(width + height)) * relief * DipShare;

                _depth[(y * width) + x] =
                    crestDepth + dip + (Octaves(x, y, seed) * relief * NoiseShare);
            }
        }
    }

    internal double DepthAt(int cell) => _depth[cell];

    internal int Width => _width;

    internal int Count => _depth.Length;

    /// <summary>
    /// Every closed high on the surface (SDD-010 §2 step 5). A cell is a crest if
    /// no neighbour is shallower; the closure is everything that drains to it
    /// down to the SPILL POINT — the shallowest depth at which the fill would
    /// escape into a neighbouring low.
    ///
    /// <para>Found by growing the closure one contour at a time from the crest.
    /// The classic construction, and the reason a trap has a capacity at all:
    /// oil accumulates between the crest and the spill, and one drop more than
    /// that leaves sideways.</para>
    /// </summary>
    internal IReadOnlyList<Closure> Traps(double minimumClosureHeight)
    {
        var found = new List<Closure>();

        // Cell order, never a set walk (rule D-5): two runs of one seed must
        // find traps in the same order or their ids and beliefs drift.
        for (int cell = 0; cell < _depth.Length; cell++)
        {
            if (!IsCrest(cell)) continue;

            Closure? closure = Grow(cell, minimumClosureHeight);

            if (closure is not null) found.Add(closure);
        }

        return found;
    }

    private bool IsCrest(int cell)
    {
        double here = _depth[cell];

        int x = cell % _width;
        int y = cell / _width;

        if (x > 0 && _depth[cell - 1] <= here) return false;
        if (x < _width - 1 && _depth[cell + 1] <= here) return false;
        if (y > 0 && _depth[cell - _width] <= here) return false;
        if (y < _height - 1 && _depth[cell + _width] <= here) return false;

        return true;
    }

    /// <summary>
    /// Flood the crest upward in depth until the fill would touch the edge of the
    /// map or exceed the height a closure may reach. The last level that stayed
    /// contained is the spill point.
    ///
    /// <para>Touching the EDGE is what ends most fills, and it is correct rather
    /// than a boundary hack: a structure that runs off the map has not been shown
    /// to close, and generating a trap on the strength of the part that happens
    /// to be inside the grid would invent a seal nobody drew.</para>
    /// </summary>
    private Closure? Grow(int crest, double minimumClosureHeight)
    {
        double crestDepth = _depth[crest];

        var filled = new List<int>();
        var seen = new HashSet<int>();
        var frontier = new List<int> { crest };

        seen.Add(crest);

        double spill = crestDepth;

        // Grown in steps rather than continuously: a contour interval, which is
        // how a closure is read off a map and keeps the walk finite.
        for (double level = crestDepth + ContourInterval;
             level <= crestDepth + MaximumClosureHeight;
             level += ContourInterval)
        {
            var next = new List<int>();
            var escaped = false;

            for (int i = 0; i < frontier.Count && !escaped; i++)
            {
                int cell = frontier[i];

                int x = cell % _width;
                int y = cell / _width;

                // On the boundary of the grid with room to keep going: the
                // structure is not closed.
                if (x == 0 || y == 0 || x == _width - 1 || y == _height - 1) escaped = true;

                Consider(cell - 1, level, seen, next);
                Consider(cell + 1, level, seen, next);
                Consider(cell - _width, level, seen, next);
                Consider(cell + _width, level, seen, next);
            }

            if (escaped) break;

            filled.AddRange(frontier);
            spill = level;

            if (next.Count == 0) break;

            frontier = next;
        }

        double height = spill - crestDepth;

        if (height < minimumClosureHeight || filled.Count == 0) return null;

        // CAPACITY IS THE ROCK BETWEEN CREST AND SPILL, summed cell by cell —
        // not the footprint times the height, which would fill the corners of a
        // dome as if they reached the crest.
        double columnMetres = 0.0;

        for (int i = 0; i < filled.Count; i++) columnMetres += spill - _depth[filled[i]];

        return new Closure(crest, filled.Count, columnMetres, height);
    }

    private void Consider(int cell, double level, HashSet<int> seen, List<int> next)
    {
        if (cell < 0 || cell >= _depth.Length) return;
        if (_depth[cell] > level) return;
        if (!seen.Add(cell)) return;

        next.Add(cell);
    }

    // ------------------------------------------------------------------ noise

    /// <summary>
    /// Layered value noise. Each octave halves the amplitude and doubles the
    /// frequency, which is what gives a surface both a regional shape and local
    /// bumps — one octave would produce a basin with a single hill in it.
    /// </summary>
    private static double Octaves(int x, int y, ulong seed)
    {
        double total = 0.0;
        double amplitude = 1.0;
        double normaliser = 0.0;
        double scale = BaseWavelength;

        for (int octave = 0; octave < OctaveCount; octave++)
        {
            total += Value(x / scale, y / scale, seed + (ulong)octave) * amplitude;
            normaliser += amplitude;

            amplitude *= AmplitudeFalloff;
            scale *= AmplitudeFalloff;
        }

        return total / normaliser;
    }

    /// <summary>Bilinear value noise with a smoothstep fade — polynomial only, so
    /// it is bit-identical everywhere (rule D-3).</summary>
    private static double Value(double x, double y, ulong seed)
    {
        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);

        double fx = Fade(x - x0);
        double fy = Fade(y - y0);

        double top = Lerp(Lattice(x0, y0, seed), Lattice(x0 + 1, y0, seed), fx);
        double bottom = Lerp(Lattice(x0, y0 + 1, seed), Lattice(x0 + 1, y0 + 1, seed), fx);

        return Lerp(top, bottom, fy);
    }

    private static double Fade(double t) => t * t * (3.0 - (2.0 * t));

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    /// <summary>
    /// The lattice value at an integer coordinate — a pure function of (x, y,
    /// seed), so the surface can be evaluated anywhere without storing it and
    /// two runs agree exactly (SDD-010 §2 step 4's "hashed integer
    /// coordinates").
    /// </summary>
    private static double Lattice(int x, int y, ulong seed)
    {
        ulong h = seed;

        h = Mix(h ^ (ulong)(long)x);
        h = Mix(h ^ (ulong)(long)y);

        // Top 53 bits into [0, 1): the mantissa a double can carry exactly.
        return (h >> 11) * (1.0 / 9007199254740992.0);
    }

    private static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;

        return z ^ (z >> 31);
    }

    /// <summary>Cells per lattice cell at the coarsest octave — the size of the
    /// basin's biggest structures.</summary>
    private const double BaseWavelength = 12.0;

    private const int OctaveCount = 4;

    private const double AmplitudeFalloff = 0.5;

    /// <summary>How much of the relief is the regional dip rather than the
    /// noise. Half and half: enough dip to give the basin an up-dip direction,
    /// enough noise to put closed highs along the way.</summary>
    private const double DipShare = 0.5;

    private const double NoiseShare = 0.5;

    /// <summary>The contour a closure is grown by, in metres. Ten metres is the
    /// interval a structure map is read at.</summary>
    private const double ContourInterval = 10.0;

    /// <summary>
    /// How tall a closure may be before the walk stops. A cap rather than a
    /// physical limit: without one, a crest on a monotone slope would grow until
    /// it swallowed the basin, and the cost of that walk is the whole grid.
    /// </summary>
    private const double MaximumClosureHeight = 300.0;
}

/// <summary>
/// A closed structure on the horizon: where its crest is, how many cells it
/// covers, and how much rock stands between crest and spill.
/// </summary>
internal sealed record Closure(int Crest, int Cells, double ColumnMetres, double Height);
