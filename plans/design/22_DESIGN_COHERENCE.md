# 22 — Design Coherence

**Status:** draft · **Date:** 2026-08-06

> **Affects:** every document — it is the map of the set itself · **Affected by:** every document
> *(strong couplings only — the full row is in [22](22_DESIGN_COHERENCE.md) §2; this document is the map itself)*

[17_CROSS_IMPACT_MATRIX](17_CROSS_IMPACT_MATRIX.md) says how the *subsystems*
affect each other. [21_INTEGRATION](21_INTEGRATION.md) says how *time, events and
coupling* affect each other. **This document says how the *documents* affect each
other** — which is the level at which a 50-document design set decays.

Its three jobs: the change-impact map (§2), the consolidated rule registry (§3),
and the identifier scheme (§4). §5 logs what the coherence pass found and fixed.

---

## 1. Why this exists

A design set this size fails in a specific, predictable way. A decision changes in
one document; three other documents still describe the old behaviour; nobody
notices until code is written against the stale one.

**That has already happened three times here**, and every time it was found by
audit rather than by reading:

| Drift | How it happened | Found by |
|---|---|---|
| The tick pipeline had no environment, HSE, objectives or segmentation stage | Documents 13–20 were written after 03, and nothing pulled 03 forward | Pass-two integration review |
| `I1`/`I2` meant *invariants* in 09 and *integration rules* in 21 — both cross-cited | Two documents chose the same prefix independently | Pass-three identifier audit |
| `IEmissionsLedger` was owned by *Company* in 01 and by `OGSim.Hse` in 03 — **a direct L5 violation** | 14 was written after both, and neither was reconciled to it | Pass-four ownership check |

None was visible to a careful reader of any single document. All three were
obvious the moment the *relationships between* documents were checked — which is
the entire argument for this document existing.

### 1.1 Phases legitimately free of a cross-cutting concern

Not every document must reference every subsystem. These are checked and
**deliberately** environment-free, so a future audit does not re-flag them:
`R2` (materials), `R4` (flow solver) — both deliberately domain-free; `R5`
(subsurface, which is below the surface), `R6` and `R7` (well and lift physics;
their environment exposure arrives through R12's operations). `R2` is also the
one phase that legitimately raises no events.

---

## 2. Change-impact map

**Read as: if you change the row, re-read the columns.**
`●` must be revised · `○` likely affected · `·` check only

| Change → | 00 | 01 | 02 | 03 | 04 | 05 | 06 | 07 | 08 | 09 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 | 21 | phases |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **00** Vision | — | ● | ○ | ○ | · | · | ○ | · | ○ | · | · | · | ○ | ○ | ○ | ○ | · | ○ | ● | · | ● | · | ○ |
| **01** Concept matrix | ○ | — | ● | ● | ○ | ○ | ○ | ○ | ○ | · | ● | ● | ○ | ● | ● | · | ○ | ● | ○ | ● | ○ | · | ● |
| **02** Domain model | · | ● | — | ● | ● | ● | ○ | ○ | ○ | · | ● | ● | ○ | ○ | ○ | · | ○ | ○ | · | ● | ○ | · | ● |
| **03** Architecture | ○ | ● | ● | — | ● | · | · | ○ | · | ● | ○ | ● | ● | ○ | ○ | ● | ● | · | ○ | ○ | · | ● | ● |
| **04** Flow engine | · | ○ | ● | ● | — | ● | · | ○ | ○ | ○ | · | ○ | ● | ○ | · | ● | ○ | ● | · | ○ | ○ | ● | ● |
| **05** Models | · | ○ | ● | · | ● | — | ○ | ● | ○ | · | ● | · | ● | ● | ● | · | · | ● | · | ○ | ○ | ○ | ● |
| **06** World & exploration | ○ | ○ | ○ | · | · | ○ | — | ○ | ○ | · | ● | ○ | ○ | ● | · | · | ○ | ○ | ● | ○ | ● | · | ● |
| **07** Technology | · | ○ | ○ | ○ | ○ | ● | ○ | — | ○ | · | ● | ○ | ○ | ● | ● | · | ○ | ○ | ○ | ○ | ○ | · | ● |
| **08** Economics | ○ | ○ | ○ | · | ○ | · | ○ | ○ | — | · | ● | ○ | ○ | ○ | ● | · | ○ | ● | ● | ○ | ● | ○ | ● |
| **09** Diagnostics | · | ○ | · | ● | ○ | · | · | · | · | — | · | ● | ● | · | ○ | ○ | ● | · | · | ○ | ○ | ● | ● |
| **10** Content & units | · | ○ | ● | ○ | ○ | ● | ○ | ● | ○ | · | — | ● | ○ | ● | ● | · | · | · | ● | ○ | · | · | ● |
| **11** Persistence | · | · | ○ | ● | · | · | ○ | · | · | ● | ○ | — | ● | ○ | ○ | ● | · | · | ○ | ○ | · | ○ | ● |
| **12** Verification | · | · | · | ○ | ○ | ○ | · | · | · | ○ | · | ○ | — | ○ | ○ | ○ | ○ | ○ | ○ | · | ○ | ● | ● |
| **13** Environment | ○ | ● | ● | ● | ● | ● | ● | ● | ● | · | ● | ● | ● | — | ● | ● | ● | ● | ○ | ● | ● | ● | ● |
| **14** HSE | ○ | ● | ● | ● | ○ | ● | · | ● | ● | ○ | ● | ● | ● | ● | — | · | ● | ● | ● | ● | ● | ● | ● |
| **15** Time & execution | ○ | ○ | · | ● | ● | · | · | · | ○ | ● | · | ● | ● | ● | · | — | ● | ● | ○ | ● | ○ | ● | ● |
| **16** Events | · | ○ | · | ● | ○ | · | · | · | ○ | ● | · | · | ● | ○ | ● | ● | — | ○ | ● | ● | ● | ● | ● |
| **17** Cross-impact | ○ | ● | ○ | · | ● | ● | ○ | ○ | ● | · | · | · | ● | ● | ● | ● | ○ | — | ○ | · | ● | ● | ○ |
| **18** Game modes | ● | ○ | · | ○ | · | · | ● | ○ | ● | · | ● | ○ | ● | ○ | ○ | ○ | ● | ○ | — | ○ | ● | · | ● |
| **19** Glossary | · | ● | ● | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ● | · | · | ○ | ○ | ○ | ○ | · | ○ | — | ○ | ○ | ● |
| **20** Decisions | ● | ○ | · | · | ○ | ○ | ● | ○ | ● | ○ | · | · | ● | ● | ● | ○ | ● | ● | ● | ○ | — | ○ | ○ |
| **21** Integration | · | · | · | ● | ● | · | · | · | ○ | ● | · | ○ | ● | ● | ● | ● | ● | ● | ○ | ● | ○ | — | ● |

### 2.1 What the map shows

**Documents 13, 14, 15, 16 and 21 have the densest rows** — the cross-cutting
systems. A change to any of them touches most of the set. That is the structural
reason pass two produced so much downstream revision, and it is a standing
warning: **changing the environment, HSE, time or event model is never a local
edit.**

**Document 12 (Verification) has the densest column** — almost everything
obliges a test. If a change to any document does not produce a change in 12,
that is a signal the change was not made checkable.

**19 (Glossary) is bidirectional with everything.** Rule N7 already says a term
enters the glossary before it enters code; the map says it must also enter before
it enters another document.

### 2.2 Three chains worth knowing by heart

| If you change… | …you must also revise |
|---|---|
| **The tick pipeline** (03 §6) | 04 (segment solving) → 15 (stage refs) → 16 (stage column) → 21 (§4 map) → 09 (invariant timing) → every phase doc citing a stage number |
| **A model's fidelity or plugin set** (05 §9) | 03 §3.2 (replaceable list) → 07 (what technology swaps) → 13 (what environment changes) → 18 §5 (modifiers) → 12 (model tests) |
| **A coupling or its delay** (17 §2, 21 §2) | 21 §2.1 (class) → 21 §6 (loop entry event, if downward) → 16 (severity, loop role) → 12 (structural test) → 09 (indicator invariant INV11) |

---

## 3. Consolidated rule registry

Every binding rule in the design set, in one place, with where it lives and how
it is enforced. **If a rule is not here, it is not binding.**

### 3.1 Architectural laws — [03](03_ARCHITECTURE.md) §1

| # | Rule | Enforcement |
|---|---|---|
| L1 | No concrete type is ever a dependency | Architecture test |
| L2 | No dependency has a default | Architecture test |
| L3 | No member exists without behaviour | Architecture test |
| L4 | No failure is discarded | Architecture test |
| L5 | One owner per fact | Architecture test |

### 3.2 Integration rules — [21](21_INTEGRATION.md)

| # | Rule | Enforcement |
|---|---|---|
| IR1 | Every P5/P6 coupling has a P2/P3 leading indicator | I-V1, INV11 |
| IR2 | Every loop over two years publishes a standing indicator | I-V2, INV11 |
| IR3 | Every downward loop has an entry event firing while ≥2 exits remain | I-V10 |
| IR4 | Every entry event is severity ≥ `W` | I-V11 |
| IR5 | Every consequence event names its entry event and tick | I-V12 |
| IR6 | Every `C`/`D` event carries a cause chain ≥1 link | I-V13, INV12 |

### 3.3 Cross-impact design rules — [17](17_CROSS_IMPACT_MATRIX.md) §6

| # | Rule |
|---|---|
| CI1 | Every coupling is mechanical, never scripted |
| CI2 | Every constraint is discoverable |
| CI3 | Reinforcing loops have visible leading indicators |
| CI4 | Every downward loop has at least two exits |
| CI5 | No coupling is instantaneous unless it is physical |
| CI6 | Loop dominance shifts by stage |
| CI7 | Each downward loop has a registered entry event |
| CI8 | Every consequence event names the entry that preceded it |

### 3.4 Runtime invariants — [09](09_DIAGNOSTICS.md) §6

`INV1` mass · `INV2` cash · `INV3` reference integrity · `INV4` single ownership ·
`INV5` non-negativity · `INV6` physical bounds · `INV7` temporal monotonicity ·
`INV8` belief consistency · `INV9` segment closure · `INV10` barrier derivation ·
`INV11` indicator registration · `INV12` cause completeness.

**Checked every tick, in every build.** Violation halts the engine.

### 3.5 Decision-quality tests — [20](20_PLAYER_DECISIONS.md) §1

`T1` no dominant answer · `T2` information available · `T3` observable
consequence · `T4` composes with other decisions. A candidate failing any of
the four is automated, removed, or coupled — never shipped as a chore.

### 3.6 Naming rules — [19](19_GLOSSARY.md)

`N1`–`N7`, of which the two most often violated in practice are **N3** (no
`Manager`/`Helper`/`Service` in a contract name) and **N7** (a term enters the
glossary before it enters code — or, per §2.1 above, before another document).

### 3.7 The non-negotiables — [README](../README.md)

Eleven owner-stated constraints. Rules 9 and 10 are IR1–IR4 and IR6 restated at
project level; **rule 11 (definition-driven, plugin-first)** consolidates what
lived scattered across L1–L2, 03 §3.2–3.3, 10 and SDD-004 into one owner-stated
promise: content edits for everything, plugins for new behaviour, engine edits
for neither. Enforced by the existing machinery — no new mechanism was needed,
which is itself the evidence the architecture already kept the promise.

---

## 4. Identifier scheme

Every cross-document identifier has a unique prefix. **Established in pass three
after an audit found `E1`, `C1`, `S1`, `V1` and `I1` each meaning between three
and five different things.**

### 4.1 Verification suites

| Prefix | Suite | Owner |
|---|---|---|
| `L1–L5` | Architectural laws | [03](03_ARCHITECTURE.md) §1 |
| `MX1–MX8` | Exact analytic model tests | [12](12_VERIFICATION.md) §3.1 |
| `MB1–MB7` | Industry band tests | [12](12_VERIFICATION.md) §3.2 |
| `CAL1–CAL10` | Physical calibration | [05](05_SIMULATION_MODELS.md) §10 |
| `FV1–FV13` | Flow solver | [04](04_MATERIAL_AND_FLOW.md) §9 |
| `PV1–PV8` | Persistence and determinism | [11](11_PERSISTENCE.md) §4 |
| `EN1–EN12` | Environment | [13](13_ENVIRONMENT.md) §8 |
| `HS1–HS14` | HSE | [14](14_HSE.md) §10 |
| `TM1–TM11` | Time and segmentation | [15](15_TIME_AND_EXECUTION.md) §11 |
| `EM1–EM10` | Events | [16](16_EVENT_MATRIX.md) §7 |
| `CI-V1–CI-V13` | Couplings and loops | [17](17_CROSS_IMPACT_MATRIX.md) §7 |
| `GM1–GM13` | Objectives and modes | [18](18_GAME_MODES.md) §7 |
| `PD1–PD7` | Decision catalogue | [20](20_PLAYER_DECISIONS.md) §8 |
| `I-V1–I-V16` | Integration | [21](21_INTEGRATION.md) §8 |
| `SC1–SC13` | End-to-end scenarios | [12](12_VERIFICATION.md) §4 |
| `R<n>-V<m>` | Phase-local tests | each phase doc |

### 4.2 Rules and invariants

`L*` laws · `IR*` integration rules · `CI*` cross-impact rules ·
`INV*` runtime invariants · `N*` naming rules · `T*` decision-quality tests.

### 4.3 Player decisions — [20](20_PLAYER_DECISIONS.md)

`DEX*` exploration · `DDV*` development · `DPR*` production · `DCO*` company ·
`DEN*` environment · `DHS*` HSE. **61 decisions.**

### 4.4 Open decisions

`D*` vision · `M*` domain · `AD*` architecture · `FD*` flow · `SD*` models ·
`W*` world · `TD*` technology · `ED*` economics · `DGD*` diagnostics ·
`CD*` content · `PSD*` persistence · `EV*` environment · `HS-D*` HSE ·
`TM-D*` time · `EM-D*` events · `CI-D*` cross-impact · `GM-D*` game modes ·
`PD-D*` decisions · `I-D*` integration.

### 4.5 Concept matrix rows

Section-lettered `A`–`J`, always cited as "concept A6". **These are the one
exception to prefix uniqueness** and are never renamed — they are the stable
index the whole design keys on.

### 4.6 The rule going forward

> **A new numbered series must claim an unused prefix in §4 before it is used.**

---

## 5. Coherence log

What the three passes found and resolved. Kept because a design set's defect
history is as instructive as a codebase's.

| # | Finding | Pass | Resolution |
|---|---|---|---|
| 1 | Tick pipeline had no environment, HSE, objectives or segmentation stage | 2 | 03 §6 rebuilt at 14 stages; physical/administrative split at stages 4 and 9 |
| 2 | Solver described as running once per tick, but segmentation requires per-segment | 2 | 04 §4.0 added; FV11–FV13; R4 §2.4b |
| 3 | Concept matrix had no environment, HSE, weather or objective rows | 2 | Sections H, I, J added — 22 rows |
| 4 | Domain model had no environment or HSE entities | 2 | §7b added with two structural rules |
| 5 | Verification knew none of the new suites | 2 | Architecture checks 13 → 23; scenarios 10 → 13; suite index added |
| 6 | `I1`/`I2` meant invariants in 09 and integration rules in 21, both cross-cited | 3 | Invariants → `INV*`; integration rules → `IR*` |
| 7 | `E1`, `B1`, `C1`, `S1`, `V1` each meant 3–5 different things | 3 | Suites re-prefixed `MX`/`MB`/`CAL`/`PV`/`FV`; open decisions given unique prefixes |
| 8 | `(E10)` cited in R11 did not exist — exact tests stop at E8 | 3 | Corrected to `CAL10` |
| 9 | `V1–V10` cited after the suite grew to 13 | 3 | Corrected to `FV1–FV13` in five places |
| 10 | Model registry (05 §9) omitted every environment and HSE model | 3 | Eight added, with the fidelity-dial rule restated |
| 11 | Exploration ignored that setting prices information and gates surveys by season | 3 | 06 §3.1a added |
| 12 | Technology and environment share an effect vocabulary; only 13 said so | 3 | 07 §3.0a added with the symmetry table |
| 13 | Cash-flow spine had no carbon price, weather downtime, HSE programme or environmental liability | 3 | 08 §2.2 extended; ESG added to reserve-based lending |
| 14 | Invariants did not cover segmentation, barrier derivation, indicator registration or cause chains | 3 | `INV9`–`INV12` added |
| 15 | Content type list omitted eight kinds introduced in pass two | 3 | 10 §2 extended |
| 16 | Save had no environment, HSE or objectives block; `weather` stream unlisted | 3 | 11 §2 and §3 corrected |
| 17 | **Decision catalogue had no environment or HSE decisions** — two whole subsystems unrepresented in the document that proves the game has depth | 3 | §5b added; 51 → 59 decisions; density table updated |
| 18 | Glossary lacked every term introduced in passes two and three | 3 | Sixteen terms added, plus a new §G |
| 19 | `IEmissionsLedger` and `IIncident` sat under *Operations & company* in 01 while 03 §8 assigned them to `OGSim.Hse` — **two documents naming different owners for one fact, which law L5 forbids** | 4 | Moved to the HSE section as `I11`/`I12`; ownership note added; `IRegulator` explicitly retained by the company |
| 20 | R16's deliverables still claimed `IEnvironmentalLedger` after its own §2.6 assigned that state to R23 | 4 | Deliverables corrected with an explicit "not here" clause |
| 21 | **19 of 24 phase documents never mentioned the environment**; 8 never mentioned events or segmentation | 4 | Cross-cutting coupling sections added to 14 phases where the coupling is load-bearing |
| 22 | R21 never enumerated what the read model must contain, so the features 09 §7 and rules IR1–IR2 promise were unbuildable from the contract | 4 | §2.4b added — 16 required projections, plus R21-V11–V13 |
| 23 | R3 listed four content kinds in its deliverables while 10 §2 had grown to 27 | 4 | Type-agnostic loader stated as R3's real acceptance criterion |
| 24 | 00's scope diagram showed neither environment nor HSE as inputs to the chain | 4 | Both added as cross-cutting influences |
| 25 | 02 §9 recorded no environment or HSE exclusions, so five simplifications were undocumented omissions rather than decisions | 4 | Five added (weather prediction, personnel-level safety, QRA, dispersion, ecological modelling) |
| 26 | Concept matrix listed no tick pipeline, segmentation or calendar service | 4 | `G13`–`G15` added |
| 27 | Coherence checks 4, 5 and 6 from §6.1 had been *defined but never run* | 4 | All three executed; 4 and 6 pass; 5 surfaced findings 19–21 |
| 28 | "51 decisions" survived in 00 and the README after the catalogue grew to 59 in pass 3 | 5 | Corrected — a count cited outside its owner is a drift magnet; prefer "the catalogue in 20" over a number where the number can age |
| 29 | The ownership map still showed `E17` under *Operations* after finding 19 moved it to HSE | 5 | Corrected — finding 19's fix touched the table but not the diagram twenty lines below it. **A fact stated twice in one file is still two copies** |
| 30 | The E16–E17 gap in the concept matrix was undocumented at the point a reader encounters it | 5 | Gap row added referring to the ownership note; row numbers stated as stable and never reused |
| 31 | *Segment* and *leading indicator* each had two glossary entries with diverging wording — a violation of N1 in the document that defines N1 | 5 | Deduplicated; the HSE-specific *lagging indicator* entry now cross-references the general term |
| 32 | **The conservation equation omitted fuel gas consumed on site and permitted water discharge** — two legitimate outflows. R10-V9 knew about discharge; the master balance in 04 §7 did not, and electrification's "frees fuel gas for sale" implied a fuel term that did not exist | 6 | Both terms added; fuel combustion routed to the emissions ledger; invariant-vs-non-convergence distinction stated |
| 33 | **Solver non-convergence was an unrecoverable fault — a game that cannot continue on a numerics failure.** The player would be stopped by the solver, not the simulation | 6 | Replaced with the physical shut-in ladder (04 §4.0b): shut in the worst branch, audit, re-solve; termination guaranteed. AD3 resolved (c); events split into `flow.forcedShutIn` (W) and `flow.solverFault` (C, recurrence) |
| 34 | **Stage 4 needs service severity (rates, water cut) that stage 5 has not yet solved** — the circularity was never resolved in the design; "which tick's water cut drives corrosion?" had no answer | 6 | One-tick lag declared in 03 §6.1: stage 4 uses the previous tick's solved service data; the power balance uses declared duty, not solved rates |
| 35 | **`p/Z` was free telemetry**: the plot needs average reservoir pressure, which a flowing well does not report — the design had no pressure-survey information source | 6 | Build-up survey added, priced in **deferred production** (the shut-in is the cost). The game's best deduction mechanic now has an honest price |
| 36 | The intra-tick pressure integration scheme was unstated — start-of-tick rates against end-of-tick depletion is explicit Euler, and nothing bounded its error | 6 | Policy declared in 05 §3.1 with a validity limit (model fault beyond a per-tick withdrawal fraction) and a sub-stepped reference test (R5-V11) |
| 38 | **The design assumed an engineer at the controls.** Fidelity levels existed, but physics fidelity is the *least* important accessibility axis — flight sims are playable by anyone because of *assists*, and the design had no equivalent. PD-D1/D2/D4 and I-D5 were four partial answers to one unasked question | 7 | Reality-level system designed in [18](18_GAME_MODES.md) §5b: three independent axes (fidelity × assists × forgiveness), the Advisor as a player-side autopilot acting through the command bus, presets Story→Simulation, scores stamped with profile. Four open decisions resolved into it; phase R25 added |
| 40 | **The surface world was one line of the generation pipeline** — "terrain, access, infrastructure" as scenery — while the decision catalogue quietly depended on it: routing (DDV9), port choice, labour, remoteness, sensitivity, rent-vs-build (D10) all assume surface facts nothing generated | 8 | 06 §5.1a: an eight-step causal surface sub-pipeline (terrain → hydrology → climate → settlements → networks → utilities/third-party → land status → **derived profiles**); concept rows H8–H10; R15.7 split into four tasks with four new tests; remoteness becomes computed, not painted; settlements grow slowly in response to the player |
| 53 | The tier/gate system (findings 50–52) existed as *mechanisms* with no *inventory* — no statement of which equipment exists at each station, which node gates it, or what the shipped tree actually is. Content authors would have invented all of it | 11 | The catalogue layer: 14 station sheets ([catalog/](../catalog/CATALOG_INDEX.md)) — visible equipment, tier ladders, gates, eras, cost bands, install operations, datasheet effects — plus the [TECH_TREE](../catalog/TECH_TREE.md) registry (~50 nodes: era, prereqs, routes, what each opens). Declared the authoring spec for content (10 §2b), with sheet↔tree consistency a mechanical check |
| 78 | `IRandomStream.NextInt` existed in code (SDD-012 §2 needs the failure-day draw) but SDD-001 §4's pinned block never listed it | code pass 6 | Block amended — doc and code identical again |
| 82 | **The contract layer's completeness claim was false, and the phase documents were never back-annotated after the eight passes that made it.** A sweep of every `I<Name>` the 25 phase documents promise against code and SDDs found 62 undeclared. Three classes, and only the first is a hole: **(a)** three of the eleven [03](03_ARCHITECTURE.md) §3.2 replaceable slots — `IHydraulicModel`, `ISeparationModel`, `IObservationModel` — were never declared, so pass R1-C5's "every §3.2 replaceable slot is now a compiled type" was untrue and the plug-and-play table had three holes in it; **(b)** R2's whole property/material surface (`IPropertyKind`, `IProperty`, `IMaterial`, `IMaterialCatalog`, the distribution types) appeared in R2's deliverables and in no SDD and no code; **(c)** ~20 names are equipment — `ISeparator`, `ICompressor`, `ITank`, `ITreater`, `IDesalter`, `IStabiliser`, `IFlare`, `IDehydrator`, `INglExtraction`, `IAcidGasRemoval`, `IFacilityUnit`, `IChoke` — which **must never be declared**: [02](02_DOMAIN_MODEL.md) §4.1 says there is no facility-type hierarchy in code at all and non-negotiable 11 makes every one of them a content template behind `IFlowElement`. The phase docs predate that hardening and still read as if each were an interface | code pass 9 (R2.0 — phase-doc vs code sweep) | (a) the three slots declared, `IHydraulicModel`/`ISeparationModel` in [SDD-006](../sdd/SDD-006_FACILITIES_AND_TRANSPORT_ELEMENTS.md), `IObservationModel` in [SDD-008](../sdd/SDD-008_INFORMATION_AND_BELIEFS.md); (b) [SDD-002](../sdd/SDD-002_STREAMS_AND_FLOW.md) §2b written, with the **P90-is-low/P10-is-high** convention pinned on the contract because reading it the statistical way books possible reserves as proved and no type objects; (c) recorded as a standing correction — the equipment names stay out of code, and each phase document is corrected at its own `Rn.0` rather than in one sweep (SDD-000 §4) |
| 81 | **A map game whose read surface could not draw a map**: the world surface entered the engine at tick zero through `IWorldSink` and no declared type ever let it back out; `WellView`/`FacilityView` had no coordinates, licences no polygons, prospects no believed outlines — R21 G5 promised map fuzziness with nothing to render it on | code pass 8 (stage-and-screen walk) | `IEngine.World` (`WorldView`, static-beside-ReadModel, public knowledge only — no accumulations by construction); `Site` on well/facility views; licence `Area`; `Prospects` with believed outline + POS |
| 80 | Terrain classes had no authoring spec: `GeneratedTerrain.Classes` referenced content that no sheet defined and no kind covered | owner question ("what about sea, desert, mountains…") | [C16](../catalog/C16_TERRAIN_CLASSES.md) sheet (6 shipped classes; sea = elevation, not a class); `terrain-class` kind added to SDD-004 |
| 79 | `IWorldGenerator.Generate` took no parameters — no map size, richness, maturity, climate or era knobs; a sim without a new-world screen | owner request | `WorldParameters` (9 knobs, each landing on a named SDD-010 step; template-declared ranges; out-of-range ⇒ refused, never clamped) threaded through `EngineSetup` |
| 77 | **Finding 72's fix was wrong-family**: typing `Bg` as the oil `FormationVolumeFactor` bridged reservoir gas to `SurfaceVolume` (stock-tank) instead of `StandardGasVolume` — the precise wrong-bucket error the volume types exist to make uncompilable | code pass 6 | `GasFormationVolumeFactor` with its own Shrink/Swell to `StandardGasVolume`; SDD-001 §1.1 + SDD-003 §4 amended |
| 76 | World-gen and weather were deferred as "shape unknown" — inconsistent with a contracts phase, as the owner pointed out: the same SDD-pin-then-declare route used for every other slot applied here too. `IWorldGenerator`/`IWorldSink` (typed handoff; beliefs via the observation door) and `IWeatherModel` (the AR(1) advance only) declared; SDD-010/016 amended first | code pass 5 | The 03 §3.2 replaceable list is 100% typed; no contract IOUs remain |
| 75 | Event-id issuance was unspecified: `EngineEvent.Id` was required at construction but the per-tick sequence — the total order's tiebreaker — was a number no module could know | code pass 4 | `Publish` stamps and returns the id (the `IAuditTrail.Record` pattern); callers pass default |
| 74 | `Money` had no exact integer scaling — day-rate × days was forced through the lossy `RoundHalfEven(double)` door | code pass 4 | checked `Money * long` (both orders); `* double` stays deliberately absent |
| 73 | **`SolveReport` was diagnostics-only**: SDD-002 §9's commit step had no converged flows to commit, and S0 had no committed rates to seed from — the solve produced attribution but not its answer | code pass 4 | `ElementSolution` + `CompletionState` added to the report; SDD-002 §8 amended |
| 72 | `Bo` returned `FormationVolumeFactor` but `Bg` returned raw `double` for the same rm³/sm³ dimension — reopening exactly the rb↔stb hole the volume types close | code pass 3 | Bg typed like Bo, SDD-003 §4 amended |
| 71 | `IMigrationStep` (SDD-013 §5, exact pin) was never declared | code pass 3 | PersistenceContracts.cs |
| 70 | The read model was missing six R21 §2.4b projections: per-compartment pressure/watercut/GOR, IPR/VLP curves, spec margins, cargo nominations, pending-VOI, cost/revenue-by-cause (+ ESG rate spread not explicit) — R21-V11 would have failed on day one | code pass 3 | CompartmentView, curves on WellView, SpecMargins, Nominations, PendingValueOfInformation, FinanceView, EsgRateSpread |
| 69 | **Non-negotiable 11's front door had no contract**: ICatalog, ICatalogSet, IContentSource, GatedDefinition, Era, LoadFailure, IModuleRegistry — all pinned in SDD-004, none declared | code pass 3 | ContentContracts.cs + IModuleRegistry in Kernel |
| 68 | **The host could not save or start a game through any declared type** — SDD-017 §1 said "nothing else exists" while SDD-013 defined a save format nothing could write | code pass 3 | `IEngine.WriteSave`, `IEngineFactory`, `EngineSetup`, `EngineStartResult`; SDD-017 §1b |
| 67 | SDD-002 §9 named `ICommitTarget` as "the only mutation path" — no type declared it; the pure-Transform design had no committed counterpart | code pass 3 | ICommitTarget + Withdrawal/Receipt/Custody family, signatures pinned |
| 66 | Observations had no declared shape — the truth wall existed in prose only. `Observation` + `IBeliefStore` now pin the ONE shape that crosses (SDD-008 §3) | code pass 2 | Beliefs consume a record, not a convention |
| 65 | Four 03 §3.2 replaceable models had no contract (fiscal, price, degradation, hazard); `IPipeline` and SDD-001 §7's `ICommandValidator/Applier` undeclared | code pass 2 | EconomicsContracts, IntegrityContracts, IPipeline, validator/applier pair |
| 64 | `TickContext.Segments` was `required` — but the segment plan does not exist until stage 4 builds it; the contract demanded a value before its producer had run | code pass 2 | Nullable, set once by Availability; early read = I-V5 violation |
| 63 | `IGatingValidator.Check` could not perform its own envelope checks — effective envelope values were unreachable from its parameters | code pass 2 | `IEffectState` declared (SDD-005 §4.2); passed as the fourth argument |
| 62 | **The solver had elements but no topology** — no type in any assembly declared which element feeds which. The network was a prose concept only | code pass 2 | `FlowConnection`/`FlowTopology`; `IFlowSolver.Solve` takes the wiring |
| 61 | **First rule-F-4 event, at first compiler contact:** the pinned type name `Stream` is ambiguous with `System.IO.Stream` under implicit usings — a defect no paper review could catch and exactly what F-4 exists for. Process followed as written: stop, amend SDD-002 (`MaterialStream`; the design term "stream" is unchanged), then fix the code | code | The pipeline works: SDD amended before the compiler was appeased |
| 60 | **Fourth SDD review pass — walkthrough-driven** (seven gaps, one architectural): **gas lift is a cycle in a tree-only network** — produced gas recompressed into the producing well violates FD4; closed with a one-tick lag (t uses t−1's committed lift rate as a matched sink/source pair, the genset-fuel shape reused; a new lift well ramps one month, physically fine). Also: the command inventory now derives from the 61-decision catalogue via PD1 rather than being invented per module; carried-interest arithmetic (costs only, drawn before the carried party's cash); well-test masses post into conservation as operation-level entries; **the pre-drawn outcome is readable in an inspectable save — accepted explicitly** rather than obfuscated at the cost of the save's debuggability; the asset-market data room is a replay of the seller's audited observations (no second store); prospect beliefs re-key to the accumulation on discovery; an MST tie-break | 14 | All fixed. Pass yields: 9 → 10 → 10 → 7, with one architectural item — the walkthrough method still earns its keep, but the curve says the set is converging |
| 59 | **Third SDD review pass** (ten gaps — the referenced-but-undefined class): `ContentId` used in every SDD, declared in none; `DisposedMass` shapeless; **the `double`→`Money` rounding boundary unpinned** — INV2's cent-exactness was a slogan until "half-even, once, at the Movement" was written; reserves computation would have tempted quarterly full-field re-simulation (now: content type-curves, solver forbidden); **`AdvisorView` sat inside the engine's read model though the Advisor is a client** — removed, restoring the exact 16-section correspondence; scripted scenario events were raw notifications (16 §1 violation — now commands/overrides so the models publish honestly); events retention, the segment-merge impact estimator, operation completion day, player diffusion timing, completion-as-source, and the weather region each pinned | 13 | All fixed. Three passes over the SDDs have yielded 9 → 10 → 10 findings of successively finer grain; the remaining risk is now implementation-shaped, which is what CI and rule F-4 are for |
| 58 | **Second SDD review pass** (ten gaps, three of them contradictions): `IEngine` declared with 3 members in SDD-001 and 5 in SDD-017; `NextNormal` said "ziggurat" while `DetMath` has no trig (now Marsaglia polar — Ln/Sqrt only); **the /30 segment grid contradicted the real calendar** — resolved by pinning the industry's own 30/360 convention (every month 30 days, leap years deliberately absent, TM11 amended); the element conservation check could not pass a flare or a completion (`Sourced` and kind-tagged `Disposed` added to the transform result and the check); boosters' negative ΔP in the backward pass; the registry↔content-id slug rule; rival tech acquisition (diffusion at era+lag — clocks, not dice); the `Aggregate` predicate node ("any well's water cut > 0.6" was inexpressible); a typo; bounded-kind quantiles | 13 | All fixed; the calendar decision is the pass's most consequential — one uniform grid for segments, weather days, berth calendars and day-rates |
| 57 | **The SDD review pass** (nine gaps): (a) two stochastic positions drawn continuous where segment boundaries need the /30 day grid — failure day and disaster day are now integer-day draws; (b) the solver's backward pass contradicted the choke's critical-flow decoupling — the exception is now in the algorithm; (c) **power sources, flare and VRU had no pinned transforms** — merit-order balancing with fuel-sink routing, the flare's combustion-efficiency split, VRU recovery now specified; (d) `MeterSocket` drifted from 07's slot table — removed; (e) SDD-004's own `GatedDefinition` lacked the `Fits` field SDD-005 imposed on it; (f) **the "every phase covered" claim was false** — R21's read model, R22's weather generator and R23's bow-tie arithmetic were pinned nowhere; SDD-016, SDD-017 and SDD-012 §4b close them, and the completeness claim is now a phase↔SDD table, not a sentence | 13 | All nine fixed; the weather generator's daily AR(1) lands on the same /30 grid as segmentation — one clock for everything sub-tick |
| 56 | **The tech→effect chain was closed for equipment and open for everything else.** A tech unlocking a mud, an inhibitor or an injectant had no declared landing place — "chemicals" was an OPEX line, and "how does the system know what a new material affects?" had no answer | 12 | The slot system (07 §4b.3b, SDD-005 §4.0b): every unlockable declares `fits` (a SlotKind); treatments carry **slot-scoped effect lists as their datasheets**, applied to the owning instance with per-contribution provenance; stream injectants stay ordinary `IMaterial`s with drive plugins declaring accepted injectants; `treatment` content kind; catalogue sheet C15; discoverability via SlotKind in `tech.available` and slot-filtered pickers |
| 55 | "All types the same way" had two soft spots: facility *types* lived as prose in the research doc rather than as declared template content, and **non-process buildings** (camps, warehouses, bases) existed only implicitly inside cost assumptions — objects that never touch a stream had no stated home in the unit system | 12 | Stated flatly: no facility-type hierarchy in code, the PPDM-style list ships as `facility-template` JSON; the **Support unit family** added (02 §4.2, C13 rows) — datasheets acting on operations (rotation cost, spares lead time, response time) instead of streams, through the identical content/construction/condition machinery; definitions-vs-instances clarified in 10 §1b |
| 54 | The definition-driven/plugin-first principle existed in five places (L1–L2, 03 §3.2, 10, SDD-004, the catalogue) but was never stated as a single global rule the owner could point at — and its sharpest justification (the predecessor's always-empty property bag) was documented as a loader rule, not as the moddability argument | 12 | Non-negotiable 11 added; the moddability contract table (10 §1b) states exactly what a modder touches for each kind of change; datasheet strictness reframed as *for* the modders |
| 51 | **Geology had no tech dimension.** Observation tiers only narrowed error bars — a 2-D survey over a stratigraphic trap returned a noisy estimate instead of *nothing*, so better imaging was an efficiency, not an exploration lever, and the industry's defining dynamic (each imaging generation re-opens mature basins; tight/HPHT/subsalt waves) was inexpressible | 11 | Detectability classes D0–D3 and access requirements as **truth attributes on the accumulation** (06 §2.3, 02 §2.2b, A15); below-tier surveys spawn nothing and leak nothing; re-screening and re-processing; era-layering bands in world-gen (R15-V11); DEX11; the liquidation exit widened |
| 52 | Activity-level tech dependencies were scattered or absent — no single statement of what gates a survey, an HPHT drill, a frac, an LNG train; and phases R5–R16 implicitly assumed a technology state that R17 builds thirteen slots later | 11 | The activity-gating matrix (07 §2c): every operation's tech/tier/environment requirements in one table, validated at scheduling with the missing item named, never re-checked at execution; operation templates carry `requiresTech`; **`AllCapabilities` composition** declared as the shipped sandbox mode pre-R17 suites run under (R17 §2.6c, R17-V14) |
| 50 | **Nothing connected the technology graph to the equipment catalogue.** 07 unlocked "options" and 10 declared equipment content, but no `requiresTech` existed — so "we researched better ESPs" had no path to more flow from a well, and the service-contract route had nothing concrete to rent | 10 | The equipment-tier system (07 §4b): two layers (capability, then procurement), tiers in eleven places, the ESP worked ladder, **the datasheet is the effect**, early-generation reliability as a real trade, rentable gated tiers. `requiresTech`/`availableFromEra` fields (10), instance-references-tier (02 §3.2), R17.7 + V11–V13, SDD-003 tier-curve consumption, DDV8 widened |
| 41 | Dangling section reference: 04 §7 pointed the shut-in ladder at a §4.2 that does not exist | 9 | Corrected to §4.0b |
| 42 | An injector could never be abandoned — the well lifecycle had no `Injecting → Abandoned` transition, so a converted producer was immortal | 9 | Transition added |
| 43 | 05 §3.4 still cited pre-rename test id `V2` | 9 | `FV2` |
| 44 | **Gas condensate had no valid fluid model** — plain black-oil cannot represent retrograde dropout, and `gas condensate` was listed as shipped content. Producing one too fast strands the most valuable liquids, and nothing modelled it | 9 | Modified black-oil (CGR) form specified; content declares its required form; wrong form is a model fault |
| 45 | Two hazards the design itself promised were missing from the catalogue: **scale** (glossary listed it under flow assurance; the hazard table did not) and the **legacy plugged-well leak** (02's own deletion rule cites it as the reason entities are never deleted) | 9 | Both added — scale with the scale-versus-skin diagnostic question, the legacy leak as the campaign's long tail |
| 46 | **Nothing connected perforation placement to water arrival.** Contacts existed and moved; per-well breakthrough did not depend on standoff, so DDV6 (zone choice), DPR2 (choke policy) and half the completion decisions had no mechanism | 9 | Coning proxy added (05 §3.3b, `IConingModel`): critical rate from standoff; perforations carry standoff (R6 §2.4) |
| 47 | Insurance did not exist — the industry's actual mechanism for holding the fat-tail risks the HSE model generates | 9 | Added as a finance instrument, premiums rated on the player's own record; open decision ED6 |
| 48 | **The liquidation spiral's *acquire / farm in* exit was named in 21 §3 and designed nowhere** — CI4 requires two exits, and one of the two was a word | 9 | The asset market (08 §5b, R16 §2.6b, `rival.assetOffer`, DCO15): rivals sell from their own noisy beliefs; acquisitions carry operatorship, stakes are passive; reserves at market price versus finding cost |
| 49 | **`PSD1–PSD4` meant both the requirements and the open decisions of document 11** — an identifier collision inside a single document, created by pass three's own renaming script | 9 | Requirements re-keyed `PR1–PR6`; the pass-3 lesson (finding 7) extended: **a renaming script is itself a sweep, and its output needs the same review any sweep does** |
| 39 | **World generation was designed (06 §5, R15, `OGSim.World`) but invisible from every entry point** — absent from the concept matrix, the ownership map and the README quick answers. The owner reasonably asked whether it existed. Also surfaced: no module owned spatial primitives (coordinates, areas, distance), which pipelines, logistics and world-gen all silently assumed | 8 | Concept rows G16 (world generator) and G17 (spatial primitives, kernel-owned) added; WG subgraph and its four "generates" edges added to the ownership map; README quick answer added; SDD-001 open item S001-5 |
| 37 | Provenance died at the first tank: streams carried allocation weights but inventory did not, so royalties on anything sold from commingled storage were unattributable. Quality blending was likewise unaddressed | 6 | Inventory carries mass-weighted provenance; distinct grades are distinct materials so blend quality falls out of composition — and segregated storage becomes a real decision |

**Finding 27 is the process lesson.** Checks 4, 5 and 6 were written into §6.1
in pass three and not executed until pass four — and when run, they immediately
produced findings 19–21, including an L5 ownership violation. **A check that is
specified but never run provides no protection at all**, which is precisely the
argument §7 DC-D1 makes for scripting them.

**Finding 17 is the one worth dwelling on.** Documents 13 and 14 were written,
integrated into the architecture, given phases and test suites — and never
produced a single entry in the catalogue that asks *"is this actually a game?"*.
A subsystem with no player decision is a simulation feature, not a game feature,
and only the coherence check surfaced the difference.

---

## 6. Maintenance protocol

Every design document now carries an **Affects / Affected-by header** directly
under its status line — the strong (`●`) entries of its §2 row, stated where the
editor will actually see them. The header is a *cache* of §2: **§2 remains
authoritative, and a change to the matrix must update the affected headers in the
same edit.** Rationale: pass five's findings 28–29 both happened because a fact
lived far from the place it was consumed; the headers put the coupling knowledge
at the point of edit.

For every future change to any design document:

1. **Read the document's own header, then locate the full row** in §2 and read
   the marked columns.
2. **If a rule changes**, update §3 in the same edit. A rule that exists only in
   its home document is not binding.
3. **If a new numbered series appears**, claim its prefix in §4 first.
4. **If a subsystem is added**, it must produce: a concept-matrix row, a domain
   entity or an explicit note that it has none, a tick-stage placement, at least
   one event, at least one verification entry, **at least one player decision**,
   and glossary terms.
5. **Append to §5** whatever the change reveals about the set's coherence.
6. **SDDs are inside the protocol** ([sdd/](../sdd/)): an SDD implements the
   design docs and never contradicts them — a conflict found while writing one
   reopens the design doc and lands in §5. Signature changes after an SDD is
   merged reopen the SDD, not the code review.

### 6.1 The mechanical checks

These are cheap and catch most drift. Worth running before any review:

| Check | Detects |
|---|---|
| Every `](*.md)` link resolves | Renamed or missing documents |
| Code fences balance per file | Truncated edits |
| No identifier cited outside its §4 range | Dangling references like finding 8 |
| Every tick-stage number cited matches [03](03_ARCHITECTURE.md) §6 | Findings 1 and 9's class |
| Every suite named in [12](12_VERIFICATION.md) §4.1 exists at its stated location | Suite-index drift |
| Every subsystem in §2's row list appears in [20](20_PLAYER_DECISIONS.md) | Finding 17's class |
| Every design doc's Affects/Affected-by header matches its §2 row | Header-cache drift |
| Every catalogue-sheet `Tech gate` exists in the TECH_TREE registry, and every registry node is referenced somewhere | Sheet/tree drift ([catalog](../catalog/CATALOG_INDEX.md)) |
| Every row of the never-save table names a rebuild source, and no module registers a state key for a listed item | Derived-state shadowing ([SDD-013](../sdd/SDD-013_PERSISTENCE_FORMATS.md) §4) |
| No count of another document's contents is stated as a number that can age | Findings 28's class — cite the owner, not the number |
| Any fact stated in both a table and a diagram in one file agrees with itself | Finding 29's class |

---

## 7. Open decisions

| # | Decision | Options | Recommendation |
|---|---|---|---|
| DC-D1 | Coherence checks | (a) manual before review, (b) scripted in CI once code exists | **(b)** — §6.1 is mechanical, and the design set outlives any one reviewer's memory |
| DC-D2 | This document's scope | (a) design set only, (b) extended to code once it exists | **(b)** — the change-impact map's natural successor is a module-level one, and the maintenance protocol transfers unchanged |
| DC-D3 | Coherence log retention | (a) keep all findings, (b) prune resolved ones | **(a)** — the log is why the rules exist; pruning it loses the reasoning and invites the same defect twice |
