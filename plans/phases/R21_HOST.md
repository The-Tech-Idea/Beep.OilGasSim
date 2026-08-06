# Phase R21 — Host Contract

**Arc IV · Hardening** · Status ⬜ · Depends on: R20 · Enables: any client

---

## 0. Purpose

Define the boundary between the engine and whatever draws it — and prove the
boundary is sufficient by building a client against it that uses nothing else.

**The engine has had no host dependency since R1.** R21 does not add one; it
formalises the surface a host may use.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | The host cannot corrupt the simulation | The read model is immutable; the host holds no mutable reference into engine state |
| G2 | The contract is sufficient | A reference headless client plays a full game using only the published surface |
| G3 | The engine stays presentation-free | Architecture test: no engine type references any presentation concept |
| G4 | "Why?" is answerable from the host | The audit query surface supports every explanation feature in [09](../design/09_DIAGNOSTICS.md) §7 |
| G5 | Uncertainty is projected | Beliefs reach the host with their distributions, so the map can render fuzziness |

---

## 2. Design decisions

### 2.1 An immutable read model published at tick close

Per open decision A2: a full snapshot per tick, not incremental diffs. Simpler,
provably correct, and the per-tick cost at monthly steps is negligible. Diffs are
an optimisation to add only if profiling demands.

### 2.2 Commands in, read model out — nothing else

The entire surface is: submit a command, receive an accept/reject with a reason,
read the current read model, subscribe to events, query the audit trail.

**There is no accessor into live state.** This is what makes G1 structural rather
than conventional.

### 2.3 Beliefs, never truth

The read model is projected from beliefs. An architecture test asserts it does not
reference any truth type. **This is the last place the exploration game could
leak**, and it is closed here as it was closed in R14 and R5.

### 2.4 The audit query surface is a first-class API

Not a debug endpoint. It backs the production loss report, the field history
timeline, the "where did my money go?" view and the fairness check. The host does
not need to record anything itself.

### 2.4b What the read model must contain

The player-facing features in [09_DIAGNOSTICS](../design/09_DIAGNOSTICS.md)
section 7 and the indicator rules IR1–IR2 together fix a minimum surface. If any
row here is missing, a designed feature is unbuildable:

| Projection | Serves |
|---|---|
| Production actual, potential and **deferred by binding element** | The bottleneck report — the game's core operations loop |
| Reservoir pressure, water cut, GOR trends per compartment | Depletion and water-spiral detection |
| Well operating point, IPR/VLP curves, status and cause | "Why is this well shut in?" |
| Facility unit capacities, utilisation and spec margins | Debottlenecking decisions |
| Tank levels and ullage, berth schedule, cargo nominations | The export rhythm |
| **Barrier status, overdue tests, deferred backlog** | The HSE leading indicators (HS3) |
| Personal and process safety indicators, **separately** | The two-metric design intent |
| **ESG standing and its current cost-of-capital effect** | The slowest loop's standing indicator (IR2) |
| **RRR and reserves by class** | The liquidation spiral's standing indicator (IR2) |
| Weather state, forecast with horizon confidence, **days lost this tick by cause** | Scheduling and the downtime account |
| Access windows with time remaining | `env.accessWindowClosing` responses |
| **The world surface (terrain, settlements, transport) and every spatial anchor (well/facility sites, licence and believed-prospect polygons)** | The map itself — pass-8 addition (finding 81): every other row assumed a map nothing could draw |
| Beliefs as distributions with provenance and as-of | Map uncertainty rendering |
| Value-of-information for pending purchases | The exploration decision |
| Operation progress, expected completion, standby state | "What is my company doing?" |
| Cost and revenue by cause for the period | "Where did my money go?" |
| Objective progress and the eight score dimensions | Every non-sandbox mode |

**Rule:** every `C` or `D` severity event must have a projection the player can
act on (EM9). A read model that cannot answer an alert is an incomplete surface.

### 2.5 The reference client is headless and is a real test

It plays a full game — bids, surveys, drills, develops, produces, sells,
abandons — using only the published surface. **If it needs anything the surface
does not offer, the surface is incomplete**, and that is discovered here rather
than by a UI team six months later.

It also doubles as the automation harness for SC1.

---

## 3. Deliverables

`OGSim.Composition`: `IEngine` — the public entry point; `EngineBuilder`; the
read model contracts; command submission; event subscription; the audit query
surface; belief projection with uncertainty.
`OGSim.ReferenceClient`: a headless client playing a full game through the
surface alone.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R21-V1 | Immutability | The read model cannot be mutated; no live reference escapes |
| R21-V2 | Sufficiency | The reference client completes a full game using only the published surface |
| R21-V3 | No presentation vocabulary | Architecture test passes across every engine assembly |
| R21-V4 | No truth in the read model | Architecture test passes |
| R21-V5 | Command rejection | Every rejection carries a domain reason usable directly as player-facing text |
| R21-V6 | Audit queries | All seven features in [09](../design/09_DIAGNOSTICS.md) §7 are answerable |
| R21-V7 | Belief projection | Distributions reach the host, including P10/P50/P90 and provenance |
| R21-V8 | Snapshot cost | Read-model construction stays within the tick budget at SC1 scale |
| R21-V9 | Event completeness | Every state change the host must react to publishes an event |
| R21-V10 | Determinism through the surface | The reference client, given the same seed and script, produces an identical digest |
| R21-V11 | Read-model completeness | Every row of section 2.4b is present and populated |
| R21-V12 | Alert actionability (EM9) | Every `C`/`D` event has a read-model projection the player can act on |
| R21-V13 | Standing indicators (IR2) | ESG standing and RRR are present in every snapshot, not only on request |

---

## 5. Out of scope

Any actual UI. Rendering, input, audio, file dialogs, scene management — all host
concerns. The engine ships with a headless reference client and nothing more.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| The read model is insufficient for a real UI | The reference client is a floor, not a ceiling; the surface is versioned and extensible, and gaps found by a real UI are additive |
| Full snapshots are too expensive | R21-V8 measures at SC1 scale; diffs are the known fallback if it fails, and A2 records that decision |
| The audit query surface is too slow for interactive use | Indices exist from R1.6; measure and add indices as needed |
| A host eventually wants a mutable handle "just for performance" | Refuse. G1 is structural, and the whole architecture depends on it |
