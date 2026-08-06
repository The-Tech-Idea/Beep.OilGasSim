# SDD-001 — Kernel Contracts

**Status:** drafted · **Serves:** R1 · **Design docs:** [01](../design/01_CONCEPT_MATRIX.md) §G, [03](../design/03_ARCHITECTURE.md) §4–6, [15](../design/15_TIME_AND_EXECUTION.md), [16](../design/16_EVENT_MATRIX.md), [R1](../phases/R1_KERNEL.md)

The signatures for `OGSim.Kernel`. Everything here is reviewable design — the
point is to argue about these shapes **now**, on paper, not in a pull request
with forty files.

---

## 1. Quantities and units — R1.1

**Pattern: one readonly record struct per dimension**, canonical SI magnitude
inside, factory-per-unit, formatting via an explicit unit. Cross-dimension
arithmetic does not exist; the legal products/quotients are declared operators.

```csharp
public readonly record struct Pressure(double Pascals) : IComparable<Pressure>
{
    public static Pressure FromPsi(double psi) => new(psi * 6894.757293168);
    public static Pressure FromBar(double bar) => new(bar * 1e5);
    public double ToPsi() => Pascals / 6894.757293168;

    public static Pressure operator +(Pressure a, Pressure b) => new(a.Pascals + b.Pascals);
    public static Pressure operator -(Pressure a, Pressure b) => new(a.Pascals - b.Pascals);
    public static double   operator /(Pressure a, Pressure b) => a.Pascals / b.Pascals; // ratio: dimensionless
    public static bool operator >(Pressure a, Pressure b) => a.Pascals > b.Pascals;
    // no operator *(Pressure, Pressure) — inexpressible by omission
}
```

**The full dimension set (18):** `Length`, `Area`, `Mass`, `Duration` (sim days,
not `TimeSpan`), `Pressure`, `Temperature`, `MassRate`, `Power`, `Energy`,
`Permeability`, `Viscosity`, `Density`, `HeatingValue`, plus the volume family
(§1.1) and rate family (`VolumetricRate` per volume type), and `Money` (§8).

Written by hand, not generated — 18 small structs is an afternoon, and the
declared-operator surface *is* the design.

> **R1.0 review correction:** `Area` (canonical m²) was used by §1.4's
> `Polygon.Area` but appeared in no dimension list and in no code — licence
> polygons, footprints and drainage areas all need it. Added here, count 17 → 18.
> Its one legal quotient is `Area / Length → Length`; `Length * Length → Area` is
> the declared product.

### 1.1 Volume conditions are types, not fields

Per [R1](../phases/R1_KERNEL.md) §2.2 — the double-count killer:

```csharp
public readonly record struct ReservoirVolume(double CubicMetres);   // rb-family
public readonly record struct SurfaceVolume(double CubicMetres);     // stb-family
public readonly record struct StandardGasVolume(double CubicMetres); // scf-family

public readonly record struct FormationVolumeFactor(double RbPerStb)
{
    public SurfaceVolume Shrink(ReservoirVolume v) => new(v.CubicMetres / RbPerStb);
    public ReservoirVolume Swell(SurfaceVolume v)  => new(v.CubicMetres * RbPerStb);
}
```

`reservoir + surface` is a compile error. Conversion **requires** an FVF in
hand. There is no implicit path — that is the entire point.

### 1.2 Nonlinear scales are conversions

```csharp
public readonly record struct ApiGravity(double Degrees)
{
    public static ApiGravity FromDensity(Density d) => new(141.5 / d.SpecificGravity() - 131.5);
    public Density ToDensity() => Density.FromSpecificGravity(141.5 / (Degrees + 131.5));
    // deliberately: no operator +, no Average(). You average densities.
}
```

### 1.3 `DetMath` — deterministic transcendentals (D-2)

```csharp
public static class DetMath
{
    public static double Exp(double x);
    public static double Ln(double x);      // x <= 0 → domain fault, never NaN
    public static double Pow(double x, double y);
    public static double Sqrt(double x) => Math.Sqrt(x); // IEEE-correct, portable
}
```

Accuracy target: ≤ 2 ulp over the correlation input ranges of
[05](../design/05_SIMULATION_MODELS.md); verified against reference values in
`MX`-class tests, byte-identical across the CI matrix. Domain violations raise
**model faults** — `Ln(-1)` in a correlation is out-of-range input, and
[09](../design/09_DIAGNOSTICS.md) §5.1 already says what happens then. `NaN` is
never produced and never accepted: any `NaN`/`Infinity` reaching a commit is an
invariant fault (INV6).

## 1.4 Spatial primitives — concept G17 (resolves open item S001-5)

```csharp
public readonly record struct Coordinate(double X, double Y);   // metres on the world plane
// Fictional basins on a flat plane (open decision W1): no geodesy, ever.

public readonly record struct Polygon                            // immutable ring, CCW, no self-intersection (validated)
{
    public Area Area { get; }                                    // shoelace — exact-order summation
    public bool Contains(Coordinate p);                          // ray-cast, ties resolved by the half-open rule
    public bool Overlaps(in Polygon other);
    public Coordinate Centroid { get; }
}

public static class Distances
{
    public static Length Euclidean(Coordinate a, Coordinate b);       // DetMath.Sqrt
    // Network distance (remoteness) is NOT here — it lives on the generated
    // transport graph (06 §5.1a step 9.5) and is computed by OGSim.World.
}
```

All algorithms are pure double arithmetic (D-1 safe) with the vertex order fixed
by construction — no epsilon-tuned geometry kernels, because blocks and
footprints never need robust intersection: `Overlaps` may be conservative
(bounding-box prefilter + edge test) as long as it is deterministic.

> **Pass-6 amendment (finding 77):** §1.1 gains `GasFormationVolumeFactor`
> (rm³/sm³): `Shrink(ReservoirVolume) → StandardGasVolume`,
> `Swell(StandardGasVolume) → ReservoirVolume`. Oil's `FormationVolumeFactor`
> bridges to `SurfaceVolume`; gas standard volumes are a DIFFERENT family and
> get a different bridge — mixing them is a compile error, as intended.

## 2. Identity — R1.2

```csharp
public readonly record struct EntityId<T>(ulong Value); // T: marker type, e.g. EntityId<IWell>

public interface IEntityRegistry
{
    EntityId<T> Issue<T>();                       // monotonic per T, save-stable
    void Register<T>(EntityId<T> id, T entity);    // completes the id issued above
    T Resolve<T>(EntityId<T> id);                  // unresolvable → INV3 invariant fault
    bool TryResolve<T>(EntityId<T> id, out T entity); // for the *few* places absence is a state
    IReadOnlyList<T> All<T>();                     // ordered by id — D-5 safe enumeration
}
```

> **R1.2 review correction:** the interface had no member that associated an
> entity with an id, so `Resolve` could never return anything and `All` was
> always empty — the registry was unimplementable as declared. `Register` closes
> it. Issue and register are deliberately **two** steps rather than one
> `Issue<T>(T entity)`: an entity carries its own `EntityId<T>` (`IFlowElement.Id`
> and every PPDM-shaped record), so the id must exist before the entity that
> holds it can be constructed. The window between them is not a silent hazard —
> resolving an issued-but-unregistered id is exactly the INV3 fault above.
>
> Two further pins the implementation needs and the SDD did not state:
> **ids begin at 1**, so `default(EntityId<T>)` is a detectably invalid id rather
> than a valid reference to the first entity ever issued; and **`Register` is
> write-once** — re-registering an id is an INV3 fault, because law L5 (one owner
> per fact) is worth nothing if a reference can be repointed.

No `Guid` (banned, D-6): ids are sequential `ulong` per entity type, issued by
the registry, part of saved state. `Resolve` throwing on a dangling id is the
[11](../design/11_PERSISTENCE.md) §2.1 "never silently drop" rule made into a
signature.

## 3. Time — R1.3, R1.13, R1.15

```csharp
public readonly record struct Tick(int Value);              // 0-based, monotonic
// THE 360-DAY YEAR, pinned: every month is exactly 30 days (the industry's own
// 30/360 day-count convention). This is what makes the /30ths segment grid
// (§9) exact for EVERY tick — real month lengths (28-31) would break grid
// uniformity, day-rate arithmetic and TM11. GameDate labels remain real
// (year, month names, eras); day arithmetic is 30/360; leap years do not exist.
public readonly record struct GameDate(int Year, int Month) // real labels, 30/360 arithmetic (TM-D5)
{
    public Quarter Quarter { get; }
    public Season SeasonAt(ClimateHemisphere h);
}

public interface ISimulationClock
{
    Tick CurrentTick { get; }
    GameDate Date { get; }
    // no setters, no Advance() — only the engine's tick pipeline moves time
}

public interface IEngine   // FULL surface owned by SDD-017 §1 — kernel pins only the tick contract
{
    TickResult AdvanceTick();   // TickResult: Completed | Halted(Fault)
    // ReadModel, Commands, Events(tick), Audit: see SDD-017. One definition,
    // one owner — an earlier draft declared a 3-member IEngine here while
    // SDD-017 declared 5; the host-surface SDD owns the interface.
}
```

`AdvanceTick()` returning `Halted` (invariant fault) rather than throwing keeps
the host's pacing loop ([15](../design/15_TIME_AND_EXECUTION.md) §4) a plain
loop; the shut-in ladder means non-convergence never reaches this surface.

## 4. Randomness — R1.4

```csharp
public enum StreamId { WorldGen, Exploration, Measurement, Hazard, Weather, Price, Market, Operations }

public interface IRandomSource
{
    IRandomStream Stream(StreamId id);
}

public interface IRandomStream
{
    double NextUnit();                    // [0,1)
    double NextNormal();                  // MARSAGLIA POLAR, pinned: rejection over
                                          // NextUnit pairs, DetMath.Ln + Sqrt only —
                                          // chosen BECAUSE DetMath has no trig
                                          // (Box-Muller needs cos). Consumes a
                                          // variable but deterministic uniform count.
    int    NextInt(int exclusiveMax);    // uniform in [0, exclusiveMax) — e.g. failure
                                          // day in {0..29} (SDD-012 §2). Pass-6 sync
                                          // (finding 78): code had it, this block didn't.
    ulong  Position { get; }             // saved/restored exactly (11 §3)
    void   Seek(ulong position);
}
```

Implementation: **PCG64**, stream seed = `SplitMix64(worldSeed ^ Hash(streamName))`.
Counter-based position makes save/seek trivial and makes R1-V5 (stream
independence) provable rather than probable.

## 5. Log, audit, fault — R1.5–R1.7

```csharp
public interface ILog
{
    // R1.0: the committed interim shape, pending S001-1. `EventName` and
    // `LogFields` were named here but declared nowhere; until the profiler data
    // that S001-1 waits on exists, the event name is a plain string and the
    // fields are an ordered list of the typed LogField pair. The call-site rule
    // that matters — no string interpolation — holds in both shapes.
    void Write(LogLevel level, string eventName, IReadOnlyList<LogField> fields);
    IDisposable Scope(ScopeKind kind, string id);                    // Session→Tick→Stage→Element nesting
}

public sealed record AuditEntry(
    AuditId Id, Tick Tick, AuditCategory Category,
    EntityRef? Subject, AuditId? Cause,             // Cause: the chain of 21 §7
    IReadOnlyDictionary<string, AuditValue> Data);

public interface IAuditTrail
{
    AuditId Record(AuditCategory category, EntityRef? subject, AuditId? cause,
                   IReadOnlyDictionary<string, AuditValue> data);
    IReadOnlyList<AuditEntry> Query(in AuditQuery query);  // by entity / tick range / category / cause-walk
}

public enum FaultClass { Content, Composition, Command, Model, Invariant, Host }

public interface IFaultPolicy
{
    FaultResolution Report(in Fault fault);
    // Strict impl: throws on everything. Resilient impl: per 09 §5.1 table.
    // FaultResolution: Continue | AbandonTick | Halt — the CALLER obeys it; the policy only decides.
}
```

`catch` blocks in the engine call `Report` and obey the resolution — the L4
architecture test verifies the call, and the policy being the *decider* (not
the *handler*) keeps stack context where the fault happened.

> **R1.5–R1.7 review corrections.** Three shapes the trio needs that §5 named
> only in prose:
>
> ```csharp
> public readonly record struct LogScope(ScopeKind Kind, string Id);
> public sealed record LogRecord(LogLevel Level, string EventName,
>     IReadOnlyList<LogField> Fields, IReadOnlyList<LogScope> Scopes);
> public interface ILogSink { void Emit(LogRecord record); }   // 09 §3's "sink"
>
> public sealed record AuditRetention(int DetailWindowTicks);  // 09 §4.4's "window"
> ```
>
> - **`ILogSink`** — 09 §3 says fields "stay typed until a sink renders them",
>   but no sink type existed, so `ILog` had nowhere to write. Every record
>   carries its full scope chain, which is what makes 09 §3's "everything inside
>   the flow solve for W-014 on tick 132" a filter rather than a text search.
> - **`AuditRetention`** — 09 §4.4 requires a *configurable* window of full
>   detail. The bound is a policy, so it is a constructor argument, not a
>   constant: L2 forbids it having a default.
> - **The retention rule is a category partition, and the cause chain overrides
>   it.** 09 §4.4 keeps every state transition, financial event and fault while
>   discarding "per-tick per-element detail" — that is `ConstraintBinding`,
>   `InvariantCheck` and `Merge`; everything else is durable. But §4.4 also says
>   *nothing that explains the current state is ever discarded*, so a prunable
>   entry that is a transitive `Cause` of a surviving entry survives with it.
>   Pruning computes that closure rather than trusting the category alone —
>   otherwise the tick-4 constraint that explains a tick-400 shut-in vanishes and
>   the "why?" query 09 §4.3 promises returns a broken chain.
> - **`Record` takes no `Tick`** — it reads the clock, so the trail requires
>   `ISimulationClock`. An entry cannot be stamped with a tick its caller chose.

## 6. Events — R1.8, R1.16

> **Pass-4 amendment (finding 75):** `Publish` returns `EventId`. Callers
> construct events with `default(EventId)`; the bus stamps the per-tick publish
> sequence (the same issuance pattern as `IAuditTrail.Record`). Without this
> pin, the total order's tiebreaker was a number no module could know.

```csharp
public abstract record EngineEvent(
    EventId Id, EventCategory Category, StageId Stage,
    Tick Tick, int Day,                             // /30ths grid — see the R1.8 note
    EntityRef? Subject, Severity Severity,
    AuditId Cause,                                   // REQUIRED for C/D (IR6) — checked at publish
    LoopRole LoopRole,                               // None | Entry | MidLoop | Consequence (21 §6)
    bool IsSegmentBoundary);                         // 21 §5 rule, decided by the raiser

public interface IEventBus
{
    EventId Publish(EngineEvent e);                         // engine-internal, stages only
    IReadOnlyList<EngineEvent> Sealed(Tick tick);           // ordered: (Stage, Day, Subject, EventId)
    // deliberately NO Subscribe(). Consumers poll Sealed() after AdvanceTick —
    // the no-subscriber rule (16 §1) as an absence, not a convention.
}
```

> **R1.8 review corrections.** Four, of which the first is the same drift the
> R1.0 review fixed in §9 and missed here:
>
> - **`double SubTickPosition` → `int Day`.** §9 pins sub-tick positions to the
>   /30ths grid as whole days so INV9 is integer arithmetic; a `[0,1)` double on
>   the event record contradicted it and would reintroduce float boundaries at
>   exactly the join where segments and events must agree (design 21 §5). The
>   committed record already carries `int Day`; the ordering key is
>   `(Stage, Day, Subject, EventId)`.
> - **`Publish` returns `EventId`**, per the pass-4 amendment above — the
>   signature line here still said `void`.
> - **Sealing is a distinct operation.** EM2 requires that no event be observable
>   mid-tick, so publishing cannot be the same act as making a set visible.
>   `Seal()` lives on the concrete `EventBus`, not on `IEventBus` — the same
>   shape as `SimulationClock.Advance()`: only the pipeline that holds the
>   concrete type can close a tick. Querying an unsealed or evicted tick faults
>   rather than returning an empty list, which would read as "nothing happened".
> - **`Publish` enforces two rules that were stated but unowned**: INV12/IR6 —
>   a `Critical` or `Decision` event without a cause is refused; and IR4 — a
>   `LoopRole.Entry` event below `Warning` is refused, because a loop-entry
>   alert nobody sees is the failure mode rule IR4 exists to prevent.

Concrete events are `sealed record`s per matrix row of
[16](../design/16_EVENT_MATRIX.md) §4, with **typed payload properties** — the
EM4 "no formatted strings" rule falls out of the type system.

## 7. Commands — R1.9

```csharp
public abstract record Command(EntityRef? Subject);

public abstract record CommandResult;
public sealed record Accepted(AuditId Audit, IReadOnlyList<EngineEvent> Immediate) : CommandResult;
public sealed record Rejected(RejectionReason Reason) : CommandResult;   // domain-typed, host-renderable (R21-V5)

public interface ICommandBus
{
    CommandResult Submit(Command command);
    // Two-phase inside (R1 §2.5): ICommandValidator<T>.Validate is pure;
    // ICommandApplier<T>.Apply cannot fail. Registered per command type by modules.
}
```

### 7.1 The command inventory — derived, not invented

Naming: `VerbNounCommand` (`ProposeWellCommand`, `InstallTierCommand`,
`AssignTreatmentCommand`, `SetChokeCommand`…), declared in the owning module's
`ModuleManifest.Commands`, specified in that module's SDD. **The required set
is derived from the decision catalogue**: PD1 ([20](../design/20_PLAYER_DECISIONS.md)
§8) demands every one of the 61 decisions map to at least one command — so the
catalogue is the inventory's source of truth, the PD1 fixture is its
completeness check, and a command no decision needs is a smell the review
should question. No SDD needs to restate the full list; each states its own.

## 8. Money

> **Pass-4 amendment (finding 74):** `Money * long` (both operand orders,
> checked) is part of the surface — day-rate × days and unit-cost × count are
> exact integer operations; routing them through `RoundHalfEven(double)` would
> trade exactness away for no reason. `Money * double` remains deliberately
> absent: fractional scaling MUST pass the half-even door, visibly.

```csharp
public readonly record struct Money(long Cents)   // scaled integer: exact, portable, D-3
{
    public static Money FromMillions(double m) => new(checked((long)(m * 100_000_000)));
    public static Money operator +(Money a, Money b) => new(checked(a.Cents + b.Cents));
    // checked arithmetic: overflow is an invariant fault, not a wrap
}
```

Cash conservation (INV2) over integers is *exact* — no tolerance term needed,
which is why money is not a `double`.

## 9. Modules, tick pipeline, segments — R1.10, R1.14, R1.17

> **Pass-2 amendment (finding 64):** `TickContext.Segments` is `SegmentPlan?` —
> null before stage 4, set exactly once by Availability. The original `required`
> shape forced the plan to exist at stage 0, before anything could have built it.


```csharp
public sealed record ModuleManifest(
    ModuleName Name,
    IReadOnlyList<Type> Provides, IReadOnlyList<Type> Requires,   // contract interfaces
    IReadOnlyList<StateKey> OwnsState,
    IReadOnlyList<StageParticipation> Stages,
    IReadOnlyList<Type> Commands);

public interface IModule
{
    ModuleManifest Manifest { get; }
    void Compose(IModuleComposition c);   // c.Provide<T>(impl), c.Require<T>() — resolution AFTER validation
}

public enum StageId  // exactly 03 §6 — the FOURTEEN stages, numbered as documented
{ Open=0, Commands=1, Environment=2, Operations=3, Availability=4, SolveFlow=5,
  MaterialBalance=6, Custody=7, Economics=8, HseRegulation=9, Information=10,
  Company=11, Objectives=12, Close=13 }

public interface ITickStage { StageId Id { get; } void Execute(TickContext ctx); }

public sealed record Segment(int StartDay, int DurationDays,
                             IReadOnlyCollection<EntityRef> Available);
public sealed record SegmentPlan(IReadOnlyList<Segment> Segments);    // built at stage 4
// invariant INV9: DurationDays sum to exactly 30 — INTEGER arithmetic on the
// /30ths grid, never float positions summing to 1.0.
```

> **R1.0 review correction:** this block previously declared
> `Segment(double Start, double Duration, AvailabilitySet Available)` and stated
> INV9 as "durations sum to 1.0 exactly" — contradicting its own next paragraph,
> which requires whole days so that INV9 *is* integer arithmetic. `AvailabilitySet`
> was also a type no SDD ever declared. The shape above is the committed one.

Segment boundaries live on a **/30ths-of-a-tick grid** (whole days) rather than
raw doubles — INV9's "sums to exactly one tick" becomes integer arithmetic, and
the 4-segment merge (TM-D2) picks grid points deterministically. The merge's
impact ranking (21 §5.2) uses a pinned estimator — **the affected element's
last-committed throughput × the boundary's remaining duration** — because true
impact needs the solve the plan precedes; last-committed is deterministic,
cheap, and wrong only when it does not matter (a boundary on an idle element).

## 10. State — R1.11

```csharp
public interface IStateOwner
{
    StateKey Key { get; }
    int SchemaVersion { get; }
    void Capture(IStateWriter w);    // writer: ordered key/value, canonical form
    void Restore(IStateReader r);    // missing/unreadable value → SaveDataFault, never default
}
```

Canonical writer (sorted keys, invariant formatting, no floats-as-strings
ambiguity: doubles as 17-digit round-trip) is what makes the PV1 byte-identity
and digest tests meaningful.

## 11. Error surface summary

| Situation | Class | Carrier |
|---|---|---|
| `DetMath` domain, correlation out of range | Model | `ModelFault` |
| Dangling `EntityId` | Invariant (INV3) | `Resolve` throws `InvariantFault` |
| `NaN`/overflow at commit | Invariant (INV6) | `InvariantFault` |
| Command invalid | not a fault | `Rejected(reason)` |
| Missing save value | Content-class on load | `SaveDataFault` |
| Publish `C`/`D` event without cause | Invariant (INV12) | `Publish` throws |

## 12. Test plan mapping

R1-V1..V22 map onto these signatures directly; three worth calling out:
**R1-V2** (dimension safety) is a *compile-failure* test — a source file of
illegal expressions asserted not to compile via Roslyn; **R1-V5** (stream
independence) follows from PCG64 counters by construction and is tested anyway;
**R1-V16..18** (segmentation) test the /30ths grid arithmetic, including the
audited merge.

## 13. Open items

| # | Item | Trigger |
|---|---|---|
| S001-1 | `LogFields`: struct of spans vs pooled dictionary — decide on first profiler data | R4 benchmarks |
| S001-2 | Audit trail storage: append-only file + in-memory index vs pure in-memory until save | R1.6 implementation |
| S001-3 | ~~EntityRef shape~~ **Decided:** `readonly record struct EntityRef(EntityKind Kind, ulong Value)` — one struct with a kind tag; an interface would box in every event and audit entry | closed |
| S001-4 | Whether `TickContext` exposes stage-scoped read isolation by interface-per-stage (strong, verbose) or by runtime assert (I-V5) | SDD-002 |
| S001-5 | ~~Spatial primitives~~ **Resolved** — specified in §1.4 | closed |
