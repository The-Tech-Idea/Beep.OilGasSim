# SDD-017 — Host Surface and Read Model

**Status:** drafted · **Serves:** R21 · **Design docs:** [R21](../phases/R21_HOST.md), [03](../design/03_ARCHITECTURE.md) §7, [09](../design/09_DIAGNOSTICS.md) §7

The complete public surface — the shapes SDD-014's path registry and SDD-015's
Advisor bind against, so all three lock together.

---

## 1. The whole surface (nothing else exists)

```csharp
public interface IEngine
{
    TickResult AdvanceTick();                          // SDD-001 §3
    ReadModel ReadModel { get; }                       // §2 — the record itself (immutable);
                                                       // no IReadModel interface exists to drift from it
    WorldView World { get; }                           // §1c — static map base layer, public knowledge only
    ICommandBus Commands { get; }                      // SDD-001 §7
    IReadOnlyList<EngineEvent> Events(Tick tick);      // sealed sets, polled (no Subscribe — SDD-001 §6);
                                                       // ONLY the most recent tick is retained here —
                                                       // history lives in the audit trail (EM-D2)
    IAuditQuery Audit { get; }                         // §4
    void WriteSave(System.IO.Stream destination);      // §1b — the SDD-013 §1 container
}
// This block IS the surface — §1b/§1c amendments are folded in above so the
// block never understates the interface again.
```

## 1b. Creation and saving (pass-3 amendment, finding 68)

The §1 surface had no way to save and no way to start — the host could not
reach tick 0 or keep tick N through any declared type. Pinned:

```csharp
// on IEngine:
void WriteSave(System.IO.Stream destination);   // the SDD-013 §1 container; host owns slots/paths/IO (R19 §5)

public sealed record EngineSetup(
    ulong WorldSeed,
    IReadOnlyList<IContentSource> Content,
    ContentId RealityProfile,
    ContentId GameMode,
    WorldParameters World);                         // SDD-010 §4 — the new-world knobs

public abstract record EngineStartResult;
public sealed record EngineStarted(IEngine Engine) : EngineStartResult;
public sealed record EngineRefused(IReadOnlyList<LoadFailure> Reasons) : EngineStartResult;
// Composition refusals carry CompositionProblem, which has no file, no JSON path
// and no load stage to invent (R20c revision, finding 133).
public sealed record EngineCompositionRefused(
    IReadOnlyList<CompositionProblem> Problems) : EngineStartResult;

public interface IEngineFactory
{
    EngineStartResult CreateNew(EngineSetup setup);
    EngineStartResult LoadSave(System.IO.Stream container, IReadOnlyList<IContentSource> content);
}
```

> **Contract pass 10.** `EngineSetup` had four members; SDD-010's pass-7
> amendment added `WorldParameters` to world generation and nothing propagated
> it here, so the host had no declared way to pass the new-world knobs to
> `CreateNew` — the very call they parameterise. Fifth occurrence of the
> amendment-not-propagated pattern and the first that crosses two documents.
>
> `EngineStarted` and `EngineRefused` existed only as a trailing comment on the
> abstract base.

Loading composes a NEW engine — continuation identity (G2/PV2) is a property
of that composition. Composition, content and save refusals share the
`LoadFailure` shape: ALL reasons, engine does not start.

## 1c. The world surface (pass-8 amendment, finding 81)

```csharp
public sealed record WorldView(
    GeneratedTerrain Terrain,
    IReadOnlyList<Settlement> Settlements,
    IReadOnlyList<TransportLink> Transport,
    IReadOnlyList<Harbour> Harbours,
    IReadOnlyList<SensitivityZone> LandStatus,
    IReadOnlyList<ClimateRegion> ClimateRegions,
    IReadOnlyList<Jurisdiction> Jurisdictions);
```

**`WorldView` reuses SDD-010's handoff records rather than defining view copies
of them**, because the surface is static: a projection exists to keep a mutable
truth away from the host, and there is no mutable truth here to keep away. That
`GeneratedAccumulation` is *absent* from this list is the whole guarantee — the
type system, not a filter, is what stops accumulations reaching the map.

`IEngine.World` is the map screen's base layer —
immutable after creation, so it sits beside the per-tick `ReadModel` instead of
being rebuilt with it. It carries PUBLIC knowledge only: accumulations never
appear — they are truth and reach the host solely as beliefs
(`ExplorationView.Prospects` renders believed outlines with POS — R21 G5's
fuzzy map). Spatial anchors on the per-tick views: `WellView.Site`,
`FacilityView.Site`, licence `Area` polygons.

## 2. `ReadModel` — an immutable record tree

One root record, rebuilt whole each tick (AD2), sections mirroring
[R21](../phases/R21_HOST.md) §2.4b's sixteen projections:

```csharp
public sealed record ReadModel(
    Tick Tick, GameDate Date,
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
// NO AdvisorView — an earlier draft put Advisor proposals inside the engine's
// read model, but the Advisor is a CLIENT (SDD-015 §1): the engine cannot carry
// a client's state. Advisor output lives beside the read model, host-side.

public sealed record CompanyView(
    Money Cash, Money Debt, Money BorrowingBase, double BorrowingRate,
    double EsgRateSpread,                // ESG's cost-of-capital effect, explicit
    double ReserveReplacementRatio,      // the liquidation spiral's standing indicator (IR2)
    SurfaceVolume Reserves1P, SurfaceVolume Reserves2P, SurfaceVolume Reserves3P,
    double EsgStanding, double SocialLicence);

// BELIEVED values — the read model never carries truth (R21-V4).
public sealed record CompartmentView(
    EntityRef Compartment, Pressure BelievedPressure, double WaterCut, double GasOilRatio);

public sealed record FieldView(
    EntityRef Field, string DisplayId,
    MassRate ProducedActual, MassRate ProducedPotential,
    IReadOnlyList<(EntityRef Element, ConstraintKind Kind, Mass Deferred)> DeferredByElement,
    double WaterCut, double GasOilRatio,
    IReadOnlyList<CompartmentView> Compartments);

public sealed record WellView(
    EntityRef Well, string DisplayId, Coordinate Site,
    WellStatus Status, string StatusCauseLocId,       // LocId, never formatted text (EM4)
    OperatingPoint? OperatingPoint,
    IReadOnlyList<ContentId> InstalledTiers,
    IReadOnlyList<(MassRate Rate, Pressure BottomholePressure)> IprCurve,   // sampled for rendering
    IReadOnlyList<(MassRate Rate, Pressure BottomholePressure)> VlpCurve);

public sealed record FacilityView(
    EntityRef Facility, string DisplayId, Coordinate Site,
    Power PowerDemand, Power PowerSupply,
    IReadOnlyList<(EntityRef Unit, ConstraintKind Kind, double Utilisation)> UnitUtilisation,
    IReadOnlyList<(EntityRef Unit, SpecProperty Property, double Margin)> SpecMargins);

public sealed record OperationView(
    EntityRef Operation, string DisplayId, OperationState State,
    int ProgressDays, int EffectiveDurationDays, Money Accrued);

public sealed record LogisticsView(
    IReadOnlyList<(EntityRef Tank, Mass Held, Mass Ullage)> Tanks,
    IReadOnlyList<(EntityRef Berth, Tick NextFree)> Berths,
    IReadOnlyList<(EntityRef Cargo, ContentId Grade, Mass Size, Tick Window)> Nominations);

public sealed record MarketView(
    IReadOnlyList<(ContentId Benchmark, Money PerTonne)> Prices, double CostIndex);

// "Where did my money go?" — by cause, for the period (R21 §2.4b).
public sealed record FinanceView(
    IReadOnlyList<(ContentId Cause, Money Amount)> CostsByCause,
    IReadOnlyList<(ContentId Cause, Money Amount)> RevenueByCause);

public sealed record HseView(
    double ProcessSafetyIndicator, double PersonalSafetyIndicator,
    IReadOnlyList<(EntityRef Barrier, double Strength, int OverdueActions)> Barriers,
    double EmissionsIntensity, double FlaringIntensity);

public sealed record EnvironmentView(
    double CurrentSeverity,
    IReadOnlyList<(int HorizonDays, double ExpectedSeverity, double Confidence)> Forecast,
    IReadOnlyList<(ContentId Window, int DaysRemaining)> AccessWindows,
    IReadOnlyList<(ContentId Cause, int DaysLost)> DaysLostThisTick);

public sealed record BeliefEntryView(
    EntityRef Subject, ContentId PropertyKind,
    double P10, double P50, double P90,               // P90 LOW, P10 HIGH (SDD-002 §2b)
    Provenance BestSource, GameDate AsOf);

public sealed record BeliefView(
    IReadOnlyList<BeliefEntryView> Entries,
    IReadOnlyList<(EntityRef Prospect, PosFactor Factor, double Mean)> PosFactors,
    IReadOnlyList<(EntityRef PlayRegion, bool BeyondCurrentImaging)> ImagingFrontier);

public sealed record ExplorationView(
    IReadOnlyList<(EntityRef Licence, Polygon Area, Tick Expiry, int CommitmentItemsOutstanding)> Licences,
    IReadOnlyList<(EntityRef Prospect, Polygon BelievedOutline, double Pos)> Prospects,   // BELIEVED outline
    IReadOnlyList<(EntityRef Rival, string ResultLocId)> RivalPublicResults,
    IReadOnlyList<(ContentId Source, EntityRef Subject, Money Cost, Money ExpectedValue)> PendingValueOfInformation);

public sealed record ObjectiveView(
    IReadOnlyList<(ContentId Objective, double Progress)> Progress,
    IReadOnlyList<(ContentId Dimension, double Score)> ScoreDimensions,
    ContentId RealityProfile);            // scores are stamped (18 §5b.6)
```

> **R20d.1 amendment — the chain, as one row per element.** §2's views split the
> two halves of the bottleneck report across the hierarchy: `FieldView` carries
> `DeferredByElement` and `FacilityView` carries `UnitUtilisation`. That is right
> for the finished surface, and unusable for the subset a wired-but-incomplete
> loop can fill — there is no facility hierarchy yet, and the question a player
> asks does not respect one:
>
> ```csharp
> // One row per element in the chain, in the solver's own topological order.
> public sealed record ChainElementView(
>     EntityRef Element, string DisplayId,
>     MassRate Throughput,                                        // what crossed it
>     IReadOnlyList<(ConstraintKind Kind, Mass Deferred)> Deferred);   // what it refused
> ```
>
> **Deferral is the jam and throughput is the flow**, which together are the
> whole of "where is my production going and what is stopping it". They come
> from `SolveReport` directly — §8's attribution pass already computes the
> deferral per element per constraint against what each completion WANTED, so
> this projection reads a number the solver committed to rather than deriving a
> second opinion about it.
>
> `Utilisation` is deliberately absent: it needs the raw
> `ConstraintEvaluation`s, and `SolveReport` reports only the violations
> ([SDD-002](SDD-002_STREAMS_AND_FLOW.md) §8). "How full is it" therefore waits
> on a §8 amendment; "what is it refusing" does not, and is the half a player
> acts on.
>
> The rows fold into `FieldView.DeferredByElement` and
> `FacilityView.UnitUtilisation` when those views have something to hang from.

> **R21.5 amendment — the wells, as a subset read model can show them.** §2's
> `WellView` carries a site, an operating point and sampled IPR/VLP curves,
> none of which the current loop has a source for — and the subset read model
> therefore carried a well COUNT and nothing else. A count cannot be acted on:
> every well-level command names one, so a client could open a field and then
> not shut a single well in.
>
> ```csharp
> public sealed record WellStatusView(
>     EntityRef Well, string DisplayId,
>     WellStatus Status,                 // Producing · ShutIn · Abandoned
>     SurfaceVolume ProducedThisTick);
> ```
>
> Found by building the reference client (§2.5's own rule: *if it needs anything
> the surface does not offer, the surface is incomplete*), which is where it was
> supposed to be found. It folds into `WellView` when a site and an operating
> point have sources.

> **Contract pass 10 — `FinanceView` was missing from the root.** This section
> listed fourteen members and claimed "the exact 16-section ⇔ R21 §2.4b
> correspondence (V11)" while omitting the projection R21 §2.4b calls *"where
> did my money go?"*. The count in the claim and the count in the record never
> agreed, and the note asserting they did is what stopped anyone checking.
>
> The fourteen view records were described in trailing comments and declared
> nowhere. Declared above — worth doing in full rather than by summary, because
> R21-V11 fixture-tests each section and SDD-014's path registry (§3) is
> generated from these exact shapes.

- **All records, all `IReadOnlyList`, no engine entity references** — views
  carry `EntityRef` + display ids only. Immutability is structural (records of
  records), so R21-V1 is a property of the types.
- Views are **built from module projections at stage 13**, each module
  supplying its section through a projection contract — the read model
  assembly references belief and ledger types, never truth (R21-V4).

## 3. The path registry (SDD-014 §2's source)

Generated **from these records by reflection at build time**: every public
property path (`company.rrr`, `wells[*].waterCut`) with its type. Objectives
and Advisor rules validate against it at content load; a rename here breaks
content loudly at load. One generator, three consumers (objectives, advisor,
the host's own binding) — the registry *is* the read-model schema.

## 4. Audit query

```csharp
public sealed record ProductionLossReport(
    EntityRef Scope,
    TickRange Range,
    Mass Potential,
    Mass Actual,
    IReadOnlyList<(EntityRef Element, ConstraintKind Kind, Mass Deferred)> ByCause);

public interface IAuditQuery
{
    IReadOnlyList<AuditEntry> ForEntity(EntityRef entity, TickRange range);
    IReadOnlyList<AuditEntry> ByCategory(AuditCategory category, TickRange range);
    IReadOnlyList<AuditEntry> CauseChain(AuditId leaf, int maxDepth);        // 21 §7, I-D3 cap
    ProductionLossReport Losses(EntityRef fieldOrCompany, TickRange range);  // 09 §7 — pre-shaped
}
```

> **Contract pass 10.** `maxDepth` carried a default of 10. Law L2 bans
> defaulted dependencies, and while an `int` is not a collaborator, the depth cap
> here is a *policy* (I-D3) — the same argument that makes `AuditRetention` a
> constructor argument in SDD-001 §5 rather than a constant. A caller that has
> not thought about how deep a cause chain it wants should be made to. Committed
> without the default. `ProductionLossReport` was named in the return and
> declared nowhere.

`ProductionLossReport` is served, not derived by the host — the deferral ledger
(SDD-002 §8) is authoritative and pre-aggregated here.

## 5. Test mapping

R21-V1 (structural immutability) · V2 (reference client on this surface alone)
· V4 (no truth types in this assembly) · V5 (rejections carry domain reasons —
SDD-001 §7 `Rejected`) · V6 (§4 answers 09 §7's seven features) · V7 (§2
BeliefView) · V8 (stage-13 build within budget) · V11 (§2 sections ⇔ R21
§2.4b sixteen rows, checked as a fixture test) · V12/V13 (every `C`/`D` event
category maps to a view; ESG+RRR present in every snapshot) · GM4/R24-V14 and
R25-V6 bind through §3.

## 6. Open items

| # | Item | Trigger |
|---|---|---|
| S017-1 | Curve sampling density for IPR/VLP views (host draws the chart; how many points) — start 32, content | R21.1 |
| S017-2 | Localisation of `RejectionReason`/reasoning rendering (`$loc:` binding at the host boundary) | S004-2 |
