# SDD-003 — Subsurface and Well Contracts

**Status:** drafted · **Serves:** R5, R6, R7 · **Design docs:** [02](../design/02_DOMAIN_MODEL.md) §2–3, [05](../design/05_SIMULATION_MODELS.md) §1–5, [R5](../phases/R5_SUBSURFACE.md), [R6](../phases/R6_WELLS.md), [R7](../phases/R7_LIFT.md)

The second-highest hallucination risk: the physics formulas. [05](../design/05_SIMULATION_MODELS.md)
states them in conventional **field units** for legibility; the engine computes
in **SI**. This document pins the SI forms, the unit of every intermediate, and
every iteration scheme — so no implementer ever "adapts" a field-unit formula by
guessing a constant.

---

## 1. Scope

`OGSim.Subsurface` (compartments, fluids, drives, aquifers — **truth types are
`internal`**) and `OGSim.Wells` (well hierarchy, inflow/outflow, lift, choke).
Both expose contracts in `OGSim.Contracts`; completions are the network's source
`IFlowElement`s.

## 2. Units rule for this document

Every formula below is the **SI form actually implemented**. Field-unit
constants (7758, 141.2, 43560) **never appear in engine code** — they belong to
display conversion only. MX-class tests verify each SI form against worked
examples computed independently in field units, which catches a wrong constant
immediately.

## 3. The compartment

```csharp
internal interface IReservoirCompartment            // TRUTH — internal to OGSim.Subsurface
{
    CompartmentId Id { get; }
    Pressure Pr { get; }                             // average pressure, Pa
    InPlace InPlace { get; }                         // Mass per material, kg
    ContactSet Contacts { get; }                     // GOC, OWC as datum depths (Length TVD)
    RockTruth Rock { get; }                          // φ, k (m²), h (m), A (m²), c_f (1/Pa)
    IDriveMechanism Drive { get; }
    IReadOnlyList<CompartmentLink> Links { get; }    // (other id, transmissibility)
}
```

Mutation only through `MaterialBalance.Commit` (stage 6). Beliefs about all of
this live in `OGSim.Information` and never here.

### 3.0b Accumulation truth attributes (06 §2.3)

The accumulation (the world-gen grouping above compartments) additionally
carries:

```csharp
public enum DetectClass { D0, D1, D2, D3 }
public sealed record AccessRequirements(
    DepthClass Depth, WaterDepthClass WaterDepth,
    bool Hpht, bool Tight, bool Sour);
```

`TrapSubtlety : DetectClass` is consumed **only** by observation models
([SDD-005](SDD-005_CAPABILITIES_AND_EFFECTS.md) §5) — below-tier surveys yield
nothing. `AccessRequirements` are consumed **only** by the gating validator on
development commands (SDD-005 §3). Neither is ever readable from the belief
layer or the read model — the R15-V10 leak test covers both.

### 3.1 Material balance solve

Voidage-balance root-find for end-of-tick pressure:

```text
F(P_end) = E_o(P_end) + E_g(P_end) + E_w(P_end) + E_f(P_end)
           + W_influx(P_end) + V_injected − V_withdrawn        [reservoir m³]
solve F(P_end) = 0 by BISECTION on P_end ∈ [1 kPa, Pr_start]
  · 80 iterations max, tolerance 100 Pa
  · expansion terms from the fluid model's Bo/Bg/Rs and compressibilities,
    exactly the black-oil expansion forms of 05 §3.1
  · no root in bracket, or V_withdrawn > maxTickVoidageFraction (content,
    default 0.25) of expansion capacity → MODEL FAULT (05 §3.1 validity limit)
```

Bisection, not Newton: 80 deterministic iterations cost nothing at tens of
compartments, and bisection cannot diverge or need a derivative — one less
thing to get wrong.

### 3.2 Contacts

```text
ΔOWC = (net aqueous volume gained at reservoir conditions) / (A · φ · (1 − S_or))
ΔGOC analogously for the gas cap.
```

Per-tick, applied at commit. Coarse and honest — contact movement at tank
fidelity is bookkeeping of replaced volume, not a displacement front.

### 3.3 Aquifer (Fetkovich form)

```text
W_influx(tick) = J_aq · (P_aq − Pr) · Δt        capped by remaining aquifer expansion
P_aq updated by its own material balance (an aquifer is a water compartment)
J_aq: productivity index, content per aquifer (m³/s/Pa)
```

## 4. Fluid model

```csharp
public interface IFluidPropertyModel
{
    FluidForm Form { get; }                          // BlackOil | ModifiedBlackOil (condensate, 05 §2)
    Pressure Pb { get; }                             // bubble point (dew point for MBO)
    double Rs(Pressure p);                           // sm³ gas / sm³ oil  (dimensionless ratio of standard volumes)
    double Rv(Pressure p);                           // MBO only: sm³ condensate / sm³ gas; BlackOil ⇒ 0
    FormationVolumeFactor Bo(Pressure p);
    GasFormationVolumeFactor Bg(Pressure p);         // rm³ / sm³ — pass-6 amendment (finding 77, correcting 72): gas has its OWN bridge (ReservoirVolume ↔ StandardGasVolume); the oil FVF bridges to SurfaceVolume — the wrong family for standard gas
    Viscosity MuOil(Pressure p);
    Viscosity MuGas(Pressure p);
    double Z(Pressure p, Temperature t);
    PhaseSplit SplitAt(Composition c, Pressure p, Temperature t);   // pass-3: `in` dropped — Composition wraps one ImmutableArray reference; the modifier bought nothing and code/SDD now match
    ValidityRange Validity { get; }                  // outside → MODEL FAULT, never extrapolate
}
```

Standard implementation: the correlation set of [05](../design/05_SIMULATION_MODELS.md)
§2, each function carrying its published validity range. **Every correlation is
implemented against `DetMath` only** and pinned by an MX test to reference
values computed from the published papers.

## 5. The well hierarchy

```csharp
public interface IWell           // identity + status machine; NO physics
{
    EntityId<IWell> Id { get; }
    WellStatus Status { get; }                       // transitions: data-driven table, commands only
    EntityId<ILicence> Licence { get; }
    Coordinate Surface { get; }
}

public interface IWellbore
{
    Trajectory Path { get; }                         // polyline of (MD, TVD, x, y) stations
    Length ContactLengthIn(CompartmentId c);         // computed from Path ∩ compartment interval
}

public sealed record Perforation(
    PerforationId Id,
    CompartmentId Drains,
    Length TopMd, Length BottomMd,
    double Skin,                                     // dimensionless
    bool Isolated)
{
    // Standoff (05 §3.3b): computed each tick from trajectory TVD midpoint vs
    // the compartment's nearest contact — DERIVED, never stored (law L5).
}
```

## 6. The completion — the source element

`ICompletion : IFlowElement`. Its `Transform` is the operating-point solve —
and as a **source element** it has no inlets and reports its withdrawal as
`TransformResult.Sourced` (SDD-002 §5), which is how the element-level
conservation check covers wells:

### 6.1 Inflow (SI Darcy form, per perforation)

```text
q_rc = [ 2π · k · h_perf · (Pr − Pwf) ] / [ μo · ( ln(re/rw) − 0.75 + s ) ]
        q_rc: reservoir-condition volumetric rate, m³/s
        k: m² · h_perf: perforated net interval, m · pressures: Pa · μo: Pa·s
        re from drainage area: re = sqrt(A_drain / π);  rw content (default 0.108 m)
Below Pb: Vogel composite exactly as 05 §4.2 on the same q_max basis.
Gas wells: q_sc = C · (Pr² − Pwf²)^n, C and n content per completion test.
Surface-rate conversion: q_sc = q_rc / Bo(Pr̄)  (oil);  mass = q_sc · ρ_sc.
Multi-perforation: q per perf from its own kh share and skin; compartment
withdrawal and stream provenance from the per-perf contributions (FV10).
Coning gate: q_perf above critical rate (05 §3.3b constants: content) marks
the perf "coning" — its water fraction ramps ahead of compartment average.
```

### 6.2 Outflow (VLP)

```text
Pwf_required(q) = Pwh + ΔP_hydro + ΔP_friction
ΔP_hydro   = ρ_mix · g · TVD          ρ_mix: mass-weighted density of the stream
                                       evaluated at (P̄, T̄) = midpoint of wellhead
                                       and bottomhole estimates, ONE re-evaluation
                                       (fixed two-pass, not iterated — pinned)
ΔP_friction = f · (MD/D) · ρ_mix · v² / 2
              f: Colebrook-White, solved by EXACTLY 20 Newton steps from
              f₀ = 0.02 (deterministic; converged long before 20)
Lift hooks (R7): ESP adds ΔP_pump(q) from its TIER's catalogue curve —
  piecewise-linear head-vs-rate at reference density 1000 kg/m³, scaled by
  ρ_mix/ρ_ref; power curve likewise per tier; gas tolerance and temperature
  limits are the tier's envelope fields (07 §4b.3). Tier selection is fixed at
  install (command-validated against requiresTech per R17 §2.6b) — the VLP
  never asks which tech the company owns, only which tier is installed;
gas lift reduces ρ_mix above the injection valve by the injected-gas fraction —
  the injected rate is the ONE-TICK-LAGGED committed value (SDD-002 §6: the
  recycle-loop closure; a new gas-lift well ramps over its first tick), and the
  injected mass re-enters this completion's outlet stream as a lagged source,
  NOT as reservoir withdrawal (it is excluded from material balance and from
  Sourced-vs-compartment accounting);
rod pump / PCP replace the outflow relation with a displacement cap q ≤ q_pump.
```

### 6.3 Operating point

```text
Solve IPR(Pwf) = VLP⁻¹(Pwf) by BISECTION on Pwf ∈ [Pwh + ρ_min·g·TVD, Pr]:
  · 64 iterations max, tolerance 500 Pa
  · no sign change in bracket → result DEAD (a distinct outcome, not q = 0 —
    R6 §2.2; the read model and well.diedNaturally consume the distinction)
Choke (downstream of the point): critical when P_down/P_up < r_c (content,
default 0.55): rate clamps to the choke's critical rate and is INDEPENDENT of
downstream pressure — the completion then ignores backpressure changes until
sub-critical again (this is why a choked well survives S4 backpressure swings).
```

## 7. Error surface

| Situation | Response |
|---|---|
| Correlation input outside `Validity` | Model fault (naming fluid system + property) |
| MB bisection: no root / voidage over limit | Model fault (naming compartment) |
| Trajectory never intersects the declared compartment | **Content/command fault at completion time** — not discovered at solve time |
| Perforating an isolated interval, negative skin below content floor | Command rejection with domain reason |
| Installing a tier whose `requiresTech` is not held or rented | Command rejection naming the missing technology (R17-V11) |

## 8. Test mapping

MX1 (Darcy SI form vs field-unit worked example) · MX2 (skin +10 halves J at
the reference geometry — exact) · MX3 (p/Z linearity through the MB solve) ·
FV3/R6-V5 (operating point vs independent solve) · R6-V6 (DEAD distinct from
zero) · R6-V14 (shared-line backpressure kills the weak well) · R5-V11
(integration error vs sub-stepped reference) · R7-V1..V4 (lift hooks) ·
CAL2/CAL4/CAL6 bands run on this stack end-to-end.

## 9. Open items

| # | Item | Trigger |
|---|---|---|
| S003-1 | Two-pass ρ_mix vs full pressure-traverse integration for deep gassy wells — revisit if CAL6 bands fail on gas condensate | R6 model tests |
| S003-2 | Coning constants (Meyer-Garder simplification) — content defaults need calibration against CAL3's S-curve | R10 |
| S003-3 | Drainage-area assignment when several completions share a compartment (equal split vs kh-weighted Voronoi) | R6.10 review — recommend kh-weighted equal-pressure (tank ⇒ shared Pr makes this second-order) |
