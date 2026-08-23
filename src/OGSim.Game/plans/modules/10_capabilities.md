> Source read in full: `src/OGSim.Composition/Modules.cs`, plus the types this
> module composes. Part of the module review requested 2026-08-23. Nothing in the
> engine was changed to produce this.


# 10 — capabilities

`internal sealed class CapabilitiesModule(IReadOnlyList<TechnologyNode> registry, EraCalendar eras, SimulationClock clock)`

## Manifest

| | |
|---|---|
| **provides** | `IGatingValidator`, `ICapabilitySet`, `IEffectState`, `CapabilityState`, `EffectState` |
| **requires** | *(none)* |
| **ownsState** | `capabilities.technology` |
| **stages** | `Company` order 1 |

The **concrete `EffectState` is provided beside the interface** because
environment and technology both apply through `EffectState.Apply`, which is
deliberately not on `IEffectState` — so the module that applies weather resolves
the object this one built rather than a second instance.

There is **no acquire-technology command anywhere in the engine.**

## Compose

```
Provide<IGatingValidator>(new GatingValidator())
CapabilityState(registry, eras, () => clock.Date, clock.Epoch)  -> owned, provided
Provide<ICapabilitySet>(state.Technology)
EffectState(new Dictionary<EnvelopeKind, double>())             -> provided twice
Contribute(order: 1, new DiffusionStage(state, effects))
```

## The stage

**`DiffusionStage`** — stage 11, order 1. Two calls:
`ApplyDiffusion(Era, EraStart, tick)` then `effects.Apply(ActiveEffects())`.

**Stage 11 and not stage 2**, and the source states why: a node diffusing this
month must not reach stage 4's segmentation the same month — *"technology never
creates a segment boundary."*

No dice are rolled, so a technology arrives in the same month of every game with
the same start date and a player can plan against it.

## Functions and properties

**`TechnologyState`** (`OGSim.Capabilities/TechnologyState.cs`)

| Member | |
|---|---|
| `Has(TechnologyId)` | the gate |
| `MaxDetectClass` | derived from the observation nodes held — the concrete link between technology and what a survey can see |
| `Acquired` | in acquisition order, so a save replays them through the graph that authorised them |
| `Acquire(tech, currentEra)` | **throws** if the node's era has not arrived |
| `ApplyDiffusion(era, eraStart, now)` | grants any node whose era has started, whose lag has elapsed, whose prerequisites are held, **and whose routes include `Diffusion`** |
| `ActiveEffects()` | what the holdings grant |

**`AllCapabilities`** — `Has => true`, `MaxDetectClass => D3`. The source calls
this **"a shipped mode, not scaffolding"**: the sandbox all-tech modifier. It
holds no acquisitions, so a build using it could not declare
`capabilities.technology`.

## Dependencies and conditions it decides for itself

**None in `Compose`.**

## Content and Defaults consumed

`content/technologies/*.json` — **65 nodes**, each with `availableFrom`,
`diffusionLagTicks`, `prerequisites`, `routes`, `effects`, `grantsDetectClass`.
`Defaults.Eras` — E1 1950, E2 1970, E3 1990, E4 2010.
