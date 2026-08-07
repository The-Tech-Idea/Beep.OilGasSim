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
    IReadOnlyList<Jurisdiction> Jurisdictions)
{
    // Finding 131. Every record in this file carries the same override for the
    // same reason: a read model is compared against the previous tick's to
    // answer "what changed?", and reference equality answers "everything".
    public bool Equals(WorldView? other) =>
        other is not null && Terrain == other.Terrain
        && Structural.Equal(Settlements, other.Settlements)
        && Structural.Equal(Transport, other.Transport)
        && Structural.Equal(Harbours, other.Harbours)
        && Structural.Equal(LandStatus, other.LandStatus)
        && Structural.Equal(ClimateRegions, other.ClimateRegions)
        && Structural.Equal(Jurisdictions, other.Jurisdictions);

    public override int GetHashCode() =>
        HashCode.Combine(Terrain,
            Structural.HashOf(Settlements), Structural.HashOf(Transport),
            Structural.HashOf(Harbours), Structural.HashOf(LandStatus),
            Structural.HashOf(ClimateRegions), Structural.HashOf(Jurisdictions));
}

// TickResult moved to OGSim.Kernel at R1.13: SDD-001 §3 pins it, and the tick
// pipeline that produces it is a kernel type which cannot reference Contracts.
// It is still the return of AdvanceTick below — Contracts depends on the kernel.

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
    WorldParameters World)
{
    // Finding 131.
    public bool Equals(EngineSetup? other) =>
        other is not null && WorldSeed == other.WorldSeed
        && RealityProfile == other.RealityProfile && GameMode == other.GameMode
        && World == other.World
        && Structural.Equal(Content, other.Content);

    public override int GetHashCode() =>
        HashCode.Combine(WorldSeed, RealityProfile, GameMode, World,
                         Structural.HashOf(Content));
}

/// <summary>Content, composition and save refusals all mean the same thing: ALL
/// reasons reported, engine does not start. They do not share one PAYLOAD —
/// see below.</summary>
public abstract record EngineStartResult;
public sealed record EngineStarted(IEngine Engine) : EngineStartResult;

/// <summary>Content and save refusals — a file, a JSON path and a load stage.</summary>
public sealed record EngineRefused(IReadOnlyList<LoadFailure> Reasons) : EngineStartResult
{
    // Finding 131.
    public bool Equals(EngineRefused? other) =>
        other is not null && Structural.Equal(Reasons, other.Reasons);

    public override int GetHashCode() => Structural.HashOf(Reasons);
}

/// <summary>
/// Composition refusals (finding 133). This section claimed all three refusals
/// "share one shape" and only <see cref="LoadFailure"/> existed, so a factory
/// whose module set failed to compose had nothing to report it with: a
/// <see cref="CompositionProblem"/> names a module and a kind, and has no file,
/// no JSON path and no load stage to invent. Squeezing one into a
/// <c>LoadFailure</c> would have meant fabricating a filename for a defect that
/// is not in a file — and the whole value of an all-or-nothing refusal is that
/// it names precisely what is wrong.
/// </summary>
public sealed record EngineCompositionRefused(
    IReadOnlyList<CompositionProblem> Problems) : EngineStartResult
{
    // Finding 131.
    public bool Equals(EngineCompositionRefused? other) =>
        other is not null && Structural.Equal(Problems, other.Problems);

    public override int GetHashCode() => Structural.HashOf(Problems);
}

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
    IReadOnlyList<CompartmentView> Compartments)   // depletion / water-spiral detection (2.4b)
{
    // Finding 131.
    public bool Equals(FieldView? other) =>
        other is not null && Field == other.Field
        && string.Equals(DisplayId, other.DisplayId, StringComparison.Ordinal)
        && ProducedActual == other.ProducedActual
        && ProducedPotential == other.ProducedPotential
        && WaterCut.Equals(other.WaterCut) && GasOilRatio.Equals(other.GasOilRatio)
        && Structural.Equal(DeferredByElement, other.DeferredByElement)
        && Structural.Equal(Compartments, other.Compartments);

    public override int GetHashCode() =>
        HashCode.Combine(Field, DisplayId, ProducedActual, ProducedPotential,
            WaterCut, GasOilRatio,
            Structural.HashOf(DeferredByElement), Structural.HashOf(Compartments));
}

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
    IReadOnlyList<(MassRate Rate, Pressure BottomholePressure)> VlpCurve)
{
    // Finding 131.
    public bool Equals(WellView? other) =>
        other is not null && Well == other.Well
        && string.Equals(DisplayId, other.DisplayId, StringComparison.Ordinal)
        && Site == other.Site && Status == other.Status
        && string.Equals(StatusCauseLocId, other.StatusCauseLocId, StringComparison.Ordinal)
        && OperatingPoint == other.OperatingPoint
        && Structural.Equal(InstalledTiers, other.InstalledTiers)
        && Structural.Equal(IprCurve, other.IprCurve)
        && Structural.Equal(VlpCurve, other.VlpCurve);

    public override int GetHashCode() =>
        HashCode.Combine(Well, DisplayId, Site, Status, StatusCauseLocId, OperatingPoint,
            Structural.HashOf(InstalledTiers),
            HashCode.Combine(Structural.HashOf(IprCurve), Structural.HashOf(VlpCurve)));
}

public sealed record FacilityView(
    EntityRef Facility,
    string DisplayId,
    Coordinate Site,
    Power PowerDemand,
    Power PowerSupply,
    IReadOnlyList<(EntityRef Unit, ConstraintKind Kind, double Utilisation)> UnitUtilisation,
    IReadOnlyList<(EntityRef Unit, SpecProperty Property, double Margin)> SpecMargins)   // debottlenecking (2.4b)
{
    // Finding 131.
    public bool Equals(FacilityView? other) =>
        other is not null && Facility == other.Facility
        && string.Equals(DisplayId, other.DisplayId, StringComparison.Ordinal)
        && Site == other.Site
        && PowerDemand == other.PowerDemand && PowerSupply == other.PowerSupply
        && Structural.Equal(UnitUtilisation, other.UnitUtilisation)
        && Structural.Equal(SpecMargins, other.SpecMargins);

    public override int GetHashCode() =>
        HashCode.Combine(Facility, DisplayId, Site, PowerDemand, PowerSupply,
            Structural.HashOf(UnitUtilisation), Structural.HashOf(SpecMargins));
}

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
    IReadOnlyList<(EntityRef Cargo, ContentId Grade, Mass Size, Tick Window)> Nominations)   // the export rhythm (2.4b)
{
    // Finding 131.
    public bool Equals(LogisticsView? other) =>
        other is not null
        && Structural.Equal(Tanks, other.Tanks)
        && Structural.Equal(Berths, other.Berths)
        && Structural.Equal(Nominations, other.Nominations);

    public override int GetHashCode() =>
        HashCode.Combine(Structural.HashOf(Tanks), Structural.HashOf(Berths),
                         Structural.HashOf(Nominations));
}

public sealed record MarketView(
    IReadOnlyList<(ContentId Benchmark, Money PerTonne)> Prices,
    double CostIndex)
{
    // Finding 131.
    public bool Equals(MarketView? other) =>
        other is not null && CostIndex.Equals(other.CostIndex)
        && Structural.Equal(Prices, other.Prices);

    public override int GetHashCode() =>
        HashCode.Combine(CostIndex, Structural.HashOf(Prices));
}

public sealed record HseView(
    double ProcessSafetyIndicator,
    double PersonalSafetyIndicator,
    IReadOnlyList<(EntityRef Barrier, double Strength, int OverdueActions)> Barriers,
    double EmissionsIntensity,
    double FlaringIntensity)
{
    // Finding 131.
    public bool Equals(HseView? other) =>
        other is not null
        && ProcessSafetyIndicator.Equals(other.ProcessSafetyIndicator)
        && PersonalSafetyIndicator.Equals(other.PersonalSafetyIndicator)
        && EmissionsIntensity.Equals(other.EmissionsIntensity)
        && FlaringIntensity.Equals(other.FlaringIntensity)
        && Structural.Equal(Barriers, other.Barriers);

    public override int GetHashCode() =>
        HashCode.Combine(ProcessSafetyIndicator, PersonalSafetyIndicator,
            EmissionsIntensity, FlaringIntensity, Structural.HashOf(Barriers));
}

public sealed record EnvironmentView(
    double CurrentSeverity,
    IReadOnlyList<(int HorizonDays, double ExpectedSeverity, double Confidence)> Forecast,
    IReadOnlyList<(ContentId Window, int DaysRemaining)> AccessWindows,
    IReadOnlyList<(ContentId Cause, int DaysLost)> DaysLostThisTick)
{
    // Finding 131.
    public bool Equals(EnvironmentView? other) =>
        other is not null && CurrentSeverity.Equals(other.CurrentSeverity)
        && Structural.Equal(Forecast, other.Forecast)
        && Structural.Equal(AccessWindows, other.AccessWindows)
        && Structural.Equal(DaysLostThisTick, other.DaysLostThisTick);

    public override int GetHashCode() =>
        HashCode.Combine(CurrentSeverity, Structural.HashOf(Forecast),
            Structural.HashOf(AccessWindows), Structural.HashOf(DaysLostThisTick));
}

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
    IReadOnlyList<(EntityRef PlayRegion, bool BeyondCurrentImaging)> ImagingFrontier)
{
    // Finding 131.
    public bool Equals(BeliefView? other) =>
        other is not null
        && Structural.Equal(Entries, other.Entries)
        && Structural.Equal(PosFactors, other.PosFactors)
        && Structural.Equal(ImagingFrontier, other.ImagingFrontier);

    public override int GetHashCode() =>
        HashCode.Combine(Structural.HashOf(Entries), Structural.HashOf(PosFactors),
                         Structural.HashOf(ImagingFrontier));
}

public sealed record ExplorationView(
    IReadOnlyList<(EntityRef Licence, Polygon Area, Tick Expiry, int CommitmentItemsOutstanding)> Licences,
    IReadOnlyList<(EntityRef Prospect, Polygon BelievedOutline, double Pos)> Prospects,   // BELIEVED outline — the fuzzy map (R21 G5)
    IReadOnlyList<(EntityRef Rival, string ResultLocId)> RivalPublicResults,
    IReadOnlyList<(ContentId Source, EntityRef Subject, Money Cost, Money ExpectedValue)> PendingValueOfInformation)   // the exploration decision (2.4b)
{
    // Finding 131.
    public bool Equals(ExplorationView? other) =>
        other is not null
        && Structural.Equal(Licences, other.Licences)
        && Structural.Equal(Prospects, other.Prospects)
        && Structural.Equal(RivalPublicResults, other.RivalPublicResults)
        && Structural.Equal(PendingValueOfInformation, other.PendingValueOfInformation);

    public override int GetHashCode() =>
        HashCode.Combine(Structural.HashOf(Licences), Structural.HashOf(Prospects),
            Structural.HashOf(RivalPublicResults),
            Structural.HashOf(PendingValueOfInformation));
}

/// <summary>“Where did my money go?” — by cause, for the period (2.4b).</summary>
public sealed record FinanceView(
    IReadOnlyList<(ContentId Cause, Money Amount)> CostsByCause,
    IReadOnlyList<(ContentId Cause, Money Amount)> RevenueByCause)
{
    // Finding 131.
    public bool Equals(FinanceView? other) =>
        other is not null
        && Structural.Equal(CostsByCause, other.CostsByCause)
        && Structural.Equal(RevenueByCause, other.RevenueByCause);

    public override int GetHashCode() =>
        HashCode.Combine(Structural.HashOf(CostsByCause), Structural.HashOf(RevenueByCause));
}

public sealed record ObjectiveView(
    IReadOnlyList<(ContentId Objective, double Progress)> Progress,
    IReadOnlyList<(ContentId Dimension, double Score)> ScoreDimensions,
    ContentId RealityProfile)           // scores are stamped (18 §5b.6)
{
    // Finding 131.
    public bool Equals(ObjectiveView? other) =>
        other is not null && RealityProfile == other.RealityProfile
        && Structural.Equal(Progress, other.Progress)
        && Structural.Equal(ScoreDimensions, other.ScoreDimensions);

    public override int GetHashCode() =>
        HashCode.Combine(RealityProfile, Structural.HashOf(Progress),
                         Structural.HashOf(ScoreDimensions));
}

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
    ObjectiveView Objectives)
{
    // Finding 131 — the one that matters most in this file: the read model is
    // what a host diffs tick to tick, and reference equality reports every
    // collection as changed every month.
    public bool Equals(ReadModel? other) =>
        other is not null && Tick == other.Tick && Date == other.Date
        && Company == other.Company && Logistics == other.Logistics
        && Market == other.Market && Finance == other.Finance && Hse == other.Hse
        && Environment == other.Environment && Beliefs == other.Beliefs
        && Exploration == other.Exploration && Objectives == other.Objectives
        && Structural.Equal(Fields, other.Fields)
        && Structural.Equal(Wells, other.Wells)
        && Structural.Equal(Facilities, other.Facilities)
        && Structural.Equal(Operations, other.Operations);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Tick);
        hash.Add(Date);
        hash.Add(Company);
        hash.Add(Logistics);
        hash.Add(Market);
        hash.Add(Finance);
        hash.Add(Hse);
        hash.Add(Environment);
        hash.Add(Beliefs);
        hash.Add(Exploration);
        hash.Add(Objectives);
        hash.Add(Structural.HashOf(Fields));
        hash.Add(Structural.HashOf(Wells));
        hash.Add(Structural.HashOf(Facilities));
        hash.Add(Structural.HashOf(Operations));
        return hash.ToHashCode();
    }
}

// -------------------------------------------------- audit query (SDD-017 §4)

public sealed record ProductionLossReport(
    EntityRef Scope,
    TickRange Range,
    Mass Potential,
    Mass Actual,
    IReadOnlyList<(EntityRef Element, ConstraintKind Kind, Mass Deferred)> ByCause)
{
    // Finding 131.
    public bool Equals(ProductionLossReport? other) =>
        other is not null && Scope == other.Scope && Range == other.Range
        && Potential == other.Potential && Actual == other.Actual
        && Structural.Equal(ByCause, other.ByCause);

    public override int GetHashCode() =>
        HashCode.Combine(Scope, Range, Potential, Actual, Structural.HashOf(ByCause));
}

/// <summary>Pre-shaped, served — the host never re-derives attribution (SDD-017 §4).</summary>
public interface IAuditQuery
{
    IReadOnlyList<AuditEntry> ForEntity(EntityRef entity, TickRange range);
    IReadOnlyList<AuditEntry> ByCategory(AuditCategory category, TickRange range);
    IReadOnlyList<AuditEntry> CauseChain(AuditId leaf, int maxDepth);
    ProductionLossReport Losses(EntityRef fieldOrCompany, TickRange range);
}
