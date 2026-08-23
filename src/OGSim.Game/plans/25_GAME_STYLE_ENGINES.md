# 25 — A game engine per style, over one OGSim

**Status:** proposed, 2026-08-23. Supersedes the *mechanism* of
[24](24_MECHANICS_ARE_OPTIONAL.md); keeps its findings and its law.
**Preceded by:** all sixteen composed modules read in full — the inventory is §4.

---

## 1. Why this exists

Plan 24 made mechanics optional by threading a `MechanicSet` **into the modules**.
Three modules grew a `mechanics` parameter, `ProductionLoop` reached thirty
constructor arguments, and the pass landed half-applied — the tree did not build,
`Mechanics.Demurrage` was declared with **no consumer at all** (law L3), and the
takeover switch had to exist only because zeroing the working-interest cap would
have *armed* the takeover rather than disabled it.

That is the smell of the wrong seam. The modules do not want to know which game
they are in; they never did.

**The direction:** one **game style engine per product**, implementing a common
interface, each *using* OGSim. The style decides which modules compose and with
what terms. `MechanicSet` stops being a fourth axis threaded through the engine
and becomes an implementation detail of a style — or disappears.

This is also what the module inventory says the engine already wants. Every
module already takes its terms as constructor arguments — `FacilityLadders`,
`TakeOrPayTerms`, `LiftTiers`, `FluidSystems`, `ClimateProfile`, `EraCalendar`,
`RuleSet`. **Nothing needs to be invented; the choice just has to move up one
level.**

---

## 2. The shape

```csharp
public interface IGameStyle
{
    ContentId Id { get; }
    string Title { get; }
    string Premise { get; }

    /// Fidelity, what the company opens holding, and what it may do.
    EngineSettings Compose(EngineSettings baseline);

    /// WHICH modules compose, and with WHICH terms.
    IReadOnlyList<IModule> Modules(StyleInputs inputs);
}
```

`StyleInputs` is what content resolution already produces before composition —
`AuditTrail`, `SimulationClock`, `IRandomSource`, `RealityProfile`,
`FacilityLadders`, technology registry, terrain classes, `TakeOrPayTerms`,
`LiftTiers`, fluid systems. `EngineBuilder.ShippedModules` becomes
`style.Modules(inputs)` and stops being a fixed list.

Two implementations, and they are the two products:

| | `DaysStyle` | `EngineerStyle` |
|---|---|---|
| Fidelity | `arcade` | `simulation` |
| Opens holding | `bare-ground` | `opening-position` |
| Rules | `frontier` | `realistic` |
| Licence terms | `NeutralLicenceTerms` | `LicenceTerms` |
| Working interest | `NeutralWorkingInterest` | `WorkingInterest` |
| Takeover clock | `NeverTakeOver` | `TakeoverAfterAmortisingTicks` |
| Insurance / hedge | `NeutralInsurance` / `NeutralHedge` | real |
| Demurrage rate | `0.0` | `0.0005` |

**Modules go back to taking terms.** `CompanyModule(startingState,
licenceTerms)`, `HseModule(insurance)`, `FieldModule(…, hedge, workingInterest,
demurrageRate, takeoverTicks)`. No module has a `MechanicSet` parameter and none
asks `Has(...)`.

### The law that survives from 24

> **A switch has two positions: ON and NEUTRAL. There is no third.**

A style may not select an *easier* version of a mechanic — only a neutral one.
Where two live behaviours are genuinely wanted, that is a `RuleSet` (plan 23) or
a `RealityProfile`, not a style.

### OGSim remains full-featured — a style SELECTS, it never subtracts

**Nothing is removed from the engine by this plan.** Every module, every
mechanic, every command and every stage stays exactly where it is and keeps
working. OGSim is the full-fidelity engine and remains so; a style is a
*selection over it*, expressed as which modules it composes and which terms it
hands them.

The test: **delete every style and OGSim is unchanged.** If a style's existence
ever requires taking a capability out of the engine, the design is wrong — the
capability moves behind a term or a contract instead.

Two consequences worth stating:

- The **neutral terms belong to the style**, not to the engine. `Defaults` keeps
  the real `LicenceTerms`, `Insurance`, `Hedge` and `WorkingInterest`; a style
  that wants a licence which cannot be lost supplies those terms itself.
- `OperationsModule` and `ObjectivesModule` (§4.1) **stay in the engine**. They
  are placeholders for work that is coming, and a style is free not to compose
  them — that is a selection, not a deletion.

### What is NOT a style's business

Design 18 §5b's three axes stay where they are:
**fidelity** is `RealityProfile` (a style picks one, it does not redefine it),
**assists** are the Advisor and are outside the engine entirely, and
**forgiveness** is content. A style is the *bundle* — which is precisely what 18
§5b.5 calls a preset.

---

## 3. The yardstick — design 20 §1

`plans/design/20_PLAYER_DECISIONS.md` §1 already defines the test every decision
in this game had to pass. It is the criterion for §4, and it is the project's
own rather than anyone's taste:

| # | Criterion | If it fails |
|---|---|---|
| **T1** | No universally correct answer — it depends on state the player can read | Not a decision, a step. Automate it |
| **T2** | The player has, or can buy, the information to reason about it | A coin flip. Add an information source or remove it |
| **T3** | The consequence is observable and attributable | Invisible. Add an event or a report |
| **T4** | It composes with other decisions rather than standing alone | A side quest. Couple it or cut it |

> A decision belongs in a game only if a skilled player still has to think about
> it on the hundredth encounter. — 20 §7

**A mechanic stays in Oilfield Days when it puts a decision in front of a
*builder* that passes all four.** The audience differs, so the same mechanic can
pass for Engineer and fail for Days — and that is the honest reason to neutralise
it.

---

## 4. The inventory — all sixteen modules

Read in full. `provides` / `requires` / `ownsState` / `stages` / `commands` are
as declared in `src/OGSim.Composition/Modules.cs`.

### 4.1 The two that do nothing at all

| Module | Manifest | Verdict |
|---|---|---|
| **`OperationsModule`** | provides `[]`, requires `[IAuditTrail]`, owns `[]`, stages `[]`. `Compose` is a null check | **Keep in the engine; both styles compose it.** Nothing names anything it provides, so it is inert either way. The real `OGSim.Operations` types (`ObligationRegistry`, `OperationScheduler`) are built by `FieldModule` |
| **`ObjectivesModule`** | provides `[]`, requires `[]`, owns `[]`, stages `[]`. `Compose` is a null check | **Keep in the engine; both styles compose it.** `ObjectiveStage`, `ScenarioRunner` and the two `objectives.*` state keys all belong to `FieldModule` |

Two of sixteen modules are name-holders, reserving a name for work that is
coming. **They stay** — OGSim remains full-featured, and dropping a placeholder
to save two null checks would be churn against a plan that is about selection
rather than subtraction. Recorded here so the next reader knows they are inert
rather than mysterious.

### 4.2 The seven that cannot be omitted from any style

Omitting any of these is a composition refusal, not a degradation.
`ModuleComposer` checks every `requires` is provided **and** the converse — every
declared `provides`, stage slot, state key and command actually delivered.

| Module | Provides | Why it is load-bearing |
|---|---|---|
| **`DiagnosticsModule`** | `IAuditTrail`, `AuditTrail`, `SimulationClock`, `IRandomSource` | Required by seven, five and two manifests respectively. It is the seed of all determinism and the whole "why?" trail |
| **`MaterialsModule`** | `IFluidPropertyModel`, `IMaterialCatalog`, `FluidSystems` | Required by four modules. **It also holds the one real plugin slot in the build** — `Defaults.FluidSlot`, which is how `arcade` differs from `simulation` |
| **`SubsurfaceModule`** | `IDriveMechanism`, `SubsurfaceState` | Depletion itself. Stage 6 (`MaterialBalance`) is the only stage it claims, and it is where the reservoir loses pressure for what was produced |
| **`WellsModule`** | `IInflowModel`, `IOutflowModel`, `WellsState` | A well is a source element in the one network |
| **`FlowModule`** | `IFlowSolver`, `IFlowElementRegistry`, `TickProduction` | The solve. There is no "flow off" that is neutral rather than empty |
| **`FacilitiesModule`** | `ISeparationModel`, `IHydraulicModel`, `SurfaceChain`, `FacilityLadders`, `PlantBuilder` | The chain a barrel crosses. **Already carries the `startingState` decision** — it commissions a plant only at `opening-position` |
| **`FieldModule`** | `FieldControl`, `CloseStage`, `IObligationRegistry`, `Bank`, `ReserveHistory`, `WorkingInterest`, `ObjectiveStage`, `TakeOrPayContract`, `LiftTiers` | Ten stage slots, eleven state keys, **all 31 commands**. There is no engine without it |

### 4.3 The seven a style genuinely configures

| Module | Mechanic | T1–T4 for a builder | Days | How |
|---|---|---|---|---|
| **`CompanyModule`** | **Licence work commitment** | Fails T1, T2, T3 — no decision, no warning, and the consequence lands 60 months later as a silent total loss | ❌ | `NeutralLicenceTerms`: no commitment, no bond, a term no run reaches. The fiscal regime is **kept** — tax is not part of the licence mechanic |
| | Market price + cost index | Passes. The reason timing matters; a boom is a bad time to build and a good time to produce | ✅ | — |
| | Bank / borrowing base | Passes for Engineer. **Currently cannot help Days at all** — it lends against reserves, and Days runs out of money *before* the first discovery | ✅ | ON as decided, with the debt in §6 |
| | Crew competency / training | Passes. $2M once, forever, and it feeds both operation duration and bow-tie barrier strength | ✅ | — |
| **`InformationModule`** | Exploration POS, dark map, belief decay | Passes — this is the game. But see §6: at 17–24% it is currently a coin flip that kills runs | ✅ | Balance, not presence |
| | Rivals | **Fails T3 today** — they only explore, cannot take acreage or be beaten to anything, so the race is unobservable | ✅ | ON as decided; the debt is in §6. Neutral if ever needed: `RivalCount = 0` |
| **`WorldModule`** | Terrain cost, charge fraction, trap subtlety, block grid | Passes. "Where do I look first" is the opening decision | ✅ | — |
| **`CapabilitiesModule`** | Era gating, diffusion, detect class | Passes. The tech tree, and the concrete link between technology and what a survey can see | ✅ | — |
| **`IntegrityModule`** | Wear → failure → outage → repair | Passes, and it is Factorio's maintenance loop | ✅ | — |
| | *(hazard intensity)* | — | — | Design 03 §3.2 already lists `IHazardModel: Off ↔ realistic ↔ punishing`. A style may pick |
| **`EnvironmentModule`** | Weather days lost, seasonal access | Passes. Seasons a builder plans around | ✅ | Note the shipped climate is open all twelve months, so access is armed but never fires |
| **`HseModule`** | Bow-tie top events, near-misses | Passes. Weakest-link barriers mean neglected maintenance shows up as risk | ✅ | — |
| | **Insurance premium / claim** | Fails T2 and T4 for a builder — a financial instrument priced off an ESG standing, standing alone | ❌ | `NeutralInsurance`: zero rate, zero limit. The company carries its own risk, which is truthful rather than a discount |
| | ESG / flaring standing | Passes. It is what gives the gas plant a reason to exist | ✅ | — |
| **`FieldModule`** | **Demurrage / laytime** | Fails T4 — a berth-scheduling side quest | ❌ | Rate `0.0`. One-cargo-at-a-time loading and the tank-full shut-in **stay** — they are not the mechanic |
| | **Working-interest sale** | Fails T2/T4 for a builder — a farm-down priced off a distress discount | ❌ | `NeutralWorkingInterest` (`MaxSellableFraction: 0.0`) — the command always refuses |
| | **Takeover** | Fails T1 and T3 — nothing to decide, and it arrives as a verdict | ❌ | `NeverTakeOver`. **Needs its own switch**: the trigger also asks whether the partner share reached the sellable cap, and a cap of zero reads `0 >= 0` — zeroing the working interest would have *armed* it |
| | **Hedge collar** | Fails T2 for a builder | ❌ | `NeutralHedge` (`HedgedFraction: 0.0`); floor and cap keep real values so the validator still checks a real pair |
| | Take-or-pay contract | Passes. Production with a deadline and teeth | ✅ | ON as decided. Neutral if ever wanted is pure content: `committedVolumeCubicMetres: 0` |
| | Abandonment obligation | Passes. The cost of having drilled, registered unconditionally at creation | ✅ | Note a zero cost does **not** neutralise it — `AbandonWellActivity` reads zero as "already plugged" and refuses |
| | Equipment repair / service / monitoring | Passes. Condition is *bought*, not given: new gear arrives blind | ✅ | — |
| | Objectives, deadline, scoring | Passes | ✅ | Content — `Defaults.FirstField` is "the JSON a loader will hand over" |

### 4.4 Neutrals that are illegal — traps found while reading

Three things refuse a zero, and a style that tried the obvious neutral would
crash rather than compose:

- **`BowTie.Materialises` throws on `ratePerTick <= 0`.** So
  `ThreatRateAtFailure = 0` is not a legal way to switch top events off; and
  `BowTie.Resolve` throws on a threat with zero preventive or zero mitigating
  barriers, so an empty barrier list is not legal either.
- **`ClimateProfile.Validate` refuses a climate closed all twelve months.** The
  access mechanic can be turned on, never fully closed.
- **`CrewState`'s constructor throws if training buys nothing** — trained must be
  strictly better than untrained on both axes, and training must cost something.
  The design deliberately refuses a genuine zero.

And two more worth carrying:

- **`Defaults.MaxTickPressureDropFraction` is threaded into
  `ReservoirCompartment.CommitWithdrawal` and never read.** The limit that
  actually bites is the drive's own `MaxTickVoidageFraction`. A dead parameter
  (law L3).
- **Three published contracts have no consumer:** `IDriveMechanism`,
  `IInflowModel`, `IOutflowModel`. Per-well models are constructed directly in
  `ProductionLoop.Drill` and `Defaults.CompletionFor`, so registering a neutral
  in the module would change nothing. `content/materials/*.json` and
  `content/rock-types/*.json` are likewise inert — the live lists are
  `Defaults.Materials` and `Defaults.TheRock`.

---

## 5. Order of work

| Step | What | Size |
|---|---|---|
| **S1** | `IGameStyle` + `StyleInputs`; `EngineBuilder.ShippedModules` → `style.Modules(inputs)` | medium |
| **S2** | `EngineerStyle` — every module, real terms. **Acceptance: not one test moves** | small |
| **S3** | `DaysStyle` — the §4.3 neutrals, supplied by the style | small |
| **S4** | Retire `MechanicSet` and the `mechanics` parameters; modules take terms again | small |
| **S5** | The save records the style id, and a reload that disagrees is refused by name | small |
| **S6** | Re-balance Days against the game it is finally meant to be | medium |

---

## 6. Debts this plan does not pay

Named so they cannot go quiet:

1. **The bank cannot help Days.** It lends against *reserves*, so it is a
   late-game instrument in a game whose problem is early. Either give it a
   pre-discovery basis or accept it does nothing until first oil.
2. **Rivals are unobservable.** They explore and publish results, but cannot take
   acreage or be beaten to anything, so there is no race to win. They fail T3
   until they can do something a player can lose to.
3. **Exploration is a coin flip at 17–24%.** POS is the balance problem, and
   design 18 §5b.4's sanctioned lever is **higher regional POS content** — not a
   changed prior. That is S6's first move.
4. **Seasonal access never fires** — the shipped climate is open twelve months.
   A builder wants a season it plans around; this is content.
5. **S3 (player-routed connections)** must be planned as **DDV9's corridor
   choice**, not metre-by-metre routing: design 20 §7 already rejected the latter
   for failing T1. The Factorio verb is *throughput and bottlenecks*, which this
   engine already computes.
