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

## 8. Open items

| # | Item | Trigger |
|---|---|---|
| S013-1 | Audit sidecar rotation for very long games (size cap + oldest-summarised) | R19.4 |
| S013-2 | Save-diff tool (R19-V15) — ships as a dev utility over the canonical form; scope | R19.5 |
