# SDD-006 — Facility and Transport Elements

**Status:** drafted · **Serves:** R8, R9, R10, R11 · **Design docs:** [02](../design/02_DOMAIN_MODEL.md) §4–5, [04](../design/04_MATERIAL_AND_FLOW.md) §5 stages 3–10, [05](../design/05_SIMULATION_MODELS.md) §6, sheets [C06](../catalog/C06_WELLSITE_AND_GATHERING.md)–[C12](../catalog/C12_TERMINALS_AND_EXPORT.md)

Every surface element's `Transform` in its implemented form. All are
`IFlowElement`s per [SDD-002](SDD-002_STREAMS_AND_FLOW.md) §5 — pure transforms,
element-level conservation checked after each. SI throughout; datasheet fields
come from the catalogue-sheet content ([SDD-004](SDD-004_CONTENT_PIPELINE.md) §6).

---

## 0. The two replaceable slots this SDD owns

> **Ninth contract pass (finding 82).** `ISeparationModel` and `IHydraulicModel`
> are two of the eleven plug-and-play slots in [03](../design/03_ARCHITECTURE.md)
> §3.2 — they are the fidelity dial for separation and for pipeline flow — and
> neither was ever declared. Pass R1-C5 recorded that every §3.2 slot was a
> compiled type; three were not. The algorithms in §1 and §6 below are the
> *default implementations* of these interfaces, not the only ones: swapping
> Darcy-Weisbach for Panhandle, or a fixed-efficiency split for a flash
> calculation, is selecting a different plugin by name from content, never an
> edit here (non-negotiable 11).

```csharp
/// Design 03 §3.2 — fixed-efficiency split ↔ flash calculation.
public readonly record struct SeparationEfficiency(
    double LiquidFromGas,      // carry-under: liquid recovered out of the gas leg
    double GasFromLiquid,      // carry-over
    double WaterFromLiquid);   // 3-phase only; 2-phase passes 0

public interface ISeparationModel
{
    ContentId Id { get; }        // finding 132: every 03 §3.2 slot names itself

    /// The ACHIEVED split at the operating point. Note this is NOT
    /// IFluidPropertyModel.SplitAt: the fluid model answers "what phases exist
    /// at this (P,T)" — thermodynamics — and this answers "what did this vessel
    /// actually manage to separate" — equipment. A fixed-efficiency
    /// implementation applies the datasheet efficiencies to the fluid model's
    /// ideal split; a flash implementation computes equilibrium directly and
    /// ignores them. Swapping between the two must not change what a phase IS.
    PhaseSplit SeparateAt(MaterialStream inlet, SeparationEfficiency efficiency,
                          IFluidPropertyModel fluid);
}

/// Design 03 §3.2 — Darcy-Weisbach ↔ Panhandle ↔ simplified.
public readonly record struct PipeGeometry(
    Length PipeLength, Length InnerDiameter, double Roughness, Length ElevationRise);

public interface IHydraulicModel
{
    ContentId Id { get; }        // finding 132

    /// Pressure drop along one segment for the fluid ACTUALLY flowing (§6).
    /// Capacity is never configured — it emerges from geometry and the stream,
    /// which is why the geometry is an argument and not a rating.
    Pressure DropAlong(MaterialStream stream, PipeGeometry geometry,
                       IFluidPropertyModel fluid);
}
```

Both are capability-blind and stateless: they receive everything they need and
own nothing, so the fidelity dial cannot become a hidden second source of state.

> **Correction while writing this section.** The first draft gave
> `ISeparationModel` the signature `SplitAt(composition, P, T, fluid)` — which is
> `IFluidPropertyModel.SplitAt` with an extra argument, i.e. two contracts
> answering one question. The two are genuinely different: phase *existence* at a
> (P,T) is thermodynamics and belongs to the fluid model; phase *recovery* is
> what a vessel achieved and belongs here. Recorded because the duplicate would
> have been invisible once both had implementations.

## 0b. The container

Design [02](../design/02_DOMAIN_MODEL.md) §4.1: a facility is a **container and
a cost centre, never a process.** All physics is in units, each an
`IFlowElement`. There is no `GasPlant` type and no facility-type enum — "gas
plant" is a `facility-template` content entry, and after construction the engine
knows only the units.

```csharp
public interface IFacility
{
    EntityId<IFacility> Id { get; }
    Coordinate Site { get; }
    IReadOnlyList<EntityId<IFacility>> Children { get; }        // recursive (PPDM)
    IReadOnlyList<EntityId<IFlowElement>> Units { get; }
}
```

**`Units` is a list of `IFlowElement` ids and not of some `IFacilityUnit`**, and
that is the §4.1 rule expressed as a type: the container knows only that its
units are things a stream passes through. The unit taxonomy of 02 §4.2 —
separator, treater, compressor, dehydrator, tank, meter, flare — exists in
content and in this document's transforms, and in no interface anywhere
(coherence finding 82).

## 0c. A unit is a socket; a tier is what is fitted into it

> **R12b.8 declaration.** [07](../design/07_TECHNOLOGY.md) §4b.3b states the
> model — "a tier fits a **socket** and its datasheet is read by the socket's
> model" — and every element here took its tier at construction and held it
> `readonly`, which made the ladder in the catalogue sheets unreachable: a field
> could be built and never improved. Debottlenecking is the operations game's
> central verb, and it had no way to happen.

```csharp
// On every unit whose datasheet is a tier. The ELEMENT is the socket and keeps
// its identity — its id, its place in the network, its connections — while what
// is fitted into it changes.
public void Fit(SeparatorTier tier);
```

**Refitting, not replacing**, and the distinction is load-bearing. The network
is a registry of elements and their tie-ins ([SDD-002](SDD-002_STREAMS_AND_FLOW.md)
§6), registration is write-once and there is no removal — deliberately, because
an element that could vanish mid-tick would take its connections with it. A
bigger vessel is therefore the same vessel with a bigger datasheet, which is also
what actually happens on a site: the foundations, the tie-ins and the permit stay.

**Fitted by an operation, never directly.** The catalogue sheets price the
install as an operation with a duration (C06's "construction · weeks"), so a
refit takes months of a player's time and money and lands through the one
activity engine (SDD-007) like everything else. A `Fit` called outside a
completed operation would be a free upgrade.

## 1. Separator

```text
P_sep is the vessel's DECLARED operating pressure (datasheet, §8) — held by its
back-pressure controller, and therefore IMPOSED on the network rather than read
from it. Every outlet leg leaves at P_sep; the upstream element sees P_sep as
its discharge pressure, which is how a separator reaches the reservoir.

Inlet stream at (P_sep, T from stream/ambient):
  split = fluidModel.SplitAt(composition, P_sep, T)         // ideal phase fractions
  Efficiency per phase pair from the tier datasheet, DERATED by throughput:
     eff_eff = eff_rated · Clamp01(Q_rated / Q_actual)      // linear residence-time derate, pinned
  Outlets:
     gas outlet    = ideal gas    + (1 − eff_lg) · ideal liquid   // carry-over
     liquid outlet = ideal liquid + (1 − eff_gl) · ideal gas      // carry-under
     water leg (3-phase): analogous with eff_lw
Constraints: GasCapacity  = actual-condition gas volumetric rate vs rating
             LiquidCapacity = liquid volumetric rate vs rating      (either binds — R8-V2)
Multi-stage: a train is N chained separator elements at declared stage
pressures — no special multi-stage code; recovery gain emerges (R8-V4).
```

> **R20d.1 amendment (finding 157).** The first line of this block used to read
> "*P_sep from network*", three lines above a multi-stage rule that speaks of
> "*declared stage pressures*". Both cannot be true, and §8's closed datasheet
> registry settled it by accident: it listed no pressure field, so there was
> nowhere to declare one, and `Separator` shipped stamping its INLET pressure on
> every outlet leg. The consequences are not subtle once the chain is wired.
>
> - **The vessel imposes nothing.** A separator's pressure drop, as the solver
>   measures it (`inlet − outlet[0].P`), is exactly zero, so S4 propagates the
>   network's terminal sink boundary all the way to the wellhead and every well
>   flows against atmosphere.
> - **Nothing breaks out.** The flash is computed at the inlet pressure, which
>   for a completion's outlet is *reservoir* pressure — so the "ideal split" is
>   taken at reservoir conditions and a separator separates nothing.
> - **R8-V4 cannot pass.** N chained vessels would all sit at one pressure, and
>   the recovery gain that is supposed to *emerge* from a train has nothing to
>   emerge from.
>
> Resolved in favour of *declared*, which is both the physical statement — a
> vessel is held at a set pressure by a controller — and the only reading under
> which multi-stage separation means anything.
>
> **Why it survived R8.** FV5 (backpressure reaches the reservoir) is proven
> against `Restrictor`, a synthetic test element carrying a hard-coded 5-bar
> drop. The solver's propagation is correct and tested; no *shipped* element
> ever exercised it. That is finding 150's shape exactly — a mechanism proven
> against a fixture with no production counterpart — and it stays invisible for
> as long as the loop does not call the thing.

## 1c. Gathering line — wellhead to manifold

> **R20d.8 amendment (finding 167's other half).** Design 04 stage 3 is
> "Wellhead → manifold (gathering)", with a per-well flowline whose pressure
> drop is taken "over the line's length" and whose player levers include *"a
> nearer manifold"*. **That line does not exist.** The element named `flowline`
> in this composition sits AFTER the manifold — it is the trunk to the facility
> — so every well has been tied straight into the header at zero distance.
>
> The consequence is finding 167's other half. A company that develops a second
> discovery forty kilometres away ties it into the same header as the first and
> pays nothing for the journey, so where a field is stops mattering the moment
> the trunk has been laid. One host serving several fields is the ordinary
> architecture; what makes it a decision is that each tieback is as long as it
> is.
>
> ```text
> Gathering line: one per well, created at tie-in.
>   length   = distance from that well's structure to the manifold's site
>   the manifold sits where the FIRST field was developed — a company builds
>   its header at the field it is opening, and later fields reach it
> ```
>
> A well on the host's own field has a short line and a distant tieback a long
> one, so the same well drilled into two different structures is two different
> propositions: more pressure drop, and less of what the reservoir can deliver
> reaching the separator. **That is the whole reason a generated world puts
> fields in places.**
>
> The line is a `Pipeline` like any other, so backpressure travels back up it and
> the commingling trap (§1b) works through it unchanged — a strong new well
> raises manifold pressure and can shut in weaker wells however far away they
> are.

> **R20d.19 amendment (finding 173). The separation model cannot express wet
> oil, so BS&W is structurally zero and the sales spec can never fail on it.**
>
> `SeparationEfficiency` carries three terms and all three move liquid and gas
> across the gas/liquid boundary or knock water OUT of the liquid leg:
>
> ```text
> LiquidFromGas    gas carried under, into the liquid
> GasFromLiquid    liquid carried over, into the gas
> WaterFromLiquid  water knocked OUT of the liquid, into the aqueous leg
> ```
>
> There is no term for the direction that matters to a custody spec: water
> carried INTO the oil. The fluid model's split puts produced water in the
> aqueous phase and this model can only move more of it out, so the oil leaving a
> vessel is dry by construction — at any efficiency, at any load, on any tier.
>
> **BS&W is therefore not a content number waiting to be set.** It is a quantity
> the model has no way to produce, which is why `Defaults.SalesSpec` is empty and
> why the custody point's reject leg has never fired. A treater installed against
> it would have nothing to treat, which is this session's recurring finding in
> advance rather than after the fact.
>
> The missing term is `WaterIntoLiquid` — a carry-under of the aqueous phase, and
> the one that should rise with LOAD rather than sit at a rated constant, because
> the mechanic worth having is "push the vessel past its design rate and the oil
> goes off-spec". That is a change to this section and to `ISeparationModel`, and
> it belongs to whoever builds treating rather than being smuggled in beside it.
>
> **Souring is the path that is NOT blocked.** SDD-012 §5's H2S curve reads
> cumulative injected water over pore volume, which R20d.18 made real, and H2S
> can enter as a MATERIAL — content, which this composition already parameterises
> — rather than as a model term that does not exist. A sales spec that fails on
> sourness needs no change here at all.

## 1b. Manifold / header — the commingling element

> **R20d.1 declaration (finding 159).** [01](../design/01_CONCEPT_MATRIX.md) §C5
> names the concept and gives its contract as `IFlowNode`;
> [04](../design/04_MATERIAL_AND_FLOW.md) §5 stage 3 is *"Wellhead → manifold
> (gathering)"* with commingled provenance as its whole subject; catalogue
> [C06](../catalog/C06_WELLSITE_AND_GATHERING.md) prices the tier ladder; R6-V14
> — "a new high-pressure well kills weak wells" — is a statement about header
> pressure. **No SDD declared an element and `IFlowNode` exists in no
> assembly**, so a field's second well had nowhere to go: `FlowNetwork` refuses
> two edges into one inlet (FD4), which is correct, and the element that is
> supposed to accept them was never written.
>
> Declared as a `SDD-002` §5 flow element like every other, **not** as a new
> `IFlowNode` contract. 01 §C5's name predates `IFlowElement`, and a second
> element interface would be exactly the type hierarchy design 02 §4.1 forbids —
> the solver knows one kind of thing.

```text
Ports: N inlets (tier: header slots) + 1 Main outlet.
Transform:
  outlet mass     = Σ inlet masses, per material            // a header stores nothing
  outlet provenance = Allocation.Blend[(inlet_i.provenance, inlet_i.mass)]
  outlet P        = inlets[0].P                             // NO drop of its own
  outlet T        = mass-weighted mean of the inlet temperatures
Constraints: none. A header has no capacity; the flowline downstream does, and
             reporting one here would throttle wells for being connected.
```

**Why the outlet takes an inlet's pressure rather than the lowest or a mean.** A
header imposes ONE pressure on everything tied into it, and S4 already produces
exactly that: every element feeding a manifold is handed the same demanded inlet
pressure, because the demand is stored per element and they all feed the same
one. So in the converged state every inlet is at the header pressure and the
three candidate rules agree. Taking `inlets[0].P` is the one that also makes
`ΔP_element` exactly zero, which is what a header's contribution to S4 should be
— a `min` would report a fictitious drop during iteration and slow convergence
for no physical reason.

**The commingling trap falls out and is not coded.** A header passes its
downstream demand to every well equally, so a new high-rate well raises the
throughput through whatever is downstream, raises the drop across it, raises the
header pressure — and the weakest well on the line goes DEAD. That is R6-V14,
and it is backpressure arithmetic rather than a rule anybody wrote.

**Slots are a real limit.** A fixed header has a declared number of them
(C06's ladder), so a field that has filled its manifold must buy a bigger one
before the next well can be tied in. Refused at tie-in with the slot count in
the reason, never by silently sharing a port.

## 2. Oil treating (heater-treater · desalter · stabiliser)

```text
Contaminant removal to target:  out_frac = max(spec_target, in_frac · (1 − eff))
Heat duty: Q_heat = m_liquid · c_p · ΔT_datasheet
Fuel draw (gas-fired): FuelConsumed = Q_heat / heatingValue(fuelGas)   → the 04 §7 fuel term
Emulsion tightening: eff loses `emulsionPenalty(waterCut)` — content curve,
monotone increasing; the reason treating gets harder exactly when it matters.
Stabiliser: moves declared light-ends fraction from liquid outlet to gas outlet
(RVP compliance is expressed as a max light-ends fraction — pinned proxy, §8).
```

## 3. Compression

```text
Polytropic head (SI):
  W = Z̄ · (R / MW) · T_in · (n/(n−1)) · [ (P_out/P_in)^((n−1)/n) − 1 ]     [J/kg]
  Power = ṁ · W / η_poly                                                  [W]
Stages: N = ceil( ln(P_out/P_in) / ln(maxStageRatio) ), equal ratio per stage
        r = (P_out/P_in)^(1/N); interstage cooling to T_in assumed.
Driver: gas-engine/turbine → FuelConsumed = Power / (η_driver · heatingValue)
        electric            → PowerDraw only (fuel term moves to C13's source)
Heat derating: maxPower_eff = maxPower · derate(T_ambient) — content curve from
the tier datasheet; ambient from SegmentContext (R22). This is R9's summer dip.
Backward pass (SDD-002 S4): boosting elements (compressors, pumps) contribute
NEGATIVE ΔP — head at the current ṁ, capped by maxPower_eff — so a booster
lowers the required upstream pressure exactly as physics says.
```

## 3b. Power sources, flare and vapour recovery — the missing transforms

**Power sources** (genset, turbine, grid tie — [C13](../catalog/C13_POWER_AND_UTILITIES.md)):

```text
Stage 4 balances declared duty against supply in MERIT ORDER (content per
facility: typically grid → waste-heat → turbines → gensets). Each fuel-burning
source's assignment becomes a FIXED-RATE FUEL SINK in the segment's network:
  fuelRate = assignedPower / (η_driver · heatingValue(fuelGas))
so genset fuel is a known draw the solve routes gas to — and if the gas system
cannot deliver it, the shortfall re-runs the stage-4 balance with that source
derated (one bounded re-pass, pinned; a second shortfall → units offline per
priority). FuelConsumed lands in the 04 §7 fuel term; grid tie contributes
cost only. Datasheet: {maxPower, η_driver, fuelType | grid, meritRank}.
```

```csharp
public interface IPowerSource
{
    Power MaxSupply { get; }
    int MeritRank { get; }      // lower runs first: grid → waste-heat → turbine → genset
}
```

**A power source is not an `IFlowElement`.** It is balanced at stage 4, before
the solve, and its output is a fixed fuel *sink* placed into the network rather
than a transform of its own — which is why it declares supply and rank and
nothing about ports.

**Lift-gas offtake**: the compression side of the gas-lift recycle is a fixed
sink at last tick's committed lift rate (SDD-002 §6) — third member of the
fixed-draw family beside genset fuel and, below, the flare.

**Flare** (the reject destination): transform consumes its inlet entirely —
mass leaves the network as the *flared* conservation term; combustion products
(CO₂, unburnt CH₄ per a content combustion-efficiency, default 0.98) post to
the emissions ledger at stage 9. Constraint: `TotalCapacity` (the flaring cap
enters as a restriction on this element — R9's oil-throttling coupling).
Datasheet: {capacity, combustionEfficiency}.

**Vapour recovery**: transform captures `recoveryFraction` of tank-vapour
inlet back to the gas system; remainder follows the flare/fugitive path.
Datasheet: {capacity, recoveryFraction}.

## 3c. Compression — the pinned model

> **Numbered 3c, not 3b (finding 144).** This section and §3b above were both
> labelled `3b`, and both are cited from code: `IPowerSource` cites §3b for
> merit-ordered supply, and five comments in `GasProcessing.cs` cited §3b for
> polytropic compression. A citation that resolves to two different sections
> fails F-3 — "every formula cites the SDD section stating its form" — for
> whichever half of the callers lands on the wrong one.

> **R9.0 amendment (finding 115): compression was specified in no SDD at all.**
> R9.1 names `ICompressionModel`, R9-V1 pins it against "the polytropic formula"
> and MX6 tests it, and no document stated that formula, the staging rule, the
> discharge temperature or the ratio limit. Under F-1 the whole of R9.1 was
> unimplementable. Stated here.
>
> It is **not** one of [03](../design/03_ARCHITECTURE.md) §3.2's eleven
> replaceable slots — those name `ISeparationModel` and `IHydraulicModel` and not
> this — so it is a unit's own model rather than a plug-and-play seam, and the
> `ICompressionModel` name R9 §3 uses is corrected to a concrete unit behind
> `IFlowElement` like every other piece of equipment (finding 117).

```text
STAGED POLYTROPIC COMPRESSION — SI throughout

Ratio limit: a single stage is limited by discharge temperature and by
  mechanical design. r_max from the tier (default 3.5).
  N = ceil( ln(P2/P1) / ln(r_max) ),  N ≥ 1
  Each stage takes the EQUAL ratio r = (P2/P1)^(1/N), which minimises total
  power for a fixed overall ratio — the reason real trains are balanced.

Per stage, with interstage cooling back to T1 (aftercoolers are part of the
tier; without them stage 2 starts hot and the train would run away):
  T2 = T1 · r^((n−1)/n)                                   [K]
  w  = (Z̄ · R · T1 / MW) · (n/(n−1)) · ( r^((n−1)/n) − 1 ) [J/kg]

Train:
  W_shaft = ṁ · N · w / η_polytropic                       [W]
  n from the tier (default 1.25 for typical hydrocarbon gas); η from the tier.

HEAT DERATING (13 §3.3, R9 §2.6): a compressor's throughput capacity falls with
ambient temperature — the air-cooled driver and the aftercoolers both lose duty.
  capacity(T_amb) = capacity_rated · ( 1 − k_derate · max(0, T_amb − T_ref) )
  k_derate per K and T_ref from the tier; clamped at zero.
  This is why a desert field loses gas-handling capacity in exactly the hottest
  months, and — through §4's flaring cap — loses OIL rate in summer for a reason
  nowhere near the reservoir.
```

**The compressor is an ordinary `IFlowElement`.** Its constraint is
`ConstraintKind.TotalCapacity` at the derated value and its `PowerDraw` is
`W_shaft`, which stage 4's balance consumes exactly as it consumes an ESP's.

## 4. Gas treating (dehydration · sweetening · NGL)

```text
Dehydrator: water-in-gas out = min(in, spec_capable)   throughput-capped
Amine:      H2S/CO2 out = in · (1 − removal_eff)       acid gas → sulphur unit
            or flare (declared reject route)
NGL plant:  per-component recovery fractions from tier (C2, C3, C4, C5+),
            applied to the gas composition's declared component split (FD2 —
            the ONLY place components exist); products leave as distinct
            material streams. Mass closure across the split is element-checked.
```

> **R9.0 amendment (finding 116): the component split had no declared type.**
> FD2 makes the NGL plant the one place components exist, this section names the
> split, and nothing anywhere declared what a split IS. R8-V4 and R8.5 are both
> gated on it (R8's tracker entry says so), so the gap blocked three tasks across
> two phases.

```csharp
// FD2's boundary, made a type so the boundary is visible in the code rather
// than only in prose. C1 is everything lighter than ethane — methane plus the
// inerts — because nothing downstream separates them and a component nobody
// recovers does not need its own field.
public enum GasComponent { C1, C2, C3, C4, C5Plus }

// Mass fractions of a GAS stream, summing to 1. Declared by the fluid system
// and carried NOWHERE ELSE: a stream outside the NGL plant has no component
// split, and asking for one is a design error rather than a defaulted answer.
public sealed record ComponentSplit(ImmutableArray<double> MassFractionByComponent);

// Per-component recovery, from the plant's tier.
public sealed record NglRecovery(ImmutableArray<double> FractionByComponent);
```

**Why a fraction per component rather than a full compositional model.** FD2's
whole point is that compositional tracking costs a great deal for detail the
player never sees. The split enters at one element, is consumed by that element,
and the products leave as ordinary black-oil material streams — so nothing
upstream or downstream gains a component field, and the boundary cannot leak by
accident because there is no member on `MaterialStream` to leak through.

## 5. Tank

The one stateful surface element (state committed at stage 6 only):

```text
State: inventory Composition (masses) + blended Allocation (SDD-002 §3)
Receipt: inventory += inlet · duration;  Allocation = Blend(...)
Draw (cargo/pipeline): proportional composition, current allocation
Constraint: Ullage — remaining = capacity_mass(by density at storage T) − held;
  when remaining < inlet · duration, inlet capacity for the segment is the
  remaining/duration → backpressure emerges via SDD-002 S3 throttling (FV5).
  tank.full is the segment-boundary event when remaining hits zero.
Boil-off/vapour loss: rate = held_lightFraction · lossRate_tier · duration
  → routed to vapour recovery if present, else to the emissions ledger as
  fugitives. NEVER silently vanished — it is a conservation term.
```

## 6. Pipeline

```text
Liquid (Darcy-Weisbach, SI):  ΔP = f · (L/D) · ρ v² / 2  +  ρ g Δz
Gas (pressure-squared form, SI, pinned implementation):
  ṁ = sqrt[ (P1² − P2²) · π² · D⁵ · MW / (16 · f · Z̄ · R · T̄ · L) ]
Friction factor: Colebrook-White, the SAME 20-Newton-steps-from-0.02 procedure
pinned in SDD-003 §6.2 — one implementation, shared.
Two-pass property evaluation at (P̄, T̄) exactly as the VLP (no iteration).
Linefill: inventory = ρ̄ · A · L per material fraction — a real, owned mass in
the conservation check (R11-V7); updated at commit from average conditions.
Erosional velocity: v_max = C_e / sqrt(ρ_mix)  (C_e from PhysicalConstants,
API 14E-derived) — exceeding it is a Constraint(ErosionalVelocity) feeding the
hazard severity, not a hard block (05 §6.3).
Flow-assurance flags: hydrate margin = T_stream − T_hydrate(P, waterPresent);
wax margin = T_stream − T_WAT(crude). Negative margins raise the respective
hazard rates (R18 severity inputs); insulation tiers raise T_stream via a
declared U-value against ambient.
```

```csharp
public interface IPipeline : IFlowElement
{
    Length PipeLength { get; }
    Length InnerDiameter { get; }
    Pressure Rating { get; }
    ContentId PipeSpec { get; }
}
```

**A pipeline declares geometry, never a capacity.** Throughput is whatever the
hydraulics above yield for the fluid actually flowing, so a line that was
comfortable on dry oil throttles on its own when the water cut climbs — a
configured `maxRate` field would have made that emergent behaviour impossible to
express and is deliberately absent.

## 7. Terminal, berth, cargo, custody

```text
Berth: an occupancy calendar (day-granular, /30ths grid). One cargo at a time.
Cargo (an IOperation, SDD-007):
  loading: draw = min(loadingRate_berth, tankDrawCapacity) · activeDays
  laytime = contracted days; demurrage = max(0, actualDays − laytime) · rate
Custody meter:
  measured = true · (1 + ε),  ε ~ Normal(0, σ_tier) from the `measurement`
  stream — drawn ONCE per transfer, audited with the draw (FD5).
  Invoiced quantity is `measured`; `true − measured` accumulates in the audited
  measurement-tolerance term of 04 §7. σ per meter tier (C10).
Spec gate at the point (04 stage 10): checks on stream-derived properties —
  BS&W       = water mass / liquid mass
  H2S, CO2   = mass fractions (ppm by mass)
  Water-in-gas ("dewpoint")  = max water mass fraction     ← pinned proxy
  RVP        = max light-ends fraction                     ← pinned proxy
  Heating value = mass-weighted from material properties
Failing streams route to the declared Reject port in full (FV6). The proxies
are documented simplifications ([02](../design/02_DOMAIN_MODEL.md) §9 class):
they preserve the decisions (build a dehydrator/stabiliser) without vapour-
pressure thermodynamics.
```

```csharp
// The pinned proxy set above, as a closed enum — closed because a spec property
// the engine cannot derive from a stream is a spec it cannot gate on.
public enum SpecProperty
{
    BasicSedimentAndWater, H2SFraction, Co2Fraction, WaterInGasFraction,
    LightEndsFraction, HeatingValueMin, HeatingValueMax
}

public sealed record SpecLimit(SpecProperty Property, double Limit);
public sealed record Specification(IReadOnlyList<SpecLimit> Limits);

// The metered, contractual revenue event — the ONLY place revenue originates
// (SDD-009 §1, architecture test R13-V2). It is an IFlowElement because the
// stream physically passes through it and can be REFUSED there.
public interface ICustodyTransferPoint : IFlowElement
{
    Specification Spec { get; }
}
```

**`HeatingValueMin` and `HeatingValueMax` are separate members rather than one
property with a band**, because a sales-gas contract sets them independently and
a stream can fail either end — rich gas is off-spec as surely as lean.

## 7b. Export capacity — a socket, not a constant

> **R20d.8 amendment (finding 165). The offtake rate was a constant and the
> field's only hard ceiling.** Stage 6's own comment names three answers to a
> full tank — "more storage, more export and less production" — and export was
> the one with no verb behind it. So every field, at every size, produced at
> exactly the rate the shipped export line took, and the reservoir could not
> reach the player: measured, a 500e6 m³ accumulation earned the same over
> twenty years as one a tenth its size.
>
> **Export is a socket with a tier fitted, exactly like the separator** (§0c):
>
> ```text
> ExportTier:  offtake (kg/s)          — what the line contracts to take
> Fit(tier):   the socket keeps its identity; capacity is what changed
> ```
>
> A LADDER, climbed by an activity that costs money and takes months, because
> the decision is the point: a bigger line is only worth building if there is
> enough underneath to fill it, and that judgement is made on believed reserves
> against a capital bill. A player who overbuilds against a field that was
> smaller than they thought has bought capacity they will never use — which is
> the authentic version of this decision and the reason it must not be derived
> automatically from truth.
>
> **This is why the plant is NOT sized from the accumulation.** The tempting fix
> to finding 164's second half was to compute the right facilities from what the
> generator put in the ground. That would delete the decision and hand the player
> the answer to a question the whole information layer exists to make them
> guess. The reservoir sets what CAN be produced; the player sizes what actually
> lifts it, and is wrong about it at their own expense.

## 7c. The chain belongs to a field, not to the engine

> **R20d.8 finding (167), recorded rather than worked around.** The world now
> places accumulations on the map with real positions and real extents, and
> harbours with real depths — so the distance from a field to its route to market
> is a number that exists. **Nothing can consume it**, and the reason is
> structural rather than missing code.
>
> The surface chain — manifold, flowline, separator, meter, tank, terminal — is
> composed once, by `FacilitiesModule`, before any world exists. Its flowline is
> therefore a content constant (2 km), so a remote discovery costs exactly what
> one beside the harbour costs, and a basin's geography is scenery. Worse, one
> chain cannot serve a multi-prospect world at all: a company that develops two
> accumulations forty kilometres apart has two gathering systems, and this
> composition can only express one.
>
> **The fix is an ordering, and it is the same one finding 164's second half
> pointed at.** A chain is laid when a field is developed, from that field's
> position to the harbour it is routed to — so it cannot be composed before the
> world is generated OR before the player has chosen. Two consequences:
>
> 1. `CreateNew` generates BEFORE composing (the generator needs only an
>    `IRandomStream`, never a module store — SDD-010 §4 already guarantees this),
>    so composition can build what the world justifies.
> 2. A surface chain becomes a per-field object created by development, not a
>    singleton owned by a module. `SurfaceChain` is already a unit-of-composition;
>    what changes is how many there are and when they exist.
>
> **§7c.1 — the first step, taken: a pipeline is a socket and its ROUTE is what
> is fitted.** The full per-field chain is above; what can be done without it is
> the same §0c move already made for the separator and the export line. The
> element is registered at composition (the flow registry is write-once, so it
> must be), and the geometry it carries is set when the line is actually laid:
>
> ```text
> Route(geometry):  the element keeps its id, its tie-ins and its spec;
>                   PipeLength and InnerDiameter are what changed.
>   REFUSED while the line holds linefill — a route cannot change under oil
>   that is already in it, and re-routing a full line would either destroy
>   owned mass or teleport it (the conservation check would catch the second
>   and nothing would catch the first).
> ```
>
> A flowline is therefore laid from the field that is being developed to the
> harbour it is routed to, and its length is that distance. This does not fix the
> multi-prospect case — there is still one chain — but it makes geography cost
> something, which is the half a player can feel.
>
> **Done (R20d.8.5).** `Pipeline.Route` exists, `FieldControl.OpenWell` calls it on the
> first tie-in, and R20d8V4 asserts that developing a field lays the line to
> where that field is. Until the rest lands, one chain serves one field. Stated
> here so the limit reads as a known gap rather than as a decision.

## 8. Datasheet field registry (content ⇄ code)

Per-unit-kind closed datasheet blocks (SDD-004 §6): separator {gasRating,
liquidRating, **operatingPressure**, effGL, effLG, effLW}; **manifold {slots}**;
treater {eff, spec_capable, heatDuty};
compressor {maxPower, η_poly, maxStageRatio, driver, derateCurve}; tank
{capacity, lossRate}; pipe-spec {D, rating, roughness, U-value};
meter {σ}; berth {loadingRate}; power source {maxPower, η_driver, fuelType|grid,
meritRank}; flare {capacity, combustionEfficiency}; VRU {capacity,
recoveryFraction}. **A field not listed here does not exist** —
additions go through this SDD first (rule F-1).

## 9. Test mapping

R8-V1..V10 (separator/treating/tank/power) · R9-V1..V11 (compression/gas chain;
V2 staging = §3's N formula; MX6 = §3's head formula) · R10-V9 (water closure) ·
R11-V1..V13 (hydraulics = §6; MX4/MX5 exact forms; V7 linefill; V11 meter ε) ·
FV5/FV6 land here end-to-end.

## 10. Open items

| # | Item | Trigger |
|---|---|---|
| S006-1 | Slug catcher sizing model (volume vs declared slug size) — currently a capacity constraint only | R11 flow-assurance review |
| S006-2 | Storage-temperature model for tank capacity-by-density (fixed per climate vs ambient-tracking) | R8; start fixed-per-climate |
| S006-3 | LNG train transform (expansion ⚑) | when EV2 scope opens |
