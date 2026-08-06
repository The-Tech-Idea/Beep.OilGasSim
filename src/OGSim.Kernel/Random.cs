// SDD-001 §4 — seeded, per-subsystem streams. Adding a draw in one stream can
// never shift another (design 11 §3.1). PCG64; stream seed derived from the
// world seed by a stable hash of the stream name.

namespace OGSim.Kernel;

/// <summary>The eight streams (SDD-013 §2 persists all eight positions).</summary>
public enum StreamId
{
    WorldGen, Exploration, Measurement, Hazard, Weather, Price, Market, Operations
}

public interface IRandomSource
{
    IRandomStream Stream(StreamId id);
}

public interface IRandomStream
{
    /// <summary>Uniform in [0, 1).</summary>
    double NextUnit();

    /// <summary>
    /// Standard normal via MARSAGLIA POLAR (SDD-001 §4, pinned): rejection over
    /// NextUnit pairs, DetMath.Ln + Sqrt only — chosen because the deterministic
    /// math library has no trig. Consumes a variable but deterministic uniform count.
    /// </summary>
    double NextNormal();

    /// <summary>Uniform integer in [0, exclusiveMax) — e.g. failure day in {0..29} (SDD-012 §2).</summary>
    int NextInt(int exclusiveMax);

    /// <summary>Counter position — saved and restored exactly (design 11 §3).</summary>
    ulong Position { get; }

    void Seek(ulong position);
}
