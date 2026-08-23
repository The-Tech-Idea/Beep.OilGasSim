# Module review — index

Sixteen modules compose the engine (`EngineBuilder.ShippedModules`). Each has a
document here recording its manifest, what `Compose` builds, the stages it
contributes, and — the point of the review — **every dependency and condition
it decides for itself**.

| # | Module | Stages | Owns | Commands |
|---|---|---|---|---|
| 01 | [diagnostics](01_diagnostics.md) | — | — | — |
| 02 | [materials](02_materials.md) | — | — | — |
| 03 | [subsurface](03_subsurface.md) | 1 | 1 | — |
| 04 | [wells](04_wells.md) | — | 1 | — |
| 05 | [flow](05_flow.md) | — | — | — |
| 06 | [facilities](06_facilities.md) | — | 1 | — |
| 07 | [world](07_world.md) | — | 1 | — |
| 08 | [company](08_company.md) | 1 | 4 | — |
| 09 | [information](09_information.md) | 1 | 3 | — |
| 10 | [capabilities](10_capabilities.md) | 1 | 1 | — |
| 11 | [environment](11_environment.md) | 1 | 1 | — |
| 12 | [integrity](12_integrity.md) | — | 1 | — |
| 13 | [hse](13_hse.md) | 2-3 | 1 | — |
| 14 | [field](14_field.md) | 9-10 | 11 | **31** |
| 15 | [operations](15_operations.md) | — | — | — |
| 16 | [objectives](16_objectives.md) | — | — | — |

**Two modules do nothing at all** — `operations` and `objectives` both declare
nothing, own nothing, contribute nothing, and their `Compose` is a null check.

**One module is most of the engine** — `field` fills nine or ten stage slots,
owns eleven state keys and registers all thirty-one commands.

The conditions found across all sixteen are collected in
[90_CONDITIONS.md](90_CONDITIONS.md), which is the input to the proposed
dependency manager.
