// SDD-010 §4 + SDD-016 §1 — world generation and weather, the last two
// replaceable slots (03 §3.2). Pass 5: previously deferred; deferral was
// inconsistent with a contracts phase — the shapes below are now pinned in
// their SDDs first (F-1), exactly like every other slot.
//
// The generator never touches module truth directly: it emits these handoff
// records through IWorldSink, and each owning module builds its internal truth
// from them. Initial beliefs go through the SAME Observation door as every
// in-game measurement (R15-V10's leak guarantee) — there is no belief-copy path.

using System.Collections.Immutable;
using OGSim.Kernel;

namespace OGSim.Contracts;

// ------------------------------------------------------------ geology handoff

/// <summary>One truth compartment of an accumulation (SDD-010 §2.7 draws).</summary>
public sealed record GeneratedCompartment(
    ReservoirVolume PoreVolume,
    double Porosity,
    double OilSaturation,
    Pressure InitialPressure,
    Temperature Temperature,
    Length Depth);

/// <summary>
/// ONE CLOSED STRUCTURE (SDD-010 §2.5–2.7, §4b) — charged or not.
///
/// <para><c>Compartments</c> is EMPTY when fill-spill left the trap dry, and
/// that is the point: a prospect a company can drill and lose on has to reach
/// the engine, or probability of success has nothing to be right or wrong
/// about. A structure is what the world found; a compartment is what turned out
/// to be in it.</para>
///
/// <para>Subtlety (DetectClass) and AccessRequirements are TRUTH attributes
/// here — below-tier surveys spawn nothing because screening reads these, not
/// the other way round.</para>
/// </summary>
public sealed record GeneratedAccumulation(
    ContentId Play,
    Polygon Closure,
    DetectClass Subtlety,
    AccessRequirements Access,
    FluidForm Fluid,
    IReadOnlyList<GeneratedCompartment> Compartments)
{
    // Finding 131 — the reason PV7 had to compare compartments element by
    // element rather than simply comparing two accumulations.
    public bool Equals(GeneratedAccumulation? other) =>
        other is not null
        && Play == other.Play
        && Closure == other.Closure
        && Subtlety == other.Subtlety
        && Access == other.Access
        && Fluid == other.Fluid
        && Structural.Equal(Compartments, other.Compartments);

    public override int GetHashCode() =>
        HashCode.Combine(Play, Closure, Subtlety, Access, Fluid,
                         Structural.HashOf(Compartments));
}

// ------------------------------------------------------------ surface handoff

/// <summary>Sea level is elevation 0; bathymetry is negative elevation — port
/// depth falls out of the same field (SDD-010 §3 hydrology).</summary>
public sealed record Heightfield(
    Length CellSize,
    int Width,
    int Height,
    ImmutableArray<double> ElevationMetres)
{
    // Finding 131. Without these the generated-equality on this record compares
    // the elevation array by REFERENCE, so two regenerations of one seed are
    // unequal and PV2's "a reloaded world is the world that was saved" cannot
    // be asked as a question.
    public bool Equals(Heightfield? other) =>
        other is not null
        && CellSize == other.CellSize
        && Width == other.Width
        && Height == other.Height
        && Structural.Equal(ElevationMetres, other.ElevationMetres);

    public override int GetHashCode() =>
        HashCode.Combine(CellSize, Width, Height, Structural.HashOf(ElevationMetres));
}

public sealed record River(ImmutableArray<Coordinate> Path)
{
    public bool Equals(River? other) => other is not null && Structural.Equal(Path, other.Path);

    public override int GetHashCode() => Structural.HashOf(Path);
}

/// <summary>Cell class indexes the Classes list — class ids are content (SDD-010 §3 terrain).</summary>
public sealed record GeneratedTerrain(
    Heightfield Elevation,
    ImmutableArray<int> ClassByCell,
    IReadOnlyList<ContentId> Classes,
    IReadOnlyList<River> Rivers,
    IReadOnlyList<Polygon> Lakes)
{
    public bool Equals(GeneratedTerrain? other) =>
        other is not null
        && Elevation == other.Elevation
        && Structural.Equal(ClassByCell, other.ClassByCell)
        && Structural.Equal(Classes, other.Classes)
        && Structural.Equal(Rivers, other.Rivers)
        && Structural.Equal(Lakes, other.Lakes);

    public override int GetHashCode() =>
        HashCode.Combine(
            Elevation,
            Structural.HashOf(ClassByCell),
            Structural.HashOf(Classes),
            Structural.HashOf(Rivers),
            Structural.HashOf(Lakes));
}

public sealed record Settlement(Coordinate Site, long Population);

/// <summary>A road or rail edge of the MST-plus-spurs network (SDD-010 §3 transport).</summary>
public sealed record TransportLink(Coordinate A, Coordinate B, ContentId Kind);

/// <summary>Named Harbour, not Port — PortId/PortSpec are flow-element ports.</summary>
public sealed record Harbour(Coordinate Site, Length Depth);

/// <summary>Legacy third-party surface asset in mature basins — the rent-vs-build fabric (H10).</summary>
public sealed record ThirdPartyAsset(ContentId Template, Coordinate Site);

public sealed record SensitivityZone(ContentId Kind, Polygon Area);

public sealed record GeneratedSurface(
    GeneratedTerrain Terrain,
    IReadOnlyList<Settlement> Settlements,
    IReadOnlyList<TransportLink> Transport,
    IReadOnlyList<Harbour> Harbours,
    IReadOnlyList<ThirdPartyAsset> ThirdParty,
    IReadOnlyList<SensitivityZone> LandStatus)
{
    // Finding 131 — PV2 asks whether a regenerated surface IS the one it was
    // generated from, and that question needs this override to mean anything.
    public bool Equals(GeneratedSurface? other) =>
        other is not null && Terrain == other.Terrain
        && Structural.Equal(Settlements, other.Settlements)
        && Structural.Equal(Transport, other.Transport)
        && Structural.Equal(Harbours, other.Harbours)
        && Structural.Equal(ThirdParty, other.ThirdParty)
        && Structural.Equal(LandStatus, other.LandStatus);

    public override int GetHashCode() =>
        HashCode.Combine(Terrain, Structural.HashOf(Settlements),
            Structural.HashOf(Transport), Structural.HashOf(Harbours),
            Structural.HashOf(ThirdParty), Structural.HashOf(LandStatus));
}

// ------------------------------------------------- regions and jurisdictions

/// <summary>Every location maps to exactly ONE climate region (SDD-016 §1).</summary>
public sealed record ClimateRegion(ContentId Profile, Polygon Area);

public sealed record Jurisdiction(ContentId FiscalRegime, Polygon Area);

// ------------------------------------------------------------------ the sink

/// <summary>
/// The generator's ONLY output channel (SDD-010 §4). Engine composition fans
/// these out to the owning modules; the generator never sees a module store.
/// </summary>
public interface IWorldSink
{
    void AddAccumulation(GeneratedAccumulation accumulation);
    void SetSurface(GeneratedSurface surface);
    void AddClimateRegion(ClimateRegion region);
    void AddJurisdiction(Jurisdiction jurisdiction);
    /// <summary>Initial beliefs enter through the observation door — never copied from truth (R15-V10).</summary>
    void DeliverRegionalObservation(Observation observation);
}

/// <summary>
/// The new-game knobs (pass 7, owner request). A parameter never invents
/// content — it SELECTS a template and SCALES that template's declared tables,
/// each knob landing on a specific SDD-010 step:
///   Template          → the world-template content entry (all archetype/weight tables)
///   WidthCells/Height → terrain grid and region count (§3 terrain)
///   LandFraction      → sea-level percentile of the heightfield (§3 hydrology)
///   ResourceRichness  → charge-emission multiplier (§2 step 6 fill-spill)
///   BasinMaturity     → archetype-weight shift frontier↔mature (§2 step 1) and
///                       third-party industry density (§3 step 9.5)
///   ClimateSeverity   → weather amplitude/extreme-rate multiplier (SDD-016 §1–2)
///   RivalCount        → rival company roster size (SDD-011)
///   StartEra          → technology availability at tick zero (design 07 §2)
/// The template declares the legal range of every knob; out-of-range is an
/// EngineRefused at CreateNew — ALL violations named, never clamped.
/// </summary>
public sealed record WorldParameters(
    ContentId Template,
    int WidthCells,
    int HeightCells,
    double LandFraction,
    double ResourceRichness,
    double BasinMaturity,
    double ClimateSeverity,
    int RivalCount,
    Era StartEra);

/// <summary>
/// Replaceable per 03 §3.2. Consumes ONLY the WorldGen stream; per-step
/// substreams (SDD-010 §1) are the implementation's own discipline. Called
/// once, at tick zero, by engine composition. A world that violates content
/// quotas after bounded resampling is a world-gen FAULT (R15-V8) — reported,
/// never silently accepted.
/// </summary>
public interface IWorldGenerator
{
    ContentId Id { get; }
    void Generate(WorldParameters parameters, IWorldSink sink, IRandomStream worldGen);
}

// ------------------------------------------------------------------- weather

/// <summary>
/// The replaceable part of weather (SDD-016 §1): the stationary N(0,1) state
/// advance x(d+1) = ρ·x(d) + √(1−ρ²)·ε. Severity and ambient temperature are
/// CONTENT curves over x applied engine-side (same x — hot spells and storm
/// calms correlate); extremes are engine Bernoulli draws (§2). The model draws
/// only from the Weather stream, one call per region per day.
/// </summary>
public interface IWeatherModel
{
    ContentId Id { get; }
    double NextState(double x, IRandomStream weather);
}
