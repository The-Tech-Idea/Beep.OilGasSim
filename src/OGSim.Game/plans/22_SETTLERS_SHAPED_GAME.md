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

**Done, 2026-08-20.** The licence is dark and the player buys it back a block
at a time.

**What it took, and the one thing that changed shape.** The plan wrote the order
as `SurveyAreaCommand(Coordinate at, Length radius)`. That command cannot be
built: an activity is aimed at an `EntityRef` and a depth
([SDD-007](../../../plans/sdd/SDD-007_OPERATIONS_ENGINE.md) §5), a centre and a
radius fit neither, and the per-template parameter block that would generalise it
is an open item F-4 forbids inventing at a call site. The attribution in that
item was stale as well — it named R12b.16, which shipped, and shipped "one
activity, one class" instead.

So the area became an **entity**. Acreage is licensed in blocks, a block is the
area, and an activity aimed at a block is aimed at exactly one `EntityRef` — the
existing channel carries it and the open item stays closed. It is also the better
design: blocks are saveable, targetable, refusable and nameable, and "shoot
BLOCK 07" is a thing a player can say.

The two surveys now read as the two questions exploration actually asks:

| | |
|---|---|
| **`seismic-2d`** | reconnaissance over a block. **Finds** closures. $0.8M, one month |
| **`seismic-3d`** | detail over a structure already found. **Sharpens** it. $2.5M, two months |

`seismic-3d` naming a prospect stopped being circular the moment a prospect had
to be found first.

**Built:** `IBlock` + `EntityKind.Block`; `WorldState` charts rather than
registers, cuts the licence into a 4x4 grid derived from the terrain, and
`Shoot`s a block; `SurveyBlockCommand` + `SurveyBlockActivity`; `BlockView` on
the read model; the shot blocks persist (`world.decisions` schema 2 -> 3);
client `BlockOverlay`, `JobKind.SurveyBlock`, and a command-bar action.

**Acceptance — all four pass** (`S1V1`–`S1V4` in `NewGameTests`):

- a new game knows of no structure at all, while the world really placed some
- shooting a block finds what is inside it **and no others**
- a barren block comes back surveyed, saying "nothing here" rather than looking
  untouched
- a block already shot refuses a second pass and says why

`BlockHolding` in that suite throws if a structure falls outside every block, so
the grid is proven to cover the ground rather than assumed to.

**Cost to the existing suite: none.** 13 `NewGameTests` had funnelled through one
fixture, `BasinWithSeveralProspects()`, and seven more tests called `NewGame`
directly; all now shoot the licence first, through the engine's own method rather
than a stand-in. The five composition failures on master were measured with this
work stashed and fail identically without it.

**Still open from this phase:**

- A block is 6 km on a 24 km basin, which is legible from the whole-basin view
  and invisible at play zoom. The overlay holds its strokes and type at a
  constant screen size to cope, but the grid belongs on the minimap too.
- The yard's own block starts dark like any other. Arguably a company knows the
  ground it parked on.
- §9's first question is answered by default rather than by decision: the company
  begins knowing **nothing**.

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

#### S2.0 — the SDD review, 2026-08-20

**The blast radius is smaller than this plan feared, and the test exposure is
larger.**

`SurfaceChain` is an eleven-field record touched by **five files** — `Modules`,
`ProductionLoop`, `FacilitiesState`, and two test files. It is not spread across
six install commands; they all sit in `Modules`. Member access is about thirty
sites, and the compiler will enumerate every one the moment the fields go
nullable.

The exposure is in the suite: **181 test methods across eight files** assert
production, throughput or the chain, reached through **46 `EngineBuilder.Build`
calls**. A plant that starts empty makes every one of them produce nothing.

**Confirmed, not assumed:** `IFlowElementRegistry` is `Add` / `Connect` / `All` /
`ViewFor(available)`. Nothing in it requires a fixed shape, and an absent element
is simply one that was never registered. §4c's claim holds.

**What the starting plant is.** Not a composition constant. What a company begins
holding is **content** — the same answer the repo gives everywhere else
("rebalancing is a content edit"). The shipped scenario declares an empty plant;
the test fixture declares a full one. That is the lever that makes the flip
affordable: 46 build sites do not each need editing, and a test that is about
water cut keeps its separator without having to say so.

It is also the honest model. "The company already owns a refinery" is a scenario
statement, and it was only ever true because `Modules.Compose` said so.

**The order is forced, and S1 taught why.** S1 was attempted second-half-first and
took thirteen tests down; the same mistake here takes down a hundred and eighty.

1. `SurfaceChain` -> a mutable plant with optional members. Nullable, so the
   compiler finds every consumer rather than a throwing accessor hiding them.
2. Teach the three consumers absence — `ProductionLoop` refuses a tie-in with no
   manifold, `FacilitiesState` saves what EXISTS rather than what tier a fixed
   socket holds (a save-format change, so SDD-013 moves with it), the read model
   lists what is there.
3. `InstallX` creates when absent and upgrades when present.
4. **Only then** does composition stop building the chain, and the content says
   what a scenario starts with.

Steps 1-3 keep the suite green because nothing is ever absent yet. Step 4 is the
behaviour change and lands with the content and the fixtures in the same commit.

##### Step 3 in progress, 2026-08-20 — the chain can now be wired a piece at a time

**The plant's shape moved onto the plant.** Ten edges were typed out in
`Modules.Compose`, which was fine while composition built the whole chain in one
go and wrong the moment a player builds it piecemeal. `SurfaceChain.Wire` now
holds the table and connects **each edge exactly once, when its second end
appears** — so it is safe to call after every install, in any build order.

That "exactly once" is load-bearing: `FlowElementRegistry.Connect` appends
without checking for duplicates, and one edge per port is SDD-002 §6's rule. A
second call for the same pair would quietly double the edge and hand the solver a
port feeding two of the same thing.

Declared in the original connection order, so a plant built all at once produces
the identical topology. **Verified:** replacing the ten hand-written calls left
the suite at its 5-failure baseline with all 236 other composition tests passing,
including every production and chain test.

##### The open question is answered, and the answer is awkward

The review asked what the minimum producing chain is. Read off the topology:

```text
manifold -> flowline -> separator --+-- Liquid -> treater -> custody --+-- OnSpec -> tank
                                    |                                  +-- Reject -> off-spec sink
                                    +-- Gas    -> gas plant -> flare
                                    +-- Water  -> disposal
```

**Ten of the eleven elements** — everything except the water intake, which is a
source for a flood rather than a sink. The separator has three outlets and each
needs somewhere to go, so a company building strictly piece by piece would buy
**ten things and see no oil until the tenth**. That is not a Settlers first
build; it is a shopping list with no feedback.

Three ways out, and the choice is a game-design decision rather than a technical
one:

| | |
|---|---|
| **A. Ten separate builds** | Honest and slow. The player commits a lot of money before anything happens, and the tenth purchase is the only one that visibly does something |
| **B. An early production facility first** | One purchase erects the minimum train; `InstallX` then adds and upgrades individual elements. **An EPF is a real thing** — a packaged, skid-mounted plant is exactly how a small field starts — so this is industry-true rather than a convenience |
| **C. The minimum depends on the fluid** | An outlet carrying nothing needs no destination, and the code already relied on that once: the water leg "stays unconnected and carries nothing... piped the day there is water to put down it". So a dry-oil discovery could run without gas or water handling, and a gassy one could not. Emergent and elegant; the most work, and the hardest to explain to a player |

**Decided: B.** An early production facility. It gives the player a real first
decision that visibly starts production, keeps every sink present so conservation
is never at risk, and leaves the existing install commands doing exactly what
they already do — add capacity and climb tiers. **C stays the shape to grow
into**, and the code already leans that way: an outlet carrying nothing needs no
destination.

##### Built for it, 2026-08-20

- **`PlantBuilder`** — sixty lines of construction lifted out of
  `Modules.Compose`. **One builder, two callers**: composition still commissions
  a plant at startup, and the activity a company pays for calls the same code.
  Two ways to build a chain would be two chains that drifted, and the second
  would be the one nobody tested.
- **`InstallEarlyProductionFacilityCommand`** — aimed at the FIELD rather than at
  an element, because every other install names the element it upgrades and this
  one cannot: none of them exist yet, and the point is that it makes them.
  **$22M over four months** against a $50M opening balance, so it is the largest
  commitment of the early game and the thing a first discovery has to be worth.
  Less than the sum of its vessels, because a skid package IS less than eleven
  bespoke units — which is why a small field is brought on this way.
- **`Standing()` is checked twice** — at the order and again at completion. Two
  commissionings ordered in the same month would both pass the refusal, and the
  second must not register ids the first already has.
- **"Early production facility" entered the glossary first** (naming law N7).
- **`S2V1`** pins the refusal, and pins it on the useful sentence: "you already
  have a plant" is true and useless, so the reason says capacity is bought a
  vessel at a time from here.

##### Step 3 done, 2026-08-20 — the scaffolding is gone

All six upgrade activities now take the **plant** rather than the vessel they
enlarge, and refuse on bare ground with the sentence that names the remedy:

> *there is no separator here to enlarge; a field is brought on by commissioning
> an early production facility, and its vessels are upgraded after that*

"There is no separator" would be true and would leave a player hunting for a
separator to buy. The refusal has to point at the facility.

Three details worth keeping:

- **`Aim` needs a target before the refusal runs.** The shared checks ask
  `IsRunning(template, target)` before `OwnRefusals` is consulted, so an order
  with no vessel to name still has to name something. It aims at the field —
  and is then refused for the same reason it had nothing to aim at.
- **Completion re-checks.** A vessel can vanish between order and completion in
  exactly one way — a save restored onto a plant that never had one — and
  fitting a rung to nothing is worse than doing nothing.
- **`SurfaceChain.Needs` is deleted.** It said "when the last caller goes, so
  does this", and twenty-five callers went: five to the plant handover in
  `ProductionLoop`, fourteen to the save format, six to these activities. A
  member with no behaviour is a law L3 violation, so the promise was also a
  requirement.

Suite: 5-failure baseline, **237 passing**, 0 warnings.

##### What step 4 costs — cheaper than this plan assumed

The review worried that 46 `EngineBuilder.Build` calls would each need editing.
They will not. **Every test reaches composition through one factory**,
`Fixture.Settings()`, and the game builds its own settings in exactly two places.
So the flip is **three call sites**: the fixture keeps a commissioned plant, and
the two host paths start bare.

That also reopens the "content or settings" question in the review's favour but
by a different route. `EngineSettings` already carries `RealityProfile` as a
composition-time `ContentId`, on the grounds that it decides what gets registered
before anything is built — which is precisely what a starting plant does. A
starting-plant id belongs beside it, rather than a bare bool, and stays a content
edit.

**Note for the balance pass:** the EPF is charged to nobody yet, because
composition still builds the plant for free. Step 4 is where a company starts
paying the $22M, and where §8 has to be re-measured — on top of S1 having already
moved it hard.

##### Step 2 done, 2026-08-20 — the consumers handle absence, suite at baseline

**`ProductionLoop` now takes the plant instead of five loose elements.** The L5
duplication is gone, and with it five `Needs` calls: absence is asked about in
one place rather than kept in sync across two classes.

Twenty sites needed a decision, and they fell into four kinds:

| | |
|---|---|
| **Notifications** (`Promise`, `ForgetPromises`, `VapourLossOver`) | nothing built, nothing to tell — `?.` |
| **Rates and reports** (headroom, storage, captured gas) | an honest **zero**. An unbuilt disposal well accepts nothing, which is the same answer the arithmetic already gives for a full one, so nothing downstream needed a special case |
| **Solver id matching** | an unregistered element appears in no solution, so the comparison simply cannot match |
| **Mass handling** (`Receive`, `Draw`, `Commit`) | see below |

**The mass paths are where S2 could have leaked, and they are the part worth
reading.** A `?.` there would let oil that reached storage with no tank vanish
from the tick's conservation terms — silently, and only on a half-built plant,
which is exactly when nobody is looking. So `StoreAndExport` refuses instead:

> no tank and nothing stored is an ordinary state on the way up; **no tank and
> oil arriving is an `InvariantFault`**, because a downstream-closed chain should
> have shut those wells in long before stage six.

That is one guard, at the one place mass would actually disappear, rather than
four scattered ones (law L4).

**`FacilitiesState` saves what exists.** A flag per element plus its tier, on the
pattern `world.decisions` already uses for its header — a sentinel tier id would
have made some legitimate catalogue entry unusable the day content named one that
way. Schema 1 -> 2. All fourteen of its `Needs` calls are gone; a save that
describes a plant this composition did not assemble is refused rather than
half-restored, because a restore cannot CREATE an element until step 3.

**The read model needed nothing.** `Chain()` walks `FlowOrder()`, which derives
from the registry — so it has always listed what exists rather than what a record
declared. One less thing than the review expected.

**One bug, caught by the suite:** `Capture` wrote `tank-contents` while `Built`
read `tank-contents-built`, and eleven save/reload tests said so immediately.
Fixed; the suite is back to its 5 + 9 baseline with all fifteen other projects
green.

**Scaffolding left: six `Needs` calls, all in `Modules`, all install activities.**
That is precisely step 3's worklist — each is a command that fits a rung onto an
element it assumes exists, and has to start being able to build one.

##### Step 1 done, 2026-08-20 — behaviour-neutral, suite at baseline

`SurfaceChain` is now a mutable class with eleven optional elements and an
`Install` overload per type, so a caller cannot put a separator in the tank slot.
Composition installs all eleven, one call each; step 4 shortens that list.

Going nullable made the compiler produce the worklist: **50 errors in exactly the
three files the review predicted** — `FacilitiesState` 28, `Modules` 18,
`ProductionLoop` 4 — plus 18 in two test files. Nothing turned up anywhere
unexpected.

Two findings from working through them:

- **`ProductionLoop` takes tank, custody, gas plant, disposal and intake as five
  loose constructor parameters, while `FieldControl` holds the chain that owns
  them.** One fact, two owners, across two classes — law L5. (An earlier note
  here said `ProductionLoop` took the chain as well; it does not, and the fix is
  to give it the chain and drop the five.) One of the fields is even named
  `_chainGasPlant`.
- **The tie-in already refuses correctly.** `OpenWell` throws an
  `InvariantFault` when there is no free slot, on the grounds that the drilling
  command should have refused first — and `Slots` is now zero without a manifold,
  so an unbuilt header is already caught by the check that was there. The guard
  was extended to name the flowline too. What still needs writing is the player-
  facing refusal, because "the header has 0 slots and all are taken" is a poor
  way to say "you have not built one".

Sites that have not yet been taught absence call `SurfaceChain.Needs(x, "name")`,
which refuses and names the missing element. It is scaffolding with a
deliberately visible name: **every call is a step 2 worklist item, and when the
last one goes, so does it.** There are twenty-five: eleven in `Modules`, fourteen
in `FacilitiesState`. `ProductionLoop` needed none — pattern matching bound what
its existing guard had already proved.

**One thing to settle before step 3:** what the minimum producing chain is.
Availability is downstream-closed, so a separator with nowhere to send gas or
water is unavailable and shuts in everything behind it. That likely makes the
smallest working plant manifold -> flowline -> separator -> flare -> disposal ->
custody -> tank — most of the chain — which is a real design question about
whether the first build is one decision or seven.

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

### Measured after S2 — 2026-08-20. The scenario is unwinnable, and here is the arithmetic

The auto-player now buys its map, drills on odds, and commissions a facility when
it has something to build one for. It still dies on every seed tried (1, 3, 7),
and this time the numbers say why rather than the policy.

**What a freshly revealed prospect is worth: 14-18%.** That is the measurement
that matters and it was not visible before — the yearly report now prints the
best odds on the board, because a run that drills nothing looks identical to a
run with nothing worth drilling and the two want opposite fixes.

The odds are the prior, and correctly so: **2-D reconnaissance finds a closure,
it does not sharpen one** (SDD-010 §4b). Only `seismic-3d` moves trap and
reservoir.

So the cost of first oil, at shipped prices:

```text
opening balance                              $50.0M
  survey a block                    $0.8M    x16 = $12.8M for the whole licence
  3-D over a structure              $2.5M
  a hole                            $8.0M
  early production facility        $22.0M

at 15% POS, a discovery costs about 6.7 holes  =  ~$53M
                          ...before the plant  =  ~$75M to first oil
```

**Finding oil alone costs more than the company starts with**, and the plant is
another $22M on top. No policy wins this; the ones tried die in months 42-75 with
two to five dry holes.

**This is content, not code.** Every number above is a `Defaults` constant or a
scenario figure, and 22 §8 said the scenario would need re-balancing after each
phase. It has now been moved twice — S1 made the map cost money, S2 made the
plant cost money — and it needs setting once against the finished shape rather
than chased between phases.

The levers, in the order they seem worth pulling:

| | |
|---|---|
| **Opening balance** | The bluntest and the most honest: an exploration company that cannot afford its first campaign is mis-capitalised, not unlucky. $50M was set against a company that started holding the plant |
| **POS priors** | 15% is a defensible real number and a punishing game one. Worth checking that the five factors are not compounding lower than SDD-008 intends |
| **Drilling cost** | $8M a hole against a $50M balance makes six holes impossible by construction |
| **Borrowing** | `BorrowCommand` exists and nothing in the harness uses it. A bank lends against RESERVES, though, so it cannot help before the first discovery — which is exactly where the money runs out |

**Recommendation:** do not tune any of these until S3/S4 are settled or explicitly
dropped, then re-balance once with the auto-player as the instrument. Tuning now
would be the third re-balance in a day and the first two are already stale.

### Measured after S1 — 2026-08-20

Seed 3, the same auto-player policy as before, now buying its own map:

```text
before S1   $134.4M of $600M, Expired at month 120 — 18 holes, 7 surveys
after  S1   INSOLVENT at month 25-32 — 1 block shot, 4-5 holes, every one dry
```

**The loop closes** — a block is shot, structures appear, they get drilled — and
the run is far harsher. That was expected; §8 said the opening gets strictly more
expensive and it has.

**Read the policy before reading the balance, though.** The harness surveys only
when the board is completely empty, so it shot ONE block, found several
structures, drilled every one of them, and was broke before it could look
anywhere else. A company would shoot two or three blocks first and drill only the
best of what turned up. What this measures is that **a naive policy now dies**,
which is a different claim from "the scenario is unwinnable" and must not be
mistaken for it.

What it does establish:

- **$50M is about one survey and four holes.** With a dark map the opening budget
  no longer stretches to a campaign, and the shipped target of $600M in ten years
  was set against a company that started holding the map.
- **The next measurement needs a policy worth measuring** — spread surveys, drill
  the best odds rather than all of them, and keep a reserve. Until that exists,
  re-pricing the survey or the opening balance would be tuning against a strawman.

So: S1 has moved the balance and the number is not yet meaningful. Re-balancing
waits on a better auto-player, and on S2 — which will move it again.

---

## 9. Decisions needed before S1 starts

1. **Does the company begin with any regional data at all**, or literally
   nothing? Settlers shows you your immediate surroundings — the equivalent might
   be that structures within some distance of the yard are known.
2. **Does an area survey reveal a structure's POS, or only that it is there?**
   Revealing the five factors immediately makes the follow-up survey pointless;
   revealing only presence makes the first survey a weaker product than today's.
3. **Should the shipped scenario keep a cash objective**, given §8?
