# Phase R24 — Objectives, Challenges and Missions

**Arc IV · Executes before R20** · Status ⬜
Depends on: R13, R16, R23 · Enables: R20

---

## 0. Purpose

One objective system underneath five modes — sandbox, mission, challenge,
scenario and campaign — so that "add a challenge" is authoring content, never
writing engine code.

**This executes before R20** because R20's scenario suite and tutorial ladder are
built *on* this system. Building scenarios first would mean building them twice.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | No mode-specific engine code | Architecture test: scenarios reference only content and the command surface (GM1) |
| G2 | **Objectives observe and never influence** | A run with objectives and one without produce identical simulation digests (GM5) |
| G3 | Objectives cannot see truth | Architecture test: predicates cannot reference truth types (GM4) |
| G4 | Composition works | `all-of`, `any-of`, `sequence`, `count-of-N`, `sustained-for`, `never` (GM3) |
| G5 | Scoring is multi-dimensional | Eight dimensions computed independently; the composite is a declared function (GM9) |
| G6 | Campaign persistence is declared | Declared state carries between chapters; undeclared state does not (GM10) |

---

## 2. Design decisions

### 2.1 Objectives evaluate at tick stage 12, against sealed state

They read the tick's sealed state and its sealed event set. **They do not
subscribe to the event bus** ([16_EVENT_MATRIX](../design/16_EVENT_MATRIX.md)
§1) and they cannot issue commands.

*Rationale:* G2. An objective system that can influence the simulation
invalidates every determinism and replay guarantee, and creates order-dependent
behaviour of exactly the kind the architecture forbids.

### 2.2 Predicates read the read model, not internals

The same projection the host reads. This gives G3 for free — the read model is
built from beliefs and contains no truth
([R21](R21_HOST.md) §2.3) — and it guarantees that anything an objective can
test, a player can see.

**That second property is a design rule worth stating:** an objective the player
cannot verify progress on is an unfair objective.

### 2.3 `sustained-for` is a first-class combinator

"Maintain plateau for 24 months" tests operational management; "reach 50,000
bopd" tests a single peak. The former is a much better objective, and it needs
engine support to be cheap.

### 2.4 Failure conditions are objectives too

A `never` objective — never exceed an emissions cap, never have a serious
incident, never let cash go negative. Same evaluation path, same events.

### 2.5 Scoring is never a single number alone

Eight dimensions ([18_GAME_MODES](../design/18_GAME_MODES.md) §4). A composite
ranking is offered and **the dimensions are always shown**, because two players
with equal composites who got there by opposite routes should be able to see
that.

**Design intent:** a player can "win" on cash while scoring badly on reserves,
recovery and legacy, and the game says so plainly.

### 2.6 Modifiers reuse existing mechanisms

Fidelity levels, model selection, content sets — no modifier is a bare difficulty
multiplier, enforced the same way as the technology rule
([R17](R17_TECHNOLOGY.md) §2.1).

### 2.7 Campaign persistence is an explicit declaration

A campaign declares exactly what carries between chapters. Anything not declared
does not carry.

*Rationale:* the alternative — carrying everything by default — makes chapter
boundaries unpredictable and makes a chapter untestable in isolation.

---

## 3. Deliverables

`OGSim.Objectives`: `IObjective`, the predicate vocabulary across nine domains,
six combinators, deadline and expiry handling, failure conditions, progress
events, the eight scoring dimensions and composite, `IScenario` and `ICampaign`
loading, persistence declaration, branching, modifier application.
Content: scenario and campaign schemas.

---

## 4. Verification

The GM1–GM13 suite from [18_GAME_MODES](../design/18_GAME_MODES.md) §7, plus:

| # | Test | Passes when |
|---|---|---|
| R24-V14 | Predicate/read-model parity | Every predicate reads only fields present in the read model |
| R24-V15 | No command capability | Architecture test: the objectives assembly cannot reference the command bus |
| R24-V16 | Deterministic scoring | Identical play produces an identical score across platforms |
| R24-V17 | Chapter isolation | Each campaign chapter is playable standalone from its declared starting state |
| R24-V18 | Stage placement (I-V4, I-V5) | Objectives are evaluated only at stage 12, and read no state produced after it |
| R24-V19 | Per-objective audit trail (SDD-014 §3's R24.5 amendment) | An `objective.*` event is recorded the tick an individual objective settles, distinct from and ahead of the scenario's combined-verdict entry; a tick that only re-confirms an already-latched objective records nothing new |

**GM5 is the phase's gate.** If a run with objectives diverges from one without,
the objective system has reached into the simulation and the layering is broken.

---

## 5. Out of scope

The twelve missions and the scenario suite themselves — those are R20 content
built on this system. Online leaderboards (GM-D2, deferred). An in-game scenario
editor (GM-D6, deferred; the content format is already its data model).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| The predicate vocabulary is insufficient for a wanted objective | Vocabulary is extended deliberately, with the read model extended alongside so player-visibility is preserved |
| Objectives creep into influencing the simulation | GM5 and R24-V15 are architecture-level; the assembly literally cannot reference the command bus |
| Scoring dimensions are gameable | PD4 in [20](../design/20_PLAYER_DECISIONS.md) — each dimension needs at least two winning strategies |
| Campaign branching becomes combinatorially unmanageable | Branch on a small declared outcome set per chapter, not on arbitrary state |
