# SDD-010 — World Generation

**Status:** drafted · **Serves:** R15 · **Design docs:** [06](../design/06_WORLD_AND_EXPLORATION.md) §5, [R15](../phases/R15_WORLD.md)

The eleven-step pipeline as pure functions of the seed, with the algorithms an
implementer would otherwise choose silently: the noise source, the fill-spill
charge algorithm, settlement scoring, road routing, and the class-quota
resampling that guarantees era layering.

---

## 1. Determinism structure

```text
Each pipeline step draws from its OWN substream:
  stepSeed = SplitMix64( worldgenStreamSeed ^ Hash(stepName) )
so editing step 7 cannot shift step 9's draws — the 11 §3.1 stream-independence
principle applied INSIDE world-gen. PV7 (regeneration identity) follows.
```

## 2. Geology steps (1–8), pinned choices

| Step | Algorithm |
|---|---|
| 1 Tectonic | Basin archetype per region from content weights (one draw each) |
| 2 Stratigraphy | Layer-cake: unit thicknesses ~ LogNormal per archetype table; source/reservoir/seal roles per unit from archetype |
| 3 Burial & thermal | Maturity from depth bands (content: immature < oilWindow < gasWindow < overmature depths per archetype, ± noise). **The oil/gas/barren switch is a table lookup on generated depth — legible and tunable** |
| 4 Structure | Elevation of each horizon = regional trend + **value noise from hashed integer coordinates** (no external noise lib; octaves and amplitudes content). Faults: line segments seeded per archetype density, throw ~ LogNormal |
| 5 Traps | Local maxima of the reservoir horizon with closure height ≥ content minimum; closure polygon by contour walk. Trap type from context (fault-bounded vs fold) → **subtlety class** (D0–D3) from type + depth + a noise-floor table |
| 6 Charge | **Fill-spill**: mature source polygons emit charge volume; migrate up-dip along the carrier horizon; traps fill in spill-point elevation order; overflow continues up-dip. The classic algorithm — produces charged-and-empty traps naturally (R15-V7) |
| 7 Accumulations | Volume = min(charge reaching trap, closure pore volume × draws of φ, So); fluid from source maturity (oil/gas window); compartments: fault-crossing closures split with content probability; **AccessRequirements** derived from generated depth/water depth/temperature/k/H₂S |
| 8 Plays & classes | Group by (source unit, reservoir unit, trap type). **Era-layering enforcement**: per basin, per class-quota band (content) — if a class falls outside its band, deterministically resample step 5–7 noise offsets with an incrementing counter (bounded retries → world-gen fault, a content-tuning error, R15-V8/V11) |

## 3. Surface steps (9.1–9.8)

| Sub-step | Algorithm |
|---|---|
| Terrain | Heightfield = archetype base profile + value noise; terrain class by (height, slope, climate) table |
| Hydrology | Rivers: steepest-descent walks from sampled high points, carving; lakes at sinks; coast at sea level; **bathymetry = continued heightfield below sea level** (port depth falls out) |
| Settlements | Score every candidate cell: `w_coast·coast + w_river·river + w_flat·flat + w_arable·arable` (weights content); take top-N with minimum spacing; population ~ LogNormal by rank |
| Transport | Roads: **A\* on an 8-neighbour cost grid** (cost = terrain-class table), ties broken by coordinate order; network = MST over settlements (edge-weight ties broken by ordinal (idA, idB) pair) + spurs to ports; rail on the highest-traffic corridors (archetype era) |
| Third-party industry | In mature-archetype basins only: place legacy fields (small, depleted truth compartments), pipelines along road corridors, a terminal at the best port — the rent-vs-build fabric (H10) |
| Land status & profiles | Sensitivity polygons per content density; **profiles derived** per 06 §5.1a step 9.8 — a pure function of the layers above, never stored separately (R15-V9b) |

## 4. Outputs and handoff

World-gen writes truth into the owning modules' stores (compartments →
Subsurface; accumulation attributes → Information's truth; profiles/graph →
Environment; jurisdictions → Company; initial beliefs → Information's belief
store, built ONLY from the "regional data" observation pass — R15-V10's leak
guarantee is that beliefs are constructed through the same observation door as
everything else, never copied from truth).

> **Pass-5 amendment (finding 76):** the handoff is TYPED. The generator's
> only output channel is `IWorldSink`:
>
> ```csharp
> public interface IWorldSink
> {
>     void AddAccumulation(GeneratedAccumulation a);   // Play, Closure, Subtlety (DetectClass),
>                                                      // AccessRequirements, FluidForm, Compartments
>                                                      // (PoreVolume, φ, So, P0, T, Depth)
>     void SetSurface(GeneratedSurface s);             // Terrain (heightfield, classes, rivers, lakes;
>                                                      // sea level = elevation 0, bathymetry negative),
>                                                      // Settlements, TransportLinks, Harbours,
>                                                      // ThirdPartyAssets, SensitivityZones
>     void AddClimateRegion(ClimateRegion r);          // (Profile, Area) — SDD-016 §1
>     void AddJurisdiction(Jurisdiction j);            // (FiscalRegime, Area)
>     void DeliverRegionalObservation(Observation o);  // beliefs ONLY via the observation door (R15-V10)
> }
> public interface IWorldGenerator
> {
>     ContentId Id { get; }
>     void Generate(IWorldSink sink, IRandomStream worldGen);   // once, tick zero, WorldGen stream only
> }
> ```
>
> Owning modules build internal truth FROM these records — the generator never
> sees a module store, which is what makes the slot moddable (03 §3.2) without
> opening the truth wall. R15.0 reviews granularity, not existence.

> **Pass-7 amendment (findings 79–80):** generation is parameterised —
> `Generate(WorldParameters, IWorldSink, IRandomStream)`. `WorldParameters
> (Template, WidthCells, HeightCells, LandFraction, ResourceRichness,
> BasinMaturity, ClimateSeverity, RivalCount, StartEra)`: a parameter never
> invents content — it selects the world-template entry and scales that
> template's declared tables (richness → step 6 charge emission; maturity →
> step 1 archetype weights + step 9.5 third-party density; land fraction →
> sea-level percentile; climate severity → SDD-016 amplitudes/extreme rates;
> size → grid and region count; StartEra → tick-zero availability; rivals →
> SDD-011 roster). The template declares each knob's legal range; out-of-range
> ⇒ `EngineRefused` at `CreateNew`, ALL violations named, never clamped.
> Terrain classes are content (`terrain-class`,
> [C16](../catalog/C16_TERRAIN_CLASSES.md)); the template's cut tables map
> (height, slope, climate) onto class ids.

## 5. Test mapping

R15-V1 (PV7 identity) · V2 (substreams) · V3/MB5 (size log-normality emerges
from §2.7's draws) · V4/MB4 (success rate = charge/trap ratios, content-tuned)
· V5 (maturity trend = §2.3 bands) · V6 (play grouping) · V7 (fill-spill empty
traps) · V8 (viability + bounded resample) · V9 (surface coherence: rivers
reach sinks, A\* connectivity, port depth) · V9b (derived profiles) · V9c
(network remoteness) · V10 (leak — beliefs via the observation door) · V11
(class quotas).

## 6. Open items

| # | Item | Trigger |
|---|---|---|
| S010-1 | Grid resolution for heightfield/cost grids (memory vs corridor fidelity) — start 250 m cells, benchmark | R15.1 |
| S010-2 | Settlement growth (H9 slow response) lives in Environment at runtime, seeded from these populations — confirm the handoff shape | R22/R16 integration |
