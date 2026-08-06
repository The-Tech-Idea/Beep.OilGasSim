# Catalogue — equipment, activities and their gates

One sheet per station of the chain: the **visible equipment** the player buys,
its **tier ladder**, its **tech gate** (from [TECH_TREE](TECH_TREE.md)), era,
cost band, install operation and what its datasheet changes. These sheets are
the **authoring spec** for the content files of
[10](../design/10_CONTENT_AND_UNITS.md) §2 — content JSONs are written *from*
them, and a sheet row without a content file (or vice versa) is a coherence
failure.

| Sheet | Station | Chain position |
|---|---|---|
| [TECH_TREE](TECH_TREE.md) | **The gate registry** — every node, era, prereq, route, and what it opens | — |
| [C01](C01_EXPLORATION_AND_SURVEYS.md) | Exploration & surveys | before the chain |
| [C02](C02_DRILLING_AND_RIGS.md) | Drilling & rigs | creates wellbores |
| [C03](C03_COMPLETION_AND_STIMULATION.md) | Completion & stimulation | 04 stage 1 |
| [C04](C04_WELLBORE_AND_TUBING.md) | Wellbore & tubing | 04 stage 2 |
| [C05](C05_ARTIFICIAL_LIFT.md) | Artificial lift | 04 stage 2 |
| [C06](C06_WELLSITE_AND_GATHERING.md) | Wellsite & gathering | 04 stage 3 |
| [C07](C07_SEPARATION_AND_OIL_TREATING.md) | Separation & oil treating | 04 stages 4–5 |
| [C08](C08_GAS_PROCESSING.md) | Gas processing | 04 stage 6 |
| [C09](C09_WATER_HANDLING_AND_INJECTION.md) | Water handling & injection | 04 stage 7 |
| [C10](C10_STORAGE_AND_METERING.md) | Storage & metering | 04 stages 8, 10 |
| [C11](C11_PIPELINES_AND_STATIONS.md) | Pipelines & stations | 04 stage 9 |
| [C12](C12_TERMINALS_AND_EXPORT.md) | Terminals & export | 04 stage 10 |
| [C13](C13_POWER_AND_UTILITIES.md) | Power & utilities | cross-cutting |
| [C14](C14_MONITORING_AND_INTEGRITY.md) | Monitoring & integrity | cross-cutting |
| [C15](C15_CONSUMABLES_AND_TREATMENTS.md) | Consumables & treatments | slot-assigned, cross-cutting ([07](../design/07_TECHNOLOGY.md) §4b.3b) |
| [C16](C16_TERRAIN_CLASSES.md) | Terrain classes | the world surface — world facts, not unlockables (no tech gates); sea = elevation < 0 |

**Consistency rules** (mechanically checkable, [22](../design/22_DESIGN_COHERENCE.md) §6.1):
every `Tech gate` in a sheet exists in the TECH_TREE registry; every registry
node is referenced by at least one sheet, an activity in
[07](../design/07_TECHNOLOGY.md) §2c, or a detectability/access class; era and
prereq claims agree between sheet and registry.
