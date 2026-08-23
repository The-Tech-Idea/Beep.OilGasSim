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
| 7 Accumulations | Volume = min(charge reaching trap, closure pore volume × draws of φ, So); fluid from source maturity (oil/gas window); compartments: fault-crossing closures split with content probability; **AccessRequirements** derived from generated depth/water depth/temperature/k/H₂S; **each compartment draws a fluid-system (crude quality) choice from content, same step (finding 270)** |
| 8 Plays & classes | Group by (source unit, reservoir unit, trap type). **Era-layering enforcement**: per basin, per class-quota band (content) — if a class falls outside its band, deterministically resample step 5–7 noise offsets with an incrementing counter (bounded retries → world-gen fault, a content-tuning error, R15-V8/V11) |

> **Amendment (finding 270): a compartment's crude quality is drawn here,
> not assumed.** Step 7 already fixes `FluidForm` (oil vs gas, an
> *accumulation*-level fact, from source maturity) and per-compartment
> `Porosity`/`OilSaturation` (both drawn from the `sizing` stream,
> `WorldGenerator.cs:190,241`). It never fixed *which* oil — every
> compartment in every generated field used the one engine-wide
> `Defaults.Fluid` (35° API), because nothing else existed to choose
> between. SDD-004's finding-270 amendment adds a `fluid-system` content
> kind so more than one can exist; this amendment is where a generated
> compartment picks one.
>
> The draw sits immediately after the existing oil-saturation draw, same
> step, same stream: `sizing.NextInt(0, systems.Count)` indexes into the
> list of `FluidSystemDefinition` ids the current build's content loaded —
> the same "index into a content-declared list with the stream already in
> use at this step" shape the porosity/saturation draws already use, not a
> new stream and not a new RNG law. A world generated from a content set
> with exactly one fluid system draws that one every time — determinism is
> unaffected by content authored to offer no choice.
>
> `GeneratedCompartment` (§4) gains the drawn `ContentId FluidSystem`,
> appended after `Depth` so no existing positional construction reorders.
> The field travels with the compartment through `IWorldSink` exactly like
> `Temperature`/`Porosity` do; `SubsurfaceState` is where it is resolved to
> an actual `IFluidPropertyModel` (SDD-003's finding-270 amendment).
>
> **Out of scope here, named not solved:** which fluid system a THIRD-PARTY
> legacy field (§3, "Third-party industry") draws is the same mechanism —
> no separate rule is needed, since those compartments are generated
> through the same Step 7 — but nothing in this pass changes how their
> depletion is modelled. Sulfur/sourness as a second drawn axis is not
> introduced; the content kind and this draw carry API gravity only.

## 3. Surface steps (9.1–9.8)

| Sub-step | Algorithm |
|---|---|
| Terrain | Heightfield = archetype base profile + value noise; terrain class by (height, slope, climate) table |

> **Amendment (finding 242, building `TerrainClassContentKind`).** The
> shipped generator classifies by **height and slope only** — climate is not
> yet a per-cell generated fact anywhere in this composition (`AddClimateRegion`
> is never called), so classification cannot honestly read it. [C16](../catalog/C16_TERRAIN_CLASSES.md)'s
> amendment carries the exhaustive (height × slope) cut table and names the two
> classes (desert, swamp) this leaves unreachable until climate generation
> exists — both still ship as validated content. `ClassByCell` carries `-1` for
> a sea cell (elevation below zero) rather than a class index; C16's own rule,
> "sea is not a class," was previously violated by the one caller that existed.
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

**The handoff is typed** (finding 76). The generator's only output channel is
`IWorldSink`; owning modules build their internal truth *from* these records, so
the generator never sees a module store — which is what makes the slot moddable
(03 §3.2) without opening the truth wall. R15.0 reviews granularity, not
existence.

```csharp
// ---- geology
// FluidSystem (finding 270): the content id of the drawn fluid system —
// appended, not inserted, so no existing positional construction reorders.
public sealed record GeneratedCompartment(
    ReservoirVolume PoreVolume, double Porosity, double OilSaturation,
    Pressure InitialPressure, Temperature Temperature, Length Depth,
    ContentId FluidSystem);

// Subtlety and Access are TRUTH attributes here — below-tier surveys spawn
// nothing because screening reads these, not the other way round (§2.5–2.7).
public sealed record GeneratedAccumulation(
    ContentId Play, Polygon Closure, DetectClass Subtlety,
    AccessRequirements Access, FluidForm Fluid,
    IReadOnlyList<GeneratedCompartment> Compartments);

// ---- surface
// Sea level is elevation 0 and bathymetry is negative elevation, so harbour
// depth falls out of the same field rather than needing its own map (§3).
public sealed record Heightfield(
    Length CellSize, int Width, int Height, ImmutableArray<double> ElevationMetres);

public sealed record River(ImmutableArray<Coordinate> Path);

public sealed record GeneratedTerrain(
    Heightfield Elevation,
    ImmutableArray<int> ClassByCell,        // indexes Classes; class ids are content (C16)
    IReadOnlyList<ContentId> Classes,
    IReadOnlyList<River> Rivers,
    IReadOnlyList<Polygon> Lakes);

public sealed record Settlement(Coordinate Site, long Population);
public sealed record TransportLink(Coordinate A, Coordinate B, ContentId Kind);
public sealed record Harbour(Coordinate Site, Length Depth);   // Harbour, NOT Port: PortId/PortSpec
                                                               // are flow-element ports (N1)
public sealed record ThirdPartyAsset(ContentId Template, Coordinate Site);
public sealed record SensitivityZone(ContentId Kind, Polygon Area);

public sealed record GeneratedSurface(
    GeneratedTerrain Terrain,
    IReadOnlyList<Settlement> Settlements,
    IReadOnlyList<TransportLink> Transport,
    IReadOnlyList<Harbour> Harbours,
    IReadOnlyList<ThirdPartyAsset> ThirdParty,
    IReadOnlyList<SensitivityZone> LandStatus);

// ---- regions
public sealed record ClimateRegion(ContentId Profile, Polygon Area);   // exactly one per location
public sealed record Jurisdiction(ContentId FiscalRegime, Polygon Area);

// ---- the sink and the generator
public interface IWorldSink
{
    void AddAccumulation(GeneratedAccumulation accumulation);
    void SetSurface(GeneratedSurface surface);
    void AddClimateRegion(ClimateRegion region);
    void AddJurisdiction(Jurisdiction jurisdiction);
    void DeliverRegionalObservation(Observation observation);   // beliefs ONLY here (R15-V10)
}

public interface IWorldGenerator
{
    ContentId Id { get; }
    void Generate(WorldParameters parameters, IWorldSink sink, IRandomStream worldGen);
}
```

> **Contract pass 10 — the two amendments in this section disagreed with each
> other.** Pass 5 declared `Generate(IWorldSink, IRandomStream)`; pass 7, three
> paragraphs below, added `WorldParameters` as a first argument and never edited
> pass 5's block. Whichever a reader reached first was the signature they would
> have implemented. Consolidated above, with pass 7's form as the committed one.
>
> This is the **fourth** occurrence of the amendment-versus-block pattern
> (SDD-002 §6, SDD-004 §5, SDD-005 §3), and the first where the two disagreeing
> statements are both amendments in a single section — which is what makes it
> worth a standing review rule rather than four separate corrections.
>
> The fourteen handoff records were described in comments *inside* the sink's
> member list and declared nowhere. R15 cannot emit a world without them.

```csharp
public sealed record WorldParameters(
    ContentId Template,          // the world-template entry: all archetype/weight tables
    int WidthCells,              // terrain grid and region count (§3 terrain)
    int HeightCells,
    double LandFraction,         // sea-level percentile of the heightfield (§3 hydrology)
    double ResourceRichness,     // charge-emission multiplier (§2 step 6 fill-spill)
    double BasinMaturity,        // archetype weights frontier↔mature (§2 step 1)
                                 // + third-party density (§3 step 9.5)
    double ClimateSeverity,      // weather amplitude / extreme rate (SDD-016 §1–2)
    int RivalCount,              // rival roster size (SDD-011)
    Era StartEra);               // technology availability at tick zero (07 §2)
```

> **Pass-7 amendment (findings 79–80):** generation is parameterised. A
> parameter never
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

## 4b. Dry structures are prospects too

> **R20d.7 amendment (finding 169). The generator's empty traps die inside it.**
> §2.6's fill-spill produces charged-and-empty traps naturally, R15-V7 asserts
> that it does, and an uncharged closure is then discarded before it reaches
> `IWorldSink`. So every prospect a player can see holds oil, and the drilling
> activity decides dry-or-wet on a 0.38 outcome row that never consults the
> rock. **Probability of success can therefore be neither right nor wrong**, and
> exploration — the half of this game the whole information layer exists to serve
> — is a formality.
>
> **The sink receives every closed structure.** `GeneratedAccumulation` already
> has the shape for it: `Compartments` is empty when the trap took no charge.
> The record's summary line ("one charged trap") is what changes, not its
> fields.
>
> ### A prospect is not a compartment
>
> Measured while settling this: a dry structure **cannot** be modelled as a
> compartment with no oil. `SubsurfaceState.Create` derives connate water as
> `1 − So`, and the material balance refuses `Swc = 1` because the
> formation-expansion term `Efw` divides by `(1 − Swc)`. That is a correct
> refusal about a real singularity, not an obstacle to work around — a trap full
> of water has no hydrocarbon material balance to solve.
>
> So identity splits, and it should have anyway:
>
> ```text
> Prospect     EntityKind.Prospect — a closed structure, drilled or not.
>              Every one the generator finds, charged or dry.
> Compartment  created ONLY where charge arrived. Truth; the thing that flows.
> ```
>
> `DrillWellCommand` therefore targets a **prospect**, and what the well finds is
> read from truth: a prospect with a compartment behind it is a discovery, one
> without is a dry hole. The outcome table keeps what it is actually for —
> whether the job ran on time, over budget, or was lost mechanically — and stops
> deciding what is in the ground.
>
> ### Regional data sees structure, not fluid
>
> Starting beliefs currently observe **oil in place**, which cannot survive this:
> a dry trap has none, `ln(0)` is undefined, and — worse — a belief that exists
> only for charged traps would tell a player which ones hold oil for free. The
> presence of a reading would be the leak.
>
> Regional gravity and magnetics see a **structure**, not what is in it. The
> observation becomes the closure's CAPACITY (`structure-capacity`), which every
> closed high has whether or not charge reached it. Dry and charged prospects are
> then indistinguishable from the surface, which is exactly the position a
> company is really in and the reason POS is worth computing at all.
>
> ### What a dry hole proves
>
> SDD-008 §4 wants the failed element named from truth. This generator has
> exactly one way to leave a trap empty — fill-spill ran out before the charge
> reached it — so an uncharged structure failed on **Source**, and that is a
> derivation rather than a guess. When steps 1–3 model maturity and seal
> integrity there will be more failure modes to tell apart, and the diagnosis
> becomes a field on the handoff rather than a single mapping.

### The map starts dark

> **S1 amendment (plans 22 §3).** The paragraph above says regional gravity and
> magnetics see a structure rather than its fluid, and that is right. What it did
> not say is **who paid for the regional data**, and the implementation answered
> "nobody": `WorldSink.AddAccumulation` registered a risk for every charged
> structure as the world was generated, so a company opened its first month
> holding a complete structural map of the basin it had just been licensed.
>
> **That is the exploration game skipped, not modelled.** POS is computed, shown,
> and worthless — every structure is already on the board, so the only decision
> left is which of the known odds to drill. The question worth money is *where do
> I even look*, and it was answered for free.
>
> **Acreage is licensed in blocks, so it is explored in blocks.** The basin is
> divided at generation into a grid of blocks (§4's handoff gains them). A block
> is public from the first tick — a company knows the shape of its own licence —
> and what lies under it is not.
>
> ```text
> seismic-2d   RECONNAISSANCE over a block. Finds closures: every charted
>              structure inside the block is registered and appears in the
>              read model with its five factors. Cheap.
> seismic-3d   DETAIL over a structure already found. Sharpens trap and
>              reservoir, as it always did. Dear.
> ```
>
> **Find, then firm up.** The two surveys stop being one verb at two prices and
> become the two questions exploration actually asks. `seismic-3d` keeps naming a
> prospect, which stops being circular the moment a prospect has to be found
> first.
>
> **A block that returns nothing is a result the company paid for and should
> keep.** Ground known to be barren is ground it can stop paying to think about,
> and the read model says so rather than leaving the block looking unvisited.
>
> **Registration is what hides it, so nothing new hides it.** `Prospects()`
> already filters on `risks.Knows`; deferring the `Register` call is the whole
> mechanism. Generation is untouched — the world still creates every closure,
> charged or dry — and `WorldState` holds what a survey *would* say until one is
> shot.
>
> **Why the block is an entity rather than a coordinate and a radius.**
> [SDD-007](SDD-007_OPERATIONS_ENGINE.md) §5 leaves the per-template parameter
> block open, and an activity is aimed at an `EntityRef` and a depth. A survey
> ordered as a centre and a radius would have to smuggle the area through one of
> those, which is the call-site invention F-4 forbids. A block **is** the area, so
> the existing channel carries it exactly and the open item stays closed.

### A structure carries its capacity

> **R20d.7.5 amendment.** `GeneratedAccumulation` gains
> `ReservoirVolume Capacity` — what the closure could hold, whether or not
> anything reached it. A truth attribute like `Subtlety` and `Access`, and for
> the same reason: it is what a SURVEY measures.
>
> Without it, seismic could only be shot at a compartment, so a company could
> only survey fields it had already discovered — exactly backwards, and
> contradicted by the activity's own reason for existing ("a survey needs no
> wellbore, which is what makes it the first move rather than a follow-up").
> Capacity is the one quantity every structure has, so it is the one a survey can
> sharpen before anybody drills.

## 4c. Reloading a generated world (R20d.12, finding 195)

**A generated game cannot be reloaded at all today.** `WorldState` is not an
`IStateOwner`, so where the structures are, which prospect became which field
and where the header went up reach no container. Hand-placed fixtures are
unaffected — everything the rebuild reads from the world is absent in their
original runs too — which is exactly why the save arc got as far as it did
without noticing.

**The split, and it is not what PSD2 appears to say.** What this module holds
divides cleanly:

```text
regenerable   surface, harbours, climate, jurisdictions, prospects, their
              positions and capacities — a pure function of the world seed,
              which PV7 already asserts
decisions     where the HEADER went up; which prospect a discovery turned into
              which compartment — things the GAME did, reproducible from
              nothing
```

**So a load regenerates and then restores the decisions**, rather than storing a
heightfield. The seed is already in the header (§2), the generator is already
called by `CreateNew`, and PV7 is already the guarantee that running it again
gives the same world — a save that stored the terrain would be storing a
function of a number it also stores, and every future generator change would
silently fork old saves from new ones.

**Design 11 §6's PSD2 recommends (a) store the generated truth**, on the
grounds that "the world can be *modified* in play (production changes reservoir
state), so regeneration alone is insufficient". **Read closely, that reasoning is
about the SUBSURFACE and not the surface** — production changes compartment
state, which `subsurface.compartments` has stored since R20c.7. Nothing in play
moves a harbour. So regenerating the surface and storing the decisions honours
PSD2's intent rather than contradicting it, and PSD2's wording should be
narrowed to say so.

**What the owner carries** — `world.decisions`, in Layer 4 beside the other
composition-owned blocks: the header coordinate if one has been placed, and the
prospect↔compartment links `Found` creates. **What it must NOT carry** is
anything the generator produces, and the test that keeps it honest is PV7 turned
on a reload: regenerate, restore, and assert the world matches the one saved.

> **The split is clean in principle and NOT clean in the code, which is the
> implementation note this section needs.** `DeclareKnownField` both PLACES a
> prospect — appending to `_prospects`, `_at` and `_capacity`, the same lists the
> generator fills — and LINKS it through `Found`. So one object interleaves
> generated data with decided data in the same three lists, and a restore cannot
> simply replay the second kind.
>
> **That is a seam to fix before the owner is written, not around it.** Writing
> the owner first would mean guessing which entries in those lists a load should
> recreate — and a guess there restores a world subtly unlike the one saved,
> which is the failure mode this whole arc has been about.
>
> **The seam is a BOUNDARY, not a flag per entry.** Generation runs once, at
> creation, before a game has done anything; every placement after it is a
> decision. So `WorldState` records the count of prospects standing when
> generation finished — one `int`, set by the same door the generator already
> comes through (`WorldSink`) — and everything at or beyond that index is a
> decision a save replays:
>
> ```text
> [0 .. generated)   the generator's, reproduced by regenerating from the seed
> [generated .. n)   declared in play — §4b's known field, and any later
>                    placement — captured and replayed in order
> ```
>
> A per-entry flag would say the same thing less well: it invites a placement
> that is somehow both, and it leaves the ordering question open. The boundary
> cannot, because generation is finished before the first tick.
>
> **A hand-placed fixture falls out correctly with no special case** — nothing
> generated, so the boundary is zero and every prospect is a decision, which is
> exactly what those scenarios are.
>
> **Built at R20d.12.9, and the last step is a REGENERATION CALL that does not
> exist yet.** `SaveGame.Load` composes through `BuildAt`, which does not
> generate — so a generated save meets an engine holding no basin, the boundary
> check finds N against 0, and **the load is REFUSED naming the mismatch**. That
> is the correct failure and a deliberate one: a generated campaign that cannot
> be reloaded is a limitation, while one restored onto an empty basin is a
> corrupted game that looks fine.
>
> **Closing it needs one input the save does not carry.** Generation takes
> `WorldParameters` (`CreateNew(settings, world)`) and a save records only the
> seed, so `Load` cannot call the generator without them. **They must come from
> the SAVE and not from the caller**: a save that needed a host to remember which
> world it was would put the burden exactly where PR5's "loading without them
> fails explicitly" says it must not be.

### 4c.1 Where the parameters live — R20d.12.12 amendment

The paragraph above proposed the **header**, on the grounds that the parameters
are a scenario's declaration and a handful of numbers. **They belong in
`world.decisions` instead**, and the reason is that the objection to the block —
that a load needs them *before* any owner is restored — turns out not to hold.

`SaveGame` already reads a block without restoring it. `Rebuild` calls
`WellsState.Saved(StateBlock.ReaderFor(...))` for exactly this situation: the
owner cannot be told what it holds until the things it holds exist, so the
loader reads the instructions out of the block first and lets the owner check
its own work afterwards. `ReaderFor` exists for that, and carries no version
check precisely because the owner performs it moments later. Regeneration is the
same shape one step earlier.

Given that door, the block is better on three counts:

- **L5.** What a world was generated from is a fact *about the world*, and
  `WorldState` is the thing that owns the boundary those parameters are one side
  of. The header is the container's metadata — schema, engine and content
  versions, mods, seed, tick, stream positions, digests — and every entry in it
  is about the FILE.
- **The container format does not move.** `SaveHeader`, `Manifest`,
  `HeaderFrom` and `SaveFile.Validate` are untouched, so the save's schema
  version does not turn over and no migration is owed for a change no reader of
  the header cares about.
- **It is digested with the world.** A block is covered by its per-module
  digest, so tampered parameters are refused naming `world.decisions` — the same
  refusal as tampering with the boundary they are checked against, which is the
  right pairing since neither means anything without the other.

**Recorded at the door that already seals the boundary.** `SealGeneration` is
called from `CreateNew` at the one instant both halves of the split are true,
and `CreateNew` is holding the `WorldParameters` when it calls it. So the seal
takes them: `SealGeneration(parameters)`, one call, no second path for the world
to learn what made it.

**A hand-placed scenario carries none, and that is the flag.** It never seals,
so it has no parameters, and a load regenerates only when the block says the
world was generated — the same distinction the boundary already draws, rather
than a second one beside it.

> **One consequence to state rather than discover.** Regeneration at load runs
> with the clock already restored to tick N, because `BuildAt` restores it before
> the modules compose. The generator takes no clock and its beliefs are replaced
> by the save's ([SDD-008](SDD-008_INFORMATION_AND_BELIEFS.md) §4b.1), so nothing
> simulated differs — but its audit entries are stamped at N rather than at zero.
> Cosmetic, and cheaper to write down than to rediscover from a trail that says a
> basin was drawn in year forty.

**Then the test §4c asks for becomes possible, and not before**: regenerate,
restore, assert the `WorldView` matches the one saved. PV7 today asserts a seed
reproduces a world; it says nothing about regenerate-then-restore, and those are
different claims.

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
| S010-3 | `desert` and `swamp` terrain classes are shipped content, unreachable by the shipped generator until a per-cell climate/aridity signal exists (finding 242) | Climate-region generation (design 06 §5.1a step 9.3) |
