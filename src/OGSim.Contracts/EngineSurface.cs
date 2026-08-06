// SDD-017 — the complete public surface. Nothing else exists: commands in,
// read model out, sealed events polled, audit queried. The read model is an
// immutable record tree built from beliefs — never truth (R21-V4).
// NO AdvisorView: the Advisor is a client (SDD-015 §1).

using OGSim.Kernel;

namespace OGSim.Contracts;

/// <summary>The renderable world — what a map screen draws under the entity layers.</summary>
public sealed record WorldView(
    GeneratedTerrain Terrain,
    IReadOnlyList<Settlement> Settlements,
    IReadOnlyList<TransportLink> Transport,
    IReadOnlyList<Harbour> Harbours,
    IReadOnlyList<SensitivityZone> LandStatus,
    IReadOnlyList<ClimateRegion> ClimateRegions,
    IReadOnlyList<Jurisdiction> Jurisdictions);

public abstract record TickResult;
public sealed record TickCompleted : TickResult;
/// <summary>Invariant fault only — non-convergence never reaches this surface (the shut-in ladder, 04 §4.0b).</summary>
public sealed record TickHalted(Fault Fault) : TickResult;

/// <summary>The whole surface (SDD-017 §1) — the interface's single owner.</summary>
public interface IEngine
{
    TickResult AdvanceTick();
    ReadModel ReadModel { get; }
    ICommandBus Commands { get; }
    /// <summary>Sealed sets, polled; only the most recent tick retained — history is the audit trail (EM-D2).</summary>
    IReadOnlyList<EngineEvent> Events(Tick tick);
    IAuditQuery Audit { get; }
    /// <summary>
    /// The static world surface — terrain, settlements, transport, regions
    /// (pass 8, finding 81: a map game whose read surface could not draw a
    /// map). Immutable after creation, so it lives BESIDE the per-tick
    /// ReadModel rather than being rebuilt with it (the AD2 rebuild rule
    /// applies to state that changes; the heightfield does not). PUBLIC
    /// knowledge only — accumulations are absent by construction: they are
    /// truth, and reach the host only as beliefs.
    /// </summary>
    WorldView World { get; }

    /// <summary>Writes the complete save container (SDD-013 §1) to the stream.
    /// The engine owns the payload; the host owns slots, paths and file I/O
    /// (R19 §5). Third pass, finding 68: without this member the host could
    /// not save through any declared type.</summary>
    void WriteSave(System.IO.Stream destination);
}

// ------------------------------------------- creation (SDD-017 §1b, pass 3)

/// <summary>Everything a new game needs — and nothing the engine can derive.</summary>
public sealed record EngineSetup(
    ulong WorldSeed,
    IReadOnlyList<IContentSource> Content,
    ContentId RealityProfile,
    ContentId GameMode,
    WorldParameters World);

/// <summary>Composition, content and save refusals share one shape: ALL reasons, engine does not start.</summary>
public abstract record EngineStartResult;
public sealed record EngineStarted(IEngine Engine) : EngineStartResult;
public sealed record EngineRefused(IReadOnlyList<LoadFailure> Reasons) : EngineStartResult;

/// <summary>
/// The host’s two doors in (SDD-017 §1b): a new world from a seed, or a saved
/// one from a container. Loading composes a NEW engine — continuation identity
/// (G2/PV2) is a property of that composition, not of mutating a live one.
/// </summary>
public interface IEngineFactory
{
    EngineStartResult CreateNew(EngineSetup setup);
    EngineStartResult LoadSave(System.IO.Stream container, IReadOnlyList<IContentSource> content);
}

// -------------------------------------------------- read model (SDD-017 §2)
// Every R21 §2.4b projection has a home here, fixture-tested (R21-V11).
// Fields below are the contract core; each phase completes its section per its
// SDD, with the path registry (SDD-017 §3) regenerated from these records.

public sealed record CompanyView(
    Money Cash,
    Money Debt,
    Money BorrowingBase,
    double BorrowingRate,
    double EsgRateSpread,                // ESG standing’s cost-of-capital effect, explicit (2.4b)
    double ReserveReplacementRatio,      // the liquidation spiral's standing indicator (IR2)
    SurfaceVolume Reserves1P,
    SurfaceVolume Reserves2P,
    SurfaceVolume Reserves3P,
    double EsgStanding,                  // the slowest loop's standing indicator (IR2)
    double SocialLicence);

public sealed record FieldView(
    EntityRef Field,
    string DisplayId,
    MassRate ProducedActual,
    MassRate ProducedPotential,
    IReadOnlyList<(EntityRef Element, ConstraintKind Kind, Mass Deferred)> DeferredByElement,
    double WaterCut,
    double GasOilRatio,
    IReadOnlyList<CompartmentView> Compartments);   // depletion / water-spiral detection (2.4b)

/// <summary>BELIEVED values — the read model never carries truth (R21-V4).</summary>
public sealed record CompartmentView(
    EntityRef Compartment,
    Pressure BelievedPressure,
    double WaterCut,
    double GasOilRatio);

public sealed record WellView(
    EntityRef Well,
    string DisplayId,
    Coordinate Site,
    WellStatus Status,
    string StatusCauseLocId,
    OperatingPoint? OperatingPoint,
    IReadOnlyList<ContentId> InstalledTiers,
    IReadOnlyList<(MassRate Rate, Pressure BottomholePressure)> IprCurve,   // sampled for rendering (2.4b)
    IReadOnlyList<(MassRate Rate, Pressure BottomholePressure)> VlpCurve);

public sealed record FacilityView(
    EntityRef Facility,
    string DisplayId,
    Coordinate Site,
    Power PowerDemand,
    Power PowerSupply,
    IReadOnlyList<(EntityRef Unit, ConstraintKind Kind, double Utilisation)> UnitUtilisation,
    IReadOnlyList<(EntityRef Unit, SpecProperty Property, double Margin)> SpecMargins);   // debottlenecking (2.4b)

public sealed record OperationView(
    EntityRef Operation,
    string DisplayId,
    OperationState State,
    int ProgressDays,
    int EffectiveDurationDays,
    Money Accrued);

public sealed record LogisticsView(
    IReadOnlyList<(EntityRef Tank, Mass Held, Mass Ullage)> Tanks,
    IReadOnlyList<(EntityRef Berth, Tick NextFree)> Berths,
    IReadOnlyList<(EntityRef Cargo, ContentId Grade, Mass Size, Tick Window)> Nominations);   // the export rhythm (2.4b)

public sealed record MarketView(
    IReadOnlyList<(ContentId Benchmark, Money PerTonne)> Prices,
    double CostIndex);

public sealed record HseView(
    double ProcessSafetyIndicator,
    double PersonalSafetyIndicator,
    IReadOnlyList<(EntityRef Barrier, double Strength, int OverdueActions)> Barriers,
    double EmissionsIntensity,
    double FlaringIntensity);

public sealed record EnvironmentView(
    double CurrentSeverity,
    IReadOnlyList<(int HorizonDays, double ExpectedSeverity, double Confidence)> Forecast,
    IReadOnlyList<(ContentId Window, int DaysRemaining)> AccessWindows,
    IReadOnlyList<(ContentId Cause, int DaysLost)> DaysLostThisTick);

public sealed record BeliefEntryView(
    EntityRef Subject,
    ContentId PropertyKind,
    double P10,
    double P50,
    double P90,
    Provenance BestSource,
    GameDate AsOf);

public sealed record BeliefView(
    IReadOnlyList<BeliefEntryView> Entries,
    IReadOnlyList<(EntityRef Prospect, PosFactor Factor, double Mean)> PosFactors,
    IReadOnlyList<(EntityRef PlayRegion, bool BeyondCurrentImaging)> ImagingFrontier);

public sealed record ExplorationView(
    IReadOnlyList<(EntityRef Licence, Polygon Area, Tick Expiry, int CommitmentItemsOutstanding)> Licences,
    IReadOnlyList<(EntityRef Prospect, Polygon BelievedOutline, double Pos)> Prospects,   // BELIEVED outline — the fuzzy map (R21 G5)
    IReadOnlyList<(EntityRef Rival, string ResultLocId)> RivalPublicResults,
    IReadOnlyList<(ContentId Source, EntityRef Subject, Money Cost, Money ExpectedValue)> PendingValueOfInformation);   // the exploration decision (2.4b)

/// <summary>“Where did my money go?” — by cause, for the period (2.4b).</summary>
public sealed record FinanceView(
    IReadOnlyList<(ContentId Cause, Money Amount)> CostsByCause,
    IReadOnlyList<(ContentId Cause, Money Amount)> RevenueByCause);

public sealed record ObjectiveView(
    IReadOnlyList<(ContentId Objective, double Progress)> Progress,
    IReadOnlyList<(ContentId Dimension, double Score)> ScoreDimensions,
    ContentId RealityProfile);           // scores are stamped (18 §5b.6)

public sealed record ReadModel(
    Tick Tick,
    GameDate Date,
    CompanyView Company,
    IReadOnlyList<FieldView> Fields,
    IReadOnlyList<WellView> Wells,
    IReadOnlyList<FacilityView> Facilities,
    IReadOnlyList<OperationView> Operations,
    LogisticsView Logistics,
    MarketView Market,
    FinanceView Finance,
    HseView Hse,
    EnvironmentView Environment,
    BeliefView Beliefs,
    ExplorationView Exploration,
    ObjectiveView Objectives);

// -------------------------------------------------- audit query (SDD-017 §4)

public sealed record ProductionLossReport(
    EntityRef Scope,
    TickRange Range,
    Mass Potential,
    Mass Actual,
    IReadOnlyList<(EntityRef Element, ConstraintKind Kind, Mass Deferred)> ByCause);

/// <summary>Pre-shaped, served — the host never re-derives attribution (SDD-017 §4).</summary>
public interface IAuditQuery
{
    IReadOnlyList<AuditEntry> ForEntity(EntityRef entity, TickRange range);
    IReadOnlyList<AuditEntry> ByCategory(AuditCategory category, TickRange range);
    IReadOnlyList<AuditEntry> CauseChain(AuditId leaf, int maxDepth);
    ProductionLossReport Losses(EntityRef fieldOrCompany, TickRange range);
}
