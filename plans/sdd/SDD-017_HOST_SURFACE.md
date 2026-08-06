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

public sealed record EngineSetup(ulong WorldSeed, IReadOnlyList<IContentSource> Content,
                                 ContentId RealityProfile, ContentId GameMode);
public abstract record EngineStartResult;           // Started(IEngine) | Refused(IReadOnlyList<LoadFailure>)
public interface IEngineFactory
{
    EngineStartResult CreateNew(EngineSetup setup);
    EngineStartResult LoadSave(System.IO.Stream container, IReadOnlyList<IContentSource> content);
}
```

Loading composes a NEW engine — continuation identity (G2/PV2) is a property
of that composition. Composition, content and save refusals share the
`LoadFailure` shape: ALL reasons, engine does not start.

## 1c. The world surface (pass-8 amendment, finding 81)

`IEngine.World` (a `WorldView`: terrain, settlements, transport, harbours,
land status, climate regions, jurisdictions) is the map screen's base layer —
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
    CompanyView Company,          // cash, debt, borrowing base+rate, RRR, reserves by class, ESG, SL
    IReadOnlyList<FieldView> Fields,      // production actual/potential/deferred-by-element, pressures, water cut, GOR
    IReadOnlyList<WellView> Wells,        // status+cause, operating point, IPR/VLP curve samples, installed tiers
    IReadOnlyList<FacilityView> Facilities,   // unit capacities/utilisation/spec margins, power balance
    IReadOnlyList<OperationView> Operations,  // progress, expected completion, standby, accrued
    LogisticsView Logistics,      // tanks/ullage, berths, cargoes, linefill
    MarketView Market,            // benchmarks, realised components, contracts, cost index
    HseView Hse,                  // barrier status+backlog, two safety indicators, emissions vs caps, incidents
    EnvironmentView Environment,  // weather, forecast (SDD-016 §4), windows with time remaining, days lost by cause
    BeliefView Beliefs,           // per entity/kind: P10/P50/P90, provenance, as-of; POS factors; "beyond imaging" flags
    ExplorationView Exploration,  // licences+clocks+commitments, rounds, rival public results, VOI panels
    ObjectiveView Objectives);    // progress, score dimensions, profile stamp
// NO AdvisorView — an earlier draft put Advisor proposals inside the engine's
// read model, but the Advisor is a CLIENT (SDD-015 §1): the engine cannot carry
// a client's state. Advisor output lives beside the read model, host-side.
// This also restores the exact 16-section ⇔ R21 §2.4b correspondence (V11).
```

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
public interface IAuditQuery
{
    IReadOnlyList<AuditEntry> ForEntity(EntityRef e, TickRange range);
    IReadOnlyList<AuditEntry> ByCategory(AuditCategory c, TickRange range);
    IReadOnlyList<AuditEntry> CauseChain(AuditId leaf, int maxDepth = 10);   // 21 §7, I-D3 cap
    ProductionLossReport Losses(EntityRef fieldOrCompany, TickRange range);  // 09 §7 — pre-shaped, not host-derived
}
```

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
