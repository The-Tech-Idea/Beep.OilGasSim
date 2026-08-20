# 22 — The Settlers-shaped game

**A yard, a budget, and ground nobody has looked at.**

Plans 15 rebuilt the client so a player directs a company instead of driving a
truck. It did not touch what the company is *given*, and that turned out to be
the actual problem: at month one the player already owns a complete refinery and
a map of every structure in the basin. The only verbs are *upgrade* and *drill*.

This document is the plan for the game that was asked for. It is written before
any of it is built, and it is the parent of the work.

Read with [15_GAMEPLAY_REDESIGN.md](15_GAMEPLAY_REDESIGN.md) (the client shape,
already built) and [21_GAME_CODE_PATTERNS.md](21_GAME_CODE_PATTERNS.md).

---

## 1. What Settlers actually is

Not "a base builder". Four things, and the oil domain has an honest answer to
each — which is why this mapping is worth making rather than borrowing a mood.

| Settlers | Oilfield Days | Already in OGSim? |
|---|---|---|
| **The map is unknown.** You see your camp; the rest is dark. | The basin is unsurveyed. You know where you are and nothing about what is under it. | ✗ every structure is known at tick 0 |
| **Resources are hidden and must be prospected.** A geologist walks to a mountain and hits it with a hammer. | A survey crew shoots seismic over ground nobody has looked at, and finds a structure — or doesn't. | ~ risk model exists; discovery does not |
| **Everything is a building you place**, and it does nothing until its inputs arrive. | Separator, treater, gas plant, tank, meter — each placed, each fed. | ✗ all ten exist from the start |
| **Transport is the game.** Roads and carriers between buildings; the chain is spatial and it is where the difficulty lives. | Flowlines and gathering. A well with no route to a separator produces nothing. | **✓ mostly — see §5** |

The fourth is the one that makes Settlers *Settlers*, and it is the one OGSim is
closest to already.

---

## 2. The starting position

**What the company has on the first day:**

- a yard: office, workshop, warehouse, fuel, a gate
- a roster of crews and vehicles (plans 17, built)
- cash
- a licence over the basin

**What it does not have:** a well, a separator, a flowline, a tank, a meter, a
single known structure. Bare ground and a bank balance.

**The first ten minutes** should be: look at the basin, decide where to survey,
send a crew, wait, get told what is under one patch of ground. Then decide
whether it is worth a hole. That decision — spending real money on a *maybe* — is
the whole game, and today it is skipped because the answers are printed on the
map before the player arrives.

---

## 3. Discovery: the map is dark

### 3a. What is known at tick zero

Nothing about the subsurface. The company holds a licence and a regional gravity
sweep at best — **structures are not registered as known until something finds
them.**

Today `WorldSink` calls `_risks.Register(prospect, …)` for every accumulation as
the world is generated, so the company knows every structure before it has spent
a penny. That call moves: the world still *generates* everything, and the company
simply has not seen it.

### 3b. Survey an area, not a prospect

Today's `SeismicSurveyCommand` names a prospect — which you must already know
about, which is circular the moment the map is dark. It gains a sibling:

> **`SurveyAreaCommand(Coordinate at, Length radius)`** — shoot a patch of
> ground. Every generated structure whose closure falls inside it becomes
> **known**: registered with the risk model, and from that tick published in
> `Prospects` with its five factors.

The existing per-prospect survey keeps its job — *sharpening* a structure you
have already found — and the two read as what they are: **find**, then **firm
up**.

### 3c. What a survey costs and what it is worth

An area survey is cheap next to a hole and expensive next to nothing, which is
what makes the map a spending decision rather than a chore. **The engine prices
it** — a template in `content/`, like every other activity.

**A survey that finds nothing is a real outcome and must feel like one.** The
ground was looked at, it is now known to hold no structure, and that is worth
money — it is how a company stops paying to think about it.

### 3d. What the client draws

The basin renders as today, because the *surface* is not secret — a player can
see the coast, the hills and their own yard. What is hidden is what is under it.
Surveyed ground gets a visible sweep on the map; unsurveyed ground does not, and
carries no markers.

**No fog over the terrain itself.** Hiding the landscape would be hiding
something the company can plainly see, and the information rule cuts both ways:
never show more than is known, never hide what is obvious.

---

## 4. The plant is buildings, placed and connected

### 4a. Nothing exists until it is built

`SurfaceChain` is today a record of ten non-nullable elements, composed in
`Modules.cs` before the game starts and wired to each other unconditionally. It
becomes **a set that starts empty**.

An `InstallXCommand` stops meaning "fit a better rung into a socket that already
exists" and starts meaning **"build one"**. The upgrade path does not disappear —
a second separator is still how capacity grows — but the *first* one is a
construction project on empty ground.

### 4b. Connections are the player's

`IFlowElementRegistry` already has `Connect(FlowConnection)`. Today every
connection is made once, in `Modules.cs`, wiring a fixed chain. A player who
builds a separator and a tank has to **lay a flowline between them**, and that
flowline is an element with its own cost, its own pressure drop, and its own
place on the map.

This is the Settlers road network, and in this domain it is not a metaphor: a
gathering system *is* the road network of an oilfield.

### 4c. What an unfinished chain does

**Nothing, correctly, and the engine already does this.** From
`IFlowElementRegistry`:

> *An element is available only if everything it feeds is.*

A separator with no tank downstream is not available, so what feeds it shuts in.
A player who builds out of order gets no oil and no crash — which is exactly the
lesson a production chain is supposed to teach, and it is already implemented.

---

## 5. The good news: what the engine already has

Before the cost of this is estimated, what does **not** need building:

| | Already there |
|---|---|
| A runtime-mutable flow graph | `IFlowElementRegistry.Add` and `.Connect` are on the contract; the doc comment even says "registered by the module that created it" |
| Per-segment topology from whatever exists | `ViewFor(available)` builds the graph each segment; nothing assumes a fixed shape |
| Correct behaviour for a half-built chain | availability is downstream-closed, so an incomplete chain shuts in rather than leaking mass |
| Elements with ports, transforms and constraints | `IFlowElement` is a general contract; a separator is not special-cased anywhere in the solver |
| Money, activities, crews, refusals | every build is an ordinary SDD-007 operation |
| Risk and belief per structure | five factors, play correlation, dry-hole re-pricing |

**The flow engine was built for this.** What stands in the way is not the solver
— it is `Modules.cs` composing a fixed chain at startup and handing it downstream
as a ten-field record.

---

## 6. What must change

Ordered by dependency. Each is a phase with its own acceptance test.

### S1 — The map goes dark *(medium)*

- `WorldSink` stops registering risk at generation.
- New `SurveyAreaCommand` + activity + content template.
- `Prospects()` unchanged — it already filters on `risks.Knows`, so hiding is
  automatic once registration is deferred. **This is the whole reason S1 is
  cheap.**
- Client: a survey-sweep overlay, and an area-survey job on the dispatch board.

**Acceptance:** a new game publishes zero prospects. After one area survey, it
publishes the structures under that patch and no others. A survey over barren
ground reports that it found nothing.

**Risk:** the scenario objective assumes a findable field. A basin where the
first three surveys are barren is a legitimate run and a brutal one — §8.

### S2 — The plant starts empty *(large)*

- `SurfaceChain` becomes a mutable set, not a ten-field record.
- Every consumer that reads `chain.Separator` must handle absence.
- `InstallX` creates rather than fits; the ladder becomes the *upgrade* path.
- `FacilitiesState` saves and restores what exists rather than what tier a fixed
  socket holds — **a save-format change**, so SDD-013 moves with it.

**Acceptance:** a new game has an empty chain and produces nothing. Building
separator → tank → meter in order starts production. Building them out of order
produces nothing and explains why.

**Risk:** the highest in this document. Ten elements are assumed present across
the production loop, the facilities module and six install commands.

### S3 — Connections are laid *(large, blocked)*

- Flowlines as player-placed elements between two built elements.
- Needs **coordinates for facilities** — gap G-02/G-14, open since the first
  mockup review.

**Blocked until an engine placement contract exists.** Until then S2's builds go
to the next free bay and connect in chain order, which is honest and not
Settlers.

### S4 — The yard is a building the player extends *(small, later)*

Workshop, warehouse, more crews. **Blocked on G-13** — no crew or fleet entity —
and not worth doing before S1–S2.

---

## 7. What this costs the existing work

**Nothing in plans 15–19 is wasted.** The client is already a base builder: a
camera over your ground, click selection, a roster that travels, arrival-submits,
construction as a crew job, standing orders. Every one of those survives S1–S2
unchanged. What changes is what the world hands the player at the start — which
is engine and content, not client.

**Two client screens need rework**, both small: the lease board lists structures
that will start empty, and the plant panel's catalogue becomes "build the first
one" rather than "add another".

---

## 8. The question this plan cannot answer

**Is a dark map winnable in ten years at $600M?**

Today, with every structure known and the plant pre-built, one policy over eight
seeds returns 1 win, 1 expiry, 4 insolvencies and 2 dead worlds (GC-3, GC-4).
Adding a survey bill and an unbuilt plant makes the opening strictly more
expensive.

That is not an argument against the change — a game where the first decade is
genuinely hard is the game being asked for. It is an argument that **the scenario
must be re-balanced after S1 and again after S2**, with the auto-player as the
instrument, and that its objective may need to stop being a cash figure.

**Recommendation:** re-measure after each phase, and treat the scenario's
target as content to be tuned rather than a fixed requirement.

---

## 9. Decisions needed before S1 starts

1. **Does the company begin with any regional data at all**, or literally
   nothing? Settlers shows you your immediate surroundings — the equivalent might
   be that structures within some distance of the yard are known.
2. **Does an area survey reveal a structure's POS, or only that it is there?**
   Revealing the five factors immediately makes the follow-up survey pointless;
   revealing only presence makes the first survey a weaker product than today's.
3. **Should the shipped scenario keep a cash objective**, given §8?
