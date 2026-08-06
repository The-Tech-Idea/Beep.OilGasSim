# Phase R3 — Content Pipeline

**Arc I · Foundation** · Status ⬜ · Depends on: R1, R2 · Enables: every later phase

---

## 0. Purpose

Make everything that is not a model into data, and make bad data impossible to
ship silently.

**This phase exists early, not late, for a specific reason:** if content loading
arrives after the domain is built, the domain gets built against hard-coded
values and the content layer becomes a translation of them. Building the pipeline
first means every domain phase from R5 onward is content-driven from its first
commit.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Every definition kind loads from data | The catalogues in [10](../design/10_CONTENT_AND_UNITS.md) §2 load, validate and index |
| G2 | Bad content never reaches the engine | Any validation failure prevents startup, with a complete report |
| G3 | Every failure in a batch is reported | A directory with five broken files produces five diagnostics, not one |
| G4 | Unknown keys are errors | A typo'd key fails the load; it does not become a default |
| G5 | Units are parsed and dimension-checked | `"3200 psi"` in a temperature field is a load error |
| G6 | Every reference resolves at load | Naming an unregistered material, model or technology is a load error |
| G7 | Mods use the identical path | No separate loader; no mod-only code path exists |
| G8 | There is exactly one copy of the content | Architecture/build check: no duplicated content directory |

---

## 2. Design decisions

### 2.1 Explicit type declaration

Every file declares its type in a top-level field. **No inference from shape.**

*Rationale:* type inference from which keys are present means a file matching two
shapes, or none, becomes a guess — and a guess in a content loader is a defect
that surfaces months later as missing content.

### 2.2 Inline unit syntax

**Decision: `"pressure": "3200 psi"`.**

Rejected alternative: `{"value": 3200, "unit": "psi"}`. The inline form is
dramatically more readable in a file with fifty physical values, and — the
deciding argument — **a missing unit is a visible omission** (`"pressure": 3200`
fails the parse) rather than a missing key that a lenient parser might default.

### 2.3 Unknown keys are hard errors

Not warnings. A misspelled key that is silently ignored produces a definition
that declares a setting nothing reads, which is one of the exact failure shapes
this design exists to eliminate.

**Corollary:** deprecating a field requires a migration, not tolerance. That is
the intended cost.

### 2.4 Load either fully succeeds or does not start the engine

No partial loads. No skipping the broken file and carrying on. A game missing
content it believes it has is worse than a game that refuses to start with a
clear message.

### 2.5 One content directory

**Decision: exactly one canonical content location, referenced by every consumer
including tests and tools.**

*Rationale:* two copies of anything drift, always, and the drift is silent. If a
host needs content at a different path, it is copied at build time by an explicit
step with a verification, never maintained by hand.

### 2.6 Model binding by name

Content names a model plugin (`"inflowModel": "darcy-vogel-composite"`). Binding
happens at load; **an unregistered name is a load error**, so a technology or
fluid system that names a model the engine cannot build fails immediately rather
than at the moment it is first needed, mid-game.

### 2.7 Mods

Same loader, same validation, same report. Additional rules only for
*composition*: declared load order, explicit override by id, and **two mods
overriding the same id without declared precedence is an error, not last-wins.**

---

## 3. Deliverables

| Deliverable | Contents |
|---|---|
| `OGSim.Kernel.Content` | Loader, six-stage validator, load report, `ICatalog<T>`, plugin binder |
| JSON Schemas | One per content type, shipped with the engine so editors validate live |
| `content/` | The canonical content directory; R3 ships materials, property kinds, rock types, fluid systems |
| Content tests | Round-trip, validation, and a fixture corpus of deliberately broken files |

**The loader is type-agnostic and must stay so.** All 27 content kinds in
[10_CONTENT_AND_UNITS](../design/10_CONTENT_AND_UNITS.md) §2 — including
`environment-profile`, `climate-profile`, `access-mode`, `hse-regime`, `barrier`,
`scenario`, `campaign` and `objective`, all added after R3 was first drafted —
load through this one path. **If a later phase needs a loader change to add a
content kind, R3's design is wrong.** That is the phase's real acceptance
criterion, and it is why R3 sits in Arc I rather than beside the phases that
consume it.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R3-V1 | Happy path | The shipped catalogues load and index correctly |
| R3-V2 | Complete reporting | A directory with N distinct faults produces N diagnostics, each naming file, path and reason |
| R3-V3 | Unknown key | Rejected with the key name and the nearest valid key |
| R3-V4 | Missing unit | Rejected |
| R3-V5 | Wrong dimension | A pressure supplied where a temperature is expected is rejected |
| R3-V6 | Dangling reference | Naming an unregistered material, model, technology or unit is rejected |
| R3-V7 | Duplicate id | Rejected |
| R3-V8 | Dependency cycle | Rejected, naming the cycle |
| R3-V9 | Out-of-range value | A porosity of 1.5 is rejected against the property kind's valid range |
| R3-V10 | Unbound model | Naming an unregistered plugin is rejected at load, not at use |
| R3-V11 | Mod override | Recorded in the load report; the override takes effect |
| R3-V12 | Mod conflict | Two undeclared overrides of one id is an error |
| R3-V13 | Schema currency | Every content type has a schema, and every shipped file validates against it |
| R3-V14 | Single copy | Build check finds exactly one content directory |

**R3-V13 prevents a specific drift:** a content type gaining a field without its
schema being updated, so authors get no validation on the new field.

---

## 5. Out of scope

Domain content — facility units, technologies, fiscal regimes and so on arrive
with the phases that consume them. R3 ships only the catalogues R2 needs, and the
machinery everything else will use.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Schema maintenance burden | R3-V13 makes a missing schema a test failure, so it cannot quietly lag |
| Strictness frustrates content authors | Diagnostics must be excellent: file, JSON path, what was expected, and the nearest valid alternative. Budget real effort here — it is the modding experience |
| Load time grows with content volume | Measure from R3; catalogues are built once at startup, so the budget is generous |
| Unit-string parsing ambiguity | A closed, declared unit vocabulary; an unrecognised unit string is an error, never a guess |
