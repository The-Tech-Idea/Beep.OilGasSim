# 19 — Stage D: operations

**Goal: running a field that is already producing is a game, not a wait.**

Stages A–C cover getting work done. This one covers the part that fills most of a
run: the field produces, the chain jams, equipment fails, and the player has to
keep it moving. The measured ten-year run in
[14_GAME_SDD_CONFORMANCE.md](14_GAME_SDD_CONFORMANCE.md) spent **sixteen months
of a hundred and twenty on repairs and built two units in the whole decade** —
that is the shape of the game as it actually plays, and none of it is currently
visible except as a line in a log.

Parent: [15_GAMEPLAY_REDESIGN.md](15_GAMEPLAY_REDESIGN.md).
Built per [21_GAME_CODE_PATTERNS.md](21_GAME_CODE_PATTERNS.md).

---

## D1. The alert is the start of a job

An alert today is a line of text in a panel. After this stage it is the front of
a path:

```
something fails ──▶ ALERT names it ──▶ click ──▶ camera goes there,
the element is selected, the repair is the offered action ──▶ dispatch
```

Two clicks from *the separator has failed* to *a crew is on its way*. The
information the panel already carries is unchanged; what is added is that it
leads somewhere.

## D2. Standing orders

The auto-player written for measurement (`DevAutoPlayer`) encodes a policy a
human would want: *repair what has stopped, then debottleneck what is jamming,
then explore*. A player should be able to hold that policy without clicking it
every month.

A standing order is a rule the **client** applies on the player's behalf:

| Order | Effect |
|---|---|
| Keep the plant running | when an element fails, dispatch the maintenance crew automatically |
| Answer bottlenecks | when the chain reports a jam for N months running, commission the addition that answers it |
| Keep the rig busy | when the rig is idle and structures remain, drill the best undrilled one |

**Each is off by default and each is visible while it is on.** A standing order
must never do something the player would not have been offered manually — it is a
macro over the same commands, submitted through the same units, subject to the
same refusals. Anything else and the client is playing the game rather than
running it.

**Why this is not the host simulating.** A standing order chooses *when to
submit a command a player could have submitted*. It computes no outcome, and
every decision it makes is one the read model already showed. It is the same
power as travel — pacing input — held for longer.

## D3. What a producing field looks like

The world already draws wells and the chain. What it does not draw is the thing
the game is about, which is flow.

| Read from | Drawn as |
|---|---|
| `WellStatusView.ProducedThisTick` | pumpjack speed — a well making nothing is a pumpjack standing still |
| `ChainElementView.Throughput` | tanker traffic on the export road |
| `ChainElementView.Failed` | smoke, a stopped flare, a red beacon |
| `Bottlenecks` | the jammed element visibly backed up |
| `WeatherView.Severity` | the sky, and the sea state on a coastal basin |

Every one of those is a published number rendered. None of them is a host-side
model of anything.

## D4. The month, and why it should feel like one

The engine's tick is a month and the client runs it on a clock. A month should
have a shape: work advances, the ledger settles at its end, and the player is
told what changed while they were not looking. A month that passes silently is
the current experience and is the reason the mid-game reads as a wait.

- an end-of-month summary: what was produced, what it earned, what broke
- the alert list resets its "new since last month" mark
- units that finished a job are reported home rather than silently reappearing

---

## Acceptance

- [ ] Clicking an alert selects the element it names and offers its repair.
- [ ] Each standing order can be turned on, is visible while on, and submits only
      commands the player could have submitted manually.
- [ ] With every standing order on, a run matches `--play`'s policy result on the
      same seed — the client's automation and the measurement harness agree.
- [ ] A pumpjack that produced nothing this month does not move.
- [ ] A failed element is visible in the world without opening a panel.
- [ ] The end-of-month summary reports only published values.
