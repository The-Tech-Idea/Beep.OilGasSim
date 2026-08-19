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

**The replaceable part is the state advance, and only that** (finding 76):

```csharp
public interface IWeatherModel
{
    ContentId Id { get; }
    double NextState(double x, IRandomStream weather);   // one call per region per day
}
```

Severity and temperature curves over `x` stay engine-side content application,
and the extremes of §2 stay engine draws — so **a mod swapping the process
cannot touch the curves or the calendar.** The interface is one method wide for
that reason: the widest thing a weather plugin may do is decide how persistent
the weather is.

Note what returning a bare `double` buys: severity and ambient temperature are
both content curves over the *same* `x`, so hot spells and storm calms correlate
without the model knowing either curve exists.

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

## 5b. The runtime types (R22.1 amendment)

§1 pins the algorithm and the one replaceable method; these are the members that
carry it, declared here before implementation (F-1).

```csharp
// OGSim.Contracts — the plugin seam, exactly as §1 states it.
public interface IWeatherModel
{
    ContentId Id { get; }
    double NextState(double x, IRandomStream weather);
}

// OGSim.Environment — content, and the state the process lives in.
public sealed record ClimateProfile(
    ContentId Id,
    double Persistence,                    // ρ ∈ [0, 1)
    IReadOnlyList<double> Baseline,        // 12 months, severity
    IReadOnlyList<double> Amplitude,       // 12 months, severity
    IReadOnlyList<double> TemperatureBaseline,   // 12 months, °C
    double TemperatureAmplitude);

public sealed class WeatherState : IStateOwner        // "environment.weather"
{
    void Advance(GameDate month, IWeatherModel model, IRandomStream weather);
    double SeverityOn(int region, int day);           // day ∈ [0, 30)
    Temperature TemperatureOn(int region, int day);
    int DaysAbove(int region, double limit);          // §3's day-lost count
    Forecast Look(int region, int horizonDays);       // §4, consumes no draws
}

public readonly record struct Forecast(double Expected, double Sigma);
```

**`x` is STATE and is saved.** The AR(1) recursion reads its own previous value,
which is the same shape as the covenant clock and the reserve ring (SDD-013 §4):
a quantity recomputed each tick *from its own past*. A reload that resumed from
`x = 0` would restart every region at its seasonal mean — the weather would
visibly calm at the moment a game was loaded, and the RNG position alone cannot
carry it (finding 192's lesson: the right dice from the wrong place).

**Thirty draws per region per tick, region order then day order**, so adding a
region cannot shift an existing one's stream position (§8's EN8).

**`DaysAbove` is the whole of §3's operation coupling.** An operation's template
declares a severity limit; the days above it are STANDBY days, which
`Operation.Advance(activeDays, standbyDays, …)` already prices — cost without
progress. Nothing new is needed in the scheduler, and the 30/0 split it passes
today is exactly the "no weather" case.

> **R22.6 amendment. The access window is a member, and it gates STARTING
> rather than progressing.**
>
> §3 says access windows are calendar facts *from the climate profile* and §5b's
> declared `ClimateProfile` has no member for them, so the fact had nowhere to
> live. It gains one: `IReadOnlyList<bool> AccessOpen` — twelve months, validated
> beside the other three curves, and **deterministic**: no draw is taken, because
> a window is a season and weather is what happens inside it.
>
> **Twelve booleans and not a severity threshold**, which is the distinction §3
> is drawing and it is easy to lose. A monsoon closes a road for a season whether
> or not any particular day is calm; an ice road carries a rig because the ground
> is frozen, not because the wind dropped. Deriving the window from `severity >
> L` would make it a consequence of the draws — and then a lucky calm February
> would open an ice road in a thaw, which is not a thing that happens.
>
> **It gates STARTING, and that is the whole mechanic.** An operation whose
> template declares `RequiresAccess` cannot BEGIN in a closed month; one already
> under way continues, because the crew and the kit are already on site and the
> road closing behind them does not stop the work. This is the opposite of how
> severity couples — severity costs standby days to work IN PROGRESS (`DaysAbove`)
> — and the two must not be merged: a mechanic that suspended running operations
> at a window boundary would strand every long job at the same moment each year.
>
> **So the player's decision is a DEADLINE rather than a tax**, which is what
> makes a window interesting: the work has to be committed before the window
> shuts, and a month spent deciding can cost a year. `WeatherState.AccessOpenIn`
> answers for a month and `MonthsUntilAccessCloses` answers the question a player
> actually has, which is how long is left to commit.
>
> A refusal is a rejected command carrying a domain reason (design 09 §7's
> *what did I do wrong*), never a silently deferred operation: a company that
> cannot move a rig until June must be told in January.

> **R20d.8.10 amendment (finding 242's climate half, and finding 244).**
> `IWorldSink.AddClimateRegion` was never called by the shipped generator, so
> `WorldState`'s climate list stayed permanently empty and `EnvironmentModule`
> composed a `WeatherState` from a separately hand-authored `Defaults.Climate`
> — two owners of one fact (law L5), with the composed one disconnected from
> whatever a generated basin actually decided (finding 242). Alongside it, a
> DISTINCT defect at the same address: `WorldParameters.ClimateSeverity` —
> declared as "weather amplitude/extreme-rate multiplier" — was validated and
> SAVED and never read by anything (CLAUDE.md rule 7, finding 244). Two
> findings because they are two call sites that happen to need the same fix.
>
> **This composition generates exactly one basin, and §1 already says a region
> is "typically a basin"** — so the fix is not multiple regions inside one
> basin (S016-1 below is explicit that spatial variation is a MULTI-basin
> question, "one storm, two basins", deferred). It is that the basin's ONE
> region should be a real, generated fact instead of a hardcoded default that
> happens to sit beside it.
>
> **The sequencing problem this raises, and how `WorldState` already solved
> it.** `EnvironmentModule.Compose` runs at composition time, before a NEW
> game's generation has run — so `WeatherState` cannot be built FROM the
> generated region; it does not exist yet. `WorldState` faces exactly this and
> is composed EMPTY, filled once by `Surface`/`SealGeneration` after
> generation and before the first tick (SDD-010 §4c). `WeatherState` gains the
> same shape: composed from `Defaults.Climate` (so a hand-built scenario that
> never calls the generator — most of this suite's composition tests — still
> has weather, which is the correct answer for those, not a fallback standing
> in for a value that should have been supplied), then `SealGeneration`
> replaces it with what generation decided, at the identical instant
> `EngineBuilder.CreateNew` already seals `WorldState`.
>
> **What generation decides**: `BasinWorldGenerator` is handed the one climate
> id this composition can produce (`Defaults.Climate.Id`, exactly as it is
> already handed the loaded terrain-class registry — composition owns the
> default, the generator only names it) and declares ONE `ClimateRegion`
> covering the generated map, so `WorldView.ClimateRegions` reports something
> real rather than the permanently-empty list finding 242 found. `CreateNew`
> then seals `WeatherState` with `Defaults.Climate` scaled by
> `ClimateSeverity` — the amplitude and temperature-amplitude curves widen or
> narrow with it, closing the ignored-parameter half in the same change,
> because "how variable this basin's weather is" is the one thing
> `ClimateSeverity` was ever declared to answer.
>
> **Loadable `ClimateProfile` content (a `ClimateContentKind`) is explicitly
> NOT built here.** `Defaults.Climate` is still the only climate this
> composition can produce — scaling it is not the same claim as authoring a
> second one, and building a content kind with one shippable file would be
> content for its own sake. That is separate, future work, matching how a
> `TerrainContentKind`-shaped loader was built only once C16 had six real
> classes to load (R20d.8.9).

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
