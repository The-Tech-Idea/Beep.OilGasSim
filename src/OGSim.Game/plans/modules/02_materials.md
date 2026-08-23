> Source read in full: `src/OGSim.Composition/Modules.cs`.
> Part of the module review requested 2026-08-23. Nothing in the engine was
> changed to produce this — it records what is there.


# 02 — materials

`internal sealed class MaterialsModule(RealityProfile profile, IReadOnlyList<FluidSystemDefinition> fluidSystems)`

## Manifest

| | |
|---|---|
| **provides** | `IFluidPropertyModel`, `IMaterialCatalog`, `FluidSystems` |
| **requires** | *(none)* |
| **ownsState** | *(none)* |
| **stages** | *(none)* |

`requires: []` is load-bearing: this module sits at the bottom of the dependency
graph. Five modules require `IFluidPropertyModel` and nothing it needs comes
from any of them.

## Compose

1. `MaterialCatalogue(Defaults.Materials)` — the nine materials
2. A `PluginRegistry` with **two** fluid models registered by name:
   - `black-oil-correlations` → `BlackOilModel(Defaults.Fluid, Defaults.Validity)`, bound
   - `arcade-fluid` → `ArcadeFluidModel(Defaults.Fluid, Defaults.CompletionBo, Defaults.Validity, catalogue)`
3. **The profile picks**: `profile.Selected(Defaults.FluidSlot)`
4. A **per-build** dictionary of one `BlackOilModel` per declared fluid system,
   provided as `FluidSystems`

## This is the one real plugin slot in the build

`Defaults.FluidSlot` is the only `ModelSlot` that currently varies, and it is
how `arcade` differs from `simulation`. Everything else design 03 §3.2 lists as
replaceable is constructed directly.

## Functions and properties

- **`FluidSystems`** — `ByContentId : IReadOnlyDictionary<ContentId, IFluidPropertyModel>`.
  Structural equality, because a record carrying a collection compares it by reference
- **`InputsOf(FluidSystemDefinition) -> BlackOilInputs`** (private) — API gravity, gas
  specific gravity, reservoir temperature, solution GOR. `Form` is **not** read
  from content: `OGSim.Kernel` may not depend on `OGSim.Contracts` where
  `FluidForm` lives
- **`Bound(BlackOilModel, IMaterialCatalog)`** (private) — the deferred second half
  of construction. `BlackOilModel.SplitAt` asks the catalogue what phase a
  material is at standard conditions, and neither can be built first

## Dependencies and conditions it decides for itself

| Where | Condition | Note |
|---|---|---|
| `Compose` | `profile.Selected(FluidSlot) is ContentId chosen ? plugins.Bind(chosen) : Bound(new BlackOilModel(...))` | A **fallback** when the slot is unnamed. Defensible — the simulation profile is deliberately the empty bundle — but it is a default dependency in the shape law L2 warns about |

## Content and Defaults consumed

`Defaults.Materials` (nine, hardcoded), `Defaults.Fluid`, `Defaults.Validity`,
`Defaults.CompletionBo`, `Defaults.FluidSlot`.
`content/fluid-systems/*.json` (2 files) — **read**.
`content/materials/*.json` (9 files) — **present and inert**; the live list is
`Defaults.Materials`.
