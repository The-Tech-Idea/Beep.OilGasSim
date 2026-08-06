// SDD-001 §1.1 — Volume conditions are TYPES, not fields: the double-count
// killer. reservoir + surface is a compile error; conversion REQUIRES a
// formation volume factor in hand. There is no implicit path.

namespace OGSim.Kernel;

/// <summary>Volume at reservoir conditions (rb-family).</summary>
public readonly record struct ReservoirVolume(double CubicMetres)
{
    public static ReservoirVolume operator +(ReservoirVolume a, ReservoirVolume b) => new(a.CubicMetres + b.CubicMetres);
    public static ReservoirVolume operator -(ReservoirVolume a, ReservoirVolume b) => new(a.CubicMetres - b.CubicMetres);
}

/// <summary>Stock-tank / surface volume (stb-family).</summary>
public readonly record struct SurfaceVolume(double CubicMetres)
{
    public static SurfaceVolume operator +(SurfaceVolume a, SurfaceVolume b) => new(a.CubicMetres + b.CubicMetres);
    public static SurfaceVolume operator -(SurfaceVolume a, SurfaceVolume b) => new(a.CubicMetres - b.CubicMetres);
}

/// <summary>Gas volume at standard conditions (scf/sm³-family).</summary>
public readonly record struct StandardGasVolume(double CubicMetres)
{
    public static StandardGasVolume operator +(StandardGasVolume a, StandardGasVolume b) => new(a.CubicMetres + b.CubicMetres);
    public static StandardGasVolume operator -(StandardGasVolume a, StandardGasVolume b) => new(a.CubicMetres - b.CubicMetres);
}

/// <summary>
/// Bo, rb per stb — the ONLY bridge between reservoir and surface oil volumes.
/// SDD-001 §1.1, exactly as pinned.
/// </summary>
public readonly record struct FormationVolumeFactor(double RbPerStb)
{
    public SurfaceVolume Shrink(ReservoirVolume v) => new(v.CubicMetres / RbPerStb);
    public ReservoirVolume Swell(SurfaceVolume v) => new(v.CubicMetres * RbPerStb);
}

/// <summary>
/// Bg, rm³ per sm³ — the ONLY bridge between reservoir and STANDARD GAS
/// volumes (pass 6, finding 77: reusing the oil FVF here routed standard gas
/// through the stock-tank family — the exact wrong-bucket bug these types
/// exist to make uncompilable).
/// </summary>
public readonly record struct GasFormationVolumeFactor(double Rm3PerSm3)
{
    public StandardGasVolume Shrink(ReservoirVolume v) => new(v.CubicMetres / Rm3PerSm3);
    public ReservoirVolume Swell(StandardGasVolume v) => new(v.CubicMetres * Rm3PerSm3);
}

// SDD-001 §1 — the rate family: one volumetric rate per volume condition, so a
// reservoir rate can no more be added to a surface rate than the volumes can.
// R1.1 completes the family: StandardGasRate was missing, and none of the three
// declared the rate × duration → volume product that R6/R8 integrate with.

/// <summary>Reservoir-condition volumetric rate, m³/s (the IPR's q_rc — SDD-003 §6.1).</summary>
public readonly record struct ReservoirRate(double CubicMetresPerSecond) : IComparable<ReservoirRate>
{
    public static ReservoirRate operator +(ReservoirRate a, ReservoirRate b) => new(a.CubicMetresPerSecond + b.CubicMetresPerSecond);
    public static ReservoirRate operator -(ReservoirRate a, ReservoirRate b) => new(a.CubicMetresPerSecond - b.CubicMetresPerSecond);
    /// <summary>The declared product: a rate held for a duration is a volume.</summary>
    public static ReservoirVolume operator *(ReservoirRate r, Duration d) => new(r.CubicMetresPerSecond * d.Seconds);
    public static double operator /(ReservoirRate a, ReservoirRate b) => a.CubicMetresPerSecond / b.CubicMetresPerSecond;
    public static bool operator >(ReservoirRate a, ReservoirRate b) => a.CubicMetresPerSecond > b.CubicMetresPerSecond;
    public static bool operator <(ReservoirRate a, ReservoirRate b) => a.CubicMetresPerSecond < b.CubicMetresPerSecond;
    public int CompareTo(ReservoirRate other) => CubicMetresPerSecond.CompareTo(other.CubicMetresPerSecond);
}

/// <summary>Surface-condition volumetric rate, m³/s.</summary>
public readonly record struct SurfaceRate(double CubicMetresPerSecond) : IComparable<SurfaceRate>
{
    public static SurfaceRate operator +(SurfaceRate a, SurfaceRate b) => new(a.CubicMetresPerSecond + b.CubicMetresPerSecond);
    public static SurfaceRate operator -(SurfaceRate a, SurfaceRate b) => new(a.CubicMetresPerSecond - b.CubicMetresPerSecond);
    public static SurfaceVolume operator *(SurfaceRate r, Duration d) => new(r.CubicMetresPerSecond * d.Seconds);
    public static double operator /(SurfaceRate a, SurfaceRate b) => a.CubicMetresPerSecond / b.CubicMetresPerSecond;
    public static bool operator >(SurfaceRate a, SurfaceRate b) => a.CubicMetresPerSecond > b.CubicMetresPerSecond;
    public static bool operator <(SurfaceRate a, SurfaceRate b) => a.CubicMetresPerSecond < b.CubicMetresPerSecond;
    public int CompareTo(SurfaceRate other) => CubicMetresPerSecond.CompareTo(other.CubicMetresPerSecond);
}

/// <summary>Standard-condition gas rate, sm³/s — the gas family's own rate.</summary>
public readonly record struct StandardGasRate(double CubicMetresPerSecond) : IComparable<StandardGasRate>
{
    public static StandardGasRate operator +(StandardGasRate a, StandardGasRate b) => new(a.CubicMetresPerSecond + b.CubicMetresPerSecond);
    public static StandardGasRate operator -(StandardGasRate a, StandardGasRate b) => new(a.CubicMetresPerSecond - b.CubicMetresPerSecond);
    public static StandardGasVolume operator *(StandardGasRate r, Duration d) => new(r.CubicMetresPerSecond * d.Seconds);
    public static double operator /(StandardGasRate a, StandardGasRate b) => a.CubicMetresPerSecond / b.CubicMetresPerSecond;
    public static bool operator >(StandardGasRate a, StandardGasRate b) => a.CubicMetresPerSecond > b.CubicMetresPerSecond;
    public static bool operator <(StandardGasRate a, StandardGasRate b) => a.CubicMetresPerSecond < b.CubicMetresPerSecond;
    public int CompareTo(StandardGasRate other) => CubicMetresPerSecond.CompareTo(other.CubicMetresPerSecond);
}
