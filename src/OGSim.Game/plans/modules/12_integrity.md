> Source read in full: `src/OGSim.Composition/Modules.cs`, plus the types this
> module composes. Part of the module review requested 2026-08-23. Nothing in the
> engine was changed to produce this.


# 12 — integrity

`internal sealed class IntegrityModule()`

## Manifest

| | |
|---|---|
| **provides** | `IDegradationModel`, `IHazardModel`, `AssetIntegrity` |
| **requires** | `IAuditTrail`, `IRandomSource`, `SurfaceChain` |
| **ownsState** | `integrity.conditions` |
| **stages** | *(none)* |

The source states why there is no stage: **stage 4 belongs to the module that
builds the segment plan** (field), and integrity contributes the state it reads
rather than a second participant in the same slot.

## Compose

```
SeverityWeightedDegradation(new ContentId("standard"), Defaults.Decay)
ExponentialHazardModel(new ContentId("standard"), baseRatePerYear: 0.05, conditionExponent: 4.0)
AssetIntegrity(new IntegrityPass(degradation, hazard, Stream(Hazard), audit),
               element => new ContentId(chain.NameOf(element.Id)))
```

The chain names its own equipment, so an audit row reads "separator" rather than
"element 1000002" — and asking the element what it *is* would be the thing
design 04 §1 forbids.

## Functions and properties

**`AssetIntegrity`** (`OGSim.Integrity/AssetIntegrity.cs`)

| Member | |
|---|---|
| `ConditionOf(element)` | 0–1 |
| `HasFailed(element)` / `FailedCount` | |
| `NeedsRepair(element)` | |
| `Repair(element)` | restores condition — the only thing that does |
| `IsMonitored(element)` / `FitMonitoring(element)` | condition is **bought, not given**: new equipment arrives blind |
| `Advance(...)` | ages and rolls, returns `FailureOutcome`s |
| `Key` = `integrity.conditions`, `SchemaVersion` **2** | conditions survive a save — no laundering twenty years of neglect through the save menu |

Condition decays as `Δc = −baseRate · (1 + Σ severity) · Δt`, clamped at 0, and
is **never restored implicitly**. Failure is exponential —
`λ(c) = 0.05 · e^(4(1−c))` — not a threshold, so sitting just above a line buys
nothing.

## Dependencies and conditions it decides for itself

**None in `Compose`.**

## Static numbers found

`baseRatePerYear: 0.05` and `conditionExponent: 4.0` are written **inline** in
`Compose` — not `Defaults`, not content. These are the two numbers that decide
how often anything breaks.

## Content and Defaults consumed

`Defaults.Decay` — base rate 0.05/yr, water-cut factor 1.0, sour factor 2.0,
duty 0.5, temperature 1.5, service interval 0.2. **No content file.**
