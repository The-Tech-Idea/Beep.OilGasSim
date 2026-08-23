# 24 — Mechanics are optional, and a mode is a preset over them

**Status:** proposed, 2026-08-23. Nothing built.
**Depends on:** [23 — game rules are a mode](23_GAME_RULES_MODE.md) (built).
**Blocks:** the Oilfield Days re-balance, which cannot be done honestly until
this is settled.

---

## 1. The problem, measured

Oilfield Days is unwinnable, and the last measurement found out why it is
unwinnable for a reason nobody had priced.

A run at a raised opening balance of $120M, seed 11, the game's own auto-player:

```
year 1: $110.2M, 0 wells, 0 prospects       <- the map is dark; nothing to drill yet
year 2:  $99.8M, 0 wells, 3 prospects, 23%
year 3:  $79.3M, 0 wells, 3 prospects, 19%
year 4:  $57.5M, 0 wells, 3 prospects, 20%
year 5:  $25.0M, 0 wells, 3 prospects, 17%
DrillWellCommand refused: the licence's work commitment went unmet and the
bond was forfeited; no further development is possible here
years 6-10: nothing is possible. FINISHED month 120, Failed (BROKE)
```

Six holes drilled, all dry, and at month 60 the licence died. **After that the
game is over and still has five years to run.**

### Why the licence rule is not wrong, and still cannot stay

`DrillWell.cs` is explicit and defensible:

> THE COMMITMENT IS TO A WELL THAT STANDS, not to the money spent trying: a dry
> hole delivers nothing.

That is a real thing a real licence can say. The problem is what it collides
with. `Defaults.LicenceTerms` defends its 60-month deadline like this:

> this field is `DeclareKnownField`d from the first tick (SDD-010 §4b), so there
> is no unexplored acreage to hand back

**S1 made that false.** The map went dark, the field is no longer declared, and
year one now ends with *zero prospects visible*. A deadline reasoned about
against a known field is now a deadline against a dark one. At the shipped
prior — five factors at mean 0.7, about one chance in six, measured at 17-24%
after 3-D — a company gets three or four real attempts before month 60 and needs
about five. **More than half of all Oilfield Days runs lose the licence to bad
luck alone**, and there is no recourse in the rules for that.

This is F-4 exactly: implementation showed the design wrong, so the design moves
first.

### The wrong three fixes

1. **Loosen the deadline.** Buys a year, keeps the coin-flip, and quietly makes
   the realistic licence less realistic — the GC-4 mistake again (plan 23 §1).
2. **Make a dry hole discharge the commitment.** Defensible in industry terms,
   but it is still one global rule bent until the game fits, and it changes
   Oilfield Engineer to solve an Oilfield Days problem.
3. **`if (mode == arcade) skip the licence`.** Design law 03 §3.2, and the whole
   of plan 23, exist to forbid this.

### The right fix

**Oilfield Days should not have a licence mechanic at all.** Not a longer one,
not a laxer one — none. A Settlers-shaped builder about finding oil and building
a plant has no business modelling a work commitment and a forfeited bond, any
more than Settlers models planning permission.

And the licence is not the only one. Once the question is asked properly it is
asked of everything.

---

## 2. The principle

> A mode is a different set of registered models, not a set of branches.
> — design 03 §3.2

Plan 23 applied that to *rules* (what a company may do). This applies it to
*mechanics* (which systems exist at all). Same law, one level up.

**A mechanic is switched off by registering a neutral implementation in its
slot, never by deleting a call site.** `DrillWellActivity` keeps its one licence
check; in Days the licence it checks is one that is always live, holds no
commitments and never expires. The activity has no idea which game it is in —
which is the entire point, and the thing that stops the two products drifting
into two engines.

### The three ways a mechanic can be off

| | How | When to use it | Cost |
|---|---|---|---|
| **A. Neutral model** | The contract is provided by an implementation that never refuses, never charges, never fires | Something requires the contract. **This is the default.** | one class per mechanic |
| **B. Module omitted** | The module is not in the composed list | Nothing requires anything it provides | free, and rare |
| **C. Content is empty** | The mechanic exists but its content declares nothing to do | The mechanic is already content-driven | free |

**Choose A unless B or C is provably available.** `ModuleComposer` validates the
whole set and refuses to start when a requirement is unmet (design 03 §3.3), so
B fails loudly rather than silently — which is the behaviour we want, but it
means B is only reachable for genuinely leaf mechanics.

### The law that keeps it honest

> **A switch has two positions: ON and NEUTRAL. There is no third.**

A switch may not select an *easier* version of a mechanic — only its absence.
The moment "off" means "a gentler licence" instead of "no licence", it is a
difficulty slider wearing a mode's clothes, and every argument in plan 23 §2
applies again. Where a mechanic genuinely needs two live behaviours, that is a
`RuleSet` (plan 23) or a `RealityProfile`, not a toggle.

---

## 3. The inventory

Every player-facing mechanic in the engine, what it does, and where it should
stand. **Days** and **Engineer** are the two shipped presets; the third column
is how "off" is expressed.

### Off in Oilfield Days

| Mechanic | What it does today | Days | Engineer | Off via |
|---|---|---|---|---|
| **Licence** | Term, work commitment, bond forfeit; `DrillWell` refuses when lost | ❌ | ✅ | **A** — a licence that is always live, no commitments, no expiry. Required by `FieldModule`, read in 3 places |
| **Working-interest sale** | Sell a share of the field to raise cash | ❌ | ✅ | **B** — nothing requires it; drop the command |
| **Takeover** | Last-resort restructuring when insolvent | ❌ | ✅ | **A** — a restructuring that never triggers. In Days, broke is broke |
| **Insurance / hedging** | Premiums, payouts, price collars | ❌ | ✅ | **B/C** — financial instruments, no place in a builder |
| **Demurrage / laytime** | Charged when a cargo overstays the berth | ❌ | ✅ | **A** — zero rate |

### On in both

| Mechanic | Why it is in a builder too |
|---|---|
| **Exploration and POS** | Plan 22 §2: spending real money on a *maybe* **is** the game |
| **Drilling, completion, lift** | The verbs |
| **The surface chain** | Plan 22 §4: the plant is the thing being built |
| **Equipment wear, failure, repair** | Something to look after; Stage D is built on it |
| **Weather and access** | Seasons a builder plans around |
| **Reservoir depletion, water cut** | The arc that makes a field finite |
| **Market price** | The reason timing matters |
| **Technology eras and gating** | The tech tree; a builder's progression |
| **Crew competency and training** | Stage B/D; people who get better |
| **Abandonment obligation** | Registered at creation; the cost of having drilled |
| **ESG / flaring score** | Decided 2026-08-23. Gives the gas plant a reason to exist — burning it has to cost something or capture is a purchase with no payoff |
| **Take-or-pay contract** | Decided 2026-08-23. Production with a deadline and teeth; a builder wants something to be late FOR |
| **Bank / borrowing** | Decided 2026-08-23, with a caveat — see §5 |
| **Rivals** | Decided 2026-08-23, with a caveat — see §5 |

### Undecided — §6

Reservoir simulation fidelity, HSE regime, fiscal regime and tax. These are
*fidelity* questions (`RealityProfile`) rather than presence questions, and this
plan should not swallow them.

---

## 4. The shape

`GameMode` (plan 23 §4, built) already names a point in three axes. This adds a
fourth, and it is a set rather than a scalar:

```csharp
public sealed record GameMode(
    ContentId Id,
    string Title,
    string Premise,
    ContentId RealityProfile,    // which physics          (built)
    ContentId StartingState,     // what you open holding  (built)
    ContentId Rules,             // what you may do        (built)
    MechanicSet Mechanics);      // what EXISTS            (this plan)
```

`MechanicSet` is a set of named mechanics that are ON. Everything not named is
neutral. Naming what is ON rather than what is OFF matters: a mechanic added to
the engine next year is OFF in every existing preset and every existing save
until somebody says otherwise, which is the safe direction for a default to fail.

```csharp
public static class Mechanics
{
    public static ContentId Licence { get; } = new("licence");
    public static ContentId TakeOrPay { get; } = new("take-or-pay");
    // ...one per row of §3
}

GameModes.Days     => Mechanics: MechanicSet.Of(Wear, Weather, Eras, Crew, Rivals, …)
GameModes.Engineer => MechanicSet.All
```

Composition then reads the set exactly where it already reads the starting state
— in `ShippedModules`, choosing which implementation fills each slot. **No module
learns what a mode is.**

---

## 5. Toggles, and what they cost

The presets are the two products. Underneath them, each mechanic is a switch a
player can set at New Game — which is what makes this worth building rather than
hard-coding two lists.

**New Game step 1 already exists for this.** The mockup's *Mode* page currently
offers Campaign/Scenario/Sandbox/Challenge (three of them not composed). It
becomes: pick a preset, then optionally open the list and switch individual
mechanics on or off. The preset is a starting point, not a lock — the same
relationship the climate profile already has to the land slider.

Three things this has to get right:

1. **Composition-time, not run-time.** A mechanic decides what gets registered
   before anything is built, so it is chosen at New Game and changed only by a
   recompose — the same rule `RealityProfile` already carries (design 18 §5b.5:
   "allowed and logged" rather than free).
2. **The save records the set.** A reload that composed a different mechanic set
   would silently change what the player is allowed to do. This is the bug plan
   23 §4's amendment already found once in `EngineHost`, and it will be worse
   here because there are more switches to get wrong.
3. **The objective has to survive it.** A scenario whose goal is a cash figure
   means something different with hedging off. Plan 22 §8 already recommends the
   target become content; this makes it necessary rather than tidy.

### Two that stay on, and owe something

Decided 2026-08-23: both ON in Days. Each carries a debt that this plan does not
pay and should not be allowed to go quiet.

- **Bank.** It lends against *reserves*, so it cannot help before the first
  discovery — which is exactly where Days runs out of money. ON as decided, but
  it will do nothing for the arc it is most needed in until it has a second
  lending basis (against a licence, a rig, or the company itself). Until then it
  is a late-game instrument in a game whose problem is early. **Follow-up: a
  pre-discovery lending basis.**
- **Rivals.** ON as decided, and a builder does want a race. Today they only
  explore: they cannot take acreage, bid, or be beaten to anything, so the race
  is unobservable. **Follow-up: give a rival something a player can lose to.**

---

## 6. Decisions needed before this starts

1. ~~Is the §3 inventory right?~~ **Decided 2026-08-23.** Off in Days:
   **licence, working-interest sale, takeover, insurance/hedging, demurrage.**
   Everything else stays on, including bank, rivals, ESG and take-or-pay.
2. ~~Bank in Days?~~ **On** — with the debt in §5.
3. ~~Rivals in Days?~~ **On** — with the debt in §5.
4. ~~Per-mechanic toggles now, or presets first?~~ **Decided 2026-08-23:
   presets first (M1-M3), re-balance (M5), toggles after (M4).** The set will
   change once it is played, and building UI over a set that has not been played
   is building it twice.
5. **Do the fidelity questions (§3, "Undecided") belong here or in a plan of
   their own?** Recommendation: their own, and not yet.

---

## 7. Order of work, once decided

| Step | What | Size |
|---|---|---|
| **M1** | `MechanicSet`, `Mechanics`, the fourth axis on `GameMode`; both presets declared; `ShippedModules` reads it | small |
| **M2** | The neutral models, one per §3 row that needs **A** — licence first, because it is the one that is provably killing runs | medium |
| **M3** | The save records the set, and a reload that disagrees is refused by name | small |
| **M4** | The New Game *Mode* page offers the presets, then the switches | medium |
| **M5** | **Re-balance Days**, now against a game that has only the mechanics it is meant to have | medium |

M5 is the reason for all of it. Every measurement taken before M2 is a
measurement of a game nobody intends to ship.

---

## 8. What this does not change

- The engine keeps every mechanic it has. Nothing is deleted; things become
  optional.
- Oilfield Engineer keeps all of them ON and should not move by a single test.
  **That is the acceptance criterion for M1-M3**, exactly as "rules extracted,
  nothing changed" was for plan 23.
- No mechanic gets an easier version. ON or NEUTRAL (§2).
