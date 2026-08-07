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

## 1. Separator

```text
Inlet stream at (P_sep from network, T from stream/ambient):
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

## 3b. Compression

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

## 8. Datasheet field registry (content ⇄ code)

Per-unit-kind closed datasheet blocks (SDD-004 §6): separator {gasRating,
liquidRating, effGL, effLG, effLW}; treater {eff, spec_capable, heatDuty};
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
