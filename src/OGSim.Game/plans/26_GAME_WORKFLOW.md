# 26 — How the game should flow

**Status:** research, 2026-08-23.
**Grounded in:** the sixteen [module documents](modules/00_INDEX.md), the 25
activities in `content/activities/`, and the seven refusals
`ActivityOrders.Refusals` actually makes.

---

## 1. What a turn is

One tick is one month, and the engine runs fourteen stages in a fixed order.
A player does not see stages; they see **a month passing and a report**. But the
order decides what is possible, so it is worth stating what the player's month
actually consists of:

| The player's month | The stages behind it |
|---|---|
| Orders are given | (before the tick) commands are queued |
| Weather is drawn for the month | 2 — Environment |
| Jobs advance, lose days to weather, finish | 3 — Operations |
| Things break; the month splits at the break | 4 — Availability |
| The chain solves; oil moves | 5 — SolveFlow |
| The reservoir loses pressure for it | 6 — MaterialBalance |
| Oil is metered, stored, shipped | 7 — Custody |
| Money moves | 8 — Economics |
| Standing ages | 9 — HSE |
| Beliefs go stale | 10 — Information |
| Contracts settle | 11 — Company |
| The run is judged | 12 — Objectives |
| The report is published | 13 — Close |

**The order is the design.** A job finishes in stage 3 *before* the solve in
stage 5, so a well completed in January produces in January. A failed solve
commits nothing because stage 6 is separate from stage 5. An unmetered barrel
earns nothing because stage 8 is after stage 7.

---

## 2. The Days loop

Six phases. It is a **cycle**, not a sequence — a company is in several at once
once it is running.

```
        LOOK ─────► DRILL ─────► BUILD ─────► PRODUCE
          ▲                                      │
          │                                      ▼
        GROW ◄───────────────────────────────  KEEP RUNNING
```

| Phase | The player decides | The engine decides |
|---|---|---|
| **Look** | which block to survey, and whether to survey at all | what is under it |
| **Drill** | which prospect, and when to stop drilling | whether there is oil there |
| **Build** | when to commission, and what to build first | how long it takes, and whether the job fails |
| **Produce** | choke settings, what to sell | rates, pressure, water cut, price |
| **Keep running** | repair now or later; monitor or not | what breaks and when |
| **Grow** | which bottleneck to widen next | where the bottleneck actually is |

### The one that makes it a builder

**Keep running** and **Grow** are where a Settlers/Factorio game lives, and they
are the two the engine is already strongest at. `ProductionLoop.Chain()` returns
per-element **throughput, deferred mass, condition and the binding constraint** —
so "where is my chain throttled" is computed every tick and needs no new
mechanic, only a screen that shows it.

---

## 3. Where a step currently has no decision in it

Read against the activity graph and the refusal list, three phases are thin:

| Phase | Problem |
|---|---|
| **Look** | With 16 blocks at one price and no way to tell them apart before shooting, "which block" has no basis. The player is picking a number, not making a decision |
| **Drill** | At 17–24% odds and a fixed hole price, "which prospect" collapses to "the highest number on the board". There is no trade-off — no cheap-shallow versus dear-deep, no choice of target depth |
| **Grow** | Every upgrade is a single ladder rung with one price. There is never a question of *which* upgrade, only *whether* you can afford the next one |

**Build**, **Produce** and **Keep running** are genuinely decisions today:
commissioning is a real commitment that can fail; the choke is a live trade-off;
and repair-versus-service-versus-monitor is a three-way with different prices
and different information.

---

## 4. Days rules — the three changes

### 4.1 Drilling needs no seismic

Today `content/activities/drill-development-well.json` declares
`requires: ["seismic-2d"]`. In Days that prerequisite goes.

A well may be ordered on unsurveyed ground. **Surveying stops being a gate and
becomes a purchase**: you may drill blind and cheap, or pay to improve the odds
first. That is a decision where there was a step — and it fixes the "Look" phase
in §3, because "survey or drill now" is a real question with no universally
correct answer.

### 4.2 Drilling needs no licence

`ActivityOrders.Refusals` currently refuses **every** activity when the licence
is not live — the first of its seven checks. Days has no licence mechanic, so
this check must not run at all, rather than run against a licence that happens
never to expire.

This becomes the `Tenure` entry in the
[dependency manager](27_DEPENDENCY_MANAGER.md): absent in Days, so the check is
not made; present in Engineer, unchanged.

### 4.3 No banks

`content/game-styles/days.json` currently says `"bank": true`. It becomes false,
and with it:

- `BorrowCommand` and `RepayCommand` are not offered
- the covenant, the sweep and the takeover cannot fire
- `IReserveBasedLending` is still composed — **field** requires it — but nothing
  reaches it

**What this costs, stated plainly.** Borrowing was the only thing that could
bridge a company between its first discovery and its first oil. Without it the
opening balance *is* the early game: a company that mis-spends the first two
years cannot recover, and there is no lever left but the opening figure itself.

---

## 5. What the three changes do to the loop

| Phase | Before | After |
|---|---|---|
| Look | mandatory, and undifferentiated | **optional**, and priced against drilling blind |
| Drill | gated on a survey and on tenure | gated on **cash and a rig** only |
| Build | unchanged | unchanged |
| Produce | unchanged | unchanged |
| Keep running | unchanged | unchanged |
| Grow | could be financed | **funded from cash flow only** |

The loop gets shorter at the front and harder at the back. Removing two gates
means a company reaches its first hole faster; removing the bank means every
later phase is paid for out of what the field actually earns.

---

## 6. The open question this study cannot answer

**Is the loop winnable once the gates are gone?**

Removing the seismic gate lets a company drill sooner and blinder — more holes,
each at worse odds. Removing the bank removes the only bridge. Which of those
dominates is an arithmetic question, and the honest answer is that it has to be
measured rather than argued:

```
dotnet run --project src/OGSim.Engineer -- --mode=days --months=120 --seed=N
Godot: --screen=gameplay --play=120 --seed=N
```

across at least five seeds, before and after. That measurement is the last step
of the work, not the first.

### Measured, 2026-08-23 — after W5 removed the gates

`--mode=days --months=120`, seeds 1–5 and 11, tenure and banking absent:

| Seed | Outcome | Month | What happened |
|---|---|---|---|
| 1 | INSOLVENT | 68 | three dry holes at 23–24 %, nothing found |
| 2 | INSOLVENT | 29 | **found oil, built the whole chain to the custody meter** — and still went under before revenue compounded |
| 3 | INSOLVENT | 31 | surveys, nothing found |
| 4 | INSOLVENT | 41 | surveys, nothing found |
| 5 | INSOLVENT | 36 | surveys, nothing found |
| 11 | INSOLVENT | 34 | one discovery, shut in, no plant money left |

**The answer to the question above: neither effect dominates — the loop loses
either way at today's numbers.** *(Superseded the same day by the deeper
measurement below.)* A company that finds nothing dies of dry holes;
a company that finds oil dies of the plant bill. Removing the gates was
necessary (nothing above was killed by a licence) but not sufficient. What is
left is the balance pass plans 22 M5 always said comes after the mechanic set is
settled — and the mechanic set is now settled. The levers are all content:
survey and hole prices against the opening balance, the plant ladder's first
rungs, and the thin phases of §3 (blocks that differ, cheap-shallow against
dear-deep) which are the same fix seen from the player's side.

### The deeper measurement (same day, after the instrument was made honest)

Chasing the insolvencies to their causes produced, in order:

1. **The auto-player stalled when rich** — its drill-the-best fallback armed
   only on poverty, so a well-funded company surveyed a finished map forever.
   Fixed: "no way to find out more" now reads the MAP (dark blocks, unappraised
   best), not the purse.
2. **"Produced" is DELIVERED** — the read model's `ProducedThisTick` counts
   what left the field, and nothing ever left. Not because an export line was
   missing: `export-line-e1` is **rung 0 and always stands, at 20 kg/s**. The
   gate was the **cargo parcel**: nothing loads until the tank holds
   `parcelSize`, which was a C# constant of **80,000 t** — fifty-nine months
   of one well's production. First sale five years out, on every seed, at any
   opening balance up to $150M.
3. **The parcel is now the export rung's own content fact** (`parcelSize` on
   `export-line-*`), the base rungs state the 80,000 t the code shipped —
   Oilfield Engineer is bit-identical — and **per-style content overlays**
   exist (`content/styles/{id}/`, loader order 1, whole-entry override):
   Days' first rung sells an **8,000 t coaster parcel**, its first cargo
   ~$3.5M.
4. **The bottom of the stack: a Days well loses money.** With three producers
   (~4,200 t/month) and cargos cycling, the field still nets **−$3.1M a
   month**. Gross margin says +$1.4M (price $443/t, lifting $111/t, royalty
   12.5%, fixed $0.3M) — so roughly **$2M a month is going somewhere the
   margin arithmetic does not name**. The next probe is the ledger by
   category over one producing year; the levers after that are all content
   (well rate via the arcade profile's rock and completions, the lifting and
   fixed costs, the price).

The opening balance stays derived at $72M until then: no figure up to $150M
was measured to survive while the per-well economics are under water, so
raising it would only buy a slower death and call it balance.

### The ledger probe (2026-08-23, finding 285) — the $2M was revenue that never posted

The probe this log asked for exists now
(`dotnet run --project src/OGSim.Engineer -- --mode=days --probe=ledger`):
it measures a span's signed cash by category off the read model's own
`CashByCause` row and the movements joined to their audit causes, run twice —
once under the auto-player, once with a declared field left alone but
repaired — and prints the chain's own throughput/breach/status snapshot on
every exit, because a ledger that says lifting was paid and nothing was
delivered is exactly half an answer.

**The first run of it answered the question, and the answer was not a cost.**
The field-alone variant lifted ~34,500 t/month across three wells, the
custody element passed ~31,100 t of it in the solve — and `ProducedThisTick`
read zero for seventeen straight months while `field-operating` charged
lifting on every tonne. The unnamed ~$2M was **revenue that never posted plus
lifting charged on barrels the engine then discarded**: `ProductionLoop`
captured `chain.MeteredPoints` ONCE at composition, when a bare-ground
company's custody point does not exist yet, so the metered set stayed empty
for the whole game — every barrel crossed the meter unrecorded, unsold and
(since the same block feeds the tank's commit) un-stored. Opening-position
builds could never show it: their custody point exists at composition, so
the captured list was right by luck. Fixed the same day — the loop reads
`Custody` off the plant it already holds, live, the way it reads the tank
and the disposal well — pinned by `GS8_a_plant_built_mid_game_meters_what_it_delivers`,
which was confirmed failing first.

**Measured after the fix, same instrument, seed 2:** a Days field's producing
year EARNS — $11.76M/month revenue against $3.75M/month operating and
$4.09M/month fiscal, **+$3.92M/month net**, cash $48M → $95M over the twelve
months. §4's "a Days well loses money" is withdrawn: the well was never
losing money; the engine was discarding its revenue.

**What the six seeds now die of is RUNWAY, not economics** (re-measured,
`--mode=days --months=120`): seeds 1/3/4/5 find nothing and hold the plant
reserve until the running costs end them (months 37-50); seed 11 finds oil
with no plant money left (month 35); seed 2 finds oil at month 24, builds
the whole chain, delivers 7,211 m³ — and goes under at month 29, roughly two
months of runway short of a field that would have carried it from there.
**The open question is now purely the balance pass plans 22 M5 reserved**:
the opening balance / survey and hole prices against the ~30 months a
discovery actually takes, with the destination now measured to be worth
reaching. The $72M derivation above assumed a loss-making well; that
assumption is gone, and re-striking the figure is a design decision, not an
engineering one.

### The balance sweep (same day) — the decision's own numbers

Five opening balances × the six seeds, `--mode=days --months=120`, the
auto-player unchanged (drill floor 25%, reserve = a hole and a year plus
the plant). First checked that the WORLDS are fine: every seed generates
12–22 structures, all inside the block grid, all discoverable by shooting
— so nothing below is world generation.

| Opening | 1 | 2 | 3 | 4 | 5 | 11 |
|---|---|---|---|---|---|---|
| $72M  | dead 50 | dead 29 | dead 38 | dead 46 | dead 37 | dead 35 |
| $90M  | dead 48 | dead 93 (1.03M m³!) | dead 56 | dead 60 | dead 61 | dead 38 |
| $110M | dead 75 | **SOLVENT, +$37.6M, 2.09M m³** | dead 60 | dead 72 | dead 55 | dead 56 |
| $130M | dead 86 | **+$57.6M** | dead 88 | dead 84 | dead 64 | dead 71 (0.49M m³) |
| $150M | dead 77 | **+$77.6M** | dead 79 | dead 71 | dead 96 | dead 105 (1.34M m³) |

What the anatomy says (seed 1 read month by month): ~16 months of 2-D at
~$1.2M/month all-in, one 3-D, then **four dry holes in a row at 19–24%**
(~36% chance of that streak — unlucky but ordinary), $6.8M left, below the
plant reserve, and a year's slow bleed. Seeds 3/4/5 are the same story.
That is §8's own arithmetic — "a discovery costs about six holes; six
holes plus a plant costs more than the company opens with" — measured
again with the revenue defect gone.

Two distinct problems, two lever sets:

1. **The early gauntlet** (seeds 1/3/4/5, and 2/11 at $72M): best
   prospects sit at ~20–25% POS, a hole is ~$8M, and the budget affords
   four or five — about half of all runs die on the streak. Levers:
   opening balance, hole/survey prices, or PROSPECT QUALITY (richness /
   maturity / §3's cheap-shallow-vs-dear-deep blocks, which raise POS
   rather than the purse).
2. **The auto-player's late game** (seed 2 at $90M with a million m³
   lifted, seed 11 at $150M with 1.34M): a PAYING field can still be
   spent into insolvency by relentless reinvestment — the policy has no
   restraint on debottlenecking ($45M export expansions) or marginal
   holes. A measurement bound of the instrument's policy, not necessarily
   of the game: a human can hold cash where the auto-player will not.

Under this policy the survival threshold sits between $90M and $110M for
a seed that finds oil. **Which lever to pull — balance, prices, prospect
quality, or accepting a brutal wildcatter opening — is the design call
this log leaves to Fahad**, now with nothing unmeasured underneath it.

### Condemned structures (2026-08-23, finding 286) — the sweep's third problem, and it was an engine defect

The table above hid a systematic anomaly the two named problems could not
explain: seeds 3 and 4 generate **four and five real accumulations** —
the richest boards in the set — and died at every balance tried, while
seed 2 lived on one. Read directly off the engine: a structure drilled
dry stayed on the board at its re-priced odds, often still the best
number showing, so the auto-player drilled it again — and **every
re-drill counted the same source evidence against the play again**,
collapsing sibling odds on evidence from one hole. Rich boards died OF
their riches: more structures meant more chances to enter the loop. (The
Godot client had already measured the same thing as GC-1: one prospect
drilled three times, POS climbing 0.23 → 0.32 → 0.40.)

The engine fix (finding 286): a completed dry hole **condemns** its
structure — off the board, `reject.prospect-condemned` on a second hole,
persisted in the save (schema v4). A mechanically lost job condemns
nothing; a drilled discovery stays listed (infill is appraisal, and both
clients pick infill wells off the list). SDD-010 §4b carries the rule;
`S1V5` pins all three faces.

Re-measured, same policy, same seeds:

| Opening | 1 | 2 | 3 | 4 | 5 | 11 |
|---|---|---|---|---|---|---|
| $72M post-286 | dead 50 | dead 29 | dead 28 | dead 46 | dead 37 | dead 35 |
| $150M post-286 | dead 71 | **+$77.6M, 2.09M m³** | dead 92 (5 wells, 0.28M m³) | **SOLVENT, +$311.3M, 7 wells, 4.46M m³** | dead 83 | dead 105 (7 wells, 1.34M m³) |

**Seed 4 is the fix measured**: dead at month 71 pre-286, the best
outcome in any cell post-286 — $311M is the rich board finally allowed
to be rich. Seed 3 now dies as a five-well PRODUCING field at $-0.0M —
problem 2's late-game overspend, no longer the zero-producer death.
Seeds 2 and 11 are unchanged to the cubic metre (their runs never
re-drilled), which is the fix behaving as a fix rather than a rebalance.
At $72M nothing moves: the early gauntlet (problem 1) is untouched,
because condemnation cannot make four holes affordable on a $72M purse.

The two REMAINING problems are exactly the two the previous section
named, now with clean numbers under them; the third is closed. The
design call on the levers stays with Fahad.

### The purse (2026-08-23, finding 287) — the game standard, measured and shipped

Fahad's call arrived as a directive rather than a number: **"game
standard for days"** — the opening of a Settlers/Factorio-shaped game
does not dead-end on ordinary luck. The post-286 sweep gives that
standard a measured value:

| Opening | 1 | 2 | 3 | 4 | 5 | 11 |
|---|---|---|---|---|---|---|
| $90M post-286 | dead 48 | dead 93 ($-0.0M, 1.03M m³) | dead 40 | dead 60 | dead 61 | dead 38 |
| $110M post-286 | dead 75 | **+$37.6M, 2.09M m³** | dead 92 (3 wells) | **+$271.3M, 4.46M m³** | dead 72 | dead 56 |

**$110M is the threshold, and it is sharp**: every basin with a
reachable field (seeds 2 and 4) closes the decade solvent from $110M;
at $90M seed 2 falls two producing months short — twice measured — and
above $110M the extra purse converts roughly one-to-one into closing
cash (seed 4: +$271.3M at $110M, +$311.3M at $150M), so more would be
padding rather than game.

Shipped as `content/styles/days/starting-states/bare-ground.json` —
the days overlay, not the base entry, because the wildcatter's purse is
a per-product decision: the base `bare-ground` keeps its $72M neutral
derivation ($50M + the $22M plant), and its stale "no balance survives"
clause is withdrawn (it was findings 285/286's defects speaking). The
number lives in content and nothing pins it in a test; what IS pinned is
the MECHANISM — `EN5` composes Days the way the product does and asserts
the purse differs from a base-only composition by exactly what the two
JSON entries declare, values read off the content itself, proven failing
with the overlay stacking reverted.

The same pass found and closed **the overlay never reaching either
product's composed engine where it mattered most**: the Godot host's
`EngineHost` composed `Content: [content]` — base tree only — on both
the new-game and the load path, with `GodotContentSource.StyleOverrides`
shipped and never called (GC-7 in the client tracker), and the Engineer
suite's own `Compose` had the same gap. Both now stack the overlay
exactly as `Program.cs` always did.

What the purse deliberately does NOT fix: seeds 1 and 5 die at every
balance up to $150M because their boards are structure-poor (one charged
accumulation found late, and none at all). A Factorio map guarantees the
starter patch; this generator does not yet — that is world generation's
question (SDD-010), the one open Settlers-standard item, and no purse
answers it. The auto-player's late-game overspend (seed 3, dying at
$-0.0M as a five-well producing field) remains an instrument bound.
