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

> **R2.7 amendment — the rule holds for DIMENSIONAL formulas and cannot hold for
> EMPIRICAL correlations.** The two are different things and this section
> previously treated them as one.
>
> Darcy's law, the hydrostatic head and Darcy-Weisbach are *dimensional*: their
> constants are unit conversions, so an SI form exists, is exact, and is the
> honest thing to implement. 7758 genuinely does not belong in engine code.
>
> Standing, Vazquez-Beggs, Beggs-Robinson, Lee et al. and Dranchuk-Abou-Kassem
> are *empirical*: their constants are **regression coefficients fitted to
> field-unit data** (18.2, 0.0125, 1.2048, 3.0324…). They are not conversions and
> there is no SI form of them. Algebraically absorbing the unit conversions into
> new coefficients would produce numbers that appear in no paper, cannot be
> checked against the source, and would silently become unverifiable the moment
> anyone made an arithmetic slip doing it — which is precisely the failure rule
> F-3 exists to prevent.
>
> **So §4's correlations evaluate in field units, at one declared boundary.**
> Inputs convert SI → field on entry, the published correlation is transcribed
> verbatim so a reader can check it line by line against the paper, and the
> result converts field → SI on exit. The conversion factors live in the
> quantity types (SDD-001 §1) where they already are. The engine's *interfaces*
> remain SI throughout; only the inside of a correlation body is field.
>
> This is a narrowing of the rule, not an exception to it: a field-unit constant
> may appear **only** inside a function implementing a named published
> correlation, and only where the paper's own constant is being transcribed.

## 3. The compartment

```csharp
// PUBLIC — the id marker only. The compartment itself is truth and stays
// internal; other modules may hold a reference to one and learn nothing from it.
public interface IReservoirCompartmentEntity { }

// INTERNAL to OGSim.Subsurface. Everything below is truth.
internal interface IReservoirCompartment
{
    EntityId<IReservoirCompartmentEntity> Id { get; }
    Pressure Pr { get; }                             // average pressure, Pa
    InPlace InPlace { get; }                         // mass per material, kg
    ContactSet Contacts { get; }
    RockTruth Rock { get; }
    IDriveMechanism Drive { get; }
    IReadOnlyList<CompartmentLink> Links { get; }
}

// Dense mass-per-material, kg, indexed by MaterialId.Ordinal — the same layout
// as Composition and deliberately NOT the same type. Composition is a mass FLOW
// (kg/s, SDD-002 §2); this is a mass. Reusing Composition here would let a rate
// be committed as an inventory, which is the one arithmetic error the volume
// families were split up to prevent.
internal readonly record struct InPlace(ImmutableArray<double> KilogramsByOrdinal);

internal readonly record struct ContactSet(
    Length GasOilContact,                            // datum TVD
    Length OilWaterContact);

internal readonly record struct RockTruth(
    double Porosity,                                 // fraction
    Permeability Permeability,
    Length NetThickness,                             // h
    Area DrainageArea,                               // A
    double RockCompressibility);                     // c_f, 1/Pa

internal readonly record struct CompartmentLink(
    EntityId<IReservoirCompartmentEntity> Other,
    double Transmissibility);                        // m³/s/Pa
```

Mutation only through the stage-6 commit path (`IWithdrawalTarget` /
`IReceiptTarget`, SDD-002 §9). Beliefs about all of this live in
`OGSim.Information` and never here.

> **Contract pass 10.** `CompartmentId` was used throughout this document and
> declared nowhere: identity is `EntityId<T>` over a marker interface
> (SDD-001 §2), and the committed marker is `IReservoirCompartmentEntity`.
> Corrected here and in §5. `InPlace`, `ContactSet`, `RockTruth` and
> `CompartmentLink` were likewise referenced and never declared — R5 cannot
> implement a compartment without them, and F-1 says they are specified first.
>
> Writing `InPlace` surfaced a distinction worth keeping: it has the same dense
> layout as `Composition` and must not be the same type, because `Composition`
> is kg/s and this is kg. A shared type would make "commit a rate as an
> inventory" a silent unit error of exactly the kind §1.1's volume families exist
> to make uncompilable.

### 3.0b Accumulation truth attributes (06 §2.3)

The accumulation (the world-gen grouping above compartments) additionally
carries:

```csharp
public enum DetectClass { D0, D1, D2, D3 }

// Both were consumed by AccessRequirements and declared nowhere (pass 10).
public enum DepthClass      { Shallow, Standard, Deep, UltraDeep }
public enum WaterDepthClass { Onshore, Shallow, Deep, UltraDeep }

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

The black-oil material balance in Havlena-Odeh grouping, solved for end-of-tick
pressure. **All volumes reservoir m³; all pressures Pa.** Every term below is
evaluated at the trial pressure `P` except those subscripted `i`, which are at
initial conditions and never change.

```text
UNDERGROUND WITHDRAWAL — what has left the compartment, at conditions P
  F(P) = Np·[Bo(P) + (Rp − Rs(P))·Bg(P)] + Wp·Bw(P)
  where Rp = Gp / Np                      (cumulative producing GOR; Np = 0 ⇒ F = 0)

EXPANSION per stock-tank m³ of original oil
  Eo(P)  = (Bo(P) − Boi) + (Rsi − Rs(P))·Bg(P)          oil + its dissolved gas
  Eg(P)  = Boi·(Bg(P)/Bgi − 1)                          gas cap
  Efw(P) = (1 + m)·Boi·[(cw·Swc + cf)/(1 − Swc)]·(Pi − P)   connate water + rock

BALANCE — solve Φ(P) = 0 for P
  Φ(P) = N·(Eo(P) + m·Eg(P) + Efw(P)) + We + Vinj − F(P)

  N   original oil in place, stock-tank m³      m   gas-cap ratio (initial gas-cap
  We  CUMULATIVE water influx, reservoir m³         reservoir volume ÷ oil-zone
  Vinj CUMULATIVE injection, reservoir m³           reservoir volume), dimensionless

by BISECTION on P ∈ [1 kPa, Pi]
  · 80 iterations max, tolerance 100 Pa
  · no root in bracket → MODEL FAULT

VALIDITY LIMIT (05 §3.1), checked AFTER the solve:
  (P_start − P_end) / P_start > maxTickPressureDropFraction
      (content, default 0.25)  → MODEL FAULT
```

> **R5.0 amendment (finding 106): "a fraction of expansion capacity" has no
> well-defined value.** The limit was stated against the expansion capacity the
> compartment has left, and there is no pressure at which to evaluate that: taken
> to the bracket floor, `Bg → ∞`, so the liberated-gas term makes the capacity of
> any compartment effectively infinite and the limit can never fire. Implemented
> as written it is a guard that reads as present and is not — the worst kind,
> because the phase would have shipped believing 05 §3.1's refusal was in place.
>
> The limit is therefore on the **pressure step**, which is the quantity that
> actually bounds explicit first-order error, is unambiguous, and preserves the
> intent exactly: a large withdrawal in one tick produces a large pressure drop
> and is refused. It is checked after the solve because the drop is what the
> solve computes.

Bisection, not Newton: 80 deterministic iterations cost nothing at tens of
compartments, and bisection cannot diverge or need a derivative — one less
thing to get wrong.

**Everything is cumulative, measured from initial conditions.** Not from the
start of the tick — see the amendment below. At `P = Pi` with nothing yet
produced every term is zero, so an untouched compartment's root is `Pi` exactly.

> **R5.0 amendment (finding 99): §3.1 balanced cumulative expansion against ONE
> TICK's withdrawal.** The previous form wrote `… + V_injected − V_withdrawn`
> with the E-terms unqualified. Read per-tick throughout it is a valid explicit
> scheme but accumulates integration error with nothing correcting it; read as
> written — expansion terms are functions of `P_end` alone, so they are
> necessarily measured from initial conditions — it balanced the compartment's
> whole expansion since discovery against a single month's offtake, and pressure
> would have fallen roughly by the ratio of field life to one tick, which is to
> say hardly at all. Either reading is wrong; the two cannot be mixed, and the
> original text did mix them.
>
> The cumulative form is chosen because it is **self-correcting**: each tick
> re-solves from initial conditions, so a rounding error in one tick's pressure
> cannot propagate into the next. It is also the form R5-V9's invariant is
> written against (cumulative production + remaining + injected = original in
> place), and the form every published treatment states.

> **R5.0 amendment (finding 100): the expansion forms are stated HERE.** The
> previous text deferred them to "exactly the black-oil expansion forms of
> 05 §3.1", and 05 §3.1 states the balance in words — expansion of oil, gas,
> connate water and rock equals withdrawal — and no algebra at all. F-3 requires
> a formula to cite the SDD section *stating its form*, so the citation pointed
> at nothing and an implementer would have had to reconstruct the grouping from
> memory. That is the exact hallucination surface this document exists to close.

> **R5.0 amendment (finding 101): `Bw` was missing from the fluid model.** The
> withdrawal term needs a water formation volume factor and `IFluidPropertyModel`
> declared none, so `F(P)` could not be evaluated for any compartment producing
> water — which is all of them, eventually. Added in §4.

### 3.1b Gas material balance — the `p/Z` line

> **R5.0 amendment (finding 105): §3.1's form cannot solve a gas reservoir.**
> Every expansion term is expressed per stock-tank m³ of original OIL, so `N = 0`
> makes the whole balance identically zero and there is no root. [05](../design/05_SIMULATION_MODELS.md)
> §3.2 states the gas case separately and R5.7 lists it as its own task; this
> document had no form for it, so a dry-gas compartment was unimplementable.

For a volumetric gas reservoir the balance is the expansion of the gas alone,
which rearranges to a straight line — the form design 05 §3.2 calls the best
information mechanic in the game:

```text
p/Z = (pi/Zi)·(1 − Gp/G)                                    [05 §3.2]

Solved for P DIRECTLY, not by bisection: the relation gives p/Z, and P is
recovered by one bisection on Z's implicit definition — which §4.1's
Dranchuk-Abou-Kassem already owns. Two nested root-finds would be one more
than the problem has.

  target = (pi/Zi)·(1 − Gp/G)                                [Pa]
  find P in [1 kPa, pi] with P/Z(P, T) = target
    · bisection, 80 iterations, tolerance 100 Pa
    · Gp > G → MODEL FAULT: more gas produced than was ever in place

WITH WATER DRIVE the line BENDS UP, and that is the point (05 §3.2):
  p/Z = (pi/Zi)·(1 − Gp/G) · 1/(1 − (We − Wp·Bw)/(G·Bgi))
The player reads the bend as the discovery. The engine does not announce it.
```

**Exactness matters here and nowhere else quite as much.** MX3 asserts the
volumetric line is linear to floating-point tolerance, because the player is
invited to extrapolate it and read `G` off the x-intercept. A line that drifted
by a percent would make a correct deduction give a wrong answer, and the mechanic
depends on the player being able to trust their own arithmetic.

### 3.1c Water cut — the fractional-flow S-curve

> **R10.0 amendment (finding 119): CAL3 asks for "a recognisable S-curve after
> breakthrough" and no document said what curve.** R10.5, R10-V1 and SC4 all
> pin against it, and neither [05](../design/05_SIMULATION_MODELS.md) nor this
> document stated a form. Unimplementable under F-1. Stated here.
>
> The S-curve is **not** a shape to be drawn — it is what fractional flow does
> when relative permeabilities are power laws, which is the standard Corey
> treatment. A fitted sigmoid would have produced the same picture and taught
> the player nothing, because it would not respond to viscosity, and the whole
> reason a viscous oil waters out early is the mobility ratio in the denominator.

```text
NORMALISED SATURATION
  S* = (Sw − Swc) / (1 − Swc − Sor)          clamped to [0, 1]

COREY RELATIVE PERMEABILITIES (exponents and endpoints from the rock type)
  krw = krw_max · S*^nw
  kro = kro_max · (1 − S*)^no

FRACTIONAL FLOW — the water cut at the sandface
  fw = 1 / ( 1 + (kro · μw) / (krw · μo) )
  krw = 0 (S* = 0) ⇒ fw = 0: BEFORE BREAKTHROUGH, no water flows at all.

Breakthrough is therefore not an event that is scheduled. It is the first tick
at which Sw at the producer exceeds Swc, and the S-curve after it is the shape
of fw(S*) — steep in the middle because both power laws are moving at once,
flattening as kro → 0 and the well approaches pure water.

MOBILITY RATIO M = (krw/μw) / (kro/μo) at the endpoints. M > 1 is an unfavourable
flood: the water outruns the oil, breakthrough is early and the S-curve's rise is
sharp. This is why a viscous oil waters out early, and it falls out of the form
above rather than being asserted beside it.
```

### 3.1d Injectivity

> **R10.0 amendment (finding 120): `ConstraintKind.Injectivity` was declared in
> SDD-002 §5 and no document said how to compute one.** R10.2 and R10-V4 both
> need it.

```text
INJECTION RATE — §6.1's Darcy form with the pressure difference reversed
  q_inj = 2π·k·h·(Pwf − Pr) / [ μw · ( ln(re/rw) − 0.75 + s ) ]
  Water viscosity, not oil: it is water going in.

INJECTIVITY DECLINE (R10 §2.2) — the formation plugs with solids and fines
  s(V) = s0 + α · V_cumulative / V_reference
  α and V_reference from content; remediation (an acid job or a filter upgrade)
  resets the accumulated term to zero, restoring — never exceeding — s0.

Declining injectivity is what makes water disposal an ONGOING operational
problem rather than a one-time build, and it produces the authentic case where a
field is throttled by disposal capacity and by nothing upstream at all.
```

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
// Pass 10: all three were consumed by IFluidPropertyModel and declared nowhere.
public enum FluidForm { BlackOil, ModifiedBlackOil }    // condensate, 05 §2

public sealed record PhaseSplit(
    IReadOnlyList<(MaterialId Material,
                   double GasFraction, double LiquidFraction, double AqueousFraction)> Fractions);

public sealed record ValidityRange(
    Pressure MinP, Pressure MaxP, Temperature MinT, Temperature MaxT);

public interface IFluidPropertyModel
{
    ContentId Id { get; }                            // finding 132: every 03 §3.2 slot names itself
    FluidForm Form { get; }                          // BlackOil | ModifiedBlackOil (condensate, 05 §2)
    Pressure Pb { get; }                             // bubble point (dew point for MBO)
    double Rs(Pressure p);                           // sm³ gas / sm³ oil  (dimensionless ratio of standard volumes)
    double Rv(Pressure p);                           // MBO only: sm³ condensate / sm³ gas; BlackOil ⇒ 0
    FormationVolumeFactor Bo(Pressure p);
    GasFormationVolumeFactor Bg(Pressure p);         // rm³ / sm³ — pass-6 amendment (finding 77, correcting 72): gas has its OWN bridge (ReservoirVolume ↔ StandardGasVolume); the oil FVF bridges to SurfaceVolume — the wrong family for standard gas
    FormationVolumeFactor Bw(Pressure p);            // rm³/stock-tank m³ — R5.0 finding 101
    Viscosity MuOil(Pressure p);
    Viscosity MuGas(Pressure p);
    double Z(Pressure p, Temperature t);
    PhaseSplit SplitAt(Composition c, Pressure p, Temperature t);   // pass-3: `in` dropped — Composition wraps one ImmutableArray reference; the modifier bought nothing and code/SDD now match
    ValidityRange Validity { get; }                  // outside → MODEL FAULT, never extrapolate
}
```

Standard implementation: the correlation set of [05](../design/05_SIMULATION_MODELS.md)
§2, each function carrying its published validity range. **Every correlation is
implemented against `DetMath` only.**

### 4.1 The correlation forms, transcribed (R2.7)

[05](../design/05_SIMULATION_MODELS.md) §2 names these five by author and states
none of their formulas; F-3 requires a formula cite the SDD section stating its
form, so this is that section. **Field units inside, per §2's amendment:**
p psia · T °F (°R where noted) · Rs scf/STB · μ cp · ρ g/cm³.

```text
STANDING (1947) — solution GOR and bubble point
  Rs(p) = γg · [ (p/18.2 + 1.4) · 10^(0.0125·API − 0.00091·T) ]^1.2048
  Pb    = 18.2 · [ (Rsb/γg)^0.83 · 10^(0.00091·T − 0.0125·API) − 1.4 ]
  Above Pb: Rs is CONSTANT — the undersaturated plateau (05 §2's table) — and
  the plateau value is Rs(Pb) BY THE FORWARD FORM, not the declared Rsb.
  Validity: 130–7000 psia, 100–258 °F, 16.5–63.8 °API, γg 0.59–0.95.

  WHY THE PLATEAU IS NOT Rsb (R2.7, found by the continuity test). Standing's
  two forms are only APPROXIMATE inverses: the Pb form's exponent 0.83 is a
  rounding of 1/1.2048 = 0.8299468. Round-tripping Rsb → Pb → Rs lands ~1e-4
  low, so anchoring the plateau to the declared Rsb would put a step
  discontinuity in dissolved gas exactly at the bubble point — crossing Pb would
  create or destroy solution gas, in the one place the design most wants to be
  trustworthy (05 §2's "bubble-point cliff" is a steep gradient, never a jump).
  Continuity is a physical requirement; agreeing with the declared input to five
  digits is not. The deviation is far inside the correlation's own scatter.

VAZQUEZ-BEGGS (1980) — oil formation volume factor, saturated
  Bo = 1 + C1·Rs + (T − 60)·(API/γgs)·(C2 + C3·Rs)
    API ≤ 30:  C1 = 4.677e-4   C2 = 1.751e-5   C3 = −1.811e-8
    API >  30: C1 = 4.670e-4   C2 = 1.100e-5   C3 =  1.337e-9
  γgs is γg corrected to 100 psig separator conditions; where a fluid system
  declares no separator, γgs = γg (content flag, pinned default).

UNDERSATURATED (above Pb) — isothermal compressibility
  co = (−1433 + 5·Rsb + 17.2·T − 1180·γgs + 12.61·API) / (1e5 · p)
  Bo(p) = Bob · exp( co · (Pb − p) )        // p > Pb ⇒ Bo FALLS below Bob
  This is why Bo PEAKS at Pb (05 §2): it rises with Rs below, shrinks by
  compression above.

BEGGS-ROBINSON (1975) — oil viscosity
  dead:       z = 3.0324 − 0.02023·API ; y = 10^z ; x = y·T^(−1.163)
              μod = 10^x − 1
  saturated:  A = 10.715·(Rs + 100)^(−0.515) ; B = 5.44·(Rs + 150)^(−0.338)
              μob = A · μod^B
  undersat.:  m = 2.6·p^1.187 · exp(−11.513 − 8.98e-5·p)
              μo = μob · (p/Pb)^m
  Validity: 0–2070 psig, 70–295 °F, 16–58 °API. The RISE below Pb (05 §2) is
  not special-cased: it falls out of μob climbing as Rs drops.

LEE-GONZALEZ-EAKIN (1966) — gas viscosity        [T in °R, M lb/lbmol]
  K = (9.4 + 0.02·M)·T^1.5 / (209 + 19·M + T)
  X = 3.5 + 986/T + 0.01·M
  Y = 2.4 − 0.2·X
  μg = 1e-4 · K · exp( X · ρg^Y )

DRANCHUK-ABOU-KASSEM (1975) — Z factor            [T in °R]
  Standing pseudo-criticals:  Tpc = 168 + 325·γg − 12.5·γg²
                              Ppc = 677 + 15.0·γg − 37.5·γg²
  Tpr = T/Tpc ; Ppr = p/Ppc ; ρr = 0.27·Ppr / (Z·Tpr)
  Z = 1 + (A1 + A2/Tpr + A3/Tpr³ + A4/Tpr⁴ + A5/Tpr⁵)·ρr
        + (A6 + A7/Tpr + A8/Tpr²)·ρr²
        − A9·(A7/Tpr + A8/Tpr²)·ρr⁵
        + A10·(1 + A11·ρr²)·(ρr²/Tpr³)·exp(−A11·ρr²)
  A1..A11 = 0.3265, −1.0700, −0.5339, 0.01569, −0.05165, 0.5475,
            −0.7361, 0.1844, 0.1056, 0.6134, 0.7210
  Implicit in Z. Solved by BISECTION on Z ∈ [0.2, 2.0], 60 iterations,
  tolerance 1e-10 — bisection not Newton, for the same reason §3.1 gives:
  it cannot diverge and needs no derivative. Validity: 1 ≤ Tpr ≤ 3,
  0.2 ≤ Ppr ≤ 30.

McCAIN (1990) — water formation volume factor      [T in °F, p in psia]
  ΔVwT = −1.0001e-2 + 1.33391e-4·T + 5.50654e-7·T²
  ΔVwP = −1.95301e-9·p·T − 1.72834e-13·p²·T
         − 3.58922e-7·p   − 2.25341e-10·p²
  Bw   = (1 + ΔVwP)·(1 + ΔVwT)
  Pure water, gas-free. The engine models formation water as brine only through
  its density and viscosity elsewhere; salinity's effect on Bw is under 1% over
  the range the game uses, and inventing a salinity input to carry it would add
  a content field nothing else needs. Validity: 32–260 °F, atmospheric–5000 psia.

Bg — from Z, exactly, no correlation:
  Bg = (Z · T · p_sc) / (p · T_sc)          [rm³/sm³ once both p in the same unit]
  p_sc = 101 325 Pa, T_sc = 288.706 K (60 °F) — standard conditions, SDD-004.
```

**What the MX tests can honestly pin, and what they cannot.** F-3 asks for
pinning against "reference values computed from the published papers". Values
recomputed from the same formula this file transcribes are **not independent**
and pinning to them would only test that arithmetic is repeatable. Recorded
plainly so no later reader mistakes the coverage for verification:

| Pinned by | Which properties |
|---|---|
| **Physical invariants** — genuinely independent of the formulas | `Bo ≥ 1`; `Bo` peaks at `Pb`; `Rs` flat above `Pb`; `μo` rises below `Pb`; `Z → 1` as `p → 0`; `Bg` falls monotonically with pressure |
| **Continuity** at the `Pb` boundary | every property, both branches, to 1e-9 relative |
| **Round-trip** | `Pb(Rs(Pb)) = Pb` — Standing's two forms are algebraic inverses |
| **Published worked examples** | **deferred to R5 model tests** (open item S003-4): a worked example must be transcribed from the paper by someone holding it, not reconstructed |



### 4.2 The two subsurface plugin slots

> **R5.0 amendment (finding 98): this section was numbered §4.1, as was the
> correlation section above it.** F-3 makes a formula cite the SDD section
> stating its form, and two sections sharing a number makes every such citation
> ambiguous — `SDD-003 §4.1` resolved to either the Standing transcription or a
> plugin declaration depending on which the reader found first. Renumbered.

`IDriveMechanism` is one of the eleven [03](../design/03_ARCHITECTURE.md) §3.2
replaceable models; the aquifer is its own smaller slot. §3 above names both and
neither was declared (pass 10).

```csharp
// Everything §3.1's Φ(P) needs, and nothing else. Passed as a value rather than
// as the compartment, because IDriveMechanism is a PUBLIC plugin slot and the
// compartment is truth internal to OGSim.Subsurface (§3): a plugin author gets
// the numbers the balance is written in, never the object they came from.
public sealed record MaterialBalanceInput(
    // Initial conditions. Expansion is always measured from here, so the solve
    // cannot drift (§3.1's amendment).
    Pressure InitialPressure,                   // Pi
    SurfaceVolume OriginalOilInPlace,           // N, stock-tank m³
    double GasCapRatio,                         // m, dimensionless
    double ConnateWaterSaturation,              // Swc, fraction
    double WaterCompressibility,                // cw, 1/Pa
    double RockCompressibility,                 // cf, 1/Pa

    // Cumulative to the end of this tick, from initial conditions.
    SurfaceVolume CumulativeOilProduced,        // Np, stock-tank m³
    StandardGasVolume CumulativeGasProduced,    // Gp
    SurfaceVolume CumulativeWaterProduced,      // Wp
    ReservoirVolume CumulativeWaterInflux,      // We
    ReservoirVolume CumulativeInjected,         // Vinj

    // This tick alone — the validity limit is a statement about one step, not
    // about the history.
    Pressure StartPressure,                     // Pr at the start of this tick
    ReservoirVolume WithdrawnThisTick);

// Design 02 §2.2 — a plugin deliberately: the recovery factor EMERGES from the
// mechanism, so adding EOR is adding an implementation and never editing a
// reservoir. §3.1's bisection is this interface's default implementation.
public interface IDriveMechanism
{
    ContentId Id { get; }
    Pressure SolveEndPressure(MaterialBalanceInput input, IFluidPropertyModel fluid);

    // §4.2b. Which of §3.1's terms this drive admits — and therefore which
    // compartments it will accept as coherent.
    AdmittedTerms Admits { get; }

    // Which injectants the mechanism accepts — asked, never branched on by
    // material identity (SDD-005 §4.0b, and the "one engine" architecture test).
    IReadOnlyList<ContentId> AcceptedInjectants { get; }
}

// §3.3's Fetkovich form. An aquifer IS a water compartment, so the influx it
// reports is a reservoir volume like any other withdrawal or injection.
public interface IAquiferModel
{
    ContentId Id { get; }                            // finding 132
    ReservoirVolume InfluxDuring(Pressure reservoirPressure, Duration duration);
}
```

#### 4.2b What distinguishes the six mechanisms

> **R5.0 amendment (finding 104).** [02](../design/02_DOMAIN_MODEL.md) §2.2 says
> each drive is "a different pressure-vs-withdrawal relationship and a different
> recovery factor band". §3.1 above specifies ONE balance, and neither this
> document nor [05](../design/05_SIMULATION_MODELS.md) said what the six differ
> in. Implementing them against the text as it stood would have produced one real
> class and five that differed in nothing — the stub pattern SDD-000 §4 forbids —
> or five invented physics models, which F-1 forbids harder.

A drive is defined by **which terms of §3.1's balance it admits**. That is the
standard classification and it needs no physics this document has not already
stated: a different set of active terms *is* a different pressure-vs-withdrawal
relationship, and the recovery band follows from it rather than being asserted
alongside it.

| Mechanism | Eo | m·Eg | We | Efw | Band |
|---|:--:|:--:|:--:|:--:|---|
| `solution-gas-drive` | ● | — | — | ● | MB2, 5–30% |
| `gas-cap-expansion-drive` | ● | ● | — | ● | 20–40% |
| `water-drive` | ● | — | ● | ● | MB1, 35–75% |
| `compaction-drive` | ● | — | — | ● | 5–20% |
| `gravity-drainage-drive` | ● | — | — | ● | 40–70% |
| `combination-drive` | ● | ● | ● | ● | between its parts |

`Efw` is admitted by every mechanism: connate water and rock expand whatever the
drive, and for an undersaturated reservoir above `Pb` they are the *only*
expansion there is.

**A mechanism REFUSES a compartment that contradicts it** — a solution-gas drive
handed a non-zero gas-cap ratio, or a water drive handed no influx, is a content
error, and it is caught when the compartment is built rather than showing up
later as a recovery factor nobody can explain. This is the mechanism's behaviour
in the L3 sense: it is not a tag on a compartment, it is the thing that decides
whether the compartment is coherent.

Compaction and gravity drainage admit the same terms as solution gas and are
distinguished by their **content**: compaction by a large `cf`, gravity drainage
by the tighter per-tick voidage limit its segregation depends on. Both are
recorded here as content distinctions rather than code ones, per non-negotiable
11 — and neither is given a fabricated term merely to look different.

```csharp
// The admitted-terms declaration above, as the contract's own member — so a
// mechanism's definition is readable from it rather than from a table a reader
// has to find. Checked against the compartment when it is built.
public sealed record AdmittedTerms(bool GasCap, bool AquiferInflux);
```

**`AcceptedInjectants` is a list of content ids rather than a material-kind
enum**, and that is the whole "one engine" rule in one member: the drive is
*asked* whether it takes a material, so a CO₂ flood is a content entry naming a
mechanism that accepts `co2`, not a branch on what the material is.

## 5. The well hierarchy

The PPDM four-level hierarchy of [02](../design/02_DOMAIN_MODEL.md) §3: a well
is not a hole. Identity, geometry, physics and the reservoir connection are four
types because they have four different lifetimes — a sidetrack adds a wellbore
without touching the well, a recompletion replaces a completion without touching
either.

```csharp
// Design 02 §3.4 — every transition is a command; none skips abandonment.
public enum WellStatus
{
    Proposed, Permitted, Drilling, DryHole, Logged, SuspendedNonCommercial,
    Completing, Producing, ShutIn, Workover, Injecting, Abandoned
}

public enum WellClassification { Exploration, Appraisal, Development, Injector, Observation }

public interface IWell           // identity + status machine; NO physics
{
    EntityId<IWell> Id { get; }
    WellStatus Status { get; }                       // transitions: data-driven table, commands only
    WellClassification Classification { get; }
    EntityId<ILicence> Licence { get; }
    Coordinate Surface { get; }
    IReadOnlyList<EntityId<IWellbore>> Wellbores { get; }
}

public readonly record struct TrajectoryStation(Length Md, Length Tvd, Coordinate Position);
public sealed record Trajectory(IReadOnlyList<TrajectoryStation> Stations);

public interface IWellbore       // a physical hole: the original plus each sidetrack
{
    EntityId<IWellbore> Id { get; }
    EntityId<IWell> Well { get; }
    Trajectory Path { get; }
    Length ContactLengthIn(EntityId<IReservoirCompartmentEntity> compartment);   // Path ∩ interval
    IReadOnlyList<EntityId<ICompletion>> Completions { get; }
}

public sealed record Perforation(
    EntityId<IReservoirCompartmentEntity> Drains,
    Length TopMd, Length BottomMd,
    double Skin,                                     // dimensionless
    bool Isolated);
    // Standoff (05 §3.3b): computed each tick from trajectory TVD midpoint vs
    // the compartment's nearest contact — DERIVED, never stored (law L5).
```

> **Contract pass 10 — §5 against the committed shape.** `IWell` was missing
> `Classification` and `Wellbores`, `IWellbore` was missing `Id`, `Well` and
> `Completions`, and `WellStatus`, `WellClassification`, `Trajectory` and
> `TrajectoryStation` were all used here and declared nowhere.
>
> **`Perforation` has no id, deliberately**, where this document previously gave
> it a `PerforationId` that existed in no SDD and no code. A perforation is not
> an independently addressable entity: it is a component of exactly one
> completion, it is never referenced from outside it, and nothing in the design
> resolves one by id. Giving it an id would put a second identity scheme beside
> `EntityId<T>` for no consumer — and would invite storing the derived standoff
> against it, which law L5 forbids.

### 5.1 The equipment tree

> **R6.0 amendment (finding 108).** [02](../design/02_DOMAIN_MODEL.md) §3.2 and
> [01](../design/01_CONCEPT_MATRIX.md) B6 both specify `IWellComponent`, R6.8
> lists it as a task, and it was declared in no SDD and no code. F-1 makes it
> unimplementable until stated, so it is stated here.

```csharp
// Design 02 §3.2. The INSTANCE carries only what is per-asset; everything
// specifiable — size, rating, material, performance curves, capital cost,
// install duration, degradation and failure profile, requiresTech — lives in
// the catalogue TIER it references (07 §4b).
public interface IWellComponent
{
    EntityId<IWellComponent> Id { get; }
    ContentId Tier { get; }                          // the catalogue entry
    double Condition { get; }                        // 0..1, per-asset
    GameDate Installed { get; }
}
```

**There is no component-TYPE hierarchy** — no `ITubing`, no `IPacker`, and above
all no `IChoke`. 02 §4.1's rule and non-negotiable 11 apply to well equipment
exactly as they do to facility units: a component is an instance plus a content
tier, and adding a new kind of component is a JSON entry. `ILiftMethod` is the
one apparent exception and is not one — it names the tier and nothing else,
because ESP-versus-rod-pump behaviour is entirely the tier's datasheet (§6.2).

**The choke is a component tier, not a type.** §6.3's critical-flow rule reads
its critical-pressure ratio and critical rate from the tier; `IChoke` is one of
the ~20 equipment names [22](../design/22_DESIGN_COHERENCE.md) finding 82(c)
records as never to be declared, and R6 §3's deliverable list naming it is the
same stale phase-doc text that finding corrects at each `Rn.0`.

## 6. The completion — the source element

`ICompletion : IFlowElement`. Its `Transform` is the operating-point solve —
and as a **source element** it has no inlets and reports its withdrawal as
`TransformResult.Sourced` (SDD-002 §5), which is how the element-level
conservation check covers wells.

The three algorithms below are §6.1–6.3. Pass 10 declares the contracts they
run behind — the document specified every formula and none of the signatures:

```csharp
// SDD-003 §6.1. Per PERFORATION, not per completion: multi-perforation
// commingling apportions by each perf's own kh share and skin (FV10), so a
// completion-level signature could not express the case it is built for.
public interface IInflowModel
{
    ContentId Id { get; }
    ReservoirRate InflowAt(Pressure reservoirPressure, Pressure bottomholePressure,
                           Perforation perforation);
}

// SDD-003 §6.2. Inverted relative to the physics: the VLP is naturally
// "what wellhead pressure results from this rate", but the operating-point
// bisection in §6.3 searches on Pwf, so the useful direction is the one that
// answers "what bottomhole pressure does this rate DEMAND".
public interface IOutflowModel
{
    ContentId Id { get; }
    Pressure RequiredBottomhole(ReservoirRate rate, Pressure wellheadPressure);
}

// A lift method modifies the VLP, and its TIER datasheet is the whole effect
// (07 §4b) — there is no per-method interface for ESP vs rod pump vs PCP,
// because that would be an equipment hierarchy in code (02 §4.1).
public interface ILiftMethod : IWellComponent
{
    ContentId InstalledTier { get; }

    // R7 §2.2. Declared, and violating it DEGRADES rather than refuses.
    LiftEnvelope Envelope { get; }
    EnvelopeAssessment Assess(LiftConditions conditions);

    // §6.2's three hooks, as ONE value. Every method fills the fields it uses
    // and leaves the others neutral, so the outflow model applies all four
    // uniformly and no code anywhere branches on which method is installed.
    LiftEffect EffectAt(ReservoirRate rate, Density mixtureDensity);
}

// The three VLP modifications §6.2 names, plus the power §2.4 needs.
public sealed record LiftEffect(
    Pressure PressureBoost,           // ESP: ΔP_pump(q) from the tier's curve
    double DensityFactor,             // gas lift: lightens the column, ≤ 1
    ReservoirRate? DisplacementCap,   // rod pump / PCP: a hard ceiling on rate
    Power PowerDraw);                 // ESP: feeds the stage-4 power balance

public sealed record LiftEnvelope(
    ReservoirRate MinRate, ReservoirRate MaxRate,
    Length MaxDepth,
    double MaxDeviationDegrees,
    double MaxGasFraction,
    Temperature MaxTemperature,
    double MaxSolidsFraction);

public sealed record LiftConditions(
    ReservoirRate Rate, Length Depth,
    double DeviationDegrees, double GasFraction,
    Temperature Temperature, double SolidsFraction);

// R7 §2.2's consequence, not its refusal.
public sealed record EnvelopeAssessment(
    bool Within,
    double PerformanceFactor,         // ≤ 1: what the method still delivers
    double HazardMultiplier,          // ≥ 1: how much sooner it fails (R18)
    IReadOnlyList<ContentId> Exceeded);

// §6.3's outcome. DEAD is a distinct result and NOT a zero rate: "cannot flow
// at any rate" and "produced nothing this tick" have different remedies, and
// the read model and well.diedNaturally both consume the distinction (R6-V6).
public abstract record OperatingPoint;
public sealed record Flowing(ReservoirRate Rate, Pressure Bottomhole) : OperatingPoint;
public sealed record Dead : OperatingPoint;

public interface ICompletion : IFlowElement
{
    EntityId<ICompletion> CompletionId { get; }
    EntityId<IWellbore> Wellbore { get; }
    IReadOnlyList<Perforation> Perforations { get; }
    ILiftMethod? Lift { get; }                       // null: natural flow
    OperatingPoint SolveOperatingPoint(Pressure wellheadBackpressure);

    // SDD-002 §7 S4. True while the choke reports critical flow (§6.3): the
    // completion keeps its rate and ignores backpressure until sub-critical.
    // Owned HERE because the choke that decides it is the completion's own
    // component — the solver only asks.
    bool IsPressureDecoupled { get; }
}
```

> **R6.0 amendment (finding 107): the solver's completion type and this one were
> two names for one concept.** R4 declared `ICompletionTarget` in `OGSim.Flow`
> with `Id`, `OperatingPointAt` and `IsPressureDecoupled`, because its synthetic
> tests could not supply a wellbore or a perforation list. That is a testing
> convenience, and it bought a naming-law violation (N1) plus a seam R6 would
> have had to bridge with an adapter — at which point "one engine" would be true
> everywhere except where the wells attach.
>
> `ICompletionTarget` is removed. The solver takes `ICompletion`, and
> `IsPressureDecoupled` moves here, where §6.3 already decides it.
>
> **The completion list goes too.** `FlowSolver` took completions as a separate
> constructor argument alongside the topology, so the same completion was
> reachable twice and the two could disagree — which is exactly the defect FV7
> exposed at R4, where a synthetic source and a synthetic completion shared an id
> and were different objects, so S3's cap adjusted one while the network's mass
> came from the other. Since `ICompletion : IFlowElement`, every completion is
> already IN the network: the solver finds them there, in the topological order
> it already computed, and the disagreement is no longer expressible.

> **R7.0 amendment (finding 110): `ILiftMethod` could not express a single one
> of §6.2's lift hooks.** It declared `InstalledTier` and nothing else, so a
> method had no way to state its pressure boost, its density reduction or its
> displacement cap — §6.2 specified three effects against an interface with no
> member capable of carrying any of them, and R7.1's "envelope declaration" task
> named a type that existed nowhere.
>
> The four effects are returned as ONE value rather than as four members,
> because that is what keeps the method-agnostic promise real: the outflow model
> applies all four fields uniformly, a gas-lift method leaves `PressureBoost` at
> zero because it genuinely adds no pressure, and no code anywhere asks which
> method is installed. Four separate members would have invited
> `if (lift is Esp)` at the first call site that needed only one of them.
>
> `ILiftMethod : IWellComponent` per [01](../design/01_CONCEPT_MATRIX.md) B7 and
> R7 §2.1 — lift is installed, pulled and degraded like any other component, and
> declaring it outside the component tree would have made it the one piece of
> equipment R18 could not wear out.

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

### 6.1b The produced stream — what the completion puts on the network

> **R20d.3 amendment (finding 162).** §6.1 above pins the oil conversion and
> says nothing about what else comes up the hole, so `Completion` shipped with a
> single material ordinal: a well that can only ever produce one substance. That
> is the silence expressed as a field, and it is why a separator's gas and water
> legs had nothing to carry.

```text
q_rc from §6.1 is the TOTAL LIQUID rate at reservoir conditions. The sandface
water cut fw (§3.1c) splits it; Rs lifts the solution gas out of the oil:

  q_rc,oil   = q_rc · (1 − fw)
  q_rc,water = q_rc · fw
  q_sc,oil   = q_rc,oil   / Bo(Pr)
  q_sc,water = q_rc,water / Bw(Pr)
  q_sc,gas   = q_sc,oil · Rs(Pr)              // sm³ gas per sm³ stock-tank oil
  mass_m     = q_sc,m · ρ_sc,m                 per material m
  ρ_sc,gas   = γg · ρ_air,sc                   // SDD-001 §1's constant; γg is air = 1
```

**Split the RATE, not the ratio.** The obvious form —
`q_sc,water = q_sc,oil · fw/(1−fw)` — divides by zero at `fw = 1`, exactly when a
well has watered out and the answer matters most. Splitting `q_rc` has no
singularity: at `fw = 1` the oil term is zero and the completion is a water
producer, which is the physical statement rather than a guard.

**Before breakthrough the water term is structurally zero**, and that is §3.1c's
own result rather than an omission: `krw = 0` at or below connate saturation, so
`fw = 0` and no water flows at all. A compartment that has never been flooded and
has no aquifer influx produces none — so this form is complete on the day it is
written, and R20d.4 is "advance the saturation", not "add water to the stream".

**Solution gas is produced with the oil and is not free gas.** `Rs(Pr)` is the
gas *dissolved* in stock-tank oil at the compartment's current pressure
(§3.1's Standing correlation), so it falls as the reservoir depletes below
bubble point — which is the producing-GOR rise §3.1 already models through `Rp`.
A gas cap's free gas is a separate term and arrives with the gas-cap drive.

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
| S003-4 | **§4.1's correlations are pinned by invariants, continuity and round-trip — not yet against published worked examples.** Each of the five papers prints at least one; transcribing them requires the paper in hand, and a value reconstructed from the same formula the code implements verifies nothing. Until then a transcription error that preserves monotonicity would survive | R5 model tests (MX-class), before any CAL band is trusted |
| S003-5 | **§6.2's friction term states ρ_mix and pins no viscosity**, so the Reynolds number has no declared source. R6 implements it against a fixed 1e-3 Pa·s and says so in the code; the honest fix is the fluid model's own μ at the traverse midpoint, which needs §6.2's two-pass ρ_mix evaluation to be carrying μ as well | R7 (lift touches the same traverse) |
