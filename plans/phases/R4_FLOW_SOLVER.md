# Phase R4 — Flow Solver Core

**Arc I · Foundation** · Status ⬜ · Depends on: R1, R2, R3 · Enables: all of Arc II

---

## 0. Purpose

Build the one flow engine, and **prove it correct before any domain exists.**

R4 knows nothing about reservoirs, wells, separators or terminals. It knows
`IFlowElement`: ports, constraints, a transform, and availability. It is tested
entirely against synthetic elements — a source, a sink, a restrictor, a splitter,
a buffer.

**This is the most important sequencing decision in the plan.** The solver is the
highest-risk component and everything in Arc II depends on it. Proving it against
synthetic elements means its correctness is established independently of any
domain modelling. If the approach in [04](../design/04_MATERIAL_AND_FLOW.md) §4
is wrong, that is discovered here, cheaply, before seven phases of domain work
are built on top of it.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | One solver handles any network of any elements | The solver contains no type test and no material-identity branch (architecture test) |
| G2 | Mass is conserved, always | The invariant holds every tick over randomised networks and randomised operation sequences |
| G3 | The binding constraint is always identified | For a network with a known undersized element, the solver names it and the deferred volume matches the analytic answer |
| G4 | Backpressure propagates | A downstream constraint measurably reduces upstream withdrawal within the same solve |
| G5 | Non-convergence never ends the game | The shut-in ladder ([04](../design/04_MATERIAL_AND_FLOW.md) §4.0b) shuts in the worst branch, audits it, and re-solves; the tick always completes, and recurrence raises `flow.solverFault` |
| G6 | Specification gates work | An off-spec stream does not pass; the rejected mass is accounted for exactly |
| G7 | Performance is adequate | A 500-element network solves within the tick budget |

---

## 2. Design decisions

### 2.1 Solver method — forward propagate and throttle

**Decision: iterative forward propagation with constraint-driven throttling and
back-propagation** (open decision FD1, recommendation accepted).

| Considered | Verdict |
|---|---|
| Forward propagate + throttle | **Chosen.** Converges quickly on tree topologies; explainable step by step; **yields bottleneck attribution as a natural by-product** |
| Full network Newton solve | Rejected for now. More general, handles loops, but the bottleneck attribution must then be reconstructed after the fact and the failure modes are opaque to a player-facing explanation |
| Linear program | Rejected. Fast and optimal, but the physics is non-linear and the "why" is unreadable |

The deciding argument is G3. **The bottleneck report is a headline game feature**
([06](../design/06_WORLD_AND_EXPLORATION.md) §9), and a method that produces it
free is worth more than a method that is more general.

**Revisit trigger:** if looped networks become common (FD4), re-evaluate. The
contract `IFlowSolver` makes the method replaceable.

### 2.2 Availability is decided before the solve

Tick stage 4 sets availability; stage 5 solves. **An unavailable element is
simply absent from the network** — it is not present-with-zero-capacity.

*Rationale:* absence is unambiguous. A zero-capacity element invites
divide-by-zero, produces confusing attribution ("the bottleneck is the broken
compressor" is right; "the bottleneck is a compressor with 0 capacity" is a
degenerate case waiting to be mishandled), and blurs the difference between
"broken" and "very small".

### 2.3 Convergence and its budget

A declared iteration budget and a declared tolerance, both content-configurable.
Exhausting the budget engages the **shut-in ladder**
([04](../design/04_MATERIAL_AND_FLOW.md) §4.0b): the branch with the largest
residual is physically shut in with cause `solver-stability`, audited, and the
reduced network re-solved with a fresh budget. Termination is guaranteed — a
fully shut-in network converges trivially at zero rate — so **the tick always
completes**.

**There is still no fallback to a simpler model.** The ladder solves the same
physics on a smaller network; nothing is substituted, so the no-fallbacks rule
holds on exactly the ticks where the physics is hardest. Under the strict fault
policy (CI and tests), the first forced shut-in throws — a pathological network
in a test is a bug, not something to operate around.

### 2.4 Attribution is computed during the solve, not after

Each throttling event records: which element bound, which constraint of that
element, and how much rate was removed. Deferred volume is the sum over the tick.

*Rationale:* reconstructing attribution afterwards requires re-running
counterfactuals. Recording it as it happens is exact and nearly free.

### 2.4b The solver runs once per segment

Tick stage 5 invokes the solver once for each segment in the plan built at stage
4 ([03_ARCHITECTURE](../design/03_ARCHITECTURE.md) §6.2). R4 must therefore
accept a network whose available element set differs between invocations within
one tick, and stage 6 duration-weights the results.

**Availability is segmented, never averaged** — the solve is non-linear, so a
compressor available 60% of the month is not a compressor at 60% capacity. The
segmentation machinery itself is R1.14; R4 only has to be re-entrant within a
tick and to commit nothing until every segment has solved.

### 2.5 Commit is a separate step from solve

The solver produces a **proposed** solution. Commit — depleting compartments,
moving inventory, updating tank levels — happens in tick stage 6 after the
conservation check passes.

*Rationale:* this is what keeps both failure paths clean. A conservation
violation at commit can abandon the tick whole only because nothing mutated
during the solve — and the shut-in ladder can re-solve a reduced network only
because the failed attempt left no partial state behind.

### 2.6 Synthetic test elements

Five elements, in the test assembly only, implementing `IFlowElement` with exact,
analytically known behaviour:

| Element | Behaviour |
|---|---|
| `Source` | Emits a declared composition at a declared pressure |
| `Sink` | Accepts everything; records what arrived |
| `Restrictor` | Passes up to a declared rate; imposes a declared pressure drop |
| `Splitter` | Divides a stream by declared fractions |
| `Buffer` | Accumulates up to a capacity; **imposes backpressure when full** |

Every V-test in [04](../design/04_MATERIAL_AND_FLOW.md) §9 is expressible with
these, and every expected answer is computable by hand.

---

## 3. Deliverables

| Project | Contents |
|---|---|
| `OGSim.Flow` | `IFlowElement`, `IFlowNetwork`, `IFlowSolver`, constraint model, attribution, conservation invariant |
| `OGSim.Flow.Tests` | The five synthetic elements; FV1–FV13; randomised property tests; benchmarks |

---

## 4. Verification

The FV1–FV13 suite from [04](../design/04_MATERIAL_AND_FLOW.md) §9, executed
against synthetic elements, plus:

| # | Test | Passes when |
|---|---|---|
| R4-V11 | Randomised conservation | 1,000 randomly generated networks × 100 ticks conserve mass every tick |
| R4-V12 | Attribution exactness | In a network with exactly one binding element, deferred volume equals the analytic answer to tolerance |
| R4-V13 | Multiple simultaneous constraints | Two elements binding at once are both reported, with correctly apportioned deferred volume |
| R4-V14 | Buffer backpressure | Filling a `Buffer` reduces `Source` withdrawal within the same solve, by the analytically expected amount |
| R4-V15 | Availability | Removing an element re-solves around it and attributes the loss to its absence |
| R4-V16 | Shut-in ladder | A deliberately pathological network exhausts the budget; the worst branch is shut in with cause `solver-stability` and audited; the reduced network solves; the tick completes with the deferred volume attributed |
| R4-V16b | Ladder termination | The ladder provably terminates: iterating it on any network reaches a convergent state in at most branch-count steps |
| R4-V16c | Recurrence escalation | The same branch forced shut on consecutive ticks raises `flow.solverFault` at `C` with the full diagnostic |
| R4-V17 | Commit atomicity | A solve that fails the conservation check leaves state byte-identical |
| R4-V18 | Performance | A 500-element network solves within the tick budget; recorded as a benchmark with a regression threshold |
| R4-V19 | Solver purity | Architecture test: the solver assembly contains no material-identity comparison and no type test on element implementations |
| R4-V20 | Per-segment solving (FV11) | A mid-tick availability change produces the exact duration-weighted result, matched to a hand calculation |
| R4-V21 | Segmentation ≠ averaging (FV12) | A case is demonstrated where averaging availability gives a materially different, wrong answer |
| R4-V22 | Multi-segment atomicity (FV13) | A failure in the last segment leaves state byte-identical — nothing commits until all segments solve |

---

## 5. Out of scope

Every real element. R4 ships no reservoir, well, separator, tank or pipeline —
those arrive in R22 and R5–R11, each as an `IFlowElement` implementation, **and none of
them requires a solver change.** If any Arc II phase needs to modify the solver,
that is a signal the `IFlowElement` contract is wrong, and the correct response
is to fix the contract rather than special-case the solver.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| The chosen method converges poorly on some real topology | Discovered here with synthetic networks, before domain work depends on it; `IFlowSolver` is a contract so the method is replaceable |
| Attribution is ambiguous when several constraints bind together | R4-V13 makes the apportionment rule explicit and tested rather than emergent |
| Performance is inadequate at field scale | R4-V18 benchmarks from day one with a regression threshold, so a slow change fails a build rather than being discovered in Arc IV |
| `IFlowElement` proves insufficient for a later element | Prototype the two hardest cases against it now — a **tank** (stateful, backpressure-generating) and a **separator** (one inlet, three outlets, dual capacity). If both fit, the contract is very likely sufficient |
| Floating-point drift accumulates over long runs | Conservation tolerance is relative and asserted every tick, so drift is detected at the tick it appears, not after 400 of them |
