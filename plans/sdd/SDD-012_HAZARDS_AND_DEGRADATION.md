# SDD-012 — Hazards and Degradation

**Status:** drafted · **Serves:** R18 and R23 · **Design docs:** [05](../design/05_SIMULATION_MODELS.md) §7–8, [R18](../phases/R18_HAZARDS.md), [14](../design/14_HSE.md) §2

Condition, failure and maintenance in implemented form — the decay law, the
hazard law, and exactly when the dice are rolled.

---

## 0. The two replaceable models

Both are [03](../design/03_ARCHITECTURE.md) §3.2 slots — the hazard model is
named there explicitly as the "off ↔ realistic ↔ punishing" difficulty dial —
and this document pinned both algorithms without declaring either interface
(contract pass 10).

```csharp
// The §1 severity sum, as an argument rather than as ambient state: both models
// are BLIND to everything but their declared inputs, which is what lets a
// scenario swap them without the engine noticing.
public sealed record ServiceSeverity(
    double WaterCut,
    double SourFraction,
    double DutyFraction,
    double OverTemperature,       // max(0, T_amb − T_rated) / T_span
    double TicksSinceService);

public interface IDegradationModel      // §1: severity in, condition out
{
    ContentId Id { get; }
    double NextCondition(double condition, ServiceSeverity severity, Duration dt);
}

public interface IHazardModel           // §2: condition in, probability out
{
    ContentId Id { get; }
    double FailureProbability(double condition, Duration dt);
}
```

**Neither model draws.** `IHazardModel` returns a probability and the engine
performs the draw at stage 4, consuming only the `Hazard` stream — the same
separation §3 of [SDD-008](SDD-008_INFORMATION_AND_BELIEFS.md) applies to
`IObservationModel`, and for the same reason: a plugin that drew its own numbers
could consume a different count and shift every later draw in that stream,
breaking the independence R1-V5 guarantees.

## 1. Condition decay

Per component instance, per tick, at stage 4, from **previous-tick service**
(the 03 §6.1 lag rule):

```text
c ∈ [0,1];   Δc = − baseRate(tier) · (1 + Σ severityTerms) · Δt(years)
severityTerms (coefficients per equipment class, content):
  k_w · waterCut  +  k_s · sourFraction  +  k_d · dutyFraction
  + k_T · max(0, T_amb − T_rated)/T_span  +  k_i · yearsSinceService
Environmental terms (marine air, ice) enter as the location profile's declared
severity contributions — the R18 §2.7 factors, same sum.
```

Clamped at 0; **never restored implicitly** — only a completed maintenance/
repair operation sets `c = restoreLevel(opTemplate)`.

## 2. Failure hazard — the pinned law

```text
λ(c) = λ_base(tier) · exp( k_h · (1 − c) )          [1/year]   k_h content per class
p_fail(tick) = 1 − exp( −λ · Δt )
One draw per component per tick from the `hazard` stream (fixed component
order: ascending EntityId). On failure:
  failure day d ~ UniformInt{0..29} from the SAME stream (next value) —
  integer days, so the segment boundary lands on the /30ths grid exactly
  → segment boundary at d/30 (SDD-002); repair operation auto-proposable (Advisor)
  → audited (component, c, λ, draws) — the fairness record
Repair duration/cost from the tier datasheet; early-generation tiers carry
higher λ_base (07 §4b.3), which is the whole proven-vs-new trade.
```

Exponential-in-(1−c), not a threshold: the player who "sits just above the
line" is not rewarded, deferral cost grows smoothly (R18 §2.2 rationale).

## 3. Maintenance strategies

```text
Per asset class (inherited, overridable per asset — R18 risk note):
  RunToFailure:    nothing scheduled
  Scheduled:       maintenance op template every N ticks (content)
  ConditionBased:  requires monitoring tier installed (C14); trigger c < c_trig
All three produce ordinary SDD-007 operations — no special execution path.
```

## 4. Non-equipment hazards

Hydrate/wax/erosion/blowout/spill triggers evaluate their condition margins
(SDD-006 §6 flags, SDD-007 §4 disaster hook) into **threat rates**. Composition
decides the consumer:

| Composition | Consumer |
|---|---|
| With R23 | Threats enter the bow-tie; barriers (derived from §1's conditions, competency, procedure — 14 §2.2) resolve near-miss / top event |
| Pre-R23 / arcade-HSE-off | Threats resolve directly through `IHazardModel` — a complete shipped configuration (the HS-D5 "off/simple" level), not a stub |

## 4b. The bow-tie, as arithmetic (R23)

```text
Barrier strength (derived, never stored — INV10):
  s_i = min( minCondition(elements in the barrier's element set),
             crewCompetency, procedureCompliance )        each ∈ [0,1]
  — weakest-link min, the safety doctrine; competency from crew training
  state (R12.7), procedureCompliance from open-findings backlog (content map)

Threat resolution (stage 4, per materialised threat, `hazard` stream,
fixed order: threat id):
  each preventive barrier holds with probability s_i (one draw per barrier)
  all fail        → TOP EVENT → mitigating barriers sampled the same way to
                    select the consequence tier (14 §8 table)
  some fail       → NEAR MISS naming exactly the failed barriers — the free
                    warning, at the price of the draws already made
  none fail       → suppressed-threat count ++ (a leading indicator, unshown
                    by default but queryable)
Barrier independence is a stated simplification (14 §... QRA exclusion) —
the bow-tie carries the decision, not the correlation structure.
```

**ESG standing** (0–100):
`standing = 100 − Σ_k w_k · bandScore_k(intensity_k) − incidentPoints`, where
intensities are emissions/methane/flaring per unit produced scored against
content band tables, and incidentPoints decay with a content half-life —
so a clean decade genuinely rehabilitates. The lender spread table (SDD-009
§5) reads this number.

> **R20d.16 amendment. The scale, reconciled, and what is computable today.**
> This section states standing on 0–100 and `IReserveBasedLending.Redetermine`
> takes a FRACTION. They are the same number: 0–100 is the presentation scale a
> host renders and 0–1 is what crosses a contract, like every other fraction in
> the engine. Stated so the next reader does not implement a spread against 40.
>
> **Of the three terms, one has a subject.** Flaring intensity is computable now
> — the separator's gas leg goes to the flare and stage 6 already accounts what
> it burned — and it is the term that dominates a producing field's record.
> Emissions and methane need equipment that vents rather than a flare that burns,
> and `incidentPoints` needs incidents, which is R18's. The formula is not
> truncated: the missing terms contribute nothing because nothing has happened
> yet, which is the correct answer rather than a placeholder for one.
>
> **Intensity is per unit PRODUCED, which is what makes it a record rather than a
> tally.** A big field that flares more gas than a small one in absolute terms
> may be the better-run of the two, and a lender charging it more for that would
> be pricing size instead of behaviour.

**Social licence** (0–100): `SL += Σ driver deltas` per tick, clamped;
driver deltas are content per event class (visible flaring near settlements,
spills scaled by sensitivity, local employment, community investment).
Permitting time/probability tables read SL bands (R16 §5).

## 5. Souring (the long-arc hazard)

```text
Truth-side: H2S_ppm(t) = sourCurve( cumulativeInjectedWater / PoreVolume )
per waterflooded compartment; curve content per rock type. Rising H2S enters:
the fluid composition (sales spec), §1's sourFraction severity, and the
metallurgy envelope check — the DHS3 decision arriving on schedule, years late.
```

> **R20d review amendment (finding 147) — the shape, declared.**
>
> ```csharp
> public interface ISouringModel
> {
>     ContentId Id { get; }
>     double HydrogenSulphidePpm(ContentId rockType, double injectedWaterOverPoreVolume);
> }
> ```
>
> Monotonic in the ratio, and that is pinned: water already injected cannot
> un-sour a reservoir, so a curve that dipped would be a content error the
> loader refuses, not a modelling choice. Truth-side it stays — what a player
> knows about souring arrives through produced-fluid samples, like everything
> else.

## 6. Test mapping

R18-V1 (decay law with declared factors) · V2 (exponential hazard shape — no
threshold behaviour) · V3/V4 (absence + attribution via SDD-002) · V5
(strategy outcomes over long runs) · V6 (mitigations lower the specific term)
· V7 (fixed-order draws ⇒ determinism) · V8 (audit tuple) · V9 (souring curve
→ severity → envelope) · V10 (restore ops) · HS1/HS2 consume §1–§2 through the
bow-tie.

## 7. Open items

| # | Item | Trigger |
|---|---|---|
| S012-1 | Collateral damage on failure (a failed compressor damaging adjacent units) — currently absent; add as a bow-tie consequence class if SC7 feels too clean | R20 balance |
| S012-2 | Per-component vs per-class draw batching if the per-tick draw count matters | R18 benchmarks |
