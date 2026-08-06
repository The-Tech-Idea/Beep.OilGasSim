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

/// <summary>Reservoir-condition volumetric rate, m³/s (the IPR's q_rc — SDD-003 §6.1).</summary>
public readonly record struct ReservoirRate(double CubicMetresPerSecond)
{
    public static ReservoirRate operator +(ReservoirRate a, ReservoirRate b) => new(a.CubicMetresPerSecond + b.CubicMetresPerSecond);
}

/// <summary>Surface-condition volumetric rate, m³/s.</summary>
public readonly record struct SurfaceRate(double CubicMetresPerSecond);
