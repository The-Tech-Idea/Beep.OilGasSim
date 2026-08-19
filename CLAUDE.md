# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**OGSim** — a ground-up oil & gas company simulation engine in C# (.NET 10):
exploration → appraisal → development → production → processing → transport →
export. Turn-based engine (one tick = one month), real-time-with-pause game.
The engine is headless; a host renders it.

The repository is **design-first**: `plans/` holds ~100 documents settled before
code, and they are read rather than guessed at. `src/` now holds the built
engine — the kernel, the contract layer, eleven domain modules, Layer 4
composition and a reference client — plus `src/OGSim.Game`, the Godot host.

**The engine composes, advances a tick and plays**: a player drills, waits,
finds oil or doesn't, produces, declines, and wins or goes broke. Current work
is **R20d** — wiring subsystems that are built and tested but bypassed by the
running tick — and R21.5, a host consuming the published surface. Read
`plans/MASTER_TRACKER.md` before assuming anything about what exists: a phase
mark means "built and tested against its SDD", not "used by the running engine".

The previous engine in this repo's history (`OGGame.Core`, `Game/`,
`Documentation/`) is **not an input**: not referenced, ported, or consulted.

## Commands

```bash
dotnet build OGSim.slnx                 # must be 0 warnings — warnings are errors
dotnet test  OGSim.slnx                 # xUnit; ~900 tests across 16 assemblies
dotnet test  tests/OGSim.Wells.Tests/OGSim.Wells.Tests.csproj --no-build
dotnet test  OGSim.slnx --no-build --filter "FullyQualifiedName~Money_rounds_half_even"
dotnet test  tests/OGSim.Architecture.Tests/OGSim.Architecture.Tests.csproj   # the laws
```

Requires the .NET 10 SDK (`.slnx` solution format). There is no CI workflow —
`.github/workflows/` is empty, so the build and the test run are the only gates.

`src/OGSim.Game` is a **Godot 4.7 / net8.0 project and is deliberately not in
`OGSim.slnx`**; it builds through Godot, not `dotnet build OGSim.slnx`.

The Bash tool here runs Git Bash, not cmd. `> nul` creates a literal file named
`nul` in the repo (one already exists from a past slip) — use `/dev/null`.

## Where the answers live

`plans/` is authoritative:

| Need | Read |
|---|---|
| What is built, what is next, what is bypassed | `plans/MASTER_TRACKER.md` — phase status, execution order, open-item register |
| Reading order for the design set | `plans/README.md` |
| Layers, laws, modules, the tick | `plans/design/03_ARCHITECTURE.md` |
| Signatures and algorithms to implement from | `plans/sdd/` (`SDD_INDEX.md` maps phase → SDD) |
| Coding standards, determinism, CI gates | `plans/sdd/SDD-000_ENGINEERING_STANDARDS.md` |
| Which contract a function belongs to, and its test pin | `plans/design/23_FUNCTION_MATRIX.md` |
| Term definitions and naming law | `plans/design/19_GLOSSARY.md` |
| What identifier `MB6` / `IR2` / `FV13` means | `plans/design/22_DESIGN_COHERENCE.md` §4 |
| What one phase is for | `plans/phases/R<n>_*.md` |
| Equipment, tiers, tech gates | `plans/catalog/` |
| The Godot host's own plan and ground rules | `src/OGSim.Game/plans/` |

Comments in the source cite these by section (`// SDD-003 §6.1 — SI Darcy`,
`// design 03 §6`). Follow the citation before changing the code.

## Architecture

**Five layers, dependencies strictly downward** (03 §2):
kernel → simulation services → domain modules → composition → host (outside the
engine). Assembly boundaries *are* the enforcement: a layering violation is a
missing project reference, and `Layering_DependenciesPointDownwardOnly` fails.

| Project | Holds |
|---|---|
| `OGSim.Kernel` | Quantities, Money, 30/360 time, identity, RNG, effects, content loading, the tick pipeline, composition machinery, `DetMath` |
| `OGSim.Contracts` | Every domain interface and the whole `IEngine`/`ReadModel` surface |
| `OGSim.Flow` | The one solver — network, solve state |
| `OGSim.Subsurface` | Compartments, material balance, aquifer, drive mechanisms, water cut — **every type `internal`** |
| `OGSim.Wells` | Completions, inflow/outflow, operating point, lift, injectors |
| `OGSim.Facilities` | Separation, gas processing, tanks, manifolds, pipelines, spec gates, export terminal — transport lives here, not in its own project |
| `OGSim.Operations` | Operations, scheduler, obligations |
| `OGSim.Company` | Ledger, fiscal regimes, licences, rivals |
| `OGSim.Information` | Belief store, observation, prospect risk — the door truth passes through |
| `OGSim.World` | The causal world generator and its steps |
| `OGSim.Capabilities` | Technology state, gating |
| `OGSim.Integrity` | Bow-tie, degradation |
| `OGSim.Persistence` | Canonical JSON, save file, state blocks |
| `OGSim.Objectives` | Objective evaluation |
| `OGSim.Composition` | Layer 4 — **the only project entitled to name a concrete type**: modules, engine builder, read-model projection, the production loop, activities, scenario |
| `OGSim.ReferenceClient` | R21 §2.5 — a host that plays through `ReadModel` + `Commands` and nothing else. A reference to a module here is the defect it exists to catch |
| `OGSim.Game` | The Godot host — outside the engine and outside the solution |

Not every planned project materialised: there is no `Environment`, `Transport`,
`Hse` or `Advisor` assembly. HSE, the field, diagnostics and materials are
modules declared in `Composition/Modules.cs` with no project of their own; R22
(Environment) and R25 (Advisor) are unbuilt. There is **no shared
`Common`/`Utils` project, ever** — a type two modules need is either a kernel
type or a design smell.

**Contract-first, plugin-first.** Every replaceable capability is an interface
listed in 03 §3.2 (`IInflowModel`, `IHydraulicModel`, `IFiscalRegime`,
`IWorldGenerator`, `IFaultPolicy`, …); every implementation is registered at
composition time. Rebalancing is a content edit; new behaviour is a new plugin
plus the JSON naming it — **never an edit to existing engine code**.

**Content is authored JSON** under `content/` (`materials/`, `property-kinds/`,
`rock-types/`, `technologies/`). The loader and grammars exist in the kernel;
the composed engine still carries its parameterisation as explicit `Defaults`
in `EngineBuilder` until R20c.9 loads those files. Those are *values passed
explicitly at the one layer entitled to*, not fallbacks — L2 forbids a
defaulted dependency.

**Composition is all-or-nothing.** `IModule` declares Provides / Requires /
OwnsState / Stages / Commands; `ModuleComposer` validates the whole set, then
either builds the engine or refuses naming *every* problem. There is no
partially-composed engine and no degraded mode. `EngineBuilder.CreateNew`
returns `Built` or a refusal; `tests/OGSim.Composition.Tests/NewGameTests.cs`
is the worked example of starting a game from `WorldParameters` and a seed.

**The tick is 14 stages in one declared order** (03 §6):
Open → Commands → Environment → Operations → Availability/Hazards/Segmentation →
SolveFlow (once per segment) → MaterialBalance → Custody → Economics → HSE →
Information → Company → Objectives → Close. `StageId` in `Kernel/Modules.cs`
pins the numbering. Stage 4 deliberately reads the *previous* tick's solved
values (a defined one-tick lag, not a circular dependency). Only some stages do
real work so far; MASTER_TRACKER says which.

**Commands in, read model out.** `IEngine` (`EngineSurface.cs`) is the entire
public surface: `AdvanceTick`, `ReadModel`, `Commands`, `Events(tick)`, `Audit`,
`World`, `WriteSave`. The read model is rebuilt each tick from **beliefs, never
truth**, and is a copy taken at the close — a host re-reads it after every tick
and never holds one across ticks. Events are outbound-only: there is
deliberately no `Subscribe()`, and engine code cannot react to events. Every
rejection is domain-typed, so a host renders a reason rather than inventing one.

**Truth vs belief is structural.** The subsurface truth model stays `internal`
to `OGSim.Subsurface` — that assembly has no public type at all, and
`Truth_SubsurfaceExposesNoPublicType` asserts exactly that. Beliefs cross
through `Observation`, the same door every in-game measurement uses.

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
- **Transcendentals are `DetMath.cs`**, never `System.Math`.
- **Effects are a sealed four-record vocabulary** (`Effects.cs`) — technology
  and environment speak the same language, and a bare multiplier is not in it.

## Rules that will trip you up

These are laws, not style preferences, and most are **mechanically enforced** by
`tests/OGSim.Architecture.Tests` — rules asserted over compiled metadata
(`MetadataRules.cs`) and over Roslyn syntax (`SourceRules.cs`), chosen per rule
by which instrument is honest for it. `EngineCorpus.cs` defines the corpus:
every referenced engine assembly, and **every `.cs` file under `src/`** outside
`bin`/`obj` — which now includes the Godot host and its addons.

Architecture laws (03 §1): **L1** no concrete type is ever a dependency ·
**L2** no dependency has a default — no optional params, no `?? new X()`, no
singleton, no static mutable state · **L3** no member exists without behaviour —
no stubs, no `NotImplementedException`, no constant standing in for work ·
**L4** no failure is discarded — every `catch` routes through `IFaultPolicy`,
rethrows, or records a `LoadFailure` the caller receives · **L5** one owner per
fact — derived values are computed, never mirrored.

Determinism (SDD-000 §3): all simulation arithmetic is `double` · **no
`System.Math` transcendentals** in simulation code (they route to platform libm
and are not bit-identical — use `DetMath`) · no `float`, no `decimal` in
simulation · no LINQ in per-tick paths · `Dictionary`/`HashSet` may store but
never be enumerated (only `List`/arrays/`SortedDictionary` keyed by `EntityId`) ·
banned: `DateTime.Now/UtcNow`, `Random`, `Guid.NewGuid`, `Environment.TickCount`,
`Stopwatch` · no parallelism inside a tick, and `async` appears nowhere in the
engine · `InvariantCulture` on every parse and format.

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
- **F-5** an amendment edits its block, never sits beneath it · **F-6** identity
  is `EntityId<T>`; no per-entity id types.

Naming (19 §N1–N7): one concept, one name, everywhere · contracts are `I` + the
domain noun (`IWell`, not `IWellEntity`) · **no `Manager`, `Helper`, `Util`,
`Service`, `Handler`, `Data`, `Info` in any contract name** · industry terms beat
invented ones (`Perforation`, not `ReservoirConnection`) · a new term enters the
glossary before it enters code.

Also binding: **no external packages in engine assemblies** — kernel through
composition reference only the BCL; test assemblies may take packages (the
architecture suite takes Roslyn) · `InternalsVisibleTo` only to a module's own
test assembly, declared in the `.csproj` · **no regex sweeps, no batch
find-and-replace, ever** · no compatibility shims or "kept so old call sites
work".

## Working conventions

- **A phase's first task is its SDD review** (`Rn.0`) — confirm the SDD still
  matches the design set before writing code.
- Comments explain **why** and cite design docs by section. No phase tags in
  prose about chronology — git history owns that.
- Verification-suite IDs appear verbatim in test names:
  `FV5_the_separators_set_point_is_what_the_well_flows_against`,
  `R20V4_a_well_can_be_shut_in_and_stops_producing`. Tests pinning a behaviour
  rather than a suite ID are named as sentences.
- Commits: `R<phase>.<task>: <what> (<tests before> -> <after>)`, e.g.
  `R20d.8.8: the gathering line exists (906 -> 907)`. One task, one commit,
  revertable — plus a `docs:` commit when a `MASTER_TRACKER.md` row ticks.
- The Godot host never edits the engine: from `src/OGSim.Game`, engine state is
  read-only, mutations go through `Commands.Submit`, and ticks are the only
  source of time (`src/OGSim.Game/plans/README.md`).
- **A stub, fallback, default dependency or swallowed exception is never the
  answer.** If a phase appears to need one, that is a design gap — reopen the
  design document rather than working around it.

Known divergences from the written standards, all fine to close when the
relevant phase lands: `Directory.Build.props` carries the platform settings
(`net10.0`, nullable, `TreatWarningsAsErrors`) but each `.csproj` also
re-declares its TFM; the test projects use xUnit v2 where SDD-000 §1 specifies
xUnit v3 + FsCheck + BenchmarkDotNet; and moving the Godot host under `src/`
put non-engine C# inside the architecture suite's source corpus.
