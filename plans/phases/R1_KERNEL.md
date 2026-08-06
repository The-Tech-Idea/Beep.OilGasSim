# Phase R1 — Kernel

**Arc I · Foundation** · Status ⬜ · Depends on: R0 approval · Enables: everything

---

## 0. Purpose

Build the layer that everything else stands on, and **build the architectural
laws into it as executable tests before there is any code to be tempted by.**

R1 has no domain knowledge whatsoever. It does not know what a well is. It knows
about units, identity, time, randomness, logging, auditing, faults, commands and
composition. If any domain vocabulary appears in this phase, the phase is wrong.

**The sequencing argument:** the five laws in
[03_ARCHITECTURE](../design/03_ARCHITECTURE.md) §1 are only free if they are
present from the first commit. Retrofitting "no singletons" onto a codebase that
has them is a multi-month arc; establishing it before the first domain type costs
nothing. R1.12 — the architecture test suite — is therefore not the last task of
this phase in importance, only in order.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | A physical value cannot be misused | Adding a pressure to a volume does not compile; unit conversion round-trips exactly (test MX7) |
| G2 | Identity is stable and total | Every id resolves or raises a fault; no reference-equality keying anywhere |
| G3 | There is exactly one clock and one source of randomness | Architecture test finds no `DateTime.Now`, `Random.Shared`, `Guid.NewGuid` in simulation code |
| G4 | Randomness is stream-independent | Adding a draw to one subsystem provably does not change another's sequence |
| G5 | Nothing fails silently | Every `catch` outside the fault policy calls it; architecture test enforces |
| G6 | The audit trail answers "why?" | A query by entity and tick range returns every decision affecting it |
| G7 | Composition is total or refuses | A module with an unmet requirement produces a startup failure naming it; there is no partial engine |
| G8 | The laws are mechanised | All 23 architecture tests exist and pass |
| G9 | The engine is turn-based and pacing-agnostic | `AdvanceTick()` is the only way time moves; no simulation assembly references a wall-clock API |
| G10 | Sub-tick resolution works | A mid-tick availability change produces the exact duration-weighted result, by segmentation not averaging |

---

## 2. Design decisions specific to this phase

### 2.1 Quantities: struct or class?

**Decision: a readonly value type carrying a magnitude and a unit reference.**
Quantities are created in enormous numbers inside the solver; heap-allocating
each would dominate the tick. The dimension check is a comparison of unit
metadata, resolved at construction.

**Compile-time vs runtime dimension checking:** full compile-time dimensional
analysis in C# requires either generic type parameters per dimension (unwieldy,
poor error messages) or source generation. **Decision: distinct quantity types
for the ~15 dimensions actually used** (`Pressure`, `Volume`, `Temperature`,
`Rate`, …), each a thin value type over magnitude + unit. Cross-dimension
arithmetic then genuinely does not compile, and the operators that *do* make
sense (`Volume / Time → VolumetricRate`) are declared explicitly.

This is more code than a single `Quantity` type, and it is worth it: it is the
difference between "the unit bug is tested for" and "the unit bug is
inexpressible".

### 2.2 Volume conditions

`Volume` is not sufficient. A reservoir barrel and a stock-tank barrel are the
same dimension and must not be interchangeable
([10_CONTENT_AND_UNITS](../design/10_CONTENT_AND_UNITS.md) §1.3).

**Decision:** volume quantities carry a **reference condition** (reservoir /
standard / normal). Adding volumes at different conditions is a fault. Converting
between them requires an explicit formation-volume-factor argument. This closes
the single most likely double-count in the engine, structurally.

### 2.3 RNG streams

**Decision: named, independently-seeded streams**, derived from the world seed by
a stable hash of the stream name. Streams: `worldgen`, `exploration`,
`measurement`, `hazard`, `price`, `market`, `operations`.

Rationale in [11_PERSISTENCE](../design/11_PERSISTENCE.md) §3.1 — this is what
keeps world seeds stable across engine versions.

Each stream's position is part of the saved state. Drawing from a stream is
recorded in the audit trail when the draw is *consequential* (a discovery, a
failure, a price shock) — see [09_DIAGNOSTICS](../design/09_DIAGNOSTICS.md) §4.2.

### 2.4 Audit trail storage

**Decision: an append-only ordered structure with secondary indices** by entity
id, by tick, and by category. The trail is written during a tick and sealed at
tick close; entries are immutable once written.

The bounding policy ([09](../design/09_DIAGNOSTICS.md) §4.4) is implemented here,
not deferred: retention rules are cheap to write now and expensive to retrofit
once forty years of entries exist.

### 2.5 Command validation is separate from application

**Decision: two distinct steps with a hard rule — validation may not mutate, and
application may not fail.** A command that reaches application has already been
proven applicable. This makes half-applied commands structurally impossible,
which is what the "no partial state" requirement demands.

### 2.6 The tick is the only way time moves

Per [15_TIME_AND_EXECUTION](../design/15_TIME_AND_EXECUTION.md): **the engine is
turn-based; the game is real-time-with-pause.** The kernel exposes
`AdvanceTick()` and nothing else. Pacing — speed settings, auto-pause, advance-
until-condition — is entirely a host concern the engine never sees.

**Consequence:** a headless CI run, a scenario test and a player at 8× speed all
drive the identical code path, so determinism, replay and testability are
preserved by construction rather than by care.

### 2.7 Sub-tick segmentation, not availability averaging

Events carry a fraction-of-tick position and duration. Where availability changes
within a tick, the tick is **segmented** and solved per segment, with results
duration-weighted.

**Averaging is rejected**, and this is a correctness decision, not a fidelity one:
the network solve is non-linear, so a compressor available 60% of the month is
not the same as a compressor at 60% capacity. Segmenting is exact; averaging is
wrong.

**Segment budget: 4 per tick** (open decision TM-D2). Events beyond the budget
merge to the nearest boundary, **and the merge is audited** so the approximation
is never invisible.

### 2.8 Event taxonomy is established here

The categories, severity levels and the "notifications never carry control flow"
rule from [16_EVENT_MATRIX](../design/16_EVENT_MATRIX.md) are kernel-level.
Individual events are declared by the modules that raise them; the **shape**,
the **publication point** (tick close) and the **no-subscriber rule** are fixed
in R1.

*Rationale:* the architecture test that forbids engine subsystems from
subscribing must exist before there are subsystems to violate it.

### 2.9 Module registry validation

All five checks happen before any module is constructed:

1. every `Requires` is `Provides`d by exactly one module
2. no contract is provided twice
3. no state key is owned twice
4. the dependency graph is acyclic
5. tick-stage participation has no ordering conflict

**Failure reports every problem, not the first.** A composition failure is a
development-time event and the developer should get the whole list.

---

## 3. Deliverables

| Project | Contents |
|---|---|
| `OGSim.Kernel` | Everything in R1.1–R1.11. **Zero external dependencies. Zero domain vocabulary.** |
| `OGSim.Architecture.Tests` | The 23 checks in [12](../design/12_VERIFICATION.md) §2, running against every assembly |
| `OGSim.Kernel.Tests` | Unit coverage for each service |

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R1-V1 | Unit conversion round-trip | Every unit → canonical → unit returns the original within tolerance (MX7) |
| R1-V2 | Dimension safety | A curated set of invalid expressions fails to compile (compile-failure test) |
| R1-V3 | Volume condition safety | Adding rb to stb raises a fault; converting requires an explicit FVF |
| R1-V4 | Nonlinear scale | API gravity ↔ density round-trips; averaging two API values is not offered |
| R1-V5 | Stream independence | 10,000 draws from stream A are byte-identical whether or not stream B was drawn from |
| R1-V6 | Determinism | The same seed produces identical sequences across processes and platforms |
| R1-V7 | Identity | An unregistered id raises a resolution fault, never returns null |
| R1-V8 | Audit query | A synthetic 1,000-entry trail answers by-entity, by-tick and by-category queries correctly |
| R1-V9 | Audit bounding | Trail growth stays bounded over 500 simulated ticks, and no state-transition entry is discarded |
| R1-V10 | Fault classification | Each of the six fault classes routes to its designed outcome |
| R1-V11 | Composition — success | A valid module set composes |
| R1-V12 | Composition — every failure mode | Each of the five validation failures is detected and **reported completely** |
| R1-V13 | Command atomicity | A command failing validation leaves state and audit byte-identical apart from the rejection entry |
| R1-V14 | All 23 architecture tests | Pass against the kernel itself |
| R1-V15 | No wall-clock (TM9) | Architecture test: no simulation assembly references a wall-clock API |
| R1-V16 | Sub-tick segmentation (TM2) | A mid-tick availability change produces the exact duration-weighted result, verified by hand calculation |
| R1-V17 | Segmentation ≠ averaging (TM3) | A case is demonstrated where averaging gives a materially different, wrong answer |
| R1-V18 | Segment budget (TM4) | Exceeding it merges to boundaries **and audits the merge** |
| R1-V19 | Calendar (TM11) | Quarter and year boundaries fire on the correct ticks across leap years |
| R1-V20 | No engine subscribers (EM1) | Architecture test: no engine assembly subscribes to `IEventBus` |
| R1-V21 | Publication at tick close (EM2) | No event is observable mid-tick |
| R1-V22 | Typed payloads (EM4) | No event payload contains a pre-formatted display string |

---

## 5. Out of scope

Anything domain. No materials, no properties, no streams, no flow, no wells, no
money. The kernel must be able to compile and pass its tests with no knowledge
that this is an oil and gas simulation at all — **that is the test of whether the
layering is real.**

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Per-dimension quantity types are verbose to write | Accept it — it is written once and it makes a whole bug class inexpressible. Consider source generation if the count grows past ~20 |
| Quantity allocation cost in the solver's inner loop | Value types; benchmark in R4 with synthetic elements before any domain exists |
| The audit trail becomes a performance problem | Bound it in R1.6 rather than later; benchmark write cost under a synthetic 1,000-entry tick |
| Architecture tests become brittle | They assert on compiled metadata, not source text — the failure mode of the previous generation's guard tests is specifically avoided |
| The kernel accretes domain concepts | R1-V-arch: an architecture test asserting `OGSim.Kernel` references no domain assembly, and a review rule on new kernel types |
