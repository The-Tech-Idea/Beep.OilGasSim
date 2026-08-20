# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**OGSim** is a ground-up oil & gas company simulation engine in C# (.NET 10):
exploration → appraisal → development → production → processing → transport →
export. It is a turn-based engine (one tick = one month) with real-time-with-pause gameplay; the engine is headless and a host renders it.

The repository is **design-first**: `plans/` holds ~90 documents settled before
code and is still authoritative. **The engine now runs.** Seventeen projects under
`src/` compose into a playable field — a world is generated, prospects are
drilled, a chain of surface equipment moves the fluid, a market moves under it, a
bank lends against reserves, equipment wears out and breaks, and two headless
clients play the whole arc through `ReadModel` + `Commands` alone. 1134 tests
across seventeen suites, 0 warnings.

Treat counts in prose as stale until they are re-checked. The project is expected to evolve and some historical notes in this file are intentionally superseded by the live plan and the codebase itself.

The previous engine in this repo's history (`OGGame.Core`, `Game/`, `Documentation/`) is not an input: it is not referenced, ported, or consulted.

## Commands

```bash
dotnet build OGSim.slnx                 # must be 0 warnings — warnings are errors
dotnet test  OGSim.slnx                 # xUnit — the gate, and the only complete answer
dotnet test  OGSim.slnx --no-build --filter "FullyQualifiedName~Money_rounds_half_even"

# While iterating: skip the forty-year runs when needed
# dotnet test tests/OGSim.Composition.Tests --no-build --filter "Speed!=Slow"
```

Requires the .NET 10 SDK (`.slnx` solution format). There is no CI workflow checked into this repo, so the build and test run are the practical gates.

`src/OGSim.Game` is a Godot 4.x / net8.0 project and is deliberately not in `OGSim.slnx`; it builds through Godot rather than through `dotnet build OGSim.slnx`.

The Bash tool here runs Git Bash, not cmd. Use `/dev/null` instead of `> nul`.

## Where the answers live

`plans/` is authoritative and must be read rather than guessed at:

| Need | Read |
|---|---|
| What is built, what is next, what is bypassed | `plans/MASTER_TRACKER.md` |
| Reading order for the design set | `plans/README.md` |
| Layers, laws, modules, and the tick | `plans/design/03_ARCHITECTURE.md` |
| Signatures and algorithms to implement from | `plans/sdd/` and `plans/sdd/SDD_INDEX.md` |
| Coding standards, determinism, and CI gates | `plans/sdd/SDD-000_ENGINEERING_STANDARDS.md` |
| Which contract a function belongs to and its test pin | `plans/design/23_FUNCTION_MATRIX.md` |
| Term definitions and naming law | `plans/design/19_GLOSSARY.md` |
| Meaning of identifiers such as `MB6`, `IR2`, and `FV13` | `plans/design/22_DESIGN_COHERENCE.md` §4 |
| What one phase is for | `plans/phases/R<n>_*.md` |
| Equipment, tiers, and tech gates | `plans/catalog/` |
| Godot host plan and rules | `src/OGSim.Game/plans/` |

Comments in the source cite these by section, for example `// SDD-003 §6.1 — SI Darcy` and `// design 03 §6`. Follow the cited design before changing code.

## Architecture

**Five layers, dependencies strictly downward** (03 §2):
kernel → simulation services → domain modules → composition → host (outside the engine). Assembly boundaries are the enforcement: a layering violation is a missing project reference and a failing architecture test.

The general project layout is:

- `OGSim.Kernel`: primitives, quantities, money, time, identity, RNG, effects, and core utilities
- `OGSim.Contracts`: domain interfaces and public engine surface contracts
- `OGSim.Flow`: solver and flow state
- `OGSim.Subsurface`: subsurface truth models and material-balance logic
- `OGSim.Wells`: completions, inflow/outflow, lift, injectors, and operating points
- `OGSim.Facilities`: separation, gas processing, tanks, manifolds, pipelines, and export
- `OGSim.Operations`: operations and scheduling
- `OGSim.Company`: ledger, fiscal regimes, licenses, and rivals
- `OGSim.Information`: belief store, observations, and prospect risk
- `OGSim.World`: world generation and causal simulation steps
- `OGSim.Capabilities`: technology state and gating
- `OGSim.Integrity`: degradation and bow-tie logic
- `OGSim.Persistence`: save files, canonical JSON, and state blocks
- `OGSim.Objectives`: objective evaluation
- `OGSim.Composition`: layer 4 composition, engine builder, read-model projection, scenario wiring, and production loop
- `OGSim.ReferenceClient`: a headless client outside the engine, which is used to validate the published surface
- `OGSim.Game`: the Godot host, outside the engine and outside the solution

There is no shared `Common` / `Utils` project; if a second module needs a type, it should be a kernel type or a design smell.

**Contract-first, plugin-first.** Replaceable capabilities are interfaces; implementations are plugged in at composition time. Rebalancing is a content edit; new behavior is a new plugin plus the JSON naming it — not an edit to existing engine code.

**Composition is all-or-nothing.** `IModule` declares `Provides`, `Requires`, `OwnsState`, `Stages`, and `Commands`; `ModuleComposer` validates the whole set and either builds the engine or refuses to start, naming every problem.

**The tick is 14 stages in one declared order** (03 §6):
Open → Commands → Environment → Operations → Availability/Hazards/Segmentation → SolveFlow → MaterialBalance → Custody → Economics → HSE → Information → Company → Objectives → Close.

**Commands in, read model out.** The public engine surface is command-driven; a host submits commands and reads the read model produced each tick. The read model is rebuilt from beliefs, not truth, and the engine does not react to events via `Subscribe()`.

**Truth vs belief is structural.** Subsurface truth stays internal to its assembly; beliefs cross through `Observation`, which is the same door used by in-game measurements.

### The kernel type system

`OGSim.Kernel` is designed to make whole classes of defects inexpressible rather than merely tested for:

- **Quantities**: one readonly record struct per dimension, canonical SI inside, factory-per-unit conversion
- **Volume conditions are types**: `ReservoirVolume` and `SurfaceVolume` are not interchangeable without an explicit conversion step
- **Money is a checked scaled integer**: exact cash conservation; no tolerance; overflow throws
- **Time is 30/360**: every month is exactly 30 days
- **Identity is sequential and typed**: `EntityId<T>`, `EntityRef`, `ContentId`, with no `Guid`
- **RNG is eight independent named streams**: no cross-stream coupling
- **Transcendentals are `DetMath`**, not `System.Math`
- **Effects are a sealed four-record vocabulary**

## Rules that will trip you up

These are not style preferences; they are laws with mechanical enforcement in the architecture suite where applicable.

Architecture laws (03 §1):
- **L1** no concrete type is ever a dependency
- **L2** no dependency has a default; no optional params, no `?? new X()`, no singleton, no static mutable state
- **L3** no member exists without behavior; no stubs, no `NotImplementedException`, no constant standing in for work
- **L4** no failure is discarded; `catch` must route through `IFaultPolicy`, rethrow, or record a failure the caller receives
- **L5** one owner per fact; derived values are computed, never mirrored

Determinism (SDD-000 §3):
- all simulation arithmetic is `double`
- no `System.Math` transcendentals in simulation code; use `DetMath`
- no `float`, no `decimal` in simulation
- no LINQ in per-tick paths
- `Dictionary` / `HashSet` may store but must not be enumerated in per-tick logic; prefer `List` / arrays / `SortedDictionary` keyed by `EntityId`
- banned: `DateTime.Now/UtcNow`, `Random`, `Guid.NewGuid`, `Environment.TickCount`, `Stopwatch`
- no parallelism inside a tick, and no `async` in the engine
- `InvariantCulture` on every parse and format

Fidelity (SDD-000 §8):
- **F-1** Every public/internal member of an engine assembly is specified in a merged SDD before implementation
- **F-2** No numeric literal in simulation code except `0` and `1`; constants live in `PhysicalConstants` or content
- **F-3** Every formula cites the SDD section, and the behavior is pinned by an `MX*` or verification test
- **F-4** If implementation shows an SDD is wrong, stop and update the SDD and design before changing code
- **F-5** An amendment edits its block; it does not sit beneath it
- **F-6** Identity is `EntityId<T>`; no per-entity id types

Naming (19 §N1–N7):
- one concept, one name everywhere
- contracts are `I` + the domain noun (`IWell`, not `IWellEntity`)
- no `Manager`, `Helper`, `Util`, `Service`, `Handler`, `Data`, or `Info` in any contract name
- industry terms beat invented terms (`Perforation`, not `ReservoirConnection`)
- a new term enters the glossary before it enters code

Also binding:
- no external packages in engine assemblies
- `InternalsVisibleTo` only to a module's own test assembly, declared in the project file
- no regex sweeps or batch find-and-replace
- no compatibility shims or code kept just for old call sites

## Working conventions

- **A phase's first task is its SDD review** (`Rn.0`): confirm the SDD still matches the design set before writing code.
- Comments explain why and cite the design docs by section.
- Verification-suite IDs appear verbatim in test names.
- Commits follow `R<phase>.<task>: <what> (<tests before> -> <after>)`.
- The Godot host never edits the engine; engine state is read-only from the host, mutations go through commands, and ticks are the only source of time.
- **A stub, fallback, default dependency, or swallowed exception is never the answer.** If a phase appears to need one, that is a design gap; reopen the design document instead of working around it.

Known divergences from the written standards are acceptable to leave alone until the relevant phase lands, but they should not be mistaken for the canonical rule set. Current examples include project-specific platform settings and the fact that the Godot host sits outside the engine solution while still being included in the broader repo source corpus.
