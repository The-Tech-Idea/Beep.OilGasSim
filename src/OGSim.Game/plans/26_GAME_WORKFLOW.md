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
