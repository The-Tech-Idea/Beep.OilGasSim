# SDD — Software Design Documents

**Status:** active · **Date:** 2026-08-06

The layer between the design documents and code. The design set
([design/](../design/)) says **what** the engine is and **why**; an SDD says
**how**, at the level a developer implements from: signatures, data layouts,
algorithms, error types, and the trade-offs behind each.

> An SDD contains code — interface declarations, type shapes, worked
> algorithms. That is not "starting to code": it is finishing the design.
> Nothing in `sdd/` compiles or ships; everything in it is reviewable on paper.

---

## 1. The rolling-wave policy

**Foundation SDDs are written before any code.** They fix the conventions
everything else follows — and they are exactly the documents that would be
catastrophically expensive to change after Arc II.

**Per-phase SDDs are written as each phase's first task** (task `Rn.0`),
against the then-current foundation. Writing all 25 upfront would produce 20
stale documents by the time Arc III starts; writing none would put signature
design inside implementation, where it gets decided by whoever types first.

| Tier | Documents | Written |
|---|---|---|
| **Foundation** | SDD-000 standards · SDD-001 kernel · SDD-002 flow · SDD-003 subsurface/wells · SDD-004 content · SDD-005 capabilities | Before the code of the phases they serve |
| **Per-phase** | SDD-006 … SDD-015 — **all drafted** | Task `Rn.0` = review against current design before coding |

## 2. Status

| SDD | Covers | Phase | Status |
|---|---|---|---|
| [SDD-000](SDD-000_ENGINEERING_STANDARDS.md) | Solution, projects, language, determinism, testing, CI — **and the four implementation-fidelity rules F-1..F-4** | all | 🟦 drafted |
| [SDD-001](SDD-001_KERNEL_CONTRACTS.md) | Kernel contract signatures, incl. spatial primitives | R1 | 🟦 drafted |
| [SDD-002](SDD-002_STREAMS_AND_FLOW.md) | Streams, elements, **the solver algorithm pinned step-by-step** — damping, throttle rule, pro-rata apportionment, tolerances, shut-in ladder | R2/R4 | 🟦 drafted |
| [SDD-003](SDD-003_SUBSURFACE_AND_WELLS.md) | Subsurface & wells — **every formula in its SI implementation form**, every root-find's method, bracket, budget and tolerance; accumulation truth attributes | R5/R6/R7 | 🟦 drafted |
| [SDD-004](SDD-004_CONTENT_PIPELINE.md) | Content pipeline — file & unit grammars, **unknown-key rejection via source-gen `Disallow`**, six stages as code, catalogue/ordinal policy, mod layering, TECH_TREE fixture tests | R3 | 🟦 drafted |
| [SDD-005](SDD-005_CAPABILITIES_AND_EFFECTS.md) | Capabilities & effects — `ICapabilitySet` (two members, deliberately), rentals on operations, **one gating validator**, the sealed four-record Effect hierarchy, **the pinned envelope-combination rule** (extensions raise, restrictions win) | R17/R22 + all gated commands | 🟦 drafted |
| [SDD-006](SDD-006_FACILITIES_AND_TRANSPORT_ELEMENTS.md) | Every surface element's transform in implemented form — separator derate rule, compression staging formula, **tank backpressure semantics**, gas-line mass-rate form, custody ε, the pinned spec proxies | R8–R11 | 🟦 drafted |
| [SDD-007](SDD-007_OPERATIONS_ENGINE.md) | Operations — reservation with worst-case duration, accrual arithmetic (standby = day rates only), **outcome drawn once at start** (audited, unexploitable), obligations registry | R12 | 🟦 drafted |
| [SDD-008](SDD-008_INFORMATION_AND_BELIEFS.md) | Beliefs — **one conjugate update rule** (Normal in a declared space), Beta-Bernoulli POS with the play-shared factors as the correlation, WLS `p/Z`, BIC compartment inference, **Halton-sequence VOI that consumes no RNG stream** | R14 | 🟦 drafted |
| [SDD-009](SDD-009_ECONOMICS_ENGINE.md) | Economics — double-entry integer ledger (INV2 exact), **the PSC cost-recovery algorithm pinned step-by-step** with mandatory worked-example fixtures, UoP depreciation with remainder carry, the reserves algorithm the growth loop hangs off | R13 | 🟦 drafted |
| [SDD-010](SDD-010_WORLD_GENERATION.md) | World-gen — per-step substreams, **the fill-spill charge algorithm**, value-noise structure, settlement scoring, A\* road routing, class-quota resampling for era layering | R15 | 🟦 drafted |
| [SDD-011](SDD-011_COMPANY_LICENCES_RIVALS.md) | Company & rivals — **rivals are policies over beliefs with no truth path, by construction**; sealed-bid rounds; the asset market with real-observation data rooms | R16 | 🟦 drafted |
| [SDD-012](SDD-012_HAZARDS_AND_DEGRADATION.md) | Hazards — the decay law, **the exponential hazard law** (no threshold to sit above), fixed-order draws, souring curve, strategy machinery as ordinary operations | R18 | 🟦 drafted |
| [SDD-013](SDD-013_PERSISTENCE_FORMATS.md) | Persistence — the canonical-JSON byte rules, per-module digests, **the consolidated never-save table** (derived state and its rebuild sources), migration fixtures | R19 | 🟦 drafted |
| [SDD-014](SDD-014_OBJECTIVES_AND_SCENARIOS.md) | Objectives — the closed predicate AST, **`ReadModelPath` load-validation** (objectives can never see what the player cannot), the eight score formulas, campaign whitelists. §5 now **declares** `Scenario`, `Campaign` and the scripted-entry vocabulary rather than describing them in prose (finding 141) | R24, R21d | 🟦 drafted |

**Build-time amendments** (F-5: each edits its own block in place; this is the
cross-reference): SDD-001 §9–10 — `Contribute`/`Own`/`HandleCommand`, `Composed`
carrying stages/state/commands, `Structural` equality rule (findings 125–127,
131, 139–140) · SDD-002 §6 — `IFlowElementRegistry` (130) · SDD-003 §4/§3.3,
SDD-006 §1/§6, SDD-008 §3 — `ContentId Id` on the five unnamed slots (132) ·
SDD-005 §2 — the `tech` content kind and the diffusion-route gate (128–129) ·
SDD-006 — the duplicate `§3b` renumbered `§3c` (144) · SDD-007 §5b —
`OperationMass` / `IOperation.MassThisTick` (147) · SDD-008 §7 —
`IInformationValueModel` (147) · SDD-009 §5 — `IReserveBasedLending` (147) ·
SDD-011 — S011-3 closed · SDD-012 §5 — `ISouringModel` (147) · SDD-017 §1b —
`EngineCompositionRefused` (133).
| [SDD-015](SDD-015_ADVISOR.md) | Advisor — **reuses the objective AST** for triggers, closed selector vocabulary (no scripting trap), the judgement cap as engine constants, four-part bound reasoning | R25 | 🟦 drafted |
| [SDD-016](SDD-016_ENVIRONMENT_RUNTIME.md) | Weather — **daily AR(1) on the /30 grid** (unifying weather with segmentation), extremes as audited draws, **the forecast as the analytic AR(1) prediction** — its honesty is a theorem of the generator | R22 | 🟦 drafted |
| [SDD-017](SDD-017_HOST_SURFACE.md) | Host surface — the complete `IEngine` API, the read-model record tree (16 sections), **the path registry generated from it** that objectives and Advisor bind against, the audit query with pre-shaped loss reports | R21 | 🟦 drafted |
**The set is complete — verified, not asserted** (the first completeness claim
missed R21, R22 and R23's aggregates; finding 57): every build phase maps to an
SDD — R20 deliberately has none (it is tests and content over everything
already pinned). Task `Rn.0` is a *review* — confirm the phase's SDD still
matches the design set before coding.

| Phase | SDD | Phase | SDD |
|---|---|---|---|
| R1 | 001 (+000) | R13 | 009 |
| R2 | 002 §2–4 | R14 | 008 |
| R3 | 004 | R15 | 010 |
| R4 | 002 | R16 | 011 |
| R5 | 003 | R17 | 005 |
| R6, R7 | 003 | R18 | 012 |
| R8–R11 | 006 | R19 | 013 |
| R12 | 007 | R20 | — (tests + content) |
| R21 | **017** | R24 | 014 |
| R22 | 005 + **016** | R25 | 015 |
| R23 | **012 §4b** | | |

**The anti-hallucination stance, in one line:** the coder is never the author of
a contract, a constant, or a formula — only of their implementation, against a
pinned test (SDD-000 §8, rules F-1..F-4).

## 3. The template

Every SDD contains, in order:

1. **Scope** — which assembly/assemblies, which phase design doc it serves
2. **Contracts** — the actual signatures, with a one-paragraph rationale per
   non-obvious choice
3. **Data & algorithms** — layouts, worked algorithms, complexity notes
4. **Error surface** — which faults, which classes ([09](../design/09_DIAGNOSTICS.md) §5)
5. **Test plan mapping** — which design-doc verification IDs this satisfies and how
6. **Open items** — anything deferred, with its trigger

## 4. Rules

- An SDD **implements** its design documents; it never contradicts them. A
  conflict discovered while writing an SDD reopens the design doc (and lands in
  the [coherence log](../design/22_DESIGN_COHERENCE.md) §5) — it is not
  resolved quietly in the SDD.
- Signatures in a merged SDD are the review baseline: implementation may refine
  *internals* freely, but changing a **contract** reopens the SDD.
- The naming rules of [19_GLOSSARY](../design/19_GLOSSARY.md) bind here with
  full force — N3 especially: no `Manager`, `Helper`, `Service` in a contract.
