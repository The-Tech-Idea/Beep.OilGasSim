# SDD-016 — Environment Runtime (Weather)

**Status:** drafted · **Serves:** R22 · **Design docs:** [13](../design/13_ENVIRONMENT.md) §4, [R22](../phases/R22_ENVIRONMENT.md)

The weather algorithm — the last unpinned stochastic process in the engine.
Effects application was already SDD-005; this is the generator behind it.

---

## 1. The daily AR(1) severity process — on the /30 grid, deliberately

```text
Per region, per DAY (30 draws/region/tick from the `weather` stream,
region order ascending, then day order):
  x(d+1) = ρ · x(d) + sqrt(1 − ρ²) · ε(d),   ε ~ N(0,1)     // AR(1), stationary N(0,1)
  severity(d) = seasonalBaseline(month) + amplitude(month) · x(d)
ρ (persistence), baselines and amplitudes: climate-profile content per month.
A REGION is one climate-profile instance's area (typically a basin); every
location maps to exactly one region — declared at world-gen, so "which x(d)
do I read?" has one answer per element.
```

**Why daily, not per-tick:** operations lose *days* (13 EV1), segment
boundaries live on the /30ths day grid (SDD-001 §9) — a daily process makes
"the storm arrived on day 11" an exact grid fact, unifying weather with
segmentation with no interpolation.

> **Pass-5 amendment (finding 76):** the replaceable part is the state advance:
> `IWeatherModel { ContentId Id; double NextState(double x, IRandomStream weather); }`.
> Severity/temperature curves over x stay engine-side content application;
> extremes (§2) stay engine draws. One call per region per day, Weather stream
> only — a mod swapping the process cannot touch the curves or the calendar.

## 2. Extremes

```text
Per season, extreme events (storm, hurricane, flood, freeze) arrive as a
Bernoulli per tick (rate content per climate/season, `weather` stream, audited
— a hurricane is a consequential draw). An event occupies days [d, d+len):
severity is overridden to the event's class level on those days.
```

## 3. What severity does

```text
Each operation template and weather-exposed element declares a weatherClass
with a severity limit L (content):
  day lost  ⇔ severity(d) > L        → standby days for operations (SDD-007 §3)
  berth closed on days severity > L_berth  → tank.full chains (SC8)
  access windows: ice-road/monsoon windows are CALENDAR facts from the climate
  profile (deterministic), not severity draws — a window is a season, weather
  is what happens inside it
Ambient temperature for derating/flow-assurance (SDD-006) = a second content
curve of the same form: T(d) = T_baseline(month) + T_amplitude · x(d) —
the SAME x(d), so hot spells and storm calms correlate as weather does.
```

## 4. Forecast — exact AR(1) prediction, honestly degrading

```text
E[x(d+h)] = ρ^h · x(d)             σ_forecast(h) = sqrt(1 − ρ^(2h))
The forecast panel is the analytic predictive distribution — no separate
forecast model to drift, and its honesty is a THEOREM of the generator (EN9's
"error grows with horizon" is ρ^h falling). Seasonal outlook beyond the
horizon = the baselines alone. Forecasts consume no draws.
```

## 5. Profiles and effects

Static profile → effect contributions (restrictions/parameters) recomputed at
stage 2 **only when weather-relevant state changes** (season, window, event) —
the effect list is not rebuilt for an unchanged day. Settlement slow growth
(H9): population drifts toward an employment-driven target at a content rate
per year — evaluated annually, P5-slow by construction.

## 6. Test mapping

EN3/EN4 (windows as calendar facts) · EN5/EN6 (the shared x(d) into derating
and hydrate margin) · EN7 (berth closure days) · EN8 (stream isolation and
determinism: 30·regions draws per tick, fixed order) · EN9 (§4 is the test's
oracle) · R22-V14..V16 (independence, within-tick composition, ρ
autocorrelation measured over samples) · TM2 interplay (weather days feed the
segment plan).

## 7. Open items

| # | Item | Trigger |
|---|---|---|
| S016-1 | Spatial correlation between adjacent regions (one storm, two basins) — start independent regions; add a shared-front term if maps make it look wrong | R22 review |
