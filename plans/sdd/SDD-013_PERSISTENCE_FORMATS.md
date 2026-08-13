# SDD-013 — Persistence Formats

**Status:** drafted · **Serves:** R19 · **Design docs:** [11](../design/11_PERSISTENCE.md), [R19](../phases/R19_PERSISTENCE.md), [09](../design/09_DIAGNOSTICS.md) §4.4

The on-disk truth: the container, the canonical JSON rules that make PV1
byte-exact, the digest, and — consolidated from every other SDD — **the list of
derived state that must never be saved.**

---

## 1. Container (PSD1 (c))

```text
save.ogsim = ZIP (deflate):
  manifest.json          header (§2)
  state/<module>.json    one canonical block per registered module, module-name order
  audit/summary.json     the retained trail (09 §4.4 policy)
  audit/full.jsonl       sidecar: the complete trail (DGD1 (c)) — excluded from digest
```

## 2. Header

`schemaVersion, engineVersion, contentVersion, activeMods[{id, version, order}],
worldSeed, tick, rngPositions{stream: ulong × 8}, moduleDigests{name: sha256},
stateDigest` — digest = SHA-256 over the canonical module blocks concatenated
in module-name order; per-module digests localise PV divergence (R19 risk
note).

## 3. Canonical JSON — the PV1 rules

```text
· UTF-8, no BOM, LF, no trailing whitespace
· object keys ordinal-sorted; arrays ordered by entity id (D-5)
· doubles: shortest round-trip ("G17"-equivalent, invariant) — never localised,
  never fixed-point; NaN/Inf are unrepresentable (they were faults upstream)
· Money as integer cents; ids as strings (SDD-004 §6 — ordinals never persist)
· timestamps as (year, month) records — no date strings to parse ambiguously
Writer and reader live in ONE class; there is no second serialisation path to
drift (the L5 principle applied to bytes).
```

## 4. Derived — never saved (consolidated)

The continuation-identity failure class (PV2) is "restored as a value, not as a
live dependency". The inverse trap is saving derived state that then shadows
its source. **The authoritative never-save list**, gathered from every SDD:

| Derived | Rebuilt from | Source |
|---|---|---|
| Catalogue ordinals | id-sorted content | SDD-004 §6 |
| `EffectState` | tech nodes + profiles | SDD-005 §6 |
| Segment plans | availability at stage 4 | SDD-013/R19 §2.8 |
| Barrier strengths | condition + competency + procedure | 14 §2.2 / INV10 |
| Perforation standoff | trajectory + contacts | SDD-003 §5 |
| Environment profiles (generated worlds) | the surface layers | 06 §5.1a step 9.8 |
| Reserves, RRR, borrowing base | beliefs + plans + prices | SDD-009 §4–5 |
| Read model | everything | R21 |

A module attempting to register a state key for any of these fails the
**derived-state review** — enforced as a checklist item on R19's PV4 test plus
this table (a new mechanical check for [22](../design/22_DESIGN_COHERENCE.md)
§6.1: every never-save row names its rebuild source).

## 5. Migrations

```csharp
public interface IMigrationStep { int From { get; } JsonNode Migrate(JsonNode block, string module); }
```

Chain composition v→v+1; every step ships with a real fixture save of version
`From` (PV5). A gap in the chain = composition fault at startup. Saves from
newer versions: refused by header check with both versions named.

## 6. Corruption and refusal (PV6)

Refusals are specific: digest mismatch names the module whose block digest
diverged; missing mod names the mod and version; truncated zip reports the
entry. **No partial load exists as a code path** — `LoadResult` is
`Loaded | Refused(reasons)`, mirroring the content loader's shape.

## 7. Test mapping

PV1 (canonical rules §3) · PV2 (never-save table §4 — each row gets a targeted
continuation test) · PV3 (digest across the CI matrix) · PV4 (+ derived-state
review) · PV5 (fixtures per step) · PV6 (§6 specificity) · PV7 (SDD-010 §1) ·
PV8 (digest sensitivity) · R19-V9..V15 as specified in the phase doc.

> **R20d.12 review (finding 188) — every part of a save exists and nothing
> assembles them, and the reason it stayed invisible is that the missing piece
> is the only one no unit test can stand in for.**
>
> **What is built and correct.** `StateBlock` captures an owner into a flat
> ordinal-sorted block and stamps the schema version itself so an owner cannot
> forget it. `CanonicalJson` implements §3. `SaveFile.Digest` does §2's
> per-module SHA-256 in module-name order, `SaveFile.Validate` does §6's
> all-reasons-at-once refusal, and `MigrationChain` does §5 including the
> gaps-are-a-startup-fault rule. `StateRegistry.Owners` returns owners in
> state-key order **for exactly this walk** — its own comment says capture and
> restore walk this sequence. `IRandomStream` carries `Position`/`Seek` "saved
> and restored exactly", and `SimulationClock.RestoreTo` exists and says it is
> for load. Nine state owners implement `Capture`/`Restore`.
>
> **What is missing is the walk itself.** Nothing in `src/` calls
> `StateBlock.Capture`, builds a `SaveHeader`, or writes a container — so every
> one of those parts is verified by a unit test of itself and the composition of
> them by nothing. A save is not partially wired; it is absent, and R20d.25's
> imported-water history and R20d.26.4's monitoring kits are the newest facts
> riding on it.
>
> **Finding 188 names two gaps of very different cost, and they separate
> cleanly.** `IEngine.ReadModel` is SDD-017 §2's fifteen-projection `ReadModel`;
> composition publishes `FieldReadModel`, which draws 9 fields from 5 of the 16
> projections because the other eleven have no source until R20d wires their
> subsystems in. **So `IEngine` cannot be implemented today for reasons that have
> nothing to do with saving** — it is blocked on R21.6, and pretending otherwise
> would mean fabricating eleven views. **`WriteSave` is blocked on none of it**:
> a save needs the state owners, the RNG positions, the tick and the container,
> all of which exist. The save path is therefore built against composition's
> `Engine` now, and adopting `IEngine` waits for the read model it names.
>
> **Load composes a NEW engine** (SDD-017 §1b, and PV2's continuation-identity
> rule): build the module set from the header's seed, then restore each owner
> into it. Restoring into a live engine would be mutating a graph whose
> dependencies were wired against the old values, which is precisely the
> "restored as a value, not as a live dependency" failure §4 opens with.
>
> **Two header fields have no honest source yet** and are declared rather than
> invented: `engineVersion` and `contentVersion` are constants until there is a
> release process to stamp them, and `activeMods` is empty because no mod system
> exists. Each is a real value with a stated provenance, not a placeholder
> standing in for work (L3) — and a save that refused to name its versions would
> be worse than one that names honest ones.

> **R20d.12 review, second half (finding 194) — the read side needs two things
> this engine does not have, and design 11 §2.1 already specifies both.**
>
> **1. A restore order that is not the capture order.** `StateRegistry.Owners`
> returns owners in STATE-KEY order, and its own comment gives the reason:
> composing modules differently must not change a byte of the save. That is
> correct for capture and wrong for restore. Design 11 §2.1: *"Modules declare
> their restore dependencies (facilities before the pipelines that connect them;
> reservoirs before the perforations that drain them). The registry topologically
> sorts them, and a cycle is a composition error caught at startup, not a
> mysterious load failure."* **Two different orders over one registry**, and the
> engine currently has one.
>
> **2. A loader that rebuilds the field.** `WellsState.Capture` records which
> completions are open and what each drains, and says the completion's own
> configuration — tubing, choke, lift — *"is CONTENT, restored by the loader
> rather than copied into every save"*. **That loader is the missing piece.** The
> save carries the decision (which wells, draining what); content carries the
> design; and nothing yet puts the two together, so `Restore` treats the record
> as a checksum against a rebuild nobody performs.
>
> **What a rebuild has to do, from `FieldControl.OpenWell`:** take a header slot,
> route the trunk and place the manifold if it is the first tie-in, open the
> completion, register the abandonment obligation, lay the gathering line at that
> field's distance from the header, and connect both ends into the network. All
> of it is already written — a rebuild wants that path with the SAVED id instead
> of `NextWellId()`, not a second one beside it (L5).
>
> **Which fixes the ordering constraints in place:** world and subsurface must
> restore BEFORE the wells are reopened, because the run's length is read from
> where the field is; and the obligation registry must restore AFTER, because
> reopening registers an obligation and the save's own record is the one that
> should stand. Those are exactly the "declared restore dependencies" §2.1 asks
> for, discovered by trying to write the load rather than by reasoning about it.
>
> **Not built ahead of need.** The topological restore order has no caller until
> the rebuild exists, and shipping it first would be one more mechanism joined to
> nothing — which is the defect this project has now recorded fourteen times.
> They land together or not at all.

## 8. Open items

| # | Item | Trigger |
|---|---|---|
| S013-1 | Audit sidecar rotation for very long games (size cap + oldest-summarised) | R19.4 |
| S013-2 | Save-diff tool (R19-V15) — ships as a dev utility over the canonical form; scope | R19.5 |
| S013-3 | `engineVersion` / `contentVersion` stamped from a release process rather than declared as constants | a build pipeline |
| S013-4 | The audit sidecar (§1's `audit/`) — the container ships state and header first; the trail is its own task with its own retention policy | R19.4 |
| S013-5 | **The rebuild + declared restore order** — reopen saved completions through `FieldControl` with their saved ids, and give `StateRegistry` design 11 §2.1's topologically sorted restore order beside its key-ordered capture. The two land together; neither is useful alone | R20d.12 |
| S013-6 | The EPOCH is not in the header, so a save's dates depend on the `EngineSettings` it is loaded with. Harmless while one scenario ships; a relabelling bug the moment two do | a second scenario |
| S013-7 | **§4 splits state into stored and derived, and reservoir pressure is neither**: it is derived from a PATH. `RestoreTo` recomputes it from the cumulatives in one unbounded solve while the engine reaches it through hundreds of solves each clamped by `MaxTickPressureDropFraction`, so the two agree only where the clamp never bound. Decide between storing the pressure and making the restore walk the bounded path — and add the third column to §4, because every other quantity reached by a limited step has the same problem (finding 196) | R20d.12 |
