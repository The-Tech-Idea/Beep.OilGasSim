# Phase R17 — Technology

**Arc III** · Status ⬜ · Depends on: R13, R16 · Enables: R20

---

## 0. Purpose

Make capability something the player acquires, and make acquiring it change the
physics rather than a percentage.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | No technology is a bare multiplier | Every effect swaps a model, extends an envelope, or unlocks an option — architecture-tested |
| G2 | Technology is pure content | Adding one requires no engine change |
| G3 | Acquisition has four routes | R&D, licence, service contract, diffusion — each with distinct economics |
| G4 | Technology has running costs | An unlocked capability carries an ongoing burden; a company can be over-teched for its size |
| G5 | Eras gate availability | A 1960s start genuinely lacks 3-D seismic |

---

## 2. Design decisions

### 2.1 Three effect kinds, and nothing else

Model swap, envelope extension, option unlock. **A "+x% output" effect is not
representable**, and an architecture test asserts the effect vocabulary contains
no multiplier kind.

*Rationale:* this is the whole design of the phase. The moment a percentage
multiplier is available, it becomes the path of least resistance for every new
technology, and the tech tree degenerates into a power curve.

### 2.2 Technologies select models; they do not contain behaviour

A technology names a registered plugin. If a technology needs new behaviour, that
behaviour is a **new model plugin** registered independently, which the
technology merely selects.

*Rationale:* keeps the tree fully moddable, and keeps "add a technology" from
ever meaning "edit the engine".

### 2.3 Four acquisition routes

R&D (sustained budget, long, cheapest per unit, you choose the direction);
vendor licence (immediate, per-use fee forever); service contract (priced into
the job, never owned); diffusion (free, very slow).

*Rationale:* makes technology a **procurement** decision rather than a
research-points decision. A small company rents; a major develops. That
difference is characterful and true.

### 2.4 R&D outcomes are probabilistic

Per open decision TD3: fund a domain, get outcomes with uncertainty. R&D that
reliably delivers exactly what was ordered is the least realistic part of most
tycoon games.

### 2.5 Era gating

Per open decision TD1: a campaign spanning decades, with technologies era-gated.
An early-era start is a genuinely different game — no 3-D seismic, no horizontal
drilling, no ESP in hostile service — and it costs only content metadata.

### 2.6 Technology and environment are symmetric

Both use the same three effect kinds
([07_TECHNOLOGY](../design/07_TECHNOLOGY.md) section 3.0a). That symmetry is the
entire hostile-setting progression: arctic technology **moves the envelope** the
arctic environment **restricted**; insulation **changes back** the hydrate
parameter a cold seabed **changed**.

**Implementation consequence:** the effect-application path is shared with
[R22](R22_ENVIRONMENT.md) and written once. R17 adds acquisition, prerequisites
and running costs — not a second effect system.

### 2.6b The catalogue gate — where tech meets equipment

R17 implements [07](../design/07_TECHNOLOGY.md) §4b: technology's option-unlock
effect kind targets **content entries**, so acquiring a tech makes its tiers
appear in the buy list, era gating hides tiers that do not exist yet, and the
service-contract route rents a gated tier per job at a premium.

Enforcement is at **command validation**: installing a tier whose `requiresTech`
the company lacks (and is not renting) is a command rejection with the tech
named — never a silent filter the player cannot understand.

### 2.6c `AllCapabilities` — the pre-R17 world is a real mode

Phases R5–R16 build and test activities and equipment before the technology
state exists. They compose with `AllCapabilities` — everything unlocked — which
is **not scaffolding**: it is the shipped sandbox all-tech modifier
([18](../design/18_GAME_MODES.md) §5). R17 then supplies the real capability
set and the gate switches on; R17-V14 proves the retrofit changed no physics,
only which commands validate.

### 2.7 Events this phase raises

`tech.available` · `tech.acquired` · `tech.outcome` · `tech.eraChanged`.
Technology takes effect at a tick boundary and therefore **never creates a
segment boundary**.

---

## 3. Deliverables

`OGSim.Company` extension: `ITechnology`, the three effect kinds and their
application, four acquisition routes, ongoing cost accrual, era gating, R&D
outcome model.
Content: the technology graph in [07](../design/07_TECHNOLOGY.md) §2, with
prerequisites, routes, costs and effects.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R17-V1 | No multipliers | Architecture test: the effect vocabulary contains no multiplier kind, and no content declares one |
| R17-V2 | Model swap | Unlocking 3-D seismic swaps the observation model; uncertainty reduction changes accordingly |
| R17-V3 | Envelope extension | Unlocking deep drilling raises the depth limit; operations previously rejected are now accepted |
| R17-V4 | Option unlock | Sweetening makes previously unsellable sour gas sellable |
| R17-V5 | Content-only addition | A new technology added purely as content works end to end |
| R17-V6 | Acquisition routes | Each of the four behaves as declared |
| R17-V7 | Running costs | Unlocked technologies accrue their ongoing burden |
| R17-V8 | Era gating | Era-gated technologies are unavailable before their era |
| R17-V9 | R&D outcomes | Probabilistic outcomes occur at declared rates; each is audited with its draw |
| R17-V10 | Prerequisites | A technology cannot be acquired before its prerequisites |
| R17-V11 | Catalogue gating | An ungated install command is rejected naming the missing tech; acquiring the tech makes the same command valid; era hides not-yet-existing tiers |
| R17-V12 | Rented tiers | The service route runs a gated tier at the declared premium without granting the tech; the rental appears in the operation's cost |
| R17-V13 | The datasheet is the effect | Two ESP tiers differing only in head curve produce exactly the flow difference the curves imply — no other pathway exists (architecture-tested: no tier multiplier field) |
| R17-V14 | Gating retrofit | Under `AllCapabilities`, every pre-R17 suite passes unchanged; under a restricted set, only command validation differs — never a solve result |
| R17-V15 | Detectability unlock | Acquiring an observation node makes a previously-invisible class spawn leads on re-screen ([R14](R14_INFORMATION.md)-V14 end-to-end) |
| R17-V16 | Envelope combination | `Min(Max(base, extensions), restrictions)` holds on an order-shuffled matrix of environment restrictions × technology extensions ([SDD-005](../sdd/SDD-005_CAPABILITIES_AND_EFFECTS.md) §4.1) |

**R17-V5 is the phase's real acceptance test:** if a new technology cannot be
added as content alone, the effect vocabulary is insufficient and must be fixed
rather than worked around.

---

## 5. Out of scope

Rival technology advancement (open decision TD2 — recommended, deferred to R20 if
schedule allows). Technology obsolescence (open decision TD4, declined for now).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| A technology genuinely needs an effect the three kinds cannot express | Then a *fourth kind* is designed deliberately, with a rationale — never a multiplier escape hatch |
| The graph is large and hard to balance | Effects are physical, so balance emerges from the physics rather than from tuned numbers; costs are the tuning surface |
| Era gating fragments testing | Tests declare their era explicitly; the default era is the modern one |
| R&D uncertainty frustrates players | Direction is chosen and progress is visible; only the specific outcome and timing are uncertain |
