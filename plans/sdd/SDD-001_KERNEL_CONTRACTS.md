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

**The set, by canonical unit.** Each follows the `Pressure` exemplar above:
`+`, `−`, the like-over-like `/` returning a dimensionless `double`, comparison
operators and `IComparable<T>`. Only the *cross-dimension* operators are listed
here, because those are the ones that carry design information — every omission
is deliberate, and a missing line means the operation does not exist.

```csharp
public readonly record struct Length(double Metres);              // × Length → Area
public readonly record struct Area(double SquareMetres);          // ÷ Length → Length
public readonly record struct Mass(double Kilograms);
public readonly record struct Duration(double Days);              // DaysPerTick = 30 (§3)
public readonly record struct Temperature(double Kelvin);         // − Temperature → TemperatureDelta
public readonly record struct TemperatureDelta(double Kelvin);    // ± with Temperature
public readonly record struct MassRate(double KgPerSecond);       // × Duration → Mass
public readonly record struct Power(double Watts);
public readonly record struct Energy(double Joules);
public readonly record struct Permeability(double SquareMetres);       // FromMillidarcy
public readonly record struct Viscosity(double PascalSeconds);         // FromCentipoise
public readonly record struct Density(double KgPerCubicMetre);         // ↔ SpecificGravity
public readonly record struct HeatingValue(double JoulesPerKg);

// Runtime dimension tag — needed where a dimension is DATA rather than code:
// content binding "3200 psi" to a quantity (SDD-004 §4), and IPropertyKind
// declaring what it measures (SDD-002 §2b).
public enum Dimension
{
    Dimensionless,
    Length, Area, Mass, Duration, Pressure, Temperature, TemperatureDelta,
    MassRate, Power, Energy, Permeability, Viscosity, Density, HeatingValue,
    ReservoirVolume, SurfaceVolume, StandardGasVolume,
    ReservoirRate, SurfaceRate, StandardGasRate,
    Money,
}

// Rule F-2's home: every constant here carries its citation and its unit, and
// simulation code may use no other numeric literal (SDD-000 §8).
public static class PhysicalConstants
{
    public const double WaterDensityKgPerM3 = 1000.0;          // SDD-001 §1
    public const double GravityMPerS2 = 9.80665;               // SDD-003 §6.2
    public const double GasConstantJPerMolK = 8.31446261815324;// SDD-006 §3, §6
    public const double NormalZ10 = 1.281552;                  // SDD-008 §2
    public const double DefaultChokeCriticalRatio = 0.55;      // SDD-003 §6.3
}
```

> **Contract pass 10.** The dimension set was named in the prose above and
> declared nowhere but `Pressure`. `Dimension` and `PhysicalConstants` likewise —
> and `PhysicalConstants` is the *subject* of rule F-2, so leaving it undeclared
> meant the rule pointed at a type no document defined.

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

// Bg, rm³/sm³ — gas has its OWN bridge (pass-6 amendment below). Mixing the two
// factors is a compile error, which is the entire point of two types.
public readonly record struct GasFormationVolumeFactor(double Rm3PerSm3)
{
    public StandardGasVolume Shrink(ReservoirVolume v) => new(v.CubicMetres / Rm3PerSm3);
    public ReservoirVolume Swell(StandardGasVolume v)  => new(v.CubicMetres * Rm3PerSm3);
}

// One volumetric RATE per volume condition, for the same reason there is one
// volume type per condition: a reservoir rate and a surface rate are not
// interchangeable, and `× Duration → the matching volume` keeps them apart
// through integration as well as through addition.
public readonly record struct ReservoirRate(double CubicMetresPerSecond);    // × Duration → ReservoirVolume
public readonly record struct SurfaceRate(double CubicMetresPerSecond);      // × Duration → SurfaceVolume
public readonly record struct StandardGasRate(double CubicMetresPerSecond);  // × Duration → StandardGasVolume
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

public readonly record struct Polygon
{
    public ImmutableArray<Coordinate> Vertices { get; }

    // VALIDATING constructor — the ring's properties are established once, here,
    // so no consumer re-checks: at least 3 vertices, no zero-length edge, not
    // self-intersecting, counter-clockwise. Simplicity is checked BEFORE
    // orientation, because a bow-tie's lobes cancel to zero signed area and an
    // orientation-first order reports the wrong defect (R1.1).
    public Polygon(ImmutableArray<Coordinate> vertices);

    public Area Area { get; }                                    // shoelace — exact-order summation
    public Coordinate Centroid { get; }                          // AREA centroid, not the vertex mean
    public bool Contains(Coordinate p);                          // ray-cast, half-open rule
    public bool Overlaps(in Polygon other);                      // conservative: touching counts
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
// T is a marker type, e.g. EntityId<IWell>. Comparable, because ordered
// enumeration by id is what rule D-5 asks of every registry.
public readonly record struct EntityId<T>(ulong Value) : IComparable<EntityId<T>>;

// The type-ERASED reference, for events, audit entries and read-model views —
// places that must name an entity without depending on its module. One struct
// with a kind tag rather than an interface, because an interface would box on
// every event and audit entry ever written (S001-3, decided).
public enum EntityKind
{
    Well, Wellbore, Completion, Perforation, Compartment, Reservoir, Field,
    Facility, FacilityUnit, Pipeline, Tank, Berth, Cargo, CustodyPoint,
    Licence, Company, Operation, Rig, Prospect, Play, Basin, Settlement,
    FlowElement, Objective, Barrier, Threat
}

// Ordering is (Kind, Value) and is TOTAL — it is the tiebreaker in the event
// order of design 21 §5.3, so two runs cannot seal a tick differently.
public readonly record struct EntityRef(EntityKind Kind, ulong Value) : IComparable<EntityRef>;

// `where T : class` throughout: an entity is a reference, and the constraint is
// what lets TryResolve return null for absence without boxing a value type
// (pass 10 — the constraints were in the code and not in this block).
public interface IEntityRegistry
{
    EntityId<T> Issue<T>() where T : class;                    // monotonic per T, save-stable
    void Register<T>(EntityId<T> id, T entity) where T : class; // completes the id issued above
    T Resolve<T>(EntityId<T> id) where T : class;               // unresolvable → INV3 fault
    bool TryResolve<T>(EntityId<T> id, out T? entity) where T : class;  // the few places absence is a state
    IReadOnlyList<T> All<T>() where T : class;                  // ordered by id — D-5 safe
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
public readonly record struct Tick(int Value) : IComparable<Tick>   // 0-based, monotonic
{
    public Tick Next { get; }                               // the ONLY way a tick advances
    public static bool operator >(Tick a, Tick b);
    public static bool operator <(Tick a, Tick b);
}
// THE 360-DAY YEAR, pinned: every month is exactly 30 days (the industry's own
// 30/360 day-count convention). This is what makes the /30ths segment grid
// (§9) exact for EVERY tick — real month lengths (28-31) would break grid
// uniformity, day-rate arithmetic and TM11. GameDate labels remain real
// (year, month names, eras); day arithmetic is 30/360; leap years do not exist.
public readonly record struct TickRange(Tick From, Tick To);   // inclusive, for queries

public enum Quarter { Q1 = 1, Q2 = 2, Q3 = 3, Q4 = 4 }
public enum Season { Winter, Spring, Summer, Autumn }
public enum ClimateHemisphere { Northern, Southern }

public readonly record struct GameDate(int Year, int Month) // real labels, 30/360 arithmetic (TM-D5)
{
    public Quarter Quarter { get; }
    public Season SeasonAt(ClimateHemisphere h);

    public GameDate AddMonths(int months);      // floor division: −3 months crosses the year DOWN
    public int MonthsUntil(GameDate other);     // the exact inverse — licence and commitment clocks

    // R1.15 calendar boundaries. Reporting, licence clocks, reserves booking and
    // seasonal access all ask "did this tick cross one?", so it is answered once.
    public bool StartsQuarter { get; }          // months 1, 4, 7, 10
    public bool StartsYear { get; }             // month 1
    public bool StartsSeason { get; }           // months 12, 3, 6, 9 — SAME in both
                                                // hemispheres; only the NAME flips,
                                                // which is why this takes no hemisphere
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

> **R1.13/R1.17 review corrections.** Two, both found by trying to build the
> pipeline the tick contract implies:
>
> ```csharp
> // in OGSim.Kernel, not OGSim.Contracts
> public abstract record TickResult;
> public sealed record TickCompleted : TickResult;
> public sealed record TickAbandoned(Fault Fault) : TickResult;
> public sealed record TickHalted(Fault Fault) : TickResult;
> ```
>
> - **`TickResult` belongs to the kernel.** It was declared in
>   `OGSim.Contracts/EngineSurface.cs`, but this section pins it and the tick
>   pipeline that produces it is a kernel type (R1.17) — and the kernel cannot
>   reference Contracts, because layering runs Contracts → Kernel one way only.
>   Moved; `IEngine.AdvanceTick()` still returns it, since Contracts already
>   depends on the kernel.
> - **`AbandonTick` had nowhere to land.** `FaultResolution` carries three
>   outcomes — `Continue`, `AbandonTick`, `Halt` — and `TickResult` carried two.
>   A model fault abandons the tick whole ([09](../design/09_DIAGNOSTICS.md)
>   §5.1 C4) while the game continues, which is a *different* answer from both
>   "the tick happened" and "the engine has stopped": 09 §5.2 argues the
>   distinction at length. Mapping it onto `Halted` would end a run on a
>   recoverable fault; mapping it onto `Completed` would tell the host a
>   discarded tick had happened. `TickAbandoned` is the third outcome the fault
>   policy already implies.

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

public enum LogLevel { Trace, Debug, Info, Warning, Error, Critical }
public enum ScopeKind { Session, Tick, Stage, Element, Operation }
public readonly record struct LogField(string Name, string Value);

public readonly record struct AuditId(ulong Value) : IComparable<AuditId>;

// Design 09 §4.1's table, as a closed set. Closed because §4.4's retention rule
// partitions on it: ConstraintBinding, InvariantCheck and Merge are the
// per-tick per-element detail that may be pruned; everything else is durable.
public enum AuditCategory
{
    Command, StateTransition, ConstraintBinding, Rejection, Financial,
    StochasticOutcome, BeliefUpdate, Fault, InvariantCheck, ForcedShutIn, Merge
}

// A typed audit value — never a formatted display string (the EM4 rule, applied
// to the trail as well as to events).
public readonly record struct AuditValue(string Value);

public sealed record AuditEntry(
    AuditId Id, Tick Tick, AuditCategory Category,
    EntityRef? Subject, AuditId? Cause,             // Cause: the chain of 21 §7
    IReadOnlyDictionary<string, AuditValue> Data);

public sealed record AuditQuery(
    EntityRef? Subject, AuditCategory? Category,
    TickRange? Range, AuditId? CauseChainLeaf);     // all optional: unset means unfiltered

public interface IAuditTrail
{
    AuditId Record(AuditCategory category, EntityRef? subject, AuditId? cause,
                   IReadOnlyDictionary<string, AuditValue> data);
    IReadOnlyList<AuditEntry> Query(AuditQuery query);     // entity / range / category / cause-walk
    // pass 10: `in` dropped — AuditQuery is a record, so the modifier bought
    // nothing and read as though it were a struct (same as SDD-002 §5's Transform)
}

public enum FaultClass { Content, Composition, Command, Model, Invariant, Host }

public sealed record Fault(
    FaultClass Class,
    string Rule,                       // "INV1", "R2-V10", "SDD-003 §3.1 voidage limit"
    EntityRef? Subject,
    string Detail);

// The CALLER obeys this; the policy only decides (design 09 §5.1).
public enum FaultResolution { Continue, AbandonTick, Halt }

public interface IFaultPolicy
{
    FaultResolution Report(Fault fault);
    // Strict impl: throws on everything. Resilient impl: per 09 §5.1 table.
}

// §11's carriers. Named in that table and declared nowhere until pass 10 — so
// DetMath's domain rule, INV3 and the save-load rule each had a specified
// behaviour and no type to raise.
public abstract class FaultException : Exception { public Fault Fault { get; } }
public sealed class ModelFault     : FaultException { }   // 09 §5.1 C4 — abandon the tick
public sealed class InvariantFault : FaultException { }   // 09 §5.1 C5 — halt
public sealed class SaveDataFault  : FaultException { }   // §11 — content-class on load
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
public readonly record struct EventId(ulong Value);   // per-tick sequence, stamped by the bus

// Design 16 §5. Loop-entry events are at least Warning (rule IR4, enforced at
// publish); Decision means the tick pauses for the player (auto-pause, 15 §5).
public enum Severity { Info, Notice, Warning, Critical, Decision }

public enum EventCategory
{
    Time, Command, Operation, Discovery, Production, Reservoir, Equipment,
    Hse, Environment, Regulatory, Financial, Market, Licence, Technology,
    Objective, Diagnostic
}

// Design 21 §6 — severity is assigned by LOOP POSITION, not by consequence size:
// an entry event is loud because it is still cheap to act on, not because what
// just happened was large.
public enum LoopRole { None, Entry, MidLoop, Consequence }

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

// Domain-typed and host-renderable (R21-V5): LocId is a localisation key, never
// a formatted sentence; Detail is diagnostic and is not shown to the player.
public sealed record RejectionReason(string LocId, string Detail);

public abstract record CommandResult;
public sealed record Accepted(AuditId Audit, IReadOnlyList<EngineEvent> Immediate) : CommandResult;
public sealed record Rejected(IReadOnlyList<RejectionReason> Reasons) : CommandResult;

public interface ICommandBus
{
    CommandResult Submit(Command command);
}

// Phase one — PURE, may not mutate. An empty list means valid (R1 §2.5).
public interface ICommandValidator<in TCommand> where TCommand : Command
{
    IReadOnlyList<RejectionReason> Validate(TCommand command);
}
```

> **R1.9 review corrections.** Three, all forced by walking design 03 §5's
> sequence against the declared types:
>
> ```csharp
> public sealed record Rejected(IReadOnlyList<RejectionReason> Reasons) : CommandResult;
> public sealed record Applied(AuditId Audit, IReadOnlyList<EngineEvent> Raised);
>
> public interface ICommandApplier<in TCommand> where TCommand : Command
> {
>     Applied Apply(TCommand command, AuditId submission);   // cannot fail
> }
> ```
>
> - **`Rejected` carries ALL reasons**, not one. The block above still said
>   `Rejected(RejectionReason Reason)` while the committed record already took a
>   list — the same "report every problem, not the first" rule the module
>   registry and the content loader both follow.
> - **`Apply` returns the events it raised.** `Accepted.Immediate` is a list of
>   events with no source: `Apply` returned only an `AuditId`, and the bus cannot
>   construct a domain event. Returning them puts publication on the bus exactly
>   where 03 §5's sequence draws it (`B->>E: publish`), and keeps the applier
>   free of an `IEventBus` dependency it would otherwise need.
> - **`Apply` receives the submission `AuditId`.** Without it an applier cannot
>   set `Cause` on the events it raises, so *any* `Critical` or `Decision` event
>   raised by a command would be unpublishable under INV12 — the rule the event
>   bus enforces one section up. This is also what chains the applied audit entry
>   to the submission that caused it (03 §5 records both).
>
> Registration (`Register<TCommand>(validator, applier)`) lives on the concrete
> `CommandBus`, not on `ICommandBus`: modules register at composition time, and a
> module handed the interface must be able to submit without being able to
> re-point another module's handler.

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
public readonly record struct Money(long Cents) : IComparable<Money>
{
    public static readonly Money Zero;

    // THE one double→Money rule: half-even, exactly once, at the Movement that
    // enters the ledger (SDD-009 §1). Every crossing goes through here.
    public static Money RoundHalfEven(double cents);
    public static Money FromMillions(double millions);      // = RoundHalfEven(m · 1e8)

    // Checked throughout: overflow is an invariant fault, never a wrap.
    public static Money operator +(Money a, Money b);
    public static Money operator -(Money a, Money b);
    public static Money operator -(Money a);                 // unary
    public static Money operator *(Money a, long factor);    // EXACT — pass 4, finding 74
    public static Money operator *(long factor, Money a);
    public static bool operator >(Money a, Money b);
    public static bool operator <(Money a, Money b);
    public static bool operator >=(Money a, Money b);
    public static bool operator <=(Money a, Money b);
    public int CompareTo(Money other);
}
// NO operator *(Money, double): fractional scaling MUST pass the half-even door
// visibly, and an implicit double multiply is exactly how it would stop doing so.
```

> **Contract pass 10, member-level diff.** This block declared
> `FromMillions(double m) => new(checked((long)(m * 100_000_000)))` — a
> **truncating cast**, which contradicts [SDD-009](SDD-009_ECONOMICS_ENGINE.md)
> §1's rule that every double→Money crossing rounds half-even. Two documents
> specified two different roundings for the same conversion; the committed code
> follows SDD-009 and this block was wrong. The difference is not cosmetic:
> truncation biases every conversion toward zero, and INV2 claims cash
> conservation is *exact*.
>
> `Zero`, `RoundHalfEven`, subtraction, negation, the comparisons and
> `IComparable<Money>` were all in the code and in no SDD.

Cash conservation (INV2) over integers is *exact* — no tolerance term needed,
which is why money is not a `double`.

## 9. Modules, tick pipeline, segments — R1.10, R1.14, R1.17

> **Pass-2 amendment (finding 64):** `TickContext.Segments` is `SegmentPlan?` —
> null before stage 4, set exactly once by Availability. The original `required`
> shape forced the plan to exist at stage 0, before anything could have built it.


```csharp
public readonly record struct ModuleName(string Value);

// Comparable so registries order by it: capture and restore must visit owners in
// a fixed sequence or two runs produce different save bytes (R1.11).
public readonly record struct StateKey(string Value) : IComparable<StateKey>;

// Order is why this is not just a StageId: two modules in one stage need a FIXED
// relative order or the tick is non-deterministic, and 03 §6 requires that order
// be declared rather than emergent. Composition check 5 forbids a duplicate
// (Stage, Order).
public sealed record StageParticipation(StageId Stage, int Order);

public sealed record ModuleManifest(
    ModuleName Name,
    IReadOnlyList<Type> Provides, IReadOnlyList<Type> Requires,   // contract interfaces
    IReadOnlyList<StateKey> OwnsState,
    IReadOnlyList<StageParticipation> Stages,
    IReadOnlyList<Type> Commands);

// Resolution happens AFTER validation of the whole module set, so Require can
// only ever see a contract that was proven present.
public interface IModuleComposition
{
    void Provide<T>(T implementation) where T : class;
    T Require<T>() where T : class;
}

// NOT the composition validator — that is ModuleComposer (§12b). This is the
// CONTENT plugin binder of SDD-004 §5 stage 6, which resolves a plugin named in
// a content entry. The two shared a name until pass 10 (glossary rule N1).
public interface IModuleRegistry
{
    bool CanBind(ContentId plugin, Type contract);
    T Bind<T>(ContentId plugin) where T : class;
}

public interface IModule
{
    ModuleManifest Manifest { get; }
    void Compose(IModuleComposition c);
}

// Per-tick execution context. Segments is null before stage 4 builds it and is
// set exactly once by Availability; reading it earlier is a stage-isolation
// violation (I-V5).
public sealed class TickContext
{
    public required Tick Tick { get; init; }
    public required GameDate Date { get; init; }
    public SegmentPlan? Segments { get; set; }
}

public enum StageId  // exactly 03 §6 — the FOURTEEN stages, numbered as documented
{ Open=0, Commands=1, Environment=2, Operations=3, Availability=4, SolveFlow=5,
  MaterialBalance=6, Custody=7, Economics=8, HseRegulation=9, Information=10,
  Company=11, Objectives=12, Close=13 }

public interface ITickStage { StageId Id { get; } void Execute(TickContext ctx); }

public sealed record Segment(int StartDay, int DurationDays,
                             IReadOnlyCollection<EntityRef> Available);
public sealed record SegmentPlan(IReadOnlyList<Segment> Segments);    // built at stage 4

// One entity's availability changing partway through a tick — the planner's
// input. LastCommittedThroughput is the merge-ranking estimator pinned below:
// last-committed rather than current, because true impact would need the solve
// this plan precedes.
public sealed record AvailabilityChange(
    int Day, EntityRef Subject, bool Available, double LastCommittedThroughput);
// invariant INV9: DurationDays sum to exactly 30 — INTEGER arithmetic on the
// /30ths grid, never float positions summing to 1.0.
```

> **R1.0 review correction:** this block previously declared
> `Segment(double Start, double Duration, AvailabilitySet Available)` and stated
> INV9 as "durations sum to 1.0 exactly" — contradicting its own next paragraph,
> which requires whole days so that INV9 *is* integer arithmetic. `AvailabilitySet`
> was also a type no SDD ever declared. The shape above is the committed one.

> **R1.10 review corrections.** Two naming/shape problems and the types the
> five checks need:
>
> ```csharp
> public sealed record StageParticipation(StageId Stage, int Order);
>
> public enum CompositionProblemKind
> { UnmetRequirement, DuplicateProvider, DuplicateStateKey, DependencyCycle, StageConflict }
>
> public sealed record CompositionProblem(CompositionProblemKind Kind, ModuleName Module, string Detail);
> public abstract record CompositionResult;
> public sealed record Composed(IReadOnlyList<IModule> OrderedModules) : CompositionResult;
> public sealed record CompositionRefused(IReadOnlyList<CompositionProblem> Problems) : CompositionResult;
> ```
>
> - **`IModuleRegistry` names two different things.** Design 03 §3.1's diagram
>   uses it for the *composition validator*; §9 here and the committed code use
>   it for *content plugin binding* (SDD-004 §5). Glossary rule N1 is one concept
>   one name, so the plugin binder keeps `IModuleRegistry` — it is the one with
>   code — and the validator is `ModuleComposer`, a concrete kernel type. It is
>   concrete deliberately: composition is where concrete types are named (03 §2
>   layer 4), so there is nothing for it to sit behind.
> - **Check 5 had nothing to check.** R1 §2.9's fifth validation is "tick-stage
>   participation has no ordering conflict", but `StageParticipation` carried
>   only a `StageId` — there was no ordering to conflict, so the check was
>   vacuous. It cannot simply be dropped: two modules acting in the same stage
>   need a *fixed* relative order or the tick is non-deterministic, and 03 §6
>   insists the order be declared rather than emergent. `StageParticipation`
>   therefore gains `int Order`, and check 5 becomes: no two modules may claim
>   the same `(Stage, Order)`. Ordering by module name instead would have made
>   execution order a consequence of spelling.
> - **Composition reports every problem, not the first** (R1 §2.9), which is why
>   the refusal carries a list. A developer fixing composition one error per run
>   is the failure mode that rule exists to prevent.

Segment boundaries live on a **/30ths-of-a-tick grid** (whole days) rather than
raw doubles — INV9's "sums to exactly one tick" becomes integer arithmetic, and
the 4-segment merge (TM-D2) picks grid points deterministically. The merge's
impact ranking (21 §5.2) uses a pinned estimator — **the affected element's
last-committed throughput × the boundary's remaining duration** — because true
impact needs the solve the plan precedes; last-committed is deterministic,
cheap, and wrong only when it does not matter (a boundary on an idle element).

## 10. State — R1.11

```csharp
// Ordered key/value in the canonical form of SDD-013 §3. Three writers and no
// generic object: the canonical byte rules are per-type (doubles as shortest
// round-trip), and a generic Write(object) would put format decisions at every
// call site instead of here.
public interface IStateWriter
{
    void WriteString(string key, string value);
    void WriteInt64(string key, long value);
    void WriteDouble(string key, double value);
}

// No TryRead and no defaults: a missing or unreadable value is a SaveDataFault,
// because a save that has quietly lost a field is not a save that should load
// (design 11 §2.1).
public interface IStateReader
{
    string ReadString(string key);
    long ReadInt64(string key);
    double ReadDouble(string key);
}

public interface IStateOwner
{
    StateKey Key { get; }
    int SchemaVersion { get; }       // starts at 1, so an unset field cannot pass for valid
    void Capture(IStateWriter w);
    void Restore(IStateReader r);
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

## 12b. The shipped implementations — R1's concrete types

> **Contract pass 9 (finding 82).** F-1 requires every public member of an engine
> assembly to be specified in a merged SDD, and it does not exempt implementations.
> R1 built thirteen concrete types against the contracts above and this SDD named
> none of them, so the rule was satisfied for the interfaces and quietly broken
> for the classes behind them. Named here.

| Contract | Implementation | Note |
|---|---|---|
| — | `DetMath` | §1.3. The D-2 exception, and the only legal `System.Math` site |
| — | `Faults` (`FaultException`, `ModelFault`, `InvariantFault`, `SaveDataFault`) | §11's carriers, which §11 named and never declared |
| `IEntityRegistry` | `EntityRegistry` | §2. Ids from 1; `Register` write-once |
| `ISimulationClock` | `SimulationClock` | §3. `Advance()`/`RestoreTo()` are on the CLASS, not the interface |
| `IRandomSource` / `IRandomStream` | `RandomSource` (+ `PcgStream`) | §4. PCG64 XSL-RR; streams seeded by name |
| `ILog` | `Log` | §5. Takes `ILogSink` + a minimum level |
| `IAuditTrail` | `AuditTrail` | §5. `Prune()` is on the class; retention is a cause-graph closure |
| `IFaultPolicy` | `FaultPolicy` → `StrictFaultPolicy`, `ResilientFaultPolicy` | §5, design 09 §5.3. Both are complete configurations |
| `IEventBus` | `EventBus` | §6. `Seal()` is on the class — EM2 |
| `ICommandBus` | `CommandBus` | §7. `Register<T>()` is on the class |
| — | `ModuleComposer` (+ `CompositionResult`, `CompositionProblem`) | §9. NOT `IModuleRegistry` — that name is the content plugin binder |
| — | `StateRegistry` | §10. Registration only; the format is SDD-013 |
| — | `TickPipeline` (+ `TickResult`) | §3/§9. The 14 stages of design 03 §6 |
| — | `SegmentPlanner` (+ `AvailabilityChange`) | §9. /30ths grid, 4-segment budget, audited merges |

**The recurring shape is worth stating once**, because it appears five times
above and is not an accident: where a contract must stay read-only for its
consumers, the mutating member lives on the concrete class and never on the
interface — `Advance`, `Seal`, `Prune`, `Register`, `RestoreTo`. A module handed
`ISimulationClock` cannot move time; a module handed `IEventBus` cannot make a
tick observable. Capability follows from what you were handed rather than from
remembering not to call something, which is law L2 read forwards.

**Those beyond-interface members are the part F-1 actually needs pinned**, since
the interface members are already specified above. In full:

```csharp
public static class DetMath                       // §1.3
{
    public static double Exp(double x);           // overflow → ModelFault; underflow → 0
    public static double Ln(double x);            // x <= 0 → ModelFault, never NaN
    public static double Pow(double x, double y); // exact repeated squaring for integral |y| <= 64
    public static double Sqrt(double x);          // Math.Sqrt — IEEE-correct, portable
}

public sealed class EntityRegistry : IEntityRegistry
{
    public ulong HighWaterMark<T>() where T : class;              // save-stable id continuation
    public void RestoreHighWaterMark<T>(ulong mark) where T : class;
}

public sealed class SimulationClock : ISimulationClock
{
    public SimulationClock(GameDate epoch);       // the date at tick 0
    public void Advance();                        // stage 0, once per tick, pipeline only
    public void RestoreTo(Tick tick);             // load only — refused after ticking begins
}

public sealed class RandomSource : IRandomSource { public RandomSource(ulong worldSeed); }

public sealed class Log : ILog { public Log(ILogSink sink, LogLevel minimumLevel); }

public sealed class AuditTrail : IAuditTrail
{
    public AuditTrail(ISimulationClock clock, AuditRetention retention);
    public int Count { get; }
    public void Prune();                          // tick close; the §5 cause-graph closure
}

public abstract class FaultPolicy : IFaultPolicy      // records to log + trail, then decides
{
    protected FaultPolicy(ILog log, IAuditTrail audit);
    protected abstract FaultResolution Decide(Fault fault);
}
public sealed class StrictFaultPolicy    : FaultPolicy { }   // throws on everything
public sealed class ResilientFaultPolicy : FaultPolicy { }   // 09 §5.3 table

public sealed class EventBus : IEventBus
{
    public EventBus(ISimulationClock clock);
    public void Seal();                           // stage 13 — EM2: nothing observable before
}

public sealed class CommandBus : ICommandBus
{
    public CommandBus(IAuditTrail audit, IEventBus events);
    public void Register<TCommand>(ICommandValidator<TCommand> validator,
                                   ICommandApplier<TCommand> applier) where TCommand : Command;
}

public sealed class ModuleComposer                // NOT IModuleRegistry — see §9
{
    public CompositionResult Compose(IReadOnlyList<IModule> modules);
}
public enum CompositionProblemKind
{ UnmetRequirement, DuplicateProvider, DuplicateStateKey, DependencyCycle, StageConflict }
public sealed record CompositionProblem(CompositionProblemKind Kind, ModuleName Module, string Detail);
public abstract record CompositionResult;
public sealed record Composed(IReadOnlyList<IModule> OrderedModules) : CompositionResult;
public sealed record CompositionRefused(IReadOnlyList<CompositionProblem> Problems) : CompositionResult;

public sealed class StateRegistry                 // §10, registration only
{
    public void Register(IStateOwner owner);      // write-once per key (L5)
    public IReadOnlyList<IStateOwner> Owners { get; }   // KEY order, not registration order
    public bool TryGet(StateKey key, out IStateOwner? owner);
    public IStateOwner Resolve(StateKey key);     // unowned key → InvariantFault
    public int Count { get; }
}

public sealed class SegmentPlanner                // §9
{
    public const int MaxSegments = 4;             // TM-D2
    public SegmentPlanner(IAuditTrail audit);     // takes the trail because EVERY merge is audited
    public SegmentPlan Plan(IReadOnlyCollection<EntityRef> availableAtStart,
                            IReadOnlyList<AvailabilityChange> changes);
}

public sealed class TickPipeline                  // §3, design 03 §6
{
    public TickPipeline(SimulationClock clock, EventBus events, IAuditTrail audit,
                        IFaultPolicy faults, ILog log, IReadOnlyList<ITickStage> stages);
    public Tick CurrentTick { get; }
    public TickResult AdvanceTick();
    public IReadOnlyList<StageId> DeclaredOrder();
}
```

`TickPipeline` taking `SimulationClock` and `EventBus` **concretely** is the one
deliberate exception to L1 in the kernel, and it is the reason the exception
exists: the pipeline is the only thing permitted to call `Advance()` and
`Seal()`, so it must hold the types that have them. Any other module gets the
interfaces and therefore cannot.

## 13. Open items

| # | Item | Trigger |
|---|---|---|
| S001-1 | `LogFields`: struct of spans vs pooled dictionary — decide on first profiler data | R4 benchmarks |
| S001-2 | Audit trail storage: append-only file + in-memory index vs pure in-memory until save | R1.6 implementation |
| S001-3 | ~~EntityRef shape~~ **Decided:** `readonly record struct EntityRef(EntityKind Kind, ulong Value)` — one struct with a kind tag; an interface would box in every event and audit entry | closed |
| S001-4 | Whether `TickContext` exposes stage-scoped read isolation by interface-per-stage (strong, verbose) or by runtime assert (I-V5) | SDD-002 |
| S001-5 | ~~Spatial primitives~~ **Resolved** — specified in §1.4 | closed |
