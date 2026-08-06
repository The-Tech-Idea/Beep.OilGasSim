# Phase R15 — World Generation

**Arc III** · Status ⬜ · Depends on: R5, R14 · Enables: R16, R20

---

## 0. Purpose

Build the world the player explores: a deterministic, geologically coherent set
of basins whose structure rewards learning.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Deterministic from a seed | Regeneration reproduces the world exactly (PV7) |
| G2 | Geologically coherent | Correlations are structural, produced top-down, not sprinkled on |
| G3 | Realistic size distribution | Log-normal: many small accumulations, few large ones (MB5) |
| G4 | Realistic success rates | Wildcat POS lands in 10–35% across a basin (MB4) |
| G5 | Always a viable path | Not necessarily easy, but never unwinnable from the start |
| G6 | Regional patterns are learnable | Depth/maturity trends produce oil-prone and gas-prone areas a player can work out |

---

## 2. Design decisions

### 2.1 Top-down causal generation

The eleven-step pipeline in [06](../design/06_WORLD_AND_EXPLORATION.md) §5.
**Each step's output is the next step's input**, so correlations are causal rather
than imposed.

*Rationale:* a world where prospects are drawn independently and then "correlated"
by a fudge factor produces patterns that do not survive inspection. A world where
maturity is computed from burial history *actually has* a maturity trend, and a
player who deduces it is genuinely right.

### 2.2 Burial and thermal history is the pivot

Step 3 decides whether a source rock generated oil, gas or nothing, and it varies
with depth across a basin. This produces the single most valuable learnable
pattern in the game: **deep flank is gas-prone, shallow margin is oil-prone, very
shallow edge is barren.**

### 2.3 Traps are found, not placed

Structure is generated; traps are the places where a closure and a seal coincide.
Charge is then computed — which traps the migrating hydrocarbons actually
reached.

*Rationale:* this produces authentic disappointments. A perfect structure with no
charge is a real and common outcome, and it is the kind of failure that teaches
the player about migration.

### 2.3b Era layering — every tier has something to find

Step 8 assigns each accumulation its detectability and accessibility classes
([06](../design/06_WORLD_AND_EXPLORATION.md) §2.3), and generation **guarantees
a banded distribution across classes per basin** — so a mature basin holds
D1/D2/D3 yet-to-find behind the tiers, and shelved Tight/HPHT discoveries wait
on their access unlocks. Without the bands, tuning could silently strand an
era of the campaign with nothing to open.

### 2.4 Plays are derived, not authored

After accumulations exist, prospects are grouped into plays by shared source,
reservoir unit and trap style. **The correlation structure R14 depends on is a
consequence of generation**, not a separate authored layer that could drift from
it.

### 2.4b The surface sub-pipeline is causal too

Step 9 runs the eight sub-steps of [06](../design/06_WORLD_AND_EXPLORATION.md)
§5.1a in order — terrain → hydrology → climate → settlements → transport →
utilities & third-party industry → land status → **profile derivation**. The
same top-down argument as the geology: settlements *derived from* coasts and
rivers produce a believable world; settlements sprinkled at random produce a
backdrop. And profiles being **derived views** of the generated surface (9.8)
is an L5 matter: one source of truth for "what is at this location".

### 2.5 Initial beliefs are deliberately coarse

The player starts with regional knowledge only: basin outlines, a rough sense of
prospectivity, publicly available well data if any. Everything else is bought.

### 2.6 Viability is verified, not assumed

After generation, the generator runs a check: does at least one economically
viable development exist within the starting player's reach? A world failing the
check is regenerated with a derived seed, and **the regeneration is recorded** so
determinism is preserved and the event is auditable.

---

## 3. Deliverables

`OGSim.World`: `IWorldGenerator` and the eleven-step pipeline, tectonic settings,
stratigraphy, burial/thermal history, structure, traps, migration and charge,
accumulation generation, play/prospect derivation, surface generation,
jurisdictions, initial beliefs, viability check.
Content: `basin-archetype` catalogue, `jurisdiction` catalogue.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R15-V1 | Determinism (PV7) | The same seed reproduces the world exactly, on every platform |
| R15-V2 | Stream isolation | Adding a draw in another subsystem does not change generated worlds |
| R15-V3 | Size distribution (MB5) | Accumulation sizes are log-normal with realistic parameters |
| R15-V4 | Success rate (MB4) | Drilling every prospect in a basin yields a 10–35% success rate |
| R15-V5 | Maturity trend | Gas-prone deep, oil-prone shallow, barren at the margin — a detectable, consistent trend |
| R15-V6 | Play coherence | Prospects in a play share source, reservoir unit and trap style |
| R15-V7 | Charge realism | A meaningful fraction of valid traps are uncharged |
| R15-V8 | Viability | Every generated world passes the viability check; failures regenerate deterministically |
| R15-V9 | Surface coherence | Rivers reach the sea or a sink; every port has a harbour with generated depth; the transport network is connected; no settlement violates its siting rules; terrain is consistent with the tectonic setting |
| R15-V9b | Profile derivation | Every location's environment profile is derived from the generated surface (9.8) — a surface edit changes the profile, and there is no authored profile beside it |
| R15-V9c | Computed remoteness | Remoteness equals network distance over the generated infrastructure; adding a generated road changes it |
| R15-V9d | Settlement evolution | Sustained regional employment grows the nearest settlement at the declared slow rate; abandonment reverses it; determinism holds |
| R15-V10 | Belief initialisation | Starting beliefs are coarse and do not encode truth beyond what regional data would give — **including zero information about above-tier accumulations** |
| R15-V11 | Era layering | Class distribution per basin sits inside the declared bands; every tier unlock opens non-trivial yet-to-find in at least the mature basin |

**R15-V10 is a leak test**: if starting beliefs are too accurate, the exploration
game is short-circuited at generation time.

---

## 5. Out of scope

Licence rounds and rivals (R16). Real-world geography (open decision W5,
declined). Offshore (open decision D3, deferred).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Generated worlds are boring or samey | Basin archetypes provide structural variety; band tests constrain realism without constraining character |
| The pipeline is expensive | It runs once per new game; a budget of several seconds is acceptable. Benchmark it |
| Viability checking biases the distribution | Log the rejection rate; if it is high, the generator's parameters are wrong and should be fixed rather than filtered |
| Tuning eleven coupled steps is hard | Each step is independently testable with fixed inputs; band tests catch end-to-end drift |
