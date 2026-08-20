# 14 — Oilfield Days against GAME-SDD-001/002

`OilField_Days_Beep_Oil_and_Gas_Sim_Game_SDD_Expanded_Godot_WorldGen.html` is the
game-side SDD. It is authoritative for the client the way `plans/sdd/` is
authoritative for the engine, and OGSim stays authoritative for both. This
document records what the build already satisfies, what it deliberately does
differently, and what is still owed — read alongside
`13_OILFIELD_DAYS_BUILD_LOG.md`, which records how the build got there.

The document embeds **eight mockups**, three of which arrived with this revision
and had not been built to before:

| Mockup | Subject | Built |
|---|---|---|
| 5. Create New Game & World Setup | seeded setup, world knobs, surface preview | **yes** — `scenes/NewGame.tscn` |
| 5. Create New Game — UI Flow | mode → map → configure → settings → generating | partly — one page, not five |
| New gameplay scene concept | yard, fuel farm, pumpjack, separator, flare, lease | partly — the yard and chain exist |
| Main scene | world composition and interaction density | yes |
| Job board | job list, detail, vehicle strip | yes — `DispatchBoard` |
| Build/placement | catalog, tile grid, placement ghost | no — blocked, see G-02 |
| Fleet & garage | fleet list, yard, inspector | yes — `FleetBoard` |
| Results | rank, scorecard, leaderboard | yes — `ChallengeResult` |

A further thirteen mockups were supplied as art files (`assets/Oilfield Days_mockups`):
logo, key art, main menu, four setup variants, three gameplay styles and a
dispatch board. The logo and key art ship in `assets/brand`. The setup and menu
screens are built to them; `13_OILFIELD_DAYS_BUILD_LOG.md` records what changed
and which of their readouts are deliberately not drawn.

## What the build already satisfies

**Architecture rules (§1–§3).** OGSim is authoritative; the client mutates only
through commands and reads only projections; `EngineHost` is the single owner of
the engine and the only caller of `AdvanceTick`. No client-side physics or
economics formula exists. Nothing optimistic is drawn: the world redraws from the
snapshot after the tick, never before it.

**Only knowable information (§3).** Prospects show published beliefs, never
subsurface truth. `ChainElementView.Condition` is rendered as *unknown* where the
engine publishes null, and the board offers "Fit condition monitoring" rather
than filling the gap with 1.0.

**Complete rejection reasons (§9.1).** `EngineHost.RecordRefusal` collects every
`LoadFailure` and every composition problem, and the setup screen prints the
whole list. No path shows only the first reason.

**Engine-owned scoring (§11).** `ChallengeResult` reads `ScenarioProgress`. The
shipped scenario's `Scoring` list is empty and the screen says so instead of
drawing five invented bars.

**Presentation calendar (§5, G-05).** Day/Season/Year is a label over 30/360
months. Since the era picker landed, Year 1 counts from the run's own epoch
rather than a fixed 1965.

**Terrain (§6).** `TileMapLayer` per material, seeded fBm Perlin with a ridged
pass and an independent moisture field, autotiled by the project's own 17-piece
edge-mask sheets.

**Seeded world (§7A.2).** The seed is a `ulong`, shown, rerollable and copyable.
It is drawn by the client before a session exists; the engine's eight streams are
never asked for it.

## Where the build diverges, and why

**Scene names.** The SDD's catalogue names `Game/GameShell.tscn` and
`Game/WorldMap.tscn`; the build has one `Gameplay.tscn` holding both, with boards
as overlays on a `CanvasLayer` above it. The split buys nothing until a second
world view exists. Renaming is cheap and can happen when it does.

**New Game is one page, not five.** The flow mockup draws five panels
(mode → map → configure → settings → generating). The build implements mockup 5's
single page, which carries the same fields. The five-panel form is worth building
when there is more than one map and more than one scenario to choose between —
today both lists have one entry, and four screens of one choice each is a wizard
that asks nothing.

**`Boot.tscn` is `Splash.tscn`**, and it does the one job a boot scene honestly
has: showing the game's face while the autoloads come up. It starts nothing —
`EngineHost` builds only when a run is created — so the wait is a beat, not a
progress bar it would have to invent a percentage for.

**No `WorldGenerating.tscn`.** The supplied mockup for it shows eleven named
world-generation steps, a percentage and an estimated time. The steps are real
and are SDD-010's; the percentage is not, because `EngineBuilder.CreateNew` is
one synchronous call that reports no progress. A screen counting to 78% beside a
call that cannot say where it is would be exactly the fake §12 forbids. It
becomes worth building when the engine reports steps, or when generation is slow
enough that the host's own painting passes are worth showing — those it can
honestly count.

**The preview is client terrain, and labelled as such.** §7A.4 forbids building
preview on `IWorldSink`, whose entries carry hidden accumulations, and records
that no surface-only host projection exists (**G-09**). So the preview builds a
real `BasinWorld` in a `SubViewport` from the seed and knobs on screen and paints
it with `PaintBareGround` — the same tilesets, autotiling and scatter the game
uses, with no prospects and no wells, because at setup no engine exists to have
any. It is surface-only by having no subsurface rather than by hiding one.

**Two fields report instead of setting.** Mockup 5 draws a Starting Capital
stepper and the flow mockup draws Starting Reputation. Neither is a control here:

- Opening cash lives in `Defaults.OpeningCash`, `internal` to `OGSim.Composition`
  and moved by no host-reachable setting. The figure is not copied onto the
  screen either — a mirrored number goes stale the day the engine changes it
  (L5) — so the field names its owner and the ledger shows the real value from
  the first tick.
- Reputation is **G-04**: no published engine metric owns it. The field says so.

**Modes are listed and disabled.** Campaign starts, because `first-field` is the
one scenario composed. Scenario, Sandbox and Challenge appear greyed with the
reason attached, rather than being hidden — a mode that vanished would read as a
mode that was never planned.

## Findings from building to it

**A failed chain had no answer in the game.** The host offered nine commands; the
engine has eighteen. Missing were `RepairEquipmentCommand`,
`ServiceEquipmentCommand`, `InstallMonitoringCommand`, the manifold/gas-plant/
tank/treater installs, `RemediateInjectorCommand`, `SetVoidageReplacementCommand`
and `Borrow`/`Repay`. Equipment fails from about month 2, the route law shuts in
everything behind the failure, and with no repair order the field stopped
permanently: seed 3 ran 55 months to two "Producing" wells, **0 m³** and cash
bleeding $50M → $11.5M. The engine's own note above `RepairEquipment` says it —
*a failure without a repair is not a mechanic, it is an ending.*

With repair on the board the same seed runs 3 wells, **36,577 m³/month**, cash
$50M → $136.2M, and the separator becomes the visible bottleneck with 20.5 Mkg of
liquid capacity deferred — which is what "Install another separator" is for. The
missing commands are now on the dispatch board.

The lesson generalises: **the host's command list is a thing that goes stale.**
It should be diffed against the engine's `Command` subclasses whenever the engine
moves, because a command the host never offers is a mechanic the player does not
have.

**Land fraction now means what it says.** Sea level is read off the sorted height
field rather than compared against a guessed constant, so 0.4 gives a basin 40%
dry on any seed at any size. Climate severity cuts the moisture field the same
way. Both come from the setup screen and both reach the built world, so the
preview is a promise the game keeps.

## An engine finding: `Prospects` does not filter what has been drilled

`FieldReadModel.Prospects` is documented as *"every structure the world placed
that the company has **not drilled**"*. The projection
(`Gameplay.cs`, `Prospects()`) filters on `risks.Knows(at)` and nothing else, so a
structure stays on the list after a well goes into it.

Observed on seed 3: a picker taking the highest probability of success off the
list drilled prospect **#1** three times in a row, its published POS rising
0.23 → 0.32 → 0.40 as the wells proved the structure, and the count stayed at
seven throughout.

**The behaviour may well be right and only the documentation wrong.** Drilling a
second and third well into a structure already found is appraisal, and it is what
a company does after a discovery — a read model that hid the structure would make
appraisal unexpressible. What cannot both be true is the field's own comment and
what it returns.

For the engine to decide: either the comment goes, or the projection gains the
filter it claims. **The host does not paper over it.** It remembers its own
orders (`DrilledSites`) so a picker offering "the best prospect" can offer one
nothing has been sunk into yet, and falls back to the best overall once they all
have — which is recalling what the client asked for, not a second opinion about
engine state.

The difference this makes to play is large. Re-drilling one proven structure
turned three holes into three wells and hid the risk entirely; spreading across
the basin turns four holes into **one** well at POS 0.23, 0.18, 0.15 and 0.14,
which is the exploration game the design describes.

## Is the shipped scenario winnable?

`DevAutoPlayer` plays the run end to end with a plain policy — repair what has
stopped, debottleneck what is jamming, survey a poor structure before drilling
it, otherwise drill the best structure nothing has been sunk into — and reports
every year. It exists because no amount of looking at screens answers the
question.

Seed 3, arcade profile, 24 km basin, the full ten years:

| Year | Cash | Wells | Production |
|---|---|---|---|
| 1 | $43.6M | 1 | 22,638 m3 |
| 3 | $41.4M | 1 | 22,306 m3 |
| 5 | $13.9M | 1 | 22,108 m3 |
| 7 | $45.0M | 4 | 73,244 m3 |
| 10 | $134.5M | 6 | 61,165 m3 |

**Finished at $134.4M of $600M — `Expired`.** 18 holes, 7 surveys, 16 repairs, 2
units built.

This is one policy on one seed and is not proof the target is unreachable. Three
things stand out and are worth someone's attention:

- the run nearly died in year 5 at $13.9M, on a single well, five years in;
- **16 repairs against 2 units built** — the policy spent most of its months
  putting the plant back together rather than growing it, because a failure
  shuts the chain in and always looks more urgent;
- production peaked in year 7 and fell for three years while cash still climbed,
  which is the shape of a field being harvested rather than developed.

Whether that means the target is too high, the arcade profile too lean, failures
too frequent, or simply that the policy is bad, is a design question this build
can now measure rather than guess at.

## Still owed

| | Item | Note |
|---|---|---|
| G-02 | Facility placement by coordinate | The build mockup needs an authoritative placement command; the installs are capacity-only, with no footprint. Blocked on the engine. |
| G-03 | Vehicles are cosmetic | The truck carries no engine state, which matches the SDD's ruling for the first playable version. |
| G-10 | Generated-world reload | **Closed.** `SaveGame` for the payload, a host sidecar for the world draft, Continue and Load Game live. One caveat below. |
| — | Five-panel New Game flow | Worth building when there is more than one map and more than one scenario. |

All eighteen engine commands are now reachable from the host.

**One thing to take back to the engine design.** `FieldReadModel` is published by
the Close stage, so a restored engine has no projection until a tick runs. The
host therefore advances one month on load, and a loaded game resumes on the month
after the one it was saved on. A projection callable at tick zero — "project the
current state without advancing it" — would remove the compromise, and is the
only save/load behaviour the host cannot fix on its own side of the line.
