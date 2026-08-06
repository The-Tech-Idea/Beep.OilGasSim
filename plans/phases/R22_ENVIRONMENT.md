# Phase R22 — Environment and Setting

**Arc II · Executes first in Arc II, before R5** · Status ⬜
Depends on: R1–R4 · Enables: R8, R11, R12, R15, R23

> **Note on numbering.** Phase numbers are stable identifiers assigned in the
> order phases were designed; the **arc tables in
> [MASTER_TRACKER](../MASTER_TRACKER.md) give execution order.** R22 executes
> early because facilities, transport, operations and world generation all
> depend on it.

---

## 0. Purpose

Make where you operate matter. Terrain, water depth, climate, access, ground
conditions and sensitivity change the cost, feasibility and risk of every stage —
and weather makes them change month to month.

**This phase comes before the domain chain** because R8 (facilities), R11
(transport) and R12 (operations) all need to read a setting, and retrofitting
environmental effects onto completed subsystems means touching every one of them.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | The setting changes cost and feasibility | The same development in six settings produces six materially different costs (EN1) |
| G2 | Options are restricted, not merely priced | A land rig cannot be assigned offshore, and the rejection names why (EN2) |
| G3 | Seasonal windows bind | An arctic operation can only be scheduled inside its window; missing it costs a year (EN3, EN4) |
| G4 | Weather is dynamic and forecastable | Seasonal baseline + stochastic variation + extremes; forecast error grows with horizon (EN9) |
| G5 | Ambient conditions feed the physical models | Temperature into hydrates, wax and compressor derating (EN5, EN6) |
| G6 | Environment is content | A new profile added purely as content works end to end (EN12) |

---

## 2. Design decisions

### 2.1 The environment uses technology's effect vocabulary

Restricts an option · moves an envelope · changes a model parameter
([13_ENVIRONMENT](../design/13_ENVIRONMENT.md) §2.1).

*Rationale:* environment and technology then interact for free — arctic
technology *extends* the envelope the arctic environment *restricted*. That is
the entire hostile-setting progression arc with no new mechanism, and it means
the effect-application code is written once and shared.

### 2.2 Weather is a separate RNG stream

`weather`, independent of all others per
[11_PERSISTENCE](../design/11_PERSISTENCE.md) §3.1. Adding a hazard draw
elsewhere must not change a world's weather history.

### 2.3 Weather is a within-tick profile, not a single state

Per open decision EV1: a tick's weather yields *days lost* and *conditions*, which
compose with the fractional-duration model in
[15_TIME](../design/15_TIME_AND_EXECUTION.md) §6. "Eleven days lost to weather"
is the meaningful monthly quantity.

### 2.4 Forecasts degrade with horizon

Short-range is reliable, seasonal is a probability. This makes weather
**plannable without being solved** — the player can schedule a marine lift with
confidence next week and only with odds next quarter.

### 2.5 The setting is fully visible before bidding

Per open decision EV4. The **subsurface** is the uncertainty game; making the
surface uncertain as well would be noise rather than depth. A player must be able
to price the environment into a bid.

### 2.6 Scope at v1: onshore plus shallow offshore

Per open decision EV2. Shallow offshore adds platforms, weather downtime,
helicopter logistics and marine export — most of the variety — without subsea
trees, vessels, floating production and deepwater intervention. Deepwater and
arctic offshore are a designed expansion behind the same contracts.

---

### 2.7 Environment resolves at tick stage 2, before availability

Weather decides what can operate, so it must be known before stage 4 builds the
availability and segment plan ([03_ARCHITECTURE](../design/03_ARCHITECTURE.md)
§6). R22 therefore owns a tick stage of its own, and it is early.

**Weather transitions that cross an operating limit create segment boundaries**
([21_INTEGRATION](../design/21_INTEGRATION.md) §5) — a storm arriving mid-month
cuts the tick, and the days either side are solved separately.

## 3. Deliverables

`OGSim.Environment`: `IEnvironmentProfile` (terrain, water depth, climate,
access, ground, sensitivity, utilities), `IWeatherModel` (seasonal + stochastic +
extremes + persistence), `IForecast`, access-window evaluation, effect
application into operations, facilities, transport and flow-assurance models.
Content: `environment-profile` catalogue, climate parameter sets.

---

## 4. Verification

The EN1–EN12 suite from [13_ENVIRONMENT](../design/13_ENVIRONMENT.md) §8, plus:

| # | Test | Passes when |
|---|---|---|
| R22-V13 | Effect vocabulary shared | Architecture test: environment effects use the same three kinds as technology, with no fourth |
| R22-V14 | Weather stream independence | 10,000 weather draws are identical whether or not other streams were drawn from |
| R22-V15 | Within-tick weather | Days lost compose correctly with operation durations and the segment model |
| R22-V16 | Persistence | Weather is autocorrelated at the declared strength |
| R22-V17 | Setting visibility | The read model exposes the full setting for any block available to bid on |

---

## 5. Out of scope

Deepwater and arctic offshore (designed, deferred per EV2). Climate drift over a
long campaign (EV3, deferred). Pollution and emissions — those are *outputs*,
owned by [R23](R23_HSE.md).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Environmental effects touch many subsystems and become scattered | They are applied through one effect-application path, shared with technology; an architecture test asserts no subsystem reads an environment profile directly |
| Weather becomes noise rather than a decision | Forecasts make it plannable; EV1's within-tick profile makes its cost legible in days |
| Six settings is a lot of content to balance | Band-test the cost ratios between settings against industry norms; the ratios are the balance surface, not the absolute numbers |
| Scheduling around windows is frustrating rather than interesting | `env.accessWindowClosing` is a `D`-severity event with lead time ([16](../design/16_EVENT_MATRIX.md) §4.3) — the player is warned, twice |
