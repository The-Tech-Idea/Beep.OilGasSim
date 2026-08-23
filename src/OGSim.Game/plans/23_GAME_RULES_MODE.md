# 23 — Game rules are a mode, not an edit

**OGSim is built for realistic scenarios. Oilfield Days is a Settlers-shaped
game. Those want different rules, and neither is wrong.**

Written before any of it is built, and it is the parent of the work.

Read with [22_SETTLERS_SHAPED_GAME.md](22_SETTLERS_SHAPED_GAME.md), which is what
kept running into this.

---

## 1. The problem, twice

Two findings from the same cause, a month apart.

**GC-4.** `ActivityOrders.Refusals` refused every activity on a field with
`CompartmentCount == 0`. Correct for a realistic run — an operator with no
reservoir has nothing to work on. Fatal for the game, where a company begins with
nothing and has to buy its way to a discovery. **The fix that shipped deleted the
rule.** That was the wrong fix: it removed a real constraint from realistic
scenarios to unblock a game, and left nothing standing in its place.

**S2.** Drilling refuses a well with no free manifold slot (SDD-006 §1b), which
is right: a well that cannot flow is money burned. Once the plant stopped being
free, "no plant" became "zero slots", and the rule that meant *your header is
full* started meaning *you may not drill until you have bought a $22M facility*.
That inverts the real sequence — a company drills, tests, suspends, and builds a
facility once it knows it has something.

The pattern is the same both times. **A rule that is right for an operator
running a field is wrong for a company building one from nothing**, and editing
the engine's rule to suit the game damages the engine.

---

## 2. What this is NOT

**Not a fidelity choice.** `RealityProfile` already exists and is a different
axis: it decides which *physics* fills a slot — arcade fluid properties against
the full correlation set. Nothing about arcade fluid says whether a well may be
drilled before a header exists.

A run picks one of each, and they are independent:

```text
fidelity   simulation | arcade      which model computes it
rules      realistic  | frontier    what a company is allowed to do
```

**Not a difficulty multiplier.** 01 §J5: "never a bare difficulty multiplier".
Nothing here scales a number.

**And above all, not a branch.** 03 §3.2 is explicit:

> "arcade mode" is a different set of registered models, **not a set of
> `if (difficulty == …)` branches.

So a game mode is a **different set of registered rule implementations**. Every
contested rule becomes a contract with two implementations, and the mode decides
which one is composed. An `if (mode == Game)` anywhere in an activity is the
failure this document exists to prevent.

---

## 3. The shape

Each contested rule becomes an interface, sitting where the inline check is now:

```csharp
/// Whether a company may drill, and why not.
public interface IDrillingRule
{
    IReadOnlyList<RejectionReason> Refusals(DrillWellCommand order, FieldControl field);
}
```

Two implementations, both real and both shipped:

| | `realistic` | `frontier` |
|---|---|---|
| **Drilling** | needs a free slot on a built header — a well that cannot flow is money burned | may drill anywhere on the licence. The well is **suspended** until a facility exists, and tied in when one is commissioned |
| **Activity subject** | refuses work on a field with no compartments (GC-4's rule, restored) | permits it: finding the first compartment IS the game |
| **Access window** | refuses work that cannot reach the site this month (SDD-016 §5b) | *undecided — see §6* |

`frontier` because that is what the mode is: a company on ground nobody has
worked, which is exactly the Settlers position. `realistic` is the shipped
default and is what every existing test runs at.

---

## 4. Where the mode lives

Beside `RealityProfile` on `EngineSettings`, for the same reason and with the
same shape: a composition-time `ContentId`, required, no default. It decides
what gets registered before anything is built, so it cannot be chosen later.

```csharp
ContentId RealityProfile,   // which physics
ContentId Rules,            // what is allowed
ContentId StartingState,    // what the company opens holding
```

Three composition-time choices, three questions, none of them the same question.
`StartingState` (S2, built) says a company opens on bare ground; `Rules` says
what it may do from there. A run on bare ground under `realistic` rules is a
legitimate and very hard scenario — and it is currently the accidental one the
game ships.

### Amended 2026-08-23 — the three axes need a name, and the name is the product

Three independent axes is the right structure and the wrong thing to hand a
host. Nothing said which combinations were *products*, so every caller picked
three ids and hoped they were consistent — and two of them (the new-game path
and the load path in `EngineHost`) spelled the same three out twice, which is
one drift away from a saved game reloading under different rules than it was
played under.

A **game mode** is a named point in that space. It owns no new fact: it writes
the three axes and nothing else, so `EngineSettings` stays the single owner of
what the engine was composed with (law L5). What it adds is a name, a premise,
and a closed set — the same shape `RealityProfile` and `RuleSet` already have,
including refusing an id it does not know.

Two are shipped, and they are the two products:

| Mode | Reality | Starting state | Rules | The game |
|---|---|---|---|---|
| `days` | `arcade` | `bare-ground` | `frontier` | **Oilfield Days** — build the plant, forgiving rules |
| `engineer` | `simulation` | `opening-position` | `realistic` | **Oilfield Engineer** — the field as it actually behaves |

They are not easy and hard. They ask different questions. *Days* starts you with
nothing built and lets you drill on acreage that has no plant yet, because the
game is about building one — the well waits, shut in, until there is somewhere
to send it. *Engineer* starts you holding a commissioned train and refuses a
well with no tie-in slot, because that is what an operating company faces and
the remedy is a bigger header, not a dispensation.

The physics underneath is the same engine, the same fourteen stages, the same
eighteen commands and the same read model. That is the whole point: a mode
selects models, it does not fork behaviour.

---

## 5. What it costs

Small, and mostly mechanical:

- one interface and two implementations per contested rule
- a `RuleSet` record naming which implementation fills which slot, mirroring
  `RealityProfile` exactly — including `ProfileNamed`'s refusal of an unknown id
- the activity holds the contract instead of the check
- `EngineSettings` gains one required field, and there are **three** call sites
  (the test fixture and two host paths)

The existing suite runs at `realistic` and should not move by a single test. That
is the acceptance criterion for the refactor half: **rules extracted, nothing
changed.**

---

## 5b. Built, 2026-08-20 — the seam and the first rule

`RuleSet`, `RuleSets.Realistic` / `.Frontier`, and `RuleSets.Named` refusing an
unknown id — mirroring `RealityProfile` down to the refusal. `EngineSettings`
gained `Rules`, required and with no default, and there were indeed **three call
sites**: the test fixture and the two host paths.

**The first rule is GC-4's, and it is restored rather than deleted.**
`IWorkSubjectRule` has two implementations:

- `OperatingSubjectRule` — an operator with no reservoir has nothing to work on.
  **This is the constraint GC-4 removed from the engine**, back where it belongs
  and now true only where it is true.
- `FrontierSubjectRule` — permits it. Permissive, not absent: cash, rig,
  one-at-a-time, weather and every activity's own subject still refuse.

`ActivityOrders` asks the rule instead of deciding. There is no `if` on the mode
anywhere below `RuleSets`, which was the point (03 §3.2).

**`RulesV1` is the acceptance**, and it is the clearest statement of the design:
one company, same seed, same content, same bare ground — refused under
`realistic`, accepted under `frontier`. Neither answer is a bug.

Suite: 5-failure baseline, 241 passing, 0 warnings. The existing tests all run at
`realistic` and not one moved, which was the criterion in §5.

**The game now runs `frontier` + `bare-ground`** — two independent choices that
were previously one accidental behaviour.

## 5c. The drilling rule, 2026-08-20 — and suspended wells

`IDrillingRule`, two implementations, and the inline check in
`DrillWellActivity` replaced by one call.

- `OperatingDrillingRule` — a header must exist and have a free slot. Keeps the
  two-remedies distinction: a FULL header wants a bigger one, and NO header
  wants a facility.
- `FrontierDrillingRule` — a hole may go down with no plant at all. A full
  header still refuses, for the original reason.

**A well with nowhere to go is SUSPENDED, not refused.** That is what an
exploration well is — drilled, logged, and shut in for as long as it takes to
decide whether a facility is worth building. `OpenWell` opens the well, registers
the abandonment obligation as it always did, and then either ties it in or shuts
it in and remembers it. Commissioning a facility calls `TieInWaiting`, and the
holes the company already paid for come on without being re-ordered.

**§6.3 answered itself.** A suspended well needed no new read-model state: it is
shut in with the choke the game already had, so it reports as `ShutIn`, which is
exactly what it is.

**One ordering fix this forced.** `FieldControl.RestoreAfter` was empty, so on
reload the wells could be reopened before the plant existed and every one of
them would restore suspended, waiting for a facility that was already there. It
now declares `facilities.units`, which is what `RestoreAfter` is for.

**Acceptance:** `RulesV2` (a frontier company may drill with zero slots) and
`RulesV3` (the well is `ShutIn`, then comes on when the facility lands). Suite:
5-failure baseline, **243 passing**, 0 warnings.

## 6. Decisions needed before this starts

1. ~~How many rules move first?~~ **Answered: GC-4's rule.** It needed no new
   machinery and it undid a deletion, so it proved the seam and repaid a debt in
   the same change. **Drilling is next** and is the one S2 is waiting on — it
   needs suspended wells (§6.3) before `frontier` can allow a hole with no
   header, so it is a bigger piece than this one was.
2. **Does `frontier` relax the weather-access window?** It is a real constraint
   and a genuinely interesting one; it is also the kind of thing that makes a
   base-builder feel arbitrary. Recommend leaving it alone until it annoys
   somebody.
3. ~~Do suspended wells need a read-model state?~~ **No — answered by building
   it.** `WellStatus` already had `ShutIn` and the choke already had `Closed`, so
   a suspended well says what it is with no new vocabulary. The lesson is worth
   keeping: the state machine had the word before the game needed it.
