# Phase R19 — Persistence and Determinism

**Arc IV · Hardening** · Status ⬜ · Depends on: all of Arcs I–III · Enables: R20, R21

---

## 0. Purpose

Every module has been registering its state since R1.11. R19 makes save and load
real, and proves the property that matters most: **a reloaded game continues
identically.**

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Round-trip identity | save → load → save is byte-identical (PV1) |
| G2 | **Continuation identity** | Save at tick N, load, run to N+100 — identical to running straight through (PV2) |
| G3 | Cross-platform determinism | Windows and Linux produce identical digests (PV3) |
| G4 | Complete coverage | Every registered module persists; unpersisted state fails a test (PV4) |
| G5 | Migration works | Every historical version migrates forward and passes PV1 (PV5) |
| G6 | Bad saves are refused | Truncated, tampered or mod-mismatched saves fail with a specific explanation (PV6) |

---

## 2. Design decisions

### 2.1 Continuation identity is the real test

Round-trip equality proves the bytes match. **Only continuation equality proves
the behaviour matches** — and behaviour is what a player experiences.

It catches the whole class of "restored as a value but not as a live dependency":
a cached derived value that was saved but whose invalidation hook was not
restored, an RNG stream position restored to the wrong stream, a scheduled
operation restored without its resource reservation.

*Rationale:* this is the test that is usually missing, and its absence is why
"the save loaded but the game plays differently" is such a common bug.

### 2.2 Restore order comes from declared dependencies

Modules declare restore prerequisites; the registry topologically sorts. **A cycle
is a composition error at startup**, not a mysterious load failure.

### 2.3 A failed reference resolution during restore is a fault

Never a silent drop. "Re-link by id, and drop yourself if the target is missing"
is precisely how a save quietly loses content. The load either fully succeeds or
fails with an explanation.

### 2.4 The audit trail is saved

Summarised per [09](../design/09_DIAGNOSTICS.md) §4.4, so that after a reload the
player can still ask why their field is throttled and get an answer that predates
the save.

### 2.5 Truth is persisted, not regenerated

Per open decision PSD2. The world **changes** during play — reservoirs deplete, so
their state is no longer what generation produced. The seed is stored too, and
PV7 asserts that regeneration from it reproduces the *original* world.

### 2.6 Saves are inspectable

Per open decision PSD1: JSON inside a compressed container. Unpacked, a human can
read it and a tool can diff two saves. **Diffing two saves is the single most
effective debugging technique available for a simulation of this kind**, and it
is worth designing for.

### 2.7 What must be in the save that was not in the first draft

| Block | Contents |
|---|---|
| `environment` | Profiles in use, current weather state, forecast, **`weather` stream position** |
| `hse` | Barrier states, incident history, emissions ledger, ESG standing, social licence |
| `objectives` | Progress, scores, campaign chapter position |

**Eight RNG streams**, not five: `worldgen`, `exploration`, `measurement`,
`hazard`, `weather`, `price`, `market`, `operations`. Each position saves
separately, which is what keeps world seeds stable when a later version adds a
draw in one subsystem.

### 2.8 Continuation identity has a new failure mode

Segment plans are rebuilt each tick and are **not** saved — they derive from
availability, which is. PV2 must therefore verify that a save taken mid-tick
reproduces an identical plan on reload, or the derivation is not deterministic.

---

## 3. Deliverables

`OGSim.Kernel` extension: save format and header, module block serialisation,
restore ordering, migration chain and infrastructure, audit persistence and
summarisation, digest computation.
Test fixtures: a save file per historical schema version.
CI: cross-platform determinism job.

---

## 4. Verification

The PV1–PV8 suite from [11](../design/11_PERSISTENCE.md) §4, plus:

| # | Test | Passes when |
|---|---|---|
| R19-V9 | Continuation at ten points | PV2 holds when saving at ten different lifecycle stages (SC10) |
| R19-V10 | Module coverage | A module with unpersisted state fails the test — verified by deliberately breaking one |
| R19-V11 | Restore-order cycle | A declared cycle is caught at startup with the cycle named |
| R19-V12 | Dangling reference | A save referencing a missing entity fails with an explanation, never partially loads |
| R19-V13 | Audit survives reload | A "why is this shut in?" query spanning the save boundary is answerable |
| R19-V14 | Mod mismatch | Loading without the recorded mods fails with the missing mods named |
| R19-V15 | Save diff | Two saves differing by one command produce a small, readable diff |

**R19-V10 must be verified by deliberately breaking a module** and confirming the
test fails. A coverage test that has never failed is not known to work.

---

## 5. Out of scope

Slot management, file I/O location, cloud sync — all host concerns (R21). Save
compression tuning.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Continuation identity fails and the cause is hard to find | Save diffing (R19-V15) plus per-module digests localise the divergence to a module |
| Floating-point differences across platforms | One canonical evaluation order, no parallelism (AD5), and CI catches divergence per commit rather than at release |
| Migration chain grows unwieldy | Each step is small and independently tested against a real fixture from that version |
| Save size grows large in a long game | Audit summarisation is the main lever; measure at SC1 scale and tune retention |
