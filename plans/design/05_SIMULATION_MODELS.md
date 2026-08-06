# 05 — Simulation Models

**Status:** draft · **Date:** 2026-08-06

> **Affects:** 02, 04, 07, 10, 12, 13, 14, 17, phases · **Affected by:** 02, 04, 07, 10, 13, 14, 17
> *(strong couplings only — the full row is in [22](22_DESIGN_COHERENCE.md) §2)*

The equation catalogue. Every model the engine uses, in the form it uses it, with
its inputs, its validity range, and the gameplay it produces.
[04_MATERIAL_AND_FLOW](04_MATERIAL_AND_FLOW.md) says where each is applied; this
document says what each *is*.

> **Notation:** all symbols are industry-standard. All are implemented as
> dimensioned `IQuantity` values, never bare numbers — the unit sets below are
> the conventional field units, but the engine's canonical internal set is
> declared in [10_CONTENT_AND_UNITS](10_CONTENT_AND_UNITS.md).

---

## 1. Volumetrics — how much is down there

### 1.1 Original oil in place (STOOIP)

```
N = 7758 · A · h · φ · (1 − Sw) / Boi          [stb]
```

| Symbol | Meaning | Unit | Typical |
|---|---|---|---|
| `A` | Area | acres | 100 – 20,000 |
| `h` | Net pay thickness | ft | 10 – 300 |
| `φ` | Porosity | fraction | 0.05 – 0.35 |
| `Sw` | Water saturation | fraction | 0.15 – 0.50 |
| `Boi` | Initial oil formation volume factor | rb/stb | 1.05 – 1.7 |
| `7758` | Barrels per acre-foot | — | constant |

### 1.2 Original gas in place (GIIP)

```
G = 43560 · A · h · φ · (1 − Sw) / Bgi         [scf]
```

### 1.3 Recoverable volume

```
Recoverable = N · RF
```

Recovery factor `RF` is **not a constant**. It is produced by the drive mechanism
(§3) interacting with the development plan. Reference bands:

| Drive mechanism | Typical RF | Pressure behaviour |
|---|---|---|
| Solution gas drive | 5 – 30 % | Falls fast |
| Gas cap expansion | 20 – 40 % | Falls moderately |
| Water drive (strong) | 35 – 75 % | Nearly maintained |
| Gravity drainage | 40 – 80 % | Slow, needs vertical relief |
| Compaction | 10 – 25 % | Falls, with subsidence |
| **+ Waterflood** | +10 – 25 pts | Maintained by injection |
| **+ Gas injection** | +5 – 15 pts | Maintained by injection |

**Gameplay:** the player's estimate of `RF` at sanction time decides whether a
field is developed. Getting it wrong by 10 points is the difference between a
company-making project and a bankruptcy. And because `RF` **emerges** from the
drive mechanism rather than being a stored number, identifying the drive early —
from pressure data — is genuinely valuable information.

### 1.4 Uncertainty

Every volumetric input is a distribution, not a scalar. In-place volume is
therefore a distribution, and because it is a **product** of uncertain terms it
comes out **log-normal** — long-tailed to the upside. This is why the industry
quotes P90 / P50 / P10 and why the mean exceeds the median.

**This asymmetry is a real and teachable piece of the game.** Most discoveries
disappoint relative to the mean; a few vastly exceed it. A player who budgets on
the mean goes broke; a player who budgets on P90 and is delighted by the upside
survives.

---

## 2. Fluid properties (black-oil model)

The engine uses black-oil correlations: oil, gas and water as three pseudo-
components, with gas dissolving into and evolving out of oil as pressure changes.
Standard industry correlations (Standing, Vazquez-Beggs, Beggs-Robinson, Lee et
al. for gas viscosity, Dranchuk-Abou-Kassem for Z-factor) supply the parameters.

| Property | Symbol | Behaviour that matters |
|---|---|---|
| Bubble point pressure | `Pb` | **The most important number in the reservoir.** Above it, oil is undersaturated and behaves simply. Below it, gas comes out of solution and everything gets worse at once. |
| Solution gas-oil ratio | `Rs` | Gas dissolved in oil. Constant above `Pb`; falls with pressure below it. |
| Oil formation volume factor | `Bo` | rb/stb. Rises to a peak at `Pb`, then falls as gas leaves. **This is why reservoir barrels ≠ stock tank barrels.** |
| Oil viscosity | `μo` | Falls as pressure rises to `Pb`; **rises sharply below `Pb`** as light gas leaves the oil behind. |
| Gas formation volume factor | `Bg` | Gas is enormously compressible — `Bg` changes by orders of magnitude over field life. |
| Gas compressibility factor | `Z` | Deviation from ideal gas. Drives the `p/Z` material balance for gas reservoirs. |
| API gravity | `°API` | `141.5 / SG − 131.5`. Sets the price grade. Note it is a **nonlinear** transform of density — a classic source of unit errors, prevented here by dimensioned quantities. |

**Validity note — gas condensate.** Plain black-oil covers oil and dry/wet
gas. A **gas-condensate** reservoir (liquid drops out *in the reservoir* as
pressure falls below the dewpoint — retrograde behaviour) uses the **modified
black-oil form**: a condensate-gas ratio mirroring `Rs`, with liquid dropout
represented at the CGR level rather than compositionally. Each `fluid-system`
content file declares which form it requires, and running a fluid under the
wrong form is a **model fault**, not an approximation. The gameplay the form
carries is real: producing a condensate field too fast below dewpoint *leaves
the most valuable liquids stranded in the rock* — a pacing decision with a
permanent consequence.

**The bubble-point cliff** is a designed drama beat and it costs nothing to
produce, because it is simply what the correlations do: as `Pr` crosses `Pb`,
GOR climbs, oil viscosity climbs, `Bo` falls, the IPR switches from Darcy to
Vogel, and gas-handling demand spikes. The player sees production fall faster
than any decline curve predicted, and the reason is discoverable.

---

## 3. Reservoir behaviour — pressure over time

### 3.1 Tank material balance

The compartment is a tank. Withdraw fluid; pressure falls by an amount set by how
much the remaining fluid and rock expand to fill the space, plus any influx or
injection.

```
Expansion of oil + gas + connate water + rock  +  water influx  +  injection
      =  volume withdrawn (at reservoir conditions)
```

Applied per tick to solve for the new `Pr`.

**Integration policy, stated because it was previously implicit:** the flow
solve uses **start-of-tick pressure** for the whole tick; the material balance
then solves **end-of-tick pressure** from the withdrawn mass. This is explicit
first-order integration, and its error grows with the fraction of a
compartment's expansion capacity withdrawn in one tick. Two guards keep it
honest rather than silently wrong:

1. **A validity limit:** a compartment asked to deliver more than a declared
   fraction of its expansion capacity in a single tick raises a **model fault**
   (the R2-V10 out-of-range policy) — the tick is not fudged. In practice this
   binds only on absurdly small compartments with absurdly large offtake, which
   is a content error worth catching anyway.
2. **A reference test:** R5-V11 compares the one-step solution against a
   sub-stepped reference on the steepest realistic decline and asserts the error
   stays inside the calibration bands. If monthly explicit stepping is ever too
   coarse, the fix is sub-stepping the *material balance* (cheap — no network
   re-solve), not shortening the tick.

### 3.2 Gas reservoirs: the `p/Z` line

For a volumetric (no water drive) gas reservoir:

```
p/Z  =  (pi/Zi) · (1 − Gp/G)
```

Plotting `p/Z` against cumulative gas produced `Gp` gives a **straight line**
whose x-intercept is the gas initially in place `G`.

**This is the single best information mechanic in the game.** The player does not
have to be told how big their gas reservoir is. They produce for a couple of
years, plot `p/Z`, extrapolate the line, and *deduce* it. The `p` on that plot
is **average reservoir pressure, which only a shut-in build-up survey measures**
([06](06_WORLD_AND_EXPLORATION.md) §3.1) — so each point on the line was paid
for in deferred production. And if the line bends
upward, they have water drive — a completely different future. Real technique,
real deduction, trivially implementable, deeply satisfying.

### 3.3 Aquifer influx

Water encroachment from a connected aquifer, as a function of pressure drop and
elapsed time (Fetkovich-style finite aquifer, or a simpler steady-state model at
lower fidelity). Strong aquifer support means maintained pressure and high
recovery — but also **early water breakthrough**, which brings the whole
water-handling cost chain forward.

**A strong aquifer is both the best and the worst news a player can get**, and
which one it turns out to be depends on whether they built water handling in
time. That ambiguity is good design.

### 3.3b Contacts, standoff and coning — why *where* you perforate matters

The compartment tracks its fluid contacts (GOC, OWC) moving through life
([02](02_DOMAIN_MODEL.md) §2.1). Each perforation carries a **standoff** — its
vertical distance to the nearest contact — and the coning proxy turns that into
per-well breakthrough behaviour:

```
critical rate  ∝  (kv/kh)⁻¹ · Δρ · h_standoff²    (Meyer-Garder form, simplified)
```

Produce a well **below** its critical rate and the contact stays away; produce
above it and a cone grows toward the perforation — water (or gas-cap gas)
arrives at *that well* well before the field's average breakthrough, and backing
the rate off lets the cone subside slowly.

**What this buys, concretely:** perforation placement (DDV6) becomes physics
instead of a label; choke policy (DPR2) gets its real meaning — pulling a well
hard near a contact is borrowing from the future; horizontal wells' standoff
advantage is expressible; and zonal shutoff (DPR4) is the remedy when the cone
has won. Without this model, per-well water arrival would be uniform across a
compartment, and half the completion decisions in
[20](20_PLAYER_DECISIONS.md) would have no mechanism behind them.

Fidelity: the proxy is the *standard* level; *arcade* uses field-average
breakthrough only; *simulation* adds cone-height hysteresis. Registered as
`IConingModel`.

### 3.4 Decline curves (Arps) — as a check, not as the model

```
Exponential   q(t) = qi · e^(−D·t)                 b = 0
Hyperbolic    q(t) = qi / (1 + b·D·t)^(1/b)        0 < b < 1
Harmonic      q(t) = qi / (1 + D·t)                b = 1
```

**Production is not generated from these.** It is generated from the network
solve, which is driven by material balance. Arps is used two ways:

1. **As a validation harness** — a solved single-well tank reservoir must produce
   a curve that fits a hyperbolic with a plausible `b`. If it does not, the
   physics is wrong. (Test FV2 in [04](04_MATERIAL_AND_FLOW.md) §9.)
2. **As the player's forecasting tool** — the player fits a decline curve to
   their own production history to forecast and to book reserves, exactly as the
   industry does. The engine provides the fitting; the forecast can be wrong,
   because the future is driven by physics the fit does not know about.

That second use is genuinely delicious: the player's forecast is a *model of a
model*, and the gap between forecast and outcome is the game telling them
something about their reservoir.

---

## 4. Well inflow (IPR)

### 4.1 Undersaturated — Darcy radial flow

```
qo = [ k · h · (Pr − Pwf) ] / [ 141.2 · μo · Bo · ( ln(re/rw) − 0.75 + s ) ]
```

| Symbol | Meaning |
|---|---|
| `k` | Effective permeability (mD) |
| `h` | Net pay (ft) |
| `re`, `rw` | Drainage radius, wellbore radius (ft) |
| `s` | **Skin** — dimensionless near-wellbore damage (+) or stimulation (−) |

Everything before the drawdown term is the **productivity index `J`**; the
relationship is linear in drawdown.

### 4.2 Saturated — Vogel

Below bubble point, free gas in the pore space reduces oil mobility and the IPR
bends:

```
qo / qo,max  =  1 − 0.2 · (Pwf/Pr) − 0.8 · (Pwf/Pr)²
```

**Composite IPR** when `Pr > Pb > Pwf`: linear above `Pb`, Vogel below. This is
the standard industry treatment and it is what the engine uses.

### 4.3 Skin — the player's most leveraged number

```
Hawkins:   s = ( k/kd − 1 ) · ln( rd/rw )
```

Skin captures everything that happens in the few feet around the wellbore:

| Source | Skin effect |
|---|---|
| Drilling mud damage | +5 to +20 |
| Fines migration, scale, wax | rising over time |
| Partial penetration (only part of the zone perforated) | positive |
| Acid stimulation | back toward 0 |
| Hydraulic fracture | **−3 to −6** (equivalent) |
| Horizontal well | large negative equivalent |

A skin of +10 can cost **half** the well's productivity. Removing it costs a
fraction of a new well. **Diagnosing skin from a well test and deciding whether
to stimulate is one of the best-value decisions available to the player**, and it
is a real one that real engineers make constantly.

### 4.4 Horizontal wells

Modelled through effective drainage geometry — a horizontal contact length of
3,000 ft against a vertical well's 60 ft of pay is a large productivity gain, at
higher drilling cost and higher risk of intersecting water or a fault.

### 4.5 Gas well inflow

```
qg = C · ( Pr² − Pwf² )ⁿ
```

with `n` between 0.5 (fully turbulent) and 1.0 (laminar). Note the **pressure-
squared** form: gas deliverability collapses much faster than oil as reservoir
pressure declines, which is why gas fields need compression added in stages
throughout their lives.

---

## 5. Well outflow (VLP)

Total pressure loss up the tubing:

```
ΔP_total  =  ΔP_hydrostatic  +  ΔP_friction  +  ΔP_acceleration
```

| Term | Driven by | Behaviour |
|---|---|---|
| **Hydrostatic** | Average fluid density × vertical height | Usually dominant. Falls when gas lightens the column; rises hard when water loads it |
| **Friction** | Velocity², diameter, roughness | Grows steeply with rate; sets the upper limit for narrow tubing |
| **Acceleration** | Gas expanding up the well | Small except in high-rate gas wells |

### 5.1 The tubing-size trade

| Tubing | Risk |
|---|---|
| Too narrow | Friction-dominated — the well cannot flow its potential |
| Too wide | Velocity too low to carry liquid — the well **loads up and dies** |

There is a right answer, it depends on rate, and **the right answer changes over
field life** — which is why tubing is pulled and re-run. A genuine, recurring,
non-obvious decision.

### 5.2 Artificial lift models

| Method | Model | Envelope | Fails on |
|---|---|---|---|
| Gas lift | Injected gas reduces column density above the injection point | Wide rate range; tolerant | Needs compressed gas; efficiency falls at high water cut |
| ESP | Pump curve (head vs rate) added at setting depth | High rate, high water cut | Free gas at the intake; solids; heat; **power** |
| Rod pump | Volumetric displacement: stroke × rate × efficiency | Low rate, shallow, late life | Deviation; gas interference; rate ceiling |
| PCP | Volumetric, low shear | Viscous, sandy | Elastomer wear; temperature |

Each has a capital cost, an operating cost (ESPs are power-hungry), a failure
hazard rate, and a workover cost when it fails. **The failure rate is the
interesting part**: ESPs deliver the most and break the most, so the ESP-vs-rod-
pump decision is a genuine risk/return call rather than a strict upgrade.

---

## 6. Surface and transport

### 6.1 Liquid pipeline — Darcy-Weisbach

```
ΔP = f · (L/D) · (ρ v² / 2)
```

Friction factor `f` from the Reynolds number (Colebrook-White in the turbulent
regime). **Pressure drop scales roughly with the square of rate and inversely
with roughly the fifth power of diameter** — which is why looping a line, or
stepping up one pipe size, produces a startlingly large capacity gain. That
non-linearity is a good thing for the player to discover.

### 6.2 Gas pipeline — Panhandle / Weymouth

```
Q  ∝  ( P1² − P2² )^0.5 · D^2.667 / ( L · SG · T · Z )^0.5
```

Again pressure-squared: a gas line's capacity depends strongly on inlet pressure,
so declining field pressure silently erodes export capacity years before anyone
notices. Compression is the answer, and it must be added *ahead* of the problem.

### 6.3 Erosional velocity

```
v_max = C / √ρ_mixture           (API RP 14E form, C ≈ 100 for continuous service)
```

Exceeding it damages the pipe: a rising hazard rate for a failure incident rather
than a hard block. Fast is possible; fast is expensive later.

### 6.4 Compression

```
Power ∝ Q · [ (Pout/Pin)^((k−1)/k) − 1 ] / η
```

Power rises with the **pressure ratio**, and a single stage is limited to a ratio
of roughly 3–4 before discharge temperature becomes unmanageable. Higher ratios
need stages with interstage cooling. As reservoir pressure declines, the required
ratio climbs, so compression is added in stages over field life — an ongoing
capital commitment, not a one-time build.

### 6.5 Separation

Phase split at (P, T) from each material's phase behaviour, with:

- **Efficiency** per phase pair (never 100%)
- **Carry-over** — liquid escaping into the gas outlet
- **Carry-under** — gas escaping into the liquid outlet
- **Two capacity limits** — gas handling and liquid handling, independently binding
- **Residence time** — undersized vessels separate poorly at high rate

**Multi-stage separation** recovers more stock-tank liquid by dropping pressure
progressively rather than in one step. The optimum number of stages and their
pressures depends on the fluid — a real optimisation, worth real money, with a
computable answer the player can reach by experiment.

---

## 7. Degradation, failure and maintenance

### 7.1 Condition

Every piece of equipment carries a condition that decays with **service
severity**, not merely with time:

```
decay_rate = base_rate · (1 + severity_factors)
```

| Severity factor | Affects |
|---|---|
| Water cut | Corrosion — all wetted equipment |
| H₂S / CO₂ content | Corrosion — severe; may require special metallurgy |
| Sand production | Erosion — chokes, valves, pumps |
| Duty cycle | Rotating equipment — compressors, pumps, ESPs |
| Temperature | Elastomers, seals |
| Time since last service | Everything |

### 7.2 Failure hazard

```
hazard(t) = base_hazard · f(condition)          — rising sharply as condition falls
```

Failures are drawn from the seeded RNG, so they are deterministic under replay
but unpredictable in play. A failure takes the element out of the network — and
because the solver simply omits unavailable elements, the production loss and its
bottleneck attribution are computed automatically with no special handling.

### 7.3 Maintenance strategies

| Strategy | Cost | Result |
|---|---|---|
| Run to failure | Lowest planned cost | Unplanned downtime, longer outages, collateral damage |
| Scheduled | Moderate, predictable | Fewer failures, some unnecessary work |
| Condition-based | Higher (needs monitoring tech) | Best availability, needs the tech unlocked |

**A genuine strategic choice with no dominant answer**, which is what makes it
worth including. Marginal wells rationally run to failure; the main export
compressor rationally gets condition monitoring.

---

## 8. Hazards and incidents

| Incident | Trigger | Consequence |
|---|---|---|
| Equipment failure | Condition-driven hazard | Downtime, repair cost, deferred production |
| Well control / blowout | Drilling into overpressure without adequate mud weight | Severe: cost, casualties, environmental, reputational, regulatory |
| Hydrate blockage | Cold + wet + high pressure | Line blocked until remediated |
| Wax deposition | Cold + waxy crude | Capacity loss, pigging required |
| Corrosion failure | Cumulative wet + sour service | Leak → spill → penalty |
| Spill | Line or tank failure | Cleanup cost, fine, licence risk |
| Sand production | High drawdown in unconsolidated rock | Erosion; may require sand control |
| Souring | Sulphate-reducing bacteria after long waterflood | Fluid turns sour mid-life — **metallurgy chosen years earlier may now be wrong** |
| Scale deposition | Hard produced water + pressure/temperature drop across perforations and chokes | Flow restriction that mimics rising skin; acid wash or scale inhibitor — **diagnosing scale versus true skin is a real well-test question** |
| Legacy well leak | A degraded plug on an abandoned well, decades on (P6) | The liability that outlives the field: environmental release from an asset producing nothing, re-plug operation, reputational damage — the campaign's long tail made physical |

Each has a **mitigation the player can buy in advance** and a **cost of not
having done so**. That is the whole design of the hazard system: hazards are not
random punishment, they are the price of decisions made earlier.

---

## 9. Model registry

Every model above is a plugin. Fidelity levels are per model, so a player (or a
scenario) can mix — realistic reservoirs with simplified pipelines, say.

| Contract | Standard implementation | Alternatives |
|---|---|---|
| `IVolumetricModel` | Deterministic + log-normal uncertainty | Monte Carlo |
| `IFluidPropertyModel` | Black-oil correlations | Table lookup; simplified constants |
| `IMaterialBalanceModel` | Tank with drive mechanism | `p/Z` for dry gas; multi-compartment |
| `IDriveMechanism` | Per type (6 implementations) | + waterflood, gas injection, CO₂ |
| `IAquiferModel` | Finite aquifer influx | Steady-state; none |
| `IConingModel` | Critical-rate proxy per perforation standoff (§3.3b) | Field-average breakthrough (arcade); cone hysteresis (simulation) |
| `IInflowModel` | Darcy/Vogel composite | Productivity index; Fetkovich; gas back-pressure |
| `IOutflowModel` | Hydrostatic + friction, phase-aware | Fixed drop; correlation-based gradient |
| `ILiftModel` | Per method (4 implementations) | — |
| `IHydraulicModel` | Darcy-Weisbach / Panhandle | Fixed capacity |
| `ISeparationModel` | Efficiency-based with carry-over | Perfect split; multi-stage flash |
| `ICompressionModel` | Polytropic staged | Fixed power |
| `IDegradationModel` | Severity-weighted decay | Linear; none |
| `IHazardModel` | Condition-driven hazard rates | Off; punishing |
| `IWeatherModel` | Seasonal baseline + stochastic + extremes + persistence | Fixed availability %; none |
| `IForecastModel` | Accuracy declining with horizon | Perfect; none |
| `IEnvironmentEffectModel` | Setting → restriction / envelope / parameter | Cost multipliers only |
| `IBarrierModel` | Strength derived from condition, competency, procedure | Simplified single-barrier |
| `IBowTieModel` | Threat → barriers → top event → barriers → consequence | Flat hazard probability |
| `IEsgModel` | Performance → standing → cost of capital | Off |

The last five arrive with [R22](../phases/R22_ENVIRONMENT.md) and
[R23](../phases/R23_HSE.md). They sit in the same registry and obey the same
fidelity-dial rule as the physical models — an "off" implementation is a complete,
tested model that produces no effect, never a stub.

---

## 10. Calibration and validation

**The models must produce recognisable numbers, or the game teaches falsehoods.**

| # | Check | Reference |
|---|---|---|
| CAL1 | A 100 MMstb field with water drive recovers 35–55% over ~25 years | Industry norm |
| CAL2 | A solution-gas-drive field recovers 8–25% and declines steeply after `Pb` | Industry norm |
| CAL3 | Water cut follows a recognisable S-curve after breakthrough | Industry norm |
| CAL4 | GOR is flat above `Pb`, then rises sharply | Physics |
| CAL5 | A gas field's `p/Z` plot is linear for a volumetric reservoir | Physics — exact |
| CAL6 | Decline fits Arps with `b` in 0.0–1.0 (typically 0.2–0.7) | Industry norm |
| CAL7 | Lifting cost per barrel rises through field life as water cut climbs | Economics |
| CAL8 | A skin of +10 costs roughly half of productivity | Analytic — exact |
| CAL9 | Doubling pipeline diameter raises capacity several-fold, not two-fold | Analytic — exact |
| CAL10 | Gas deliverability declines faster than oil for the same pressure drop | Analytic — exact |

CAL5, CAL8, CAL9 and CAL10 are **exact** analytic checks — the engine must match the
closed-form answer. The rest are **band** checks against industry norms, with the
band recorded in the test so a tuning change that leaves the realistic range
fails loudly rather than drifting.

---

## 11. Open decisions

| # | Decision | Options | Recommendation |
|---|---|---|---|
| SD1 | Correlation set | (a) one standard set, (b) selectable per fluid type | **(a)** — a second set adds no gameplay |
| SD2 | Uncertainty propagation | (a) analytic (log-normal product), (b) Monte Carlo at world-gen | **(b) at world-gen, (a) in play** — MC once is cheap; per-tick is not |
| SD3 | Multi-layer completions | (a) one layer per perforation, (b) crossflow between layers | **(a) first** — crossflow is a fine later addition, and a good technology unlock |
| SD4 | Temperature | (a) tracked through the chain, (b) assumed | **(a)** — hydrates, wax, viscosity and treating all need it, and it is cheap |
| SD5 | Sand production | (a) modelled, (b) omitted at first | **(b)** — real but adds a whole sand-control sub-domain; a good expansion |
