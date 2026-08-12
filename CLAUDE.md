# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**OGSim** — a ground-up oil & gas company simulation engine in C# (.NET 10):
exploration → appraisal → development → production → processing → transport →
export. Turn-based engine (one tick = one month), real-time-with-pause game.
The engine is headless; a host renders it.

The repository is **design-first**: `plans/` holds ~90 documents settled before
code and is still authoritative. **The engine now runs.** Sixteen projects under
`src/` compose into a playable field — a world is generated, prospects are
drilled, a chain of surface equipment moves the fluid, a market moves under it, a
bank lends against reserves, equipment wears out and breaks, and two headless
clients play the whole arc through `ReadModel` + `Commands` alone. Around 965
tests across fifteen suites, 0 warnings.

`plans/MASTER_TRACKER.md` is the only reliable statement of what is built and
what is next — it is updated with every task and this file is not.

The previous engine in this repo's history (`OGGame.Core`, `Game/`,
`Documentation/`) is **not an input**: not referenced, ported, or consulted.

## Commands

```bash
dotnet build OGSim.slnx                 # must be 0 warnings — warnings are errors
dotnet test  OGSim.slnx                 # xUnit
dotnet test  OGSim.slnx --no-build --filter "FullyQualifiedName~Money_rounds_half_even"
```

Requires the .NET 10 SDK (`.slnx` solution format).

The Bash tool here runs Git Bash, not cmd. `> nul` creates a literal file named
`nul` in the repo (one already exists from a past slip) — use `/dev/null`.

## Where the answers live

`plans/` is authoritative and is read, not guessed at:

| Need | Read |
|---|---|
| What is built, what is next | `plans/MASTER_TRACKER.md` — phase status and execution order |
| Reading order for the design set | `plans/README.md` |
| Layers, laws, modules, the tick | `plans/design/03_ARCHITECTURE.md` |
| Signatures and algorithms to implement from | `plans/sdd/` (`SDD_INDEX.md` maps phase → SDD) |
| Coding standards, determinism, CI gates | `plans/sdd/SDD-000_ENGINEERING_STANDARDS.md` |
| Term definitions and naming law | `plans/design/19_GLOSSARY.md` |
| What identifier `MB6` / `IR2` / `FV13` means | `plans/design/22_DESIGN_COHERENCE.md` §4 |
| Equipment, tiers, tech gates | `plans/catalog/` |

Comments in the source cite these by section (`// SDD-003 §6.1 — SI Darcy`,
`// design 03 §6`). Follow the citation before changing the code.

## Architecture

**Five layers, dependencies strictly downward** (03 §2):
kernel → simulation services → domain modules → composition → host (outside the
engine). Assembly boundaries *are* the enforcement: a layering violation is a
missing project reference, not a review comment.

Projects that exist: `OGSim.Kernel`, `OGSim.Contracts`, `Subsurface`, `Wells`,
`Facilities`, `Flow`, `Information`, `Company`, `Operations`, `World`,
`Capabilities`, `Integrity`, `Objectives`, `Persistence`, `Composition`, and
`ReferenceClient` (a headless client, outside the engine — it holds no module
reference and an architecture test says so). Still unbuilt from 03 §8:
`Environment`, `Transport`, `Hse`, `Advisor`. There is **no shared
`Common`/`Utils` project, ever** — a type two modules need is either a kernel
type or a design smell.

**Contract-first, plugin-first.** Every replaceable capability is an interface
listed in 03 §3.2 (`IInflowModel`, `IHydraulicModel`, `IFiscalRegime`,
`IWorldGenerator`, `IFaultPolicy`, …); every implementation is registered at
composition time. Every domain object is instantiated from a JSON definition
binding onto a contract. Rebalancing is a content edit; new behaviour is a new
plugin plus the JSON naming it — **never an edit to existing engine code**.

**Composition is all-or-nothing.** `IModule` declares Provides / Requires /
OwnsState / Stages / Commands; `IModuleRegistry` validates the whole set, then
either builds the engine or refuses to start naming *every* unmet requirement.
There is no partially-composed engine and no degraded mode.

**The tick is 14 stages in one declared order** (03 §6):
Open → Commands → Environment → Operations → Availability/Hazards/Segmentation →
SolveFlow (once per segment) → MaterialBalance → Custody → Economics → HSE →
Information → Company → Objectives → Close. `StageId` in `Modules.cs` pins the
numbering. Stage 4 deliberately reads the *previous* tick's solved values (a
defined one-tick lag, not a circular dependency).

**Commands in, read model out.** `IEngine` (`EngineSurface.cs`) is the entire
public surface: `AdvanceTick`, `ReadModel`, `Commands`, `Events(tick)`, `Audit`,
`World`, `WriteSave`. The read model is rebuilt each tick from **beliefs, never
truth**. Events are outbound-only — there is deliberately no `Subscribe()`;
engine code cannot react to events.

**Truth vs belief is structural.** The subsurface truth model stays `internal`
to `OGSim.Information`; nothing else can reach it. Initial world beliefs cross
through `Observation` — the same door every in-game measurement uses.

### The kernel type system

`OGSim.Kernel` makes whole defect classes inexpressible rather than tested-for:

- **Quantities** (`Quantities.cs`) — one `readonly record struct` per dimension,
  canonical SI inside, factory-per-unit. Cross-dimension arithmetic does not
  exist; legal products/quotients are declared operators. There is no
  `Pressure * Pressure`.
- **Volume conditions are types** (`Volumes.cs`) — `ReservoirVolume` +
  `SurfaceVolume` is a compile error; conversion requires a
  `FormationVolumeFactor` in hand. Gas has its own bridge
  (`GasFormationVolumeFactor` → `StandardGasVolume`), never stock-tank.
- **Money is a checked scaled integer** (`Money.cs`) — cash conservation is
  exact with no tolerance; overflow throws. Exactly one double→Money rule:
  half-even, once, at the ledger boundary.
- **Time is 30/360** (`Time.cs`) — every month is exactly 30 days, so the
  /30ths segment grid is exact for every tick. Labels stay real; leap years
  do not exist.
- **Identity is sequential and typed** (`Identity.cs`) — `EntityId<T>`,
  `EntityRef`, `ContentId` (charset-validated kebab-case). No `Guid`.
- **RNG is eight independent named streams** (`Random.cs`) — adding a draw in
  one can never shift another.
- **Effects are a sealed four-record vocabulary** (`Effects.cs`) — technology
  and environment speak the same language, and a bare multiplier is not in it.

## Rules that will trip you up

These are not style preferences; each is a law. **`tests/OGSim.Architecture.Tests`
enforces most of them by reflection and source scan** — 24 tests covering L1–L4,
layering (both directions, including the read model), naming N3, determinism
D2/D3/D5/D6/D7/D8, F2's citations, F6's identity rule, the subsurface truth
boundary and the event bus's missing `Subscribe`. Breaking one of those fails the
build, so read the failure rather than working around it.

**What genuinely holds by hand is L5 (one owner per fact) and the F-1/F-3/F-4
process rules** — no test can tell that a value was mirrored rather than derived,
or that a formula reached the code before its SDD did.

Architecture laws (03 §1): **L1** no concrete type is ever a dependency ·
**L2** no dependency has a default — no optional params, no `?? new X()`, no
singleton, no static mutable state · **L3** no member exists without behaviour —
no stubs, no `NotImplementedException`, no constant standing in for work ·
**L4** no failure is discarded — every `catch` routes through `IFaultPolicy` ·
**L5** one owner per fact — derived values are computed, never mirrored.

Determinism (SDD-000 §3): all simulation arithmetic is `double` · **no
`System.Math` transcendentals** in simulation code (they route to platform libm
and are not bit-identical — the kernel will ship `DetMath`) · no `float`, no
`decimal` in simulation · no LINQ in per-tick paths · `Dictionary`/`HashSet`
may store but never be enumerated (only `List`/arrays/`SortedDictionary` keyed
by `EntityId`) · banned: `DateTime.Now/UtcNow`, `Random`, `Guid.NewGuid`,
`Environment.TickCount`, `Stopwatch` · no parallelism inside a tick, and `async`
appears nowhere in the engine · `InvariantCulture` everywhere.

Fidelity (SDD-000 §8) — the anti-hallucination rules:

- **F-1** Every public/internal member of an engine assembly is specified in a
  merged SDD *before* it is implemented. Unspecified member → update the SDD
  first, as its own reviewed change.
- **F-2** No numeric literal in simulation code except 0 and 1. Constants live
  in `PhysicalConstants` (`Quantities.cs`) with their SDD citation and unit, or
  come from content.
- **F-3** Every formula cites the SDD section stating its form and is pinned by
  an `MX*` test against an independently computed value.
- **F-4** **If implementation shows an SDD is wrong, stop.** Update the SDD (and
  the design doc if the conflict reaches it), re-review, then code. Do not "fix
  it in the code."

Naming (19 §N1–N7): one concept, one name, everywhere · contracts are `I` + the
domain noun (`IWell`, not `IWellEntity`) · **no `Manager`, `Helper`, `Util`,
`Service`, `Handler`, `Data`, `Info` in any contract name** · industry terms beat
invented ones (`Perforation`, not `ReservoirConnection`) · a new term enters the
glossary before it enters code.

Also binding: **no external packages in engine assemblies** — kernel through
composition reference only the BCL · `InternalsVisibleTo` only to a module's own
test assembly · **no regex sweeps, no batch find-and-replace, ever** ·
no compatibility shims or "kept so old call sites work".

## Working conventions

- **A phase's first task is its SDD review** (`Rn.0`) — confirm the SDD still
  matches the design set before writing code.
- Comments explain **why** and cite design docs by section. No phase tags — git
  history owns chronology.
- Verification-suite IDs appear verbatim in test names:
  `FV5_BackpressureReachesReservoir`, `R6V14_CommonLineBackpressureShutsWeakWell`.
- Commits: `R<phase>.<task>: <what> (<tests before> -> <after>)`, e.g.
  `R1.4: seeded per-subsystem RNG streams (41 -> 55)`. One task, one commit,
  revertable — plus a docs commit when a `MASTER_TRACKER.md` row ticks.
- **A stub, fallback, default dependency or swallowed exception is never the
  answer.** If a phase appears to need one, that is a design gap — reopen the
  design document rather than working around it.

Two known divergences from the written standards, both fine to close when the
relevant phase lands: `Directory.Build.props` carries the platform settings
(`net10.0`, nullable, `TreatWarningsAsErrors`) but each `.csproj` also
re-declares its TFM; and the test project uses xUnit v2 where SDD-000 §1
specifies xUnit v3 + FsCheck + BenchmarkDotNet.
