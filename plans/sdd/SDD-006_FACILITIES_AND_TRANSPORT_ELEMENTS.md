# SDD-006 — Facility and Transport Elements

**Status:** drafted · **Serves:** R8, R9, R10, R11 · **Design docs:** [02](../design/02_DOMAIN_MODEL.md) §4–5, [04](../design/04_MATERIAL_AND_FLOW.md) §5 stages 3–10, [05](../design/05_SIMULATION_MODELS.md) §6, sheets [C06](../catalog/C06_WELLSITE_AND_GATHERING.md)–[C12](../catalog/C12_TERMINALS_AND_EXPORT.md)

Every surface element's `Transform` in its implemented form. All are
`IFlowElement`s per [SDD-002](SDD-002_STREAMS_AND_FLOW.md) §5 — pure transforms,
element-level conservation checked after each. SI throughout; datasheet fields
come from the catalogue-sheet content ([SDD-004](SDD-004_CONTENT_PIPELINE.md) §6).

---

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
