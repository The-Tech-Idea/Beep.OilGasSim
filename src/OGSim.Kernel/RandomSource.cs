// R1.4 — seeded, per-subsystem randomness (SDD-001 §4). Design 11 §3.1: adding a
// draw in one subsystem must never shift another's sequence, which is what keeps
// world seeds stable across engine versions. Eight independent PCG64 streams,
// each seeded from the world seed and its own name.
//
// PCG64 rather than any BCL generator: System.Random is banned outright (D-6),
// its algorithm is an implementation detail that has changed between .NET
// versions, and it has no seek. A counter-based generator makes save/restore an
// integer and makes stream independence provable rather than merely likely.
//
// The numeric constants here are the definition of PCG64, FNV-1a and SplitMix64
// — algorithm identity, not model parameters — and fall under the SDD-000 §8 F-2
// exemption for kernel algorithm files.

using System.Numerics;

namespace OGSim.Kernel;

public sealed class RandomSource : IRandomSource
{
    private readonly PcgStream[] _streams;

    public RandomSource(ulong worldSeed)
    {
        var ids = Enum.GetValues<StreamId>();
        _streams = new PcgStream[ids.Length];
        foreach (StreamId id in ids)
            _streams[(int)id] = new PcgStream(worldSeed, NameOf(id));
    }

    public IRandomStream Stream(StreamId id)
    {
        int index = (int)id;
        if (index < 0 || index >= _streams.Length)
            throw new InvariantFault("SDD-001 §4", null, $"No such random stream: {id}.");
        return _streams[index];
    }

    /// <summary>
    /// THESE STRINGS ARE PART OF THE SAVE FORMAT. A stream's seed derives from
    /// its name, so changing one of these re-rolls every world ever generated
    /// from a seed. They are written out here rather than taken from
    /// <c>Enum.ToString()</c> precisely so that renaming an enum member is a
    /// visible decision with a compile-time home, not a silent world change.
    /// </summary>
    private static string NameOf(StreamId id) => id switch
    {
        StreamId.WorldGen => "worldgen",
        StreamId.Exploration => "exploration",
        StreamId.Measurement => "measurement",
        StreamId.Hazard => "hazard",
        StreamId.Weather => "weather",
        StreamId.Price => "price",
        StreamId.Market => "market",
        StreamId.Operations => "operations",
        _ => throw new InvariantFault("SDD-001 §4", null,
            $"Stream {id} has no seed name; every stream must be named explicitly."),
    };

    /// <summary>
    /// PCG64 XSL-RR: a 128-bit LCG whose output is the xorshifted high half,
    /// rotated by bits the state itself chooses. The LCG gives exact seek; the
    /// output function gives the statistical quality the raw LCG lacks.
    /// </summary>
    private sealed class PcgStream : IRandomStream
    {
        private static readonly UInt128 Multiplier =
            new(0x2360ED051FC65DA4UL, 0x4385DF649FCCF645UL);

        private readonly UInt128 _increment;   // per-stream, always odd
        private readonly UInt128 _origin;      // state at position 0
        private UInt128 _state;

        public PcgStream(ulong worldSeed, string streamName)
        {
            // SDD-001 §4: seed = SplitMix64(worldSeed ^ Hash(streamName)).
            ulong mixed = SplitMix64(worldSeed ^ Fnv1a64(streamName));
            ulong seedHigh = SplitMix64(mixed);
            ulong incLow = SplitMix64(seedHigh);
            ulong incHigh = SplitMix64(incLow);

            // The increment must be odd for the LCG to have full period.
            _increment = (new UInt128(incHigh, incLow) << 1) | UInt128.One;

            UInt128 seed = new(seedHigh, mixed);
            UInt128 state = _increment;
            state = state * Multiplier + _increment;
            state += seed;
            state = state * Multiplier + _increment;

            _origin = state;
            _state = state;
            Position = 0;
        }

        public ulong Position { get; private set; }

        public void Seek(ulong position)
        {
            // Jump-ahead in closed form: an LCG advanced n steps is another LCG,
            // so this is O(log n) rather than n draws.
            _state = Advance(_origin, position, Multiplier, _increment);
            Position = position;
        }

        /// <summary>Uniform in [0, 1). 53 bits — every double in the range is reachable.</summary>
        public double NextUnit() => (Next64() >> 11) * (1.0 / 9007199254740992.0);

        /// <summary>
        /// Standard normal by MARSAGLIA POLAR, pinned by SDD-001 §4 because it
        /// needs only ln and sqrt — Box-Muller needs cos, and DetMath has no
        /// trigonometry by design.
        ///
        /// The method produces two independent normals per accepted pair and
        /// this returns only the first. Caching the second would halve the cost
        /// and break Seek: position would no longer determine the next value,
        /// because a cached variate is state outside the counter. Determinism
        /// wins; the draws are cheap.
        /// </summary>
        public double NextNormal()
        {
            double u, sumOfSquares;
            do
            {
                u = 2.0 * NextUnit() - 1.0;
                double v = 2.0 * NextUnit() - 1.0;
                sumOfSquares = u * u + v * v;
            }
            while (sumOfSquares >= 1.0 || sumOfSquares == 0.0);

            return u * DetMath.Sqrt(-2.0 * DetMath.Ln(sumOfSquares) / sumOfSquares);
        }

        /// <summary>
        /// Uniform in [0, exclusiveMax). Rejection rather than a plain modulo:
        /// modulo alone biases the low values, and a hazard draw that quietly
        /// favours day 0 of the month is exactly the kind of wrong that never
        /// gets noticed (SDD-012 §2 draws the failure day this way).
        /// </summary>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
                throw new InvariantFault("SDD-001 §4", null,
                    $"NextInt requires a positive bound; got {exclusiveMax}.");

            ulong range = (ulong)exclusiveMax;
            ulong zone = ulong.MaxValue - (ulong.MaxValue % range);
            ulong draw;
            do { draw = Next64(); } while (draw >= zone);
            return (int)(draw % range);
        }

        private ulong Next64()
        {
            UInt128 current = _state;
            _state = current * Multiplier + _increment;
            Position++;

            ulong high = (ulong)(current >> 64);
            ulong low = (ulong)current;
            ulong xorshifted = high ^ low;
            int rotation = (int)(current >> 122);   // top 6 bits pick the rotation
            return BitOperations.RotateRight(xorshifted, rotation);
        }

        /// <summary>state after `delta` steps, by binary exponentiation over the
        /// LCG's (multiplier, increment) pair.</summary>
        private static UInt128 Advance(UInt128 state, UInt128 delta, UInt128 multiplier, UInt128 increment)
        {
            UInt128 accumulatedMultiplier = UInt128.One;
            UInt128 accumulatedIncrement = UInt128.Zero;
            UInt128 currentMultiplier = multiplier;
            UInt128 currentIncrement = increment;

            while (delta > UInt128.Zero)
            {
                if ((delta & UInt128.One) == UInt128.One)
                {
                    accumulatedMultiplier *= currentMultiplier;
                    accumulatedIncrement = accumulatedIncrement * currentMultiplier + currentIncrement;
                }
                currentIncrement = (currentMultiplier + UInt128.One) * currentIncrement;
                currentMultiplier *= currentMultiplier;
                delta >>= 1;
            }

            return accumulatedMultiplier * state + accumulatedIncrement;
        }

        /// <summary>
        /// FNV-1a over the stream name. Emphatically NOT string.GetHashCode,
        /// which .NET randomises per process — a world seed would then produce a
        /// different world on every run, which is the single worst determinism
        /// bug this engine could have.
        /// </summary>
        private static ulong Fnv1a64(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        private static ulong SplitMix64(ulong value)
        {
            ulong z = value + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
