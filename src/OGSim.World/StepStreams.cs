// R15.1 — per-step substreams (SDD-010 §1).
//
// EDITING STEP 7 MUST NOT SHIFT STEP 9's DRAWS. That is the stream-independence
// principle of design 11 §3.1 applied INSIDE world generation, and it is what
// makes PV7 — regeneration identity — survive development rather than only
// survive a single build.
//
// Without it, adding one draw to the charge step would change every settlement,
// every road and every jurisdiction downstream. A tester who reported "the world
// changed when you touched geology" would be describing correct behaviour of a
// broken design, and no amount of seeding discipline elsewhere would help.
//
//   stepSeed = SplitMix64( worldgenStreamSeed ^ Hash(stepName) )
//
// Named rather than numbered, deliberately: inserting a step between two others
// renumbers nothing, so a pipeline can grow without invalidating saved worlds
// whose later steps did not change.

using OGSim.Kernel;

namespace OGSim.World;

/// <summary>SDD-010's eleven steps, by name.</summary>
public static class WorldStep
{
    public const string Tectonic = "tectonic";
    public const string Stratigraphy = "stratigraphy";
    public const string BurialThermal = "burial-thermal";
    public const string Structure = "structure";
    public const string Traps = "traps";
    public const string Charge = "charge";
    public const string Accumulations = "accumulations";
    public const string PlaysAndClasses = "plays-and-classes";
    public const string Surface = "surface";
    public const string Jurisdictions = "jurisdictions";
    public const string RegionalData = "regional-data";

    /// <summary>Declared order, so a run walks them identically every time.</summary>
    public static IReadOnlyList<string> InOrder { get; } =
    [
        Tectonic, Stratigraphy, BurialThermal, Structure, Traps, Charge,
        Accumulations, PlaysAndClasses, Surface, Jurisdictions, RegionalData,
    ];
}

/// <summary>
/// SDD-010 §1's substream derivation.
/// </summary>
public sealed class StepStreams
{
    private readonly ulong _worldSeed;
    private readonly Dictionary<string, IRandomStream> _streams = [];

    public StepStreams(ulong worldSeed) => _worldSeed = worldSeed;

    /// <summary>
    /// The substream for a named step. Derived from the world seed and the
    /// step's NAME, so two steps never share a sequence and no step's draw
    /// count can affect another's.
    /// </summary>
    public IRandomStream For(string stepName)
    {
        ArgumentException.ThrowIfNullOrEmpty(stepName);

        if (_streams.TryGetValue(stepName, out IRandomStream? existing)) return existing;

        // A RandomSource seeded per step gives each one an independent PCG
        // sequence — the same construction R1 used for the eight named engine
        // streams, applied one level down.
        var stream = new RandomSource(SplitMix64(_worldSeed ^ Hash(stepName)))
            .Stream(StreamId.WorldGen);

        _streams.Add(stepName, stream);
        return stream;
    }

    /// <summary>
    /// SplitMix64's finaliser. Mixes a weakly-varying seed into a
    /// well-distributed one, so two step names differing by a character do not
    /// produce correlated sequences.
    /// </summary>
    internal static ulong SplitMix64(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>FNV-1a over the step name. Deterministic across platforms —
    /// <c>string.GetHashCode</c> is randomised per process and would make a
    /// world unreproducible between two runs of the same binary.</summary>
    internal static ulong Hash(string name)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offset;
        for (int i = 0; i < name.Length; i++)
        {
            hash ^= name[i];
            hash *= prime;
        }

        return hash;
    }
}
