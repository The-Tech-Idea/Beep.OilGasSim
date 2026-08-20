# Volumes

Source: `src\OGSim.Kernel\Volumes.cs` · Lines: 90

## File intent

> SDD-001 §1.1 — Volume conditions are TYPES, not fields: the double-count
> killer. reservoir + surface is a compile error; conversion REQUIRES a
> formation volume factor in hand. There is no implicit path.
> <summary>Volume at reservoir conditions (rb-family).</summary>

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L8` `public readonly record struct ReservoirVolume(double CubicMetres)`
- `L15` `public readonly record struct SurfaceVolume(double CubicMetres)`
- `L22` `public readonly record struct StandardGasVolume(double CubicMetres)`
- `L32` `public readonly record struct FormationVolumeFactor(double RbPerStb)`
- `L44` `public readonly record struct GasFormationVolumeFactor(double Rm3PerSm3)`
- `L56` `public readonly record struct ReservoirRate(double CubicMetresPerSecond) : IComparable<ReservoirRate>`
- `L69` `public readonly record struct SurfaceRate(double CubicMetresPerSecond) : IComparable<SurfaceRate>`
- `L81` `public readonly record struct StandardGasRate(double CubicMetresPerSecond) : IComparable<StandardGasRate>`

## Accessible members

- `L10` `public static ReservoirVolume operator +(ReservoirVolume a, ReservoirVolume b) => new(a.CubicMetres + b.CubicMetres);`
- `L11` `public static ReservoirVolume operator -(ReservoirVolume a, ReservoirVolume b) => new(a.CubicMetres - b.CubicMetres);`
- `L17` `public static SurfaceVolume operator +(SurfaceVolume a, SurfaceVolume b) => new(a.CubicMetres + b.CubicMetres);`
- `L18` `public static SurfaceVolume operator -(SurfaceVolume a, SurfaceVolume b) => new(a.CubicMetres - b.CubicMetres);`
- `L24` `public static StandardGasVolume operator +(StandardGasVolume a, StandardGasVolume b) => new(a.CubicMetres + b.CubicMetres);`
- `L25` `public static StandardGasVolume operator -(StandardGasVolume a, StandardGasVolume b) => new(a.CubicMetres - b.CubicMetres);`
- `L34` `public SurfaceVolume Shrink(ReservoirVolume v) => new(v.CubicMetres / RbPerStb);`
- `L35` `public ReservoirVolume Swell(SurfaceVolume v) => new(v.CubicMetres * RbPerStb);`
- `L46` `public StandardGasVolume Shrink(ReservoirVolume v) => new(v.CubicMetres / Rm3PerSm3);`
- `L47` `public ReservoirVolume Swell(StandardGasVolume v) => new(v.CubicMetres * Rm3PerSm3);`
- `L58` `public static ReservoirRate operator +(ReservoirRate a, ReservoirRate b) => new(a.CubicMetresPerSecond + b.CubicMetresPerSecond);`
- `L59` `public static ReservoirRate operator -(ReservoirRate a, ReservoirRate b) => new(a.CubicMetresPerSecond - b.CubicMetresPerSecond);`
- `L61` `public static ReservoirVolume operator *(ReservoirRate r, Duration d) => new(r.CubicMetresPerSecond * d.Seconds);`
- `L62` `public static double operator /(ReservoirRate a, ReservoirRate b) => a.CubicMetresPerSecond / b.CubicMetresPerSecond;`
- `L63` `public static bool operator >(ReservoirRate a, ReservoirRate b) => a.CubicMetresPerSecond > b.CubicMetresPerSecond;`
- `L64` `public static bool operator <(ReservoirRate a, ReservoirRate b) => a.CubicMetresPerSecond < b.CubicMetresPerSecond;`
- `L65` `public int CompareTo(ReservoirRate other) => CubicMetresPerSecond.CompareTo(other.CubicMetresPerSecond);`
- `L71` `public static SurfaceRate operator +(SurfaceRate a, SurfaceRate b) => new(a.CubicMetresPerSecond + b.CubicMetresPerSecond);`
- `L72` `public static SurfaceRate operator -(SurfaceRate a, SurfaceRate b) => new(a.CubicMetresPerSecond - b.CubicMetresPerSecond);`
- `L73` `public static SurfaceVolume operator *(SurfaceRate r, Duration d) => new(r.CubicMetresPerSecond * d.Seconds);`
- `L74` `public static double operator /(SurfaceRate a, SurfaceRate b) => a.CubicMetresPerSecond / b.CubicMetresPerSecond;`
- `L75` `public static bool operator >(SurfaceRate a, SurfaceRate b) => a.CubicMetresPerSecond > b.CubicMetresPerSecond;`
- `L76` `public static bool operator <(SurfaceRate a, SurfaceRate b) => a.CubicMetresPerSecond < b.CubicMetresPerSecond;`
- `L77` `public int CompareTo(SurfaceRate other) => CubicMetresPerSecond.CompareTo(other.CubicMetresPerSecond);`
- `L83` `public static StandardGasRate operator +(StandardGasRate a, StandardGasRate b) => new(a.CubicMetresPerSecond + b.CubicMetresPerSecond);`
- `L84` `public static StandardGasRate operator -(StandardGasRate a, StandardGasRate b) => new(a.CubicMetresPerSecond - b.CubicMetresPerSecond);`
- `L85` `public static StandardGasVolume operator *(StandardGasRate r, Duration d) => new(r.CubicMetresPerSecond * d.Seconds);`
- `L86` `public static double operator /(StandardGasRate a, StandardGasRate b) => a.CubicMetresPerSecond / b.CubicMetresPerSecond;`
- `L87` `public static bool operator >(StandardGasRate a, StandardGasRate b) => a.CubicMetresPerSecond > b.CubicMetresPerSecond;`
- `L88` `public static bool operator <(StandardGasRate a, StandardGasRate b) => a.CubicMetresPerSecond < b.CubicMetresPerSecond;`
- `L89` `public int CompareTo(StandardGasRate other) => CubicMetresPerSecond.CompareTo(other.CubicMetresPerSecond);`

