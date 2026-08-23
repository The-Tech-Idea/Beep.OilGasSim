> Source read in full: `src/OGSim.Composition/Modules.cs`, plus the types this
> module composes. Part of the module review requested 2026-08-23. Nothing in the
> engine was changed to produce this.


# 11 — environment

`internal sealed class EnvironmentModule(ClimateProfile climate)`

## Manifest

| | |
|---|---|
| **provides** | `IWeatherModel`, `WeatherState` |
| **requires** | `IRandomSource`, `EffectState` |
| **ownsState** | `environment.weather` |
| **stages** | `Environment` order 0 |

## Compose

```
Ar1Weather(climate.Persistence)     -> IWeatherModel
WeatherState([climate])             -> owned, provided
WeatherStage(weather, model, Stream(Weather), Require<EffectState>(), climate)
```

**The climate is a constructor argument, not a static read** — the source calls
`Defaults.Climate` in both lines "a dependency with a default wearing a
constant's clothes", and notes it made the access-window mechanic untestable
because no test could hand the module a coast that closes.

## The stage

**`WeatherStage`** — stage 2, order 0. Draws thirty days per region *before
anything decides what it can do this month*, then applies the climate's effects
through the same `EffectState.Apply` that technology uses.

## Functions and properties

**`WeatherState`** (`OGSim.Environment/Weather.cs`)

| Member | |
|---|---|
| `Regions` | how many climates |
| `Advance(month, model, stream)` | the month's thirty days |
| `SeverityOn(region, day)` / `TemperatureOn(region, day)` | per day |
| `AccessOpenIn(region, month)` | a **calendar** read, deliberately not a severity threshold — a lucky calm month cannot open an ice road |
| `MonthsUntilAccessCloses(region, from)` | turns the window into a deadline the player can see |
| `DaysAbove(region, limit)` | the days an activity loses to standby |
| `Look(region, horizonDays)` | forecast: `E = ρ^h·x`, `σ = √(1−ρ^2h)` — consumes **no draws**, and its error grows honestly with horizon |
| `SealGeneration(climates)` | replaces the composed default once world generation has decided |
| `Key` = `environment.weather`, `SchemaVersion` 1 | only `_carry` and `_month` are written — restoring from zero would visibly calm the weather at the moment a game is loaded |

`ClimateProfile.Validate()` **refuses a climate closed in all twelve months**.

## Dependencies and conditions it decides for itself

**None in `Compose`.**

## Content and Defaults consumed

`Defaults.Climate` — `temperate-offshore`, persistence 0.75, twelve-month
baseline 2.2 (July) to 5.2 (January), temperature 5.6–15.4 °C, amplitude −1.8
(a rough day is a cold one), `AccessOpen` **all twelve true**, `Effects` empty.
`Defaults.ScaledClimate(severity)` and `Defaults.OperationWeatherLimit = 6.0`.
**No content file** — climates are `Defaults`-only in this build.
