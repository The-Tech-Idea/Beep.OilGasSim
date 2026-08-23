# 28 — The trees, and where their relations live

**Status:** proposed, 2026-08-23.
**Counted from the files**, not estimated:

| Tree | Nodes | Directory |
|---|---|---|
| activities | 25 | `content/activities/` |
| equipment | 88 | `content/equipment/` |
| technologies | 65 | `content/technologies/` |
| facilities | 19 | `content/facilities/` |

---

## 1. The graph as it stands

**227 distinct edges**, every one of them stored inside the node that declares it:

| Edge | Count | Field |
|---|---|---|
| activity → activity | 24 | `requires` |
| activity → equipment | 25 | `equipment` |
| activity → tech | 13 | `requiresTech` |
| equipment → equipment | 75 | `requiresEquipment` |
| equipment → tech | 31 | `requiresTech` |
| facility → tech | 11 | `requiresTech` |
| tech → tech | 48 | `prerequisites` |

And **49 more edges that are copies of edges above, written backwards**:

| Reverse copy | Count | Duplicates |
|---|---|---|
| `unlocks` on an activity | 24 | the 24 `requires` edges |
| `enables` on equipment | 25 | the 25 `equipment` edges |

## 2. Why that is a defect and not a convenience

The reverse copies have already drifted **twice** — the second found by the
migration itself. First `unlocks`: `gas-lift` and the compressor disagreed, and
the repair (recomputing `unlocks` from `requires`) was a convention nothing
enforced. Then `enables`: the migration's identity proof refused, because **23
of the 25 authored `enables` edges named different equipment** than the
activities' own `equipment` lists — authored against prop-flavoured kit
(`drilling-rig-derrick`, `well-testing-skid`, `wireline-service-truck`) where
the activity side names the operational units the closure and the costs use
(`drilling-rig`, `construction-crew`, `workover-unit`). The activity side is
the owner; the drifted edges were deleted with the disagreement recorded.

**And the loader immediately convicted the surviving side too** (the first
run of `CatalogueContentTests`): the activity lists named four operational
units — `drilling-rig`, `workover-unit`, `construction-crew`, `service-crew` —
that had silently vanished when the 88-node equipment tree was rebuilt from
the prop assets. Twenty-three edges dangled and no session script had noticed;
the loader refused each one by name on its first pass. The four units now
exist as equipment nodes (they are the game's own dispatch vocabulary), and
the prop kit remains what it was: scenery and specialist refinement.

One judgment in that drift is worth keeping for the balance pass: the
specialist kit really is the finer-grained gate (a buildup test wants the
testing skid, a log wants the wireline truck). Re-gating activities onto
specialist kit is a *content revision* to make deliberately, beside the W9
measurement — not something a mechanical migration may decide.

It also breaks **L5 — one owner per fact**. The edge "workover requires a
completed well" is one fact. It is currently written in two files, and a
designer editing one has no way to know about the other.

---

## 3. Nodes describe themselves; a relations file describes the graph

```
content/activities/<id>.json       cost, duration, what it does
content/equipment/<id>.json        cost, turns to build
content/technologies/<id>.json     unchanged — the engine owns `prerequisites`
content/facilities/<id>.json       unchanged — the engine owns `requiresTech`

content/relations/activity-requires-activity.json     24 edges
content/relations/activity-needs-equipment.json       25
content/relations/activity-needs-tech.json            13
content/relations/equipment-needs-equipment.json      75
content/relations/equipment-needs-tech.json           31
```

**Five files, not seven** — the migration found that `facility-needs-tech`
and `tech-needs-tech` (48) are read by the engine's own content contract
(`FacilityLadders.RequiresTech`, `TechnologyContentKind.Prerequisites`), so
those edges already have an owner and stay in their nodes: writing them a
second time in a relation file would be exactly the two-owner defect this
document exists to remove. `RelationGraph` (W7) exposes them read-derived, so
queries still see one graph.

### The 11 facility gates, reverted — and why

The 11 `requiresTech` values this review added to facility rungs turned out to
**move Oilfield Engineer**: the gate was real machinery no shipped content had
ever used, so switching it on refused installs 24 tests expect to be accepted
(`compressor-e1` demanding `reciprocating-compression` the test company never
researched, and so on). The acceptance criterion is absolute — Engineer does
not move — so the gates were reverted from the shipped files. The pairing
itself is good content for the **balance pass**, where the techs' availability
can be lined up with the eras so a gate opens when its rung does:

| Rung | Proposed gate |
|---|---|
| compressor-e1 | reciprocating-compression |
| export-line-e2 | high-strength-linepipe |
| export-line-e3 | inline-inspection |
| gas-plant-e1 | glycol-dehydration |
| gas-plant-e2 | ngl-extraction |
| heater-treater | produced-water-treating |
| heater-treater-desalter | scale-management |
| manifold-16slot | smart-completions |
| pump-station-e1 | flow-improvers |
| separator-3phase-e2 | multi-stage-separation |
| tank-farm-e2 | vapour-recovery |

A second lesson came free: the kernel's `ShippedContentTests` load facilities
**standalone**, so a facility→tech reference is dangling in that load even
though the engine's own path loads all six kinds together. Any future facility
gate must be introduced alongside that test's load set, not just the content.

Each authored file is one edge kind, a flat list:

```json
{
  "kind": "relation",
  "edge": "activity-requires-activity",
  "edges": [
    { "from": "workover", "to": "drill-development-well" }
  ]
}
```

**`unlocks` and `enables` disappear from the files entirely.** They are the
inverse of an edge that is already written down, so `RelationGraph` computes
them on load and the two cannot drift because there is only one.

### What this buys

| | Before | After |
|---|---|---|
| Add an edge | edit two files, remember the second | edit one line in one file |
| "What needs this technology?" | grep 197 files | one lookup |
| A whole edge kind off for a style | impossible — it is inside the nodes | do not load that relation file |
| Reverse drift | possible, and has happened | **not representable** |

The last row is the point. A defect you cannot express is better than a defect
you test for.

---

## 4. Loading — the trees join the loader the engine already has

*(Revised at implementation, 2026-08-23. The draft proposed a parallel
`ITreeStore<T>`; the reading found the engine already owns a six-stage content
loader with per-kind readers, reference resolution and refuse-by-name —
`IContentKind`, `ContentLoader` — and a second loading system would have been
the two-owner defect again. The trees join the existing one.)*

`src/OGSim.Kernel/CatalogueKinds.cs` —

- **`ActivityContentKind`** (`"activity"`), **`EquipmentContentKind`**
  (`"equipment"`): shape-validate the nodes; edges are not their business.
- **`RelationContentKind`** (`"relation"`): knows the five edge kinds and what
  each end is, declares every `from`/`to` as a typed `ContentReference` — so
  the loader's own stage 4 proves every edge resolves — and refuses duplicate
  edges, self-edges, and **cycles by name** within a same-kind edge file.
- **`RelationGraph`**: one queryable graph over what the loader accepted,
  carrying the DERIVED reverse views (`Unlocks`, `Enables`) that left the
  files after drifting twice.

Registered in all three load paths — `EngineBuilder`'s kind list,
`RepositoryContent.Kinds`, `GodotContentSource.Loadable` — so **every engine
build validates the catalogues at startup** and a broken reference refuses by
name:

```
relations/equipment-needs-tech.json $.edges[12].to [References] dangling reference 'tech:quantum-inversion'
```

**Facilities and technologies need no new store** — they always had real
content kinds; that discovery is what re-scoped §3 to five files.

### What a save carries

Trees are **content**: they are reloaded from disk, never written into a save.
A save carries only what the run changed — and at implementation this turned
out to be **already true**: held technologies are `capabilities` state,
commissioned rungs are `company.facility` state, running and completed
activities are `field.activities` state. Nothing new to wire; the clause
survives as the rule future catalogue state must follow:

- which equipment has been built
- which technologies are held
- which activities are complete
- which facility rungs are commissioned

That keeps a save small and — more usefully — means **content can be patched
without invalidating saves**, as long as ids survive. A save that stored the
tree would freeze a balance pass into every existing game.

---

## 5. Validation, as tests rather than a script

These five run as a script today. They become tests:

| Check | Today |
|---|---|
| every reference resolves | **0 broken links** across 4 trees |
| every activity reachable from a root | **0 unreachable** |
| the inverse is exactly the inverse | holds — and after §3 is unfalsifiable |
| every activity is enabled by some equipment | holds for all 25 |
| every activity is enabled by a style, or carries a written reason | Days enables **14 of 25**, and all 11 omissions are explained |

The last one is the one worth having. It is what stops a style quietly losing an
activity: an omission must be *written down*, not merely absent.

---

## 6. Order of work

Steps W6–W8 of the plan.

| Step | What | Risk |
|---|---|---|
| **W6** | **Done 2026-08-23.** 168 edges moved into `content/relations/` (5 files); 48 engine-owned tech edges stay in their nodes; `unlocks` and `enables` deleted (24 + 25 reverse copies, 23 of the latter drifted); the 11 facility gates reverted as they moved Engineer | Proven: every surviving edge identical before and after, reachability and the Days closure intact |
| **W7** | **Done 2026-08-23** — three content kinds + `RelationGraph` (`CatalogueKinds.cs`), five checks as `CatalogueContentTests` | Landed as §4 above describes |
| **W8** | **Done 2026-08-23** — the no-duplications pass: the activity catalogue is the ONE owner of every activity's five designer facts (`Defaults.*Terms` became catalogue-fed factories after a value-identity proof); the style files own only their selection, behind a `game-style` kind with `"*"` meaning every node; the relation kind's edge table is one row per fact | The value migration moved no test result — content already mirrored the engine exactly, which was the point |

The graph must be **provably identical** across W6 — same 227 edges, same
reachability, same per-style enablement — before W7 begins.
