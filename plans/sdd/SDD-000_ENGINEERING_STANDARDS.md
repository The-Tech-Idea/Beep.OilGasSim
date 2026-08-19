# SDD-000 — Engineering Standards

**Status:** drafted · **Serves:** every phase · **Design docs:** [03](../design/03_ARCHITECTURE.md), [11](../design/11_PERSISTENCE.md), [12](../design/12_VERIFICATION.md)

The decisions every line of code inherits. Settled first because changing any of
them after Arc I is a sweep — and this project's history with sweeps is the
reason half the architecture laws exist.

---

## 1. Platform

| Decision | Choice | Rationale |
|---|---|---|
| Language | C# | Team continuity; the host ecosystem (Godot .NET or otherwise) |
| Framework | **.NET 10 (LTS)** | Current LTS as of 2026. The old engine's net8.0 is not a constraint — nothing is ported |
| C# version | Latest for the SDK | `readonly record struct`, collection expressions, required members all carry weight here |
| Nullability | `<Nullable>enable</Nullable>` everywhere, no exceptions | A `null` crossing a contract is a design error |
| Warnings | `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` everywhere | Non-negotiable from commit one |
| External packages in engine assemblies | **None.** Kernel through Composition reference only the BCL | Every dependency is a determinism and longevity risk; the engine needs math, collections and JSON — all BCL |
| Test packages | xUnit v3 + FsCheck (property tests) + BenchmarkDotNet | Test assemblies only |

## 2. Solution layout

Exactly the structure of [03](../design/03_ARCHITECTURE.md) §8, rooted at
`OGSim/`:

```
OGSim/
├── OGSim.slnx
├── src/            ← the 15 engine projects (Kernel … Composition, Advisor)
├── content/        ← the one canonical content directory
├── plans/          ← this design workspace
└── tests/          ← Architecture / Unit / Model / Integration / Scenario / Determinism
```

- **Assembly = module boundary.** Enforcement by project reference, per 03 §8.
- `InternalsVisibleTo` is granted **only** to the module's own test assembly —
  never across modules, never to the host. The one deliberate use: keeping the
  truth model `internal` to `OGSim.Information`.
- No shared `Common`/`Utils` project, ever. A type two modules need is either a
  kernel type or a design smell.

## 3. Determinism standards (the hard part)

[11](../design/11_PERSISTENCE.md) §3 demands byte-identical state across
platforms. Concretely:

| Rule | Detail |
|---|---|
| D-1 | All simulation arithmetic is `double`. IEEE-754 basic ops (`+ - * /`, comparisons) are exact and portable |
| D-2 | **No `System.Math` transcendentals in simulation code.** `Math.Pow/Exp/Log/Sqrt` route to platform libm and are *not* guaranteed bit-identical across OS/architecture. The kernel ships **`DetMath`** — software implementations (polynomial/rational, `Math.Sqrt` excepted: IEEE-correctly-rounded and safe) — and an architecture test bans `System.Math` outside `DetMath` |
| D-3 | No `float`. No `decimal` in simulation (money uses a scaled `long` — see SDD-001 §8) |
| D-4 | No LINQ in per-tick paths (allocation + ordering opacity); explicit loops over ordered collections |
| D-5 | Iteration order: only `List<T>`, arrays, or `SortedDictionary` keyed by `EntityId` are enumerable in simulation code. `Dictionary`/`HashSet` may store, never enumerate — an analyzer enforces this |
| D-6 | Banned symbols (architecture-tested, per [12](../design/12_VERIFICATION.md) §2): `DateTime.Now/UtcNow`, `Random`, `Random.Shared`, `Guid.NewGuid`, `Environment.TickCount`, `Stopwatch` in simulation assemblies |
| D-7 | Parallelism: none inside a tick (AD5/AD6). `async` appears nowhere in the engine |
| D-8 | Culture: all parsing/formatting `InvariantCulture`; analyzer bans culture-sensitive overloads |

## 4. Naming and style

- [19_GLOSSARY](../design/19_GLOSSARY.md) rules N1–N7 are compiler-adjacent law
  here; N3 (`no Manager/Helper/Service/Util/Handler/Data/Info in contract
  names`) is enforced by the architecture test suite.
- File = type; folder = concept-matrix section where sensible.
- Comments explain **why**, cite design docs by section
  (`// 04 §4.0b: the shut-in ladder — zero rate is always convergent`), and
  carry no phase tags — the git history owns chronology.
- **No regex sweeps, no batch find-and-replace, ever.** Inherited from the
  project's standing instruction and earned the hard way.

## 5. Testing standards

| Kind | Project | Convention |
|---|---|---|
| Architecture | `OGSim.Architecture.Tests` | Reflection over compiled assemblies + Roslyn analyzers for source-level rules (D-2, D-5, D-8). Test names cite the law: `L2_NoStaticMutableState` |
| Unit | per-module `*.Tests` | Arrange-act-assert, no mocks of physics (12 §7) — fakes are real simple models |
| Model | `OGSim.Model.Tests` | Exact tests (`MX*`) assert to `1e-9` relative; band tests (`MB*`) carry the band and its citation in the test source |
| Property | inside Model/Unit | FsCheck for conservation, round-trip, mix/split invariants |
| Scenario | `OGSim.Scenario.Tests` | `SC*` — each scenario is a content file plus a script of commands, run through the composition surface only |
| Determinism | `OGSim.Determinism.Tests` | State digest = SHA-256 over canonical per-module serialization; compared across runs in CI matrix (windows-x64, linux-x64, linux-arm64) |
| Naming | — | `<SuiteId>_<Behavior>`: `FV5_BackpressureReachesReservoir`, `R6V14_CommonLineBackpressureShutsWeakWell` |

Verification-suite IDs from the design docs appear **verbatim** in test names —
that is what makes [22](../design/22_DESIGN_COHERENCE.md) §6.1's "every suite
exists" check mechanical against the codebase.

## 6. CI gates (every PR)

1. Build, warnings-as-errors, all TFMs
2. Architecture tests (fast — they run first and fail loudest)
3. Unit + model + integration + property
4. Scenario suite
5. Determinism digest across the OS matrix
6. Coherence checks over `plans/` ([22](../design/22_DESIGN_COHERENCE.md) §6.1, scripted — R20.12 pulled forward: the script exists from day one, checking the documents until there is code to check too)
7. Benchmarks on `main` only, with regression thresholds ([15](../design/15_TIME_AND_EXECUTION.md) §10 budgets)

## 7. Commits

`R<phase>.<task>: <what> (<tests before> -> <after>)`, e.g.
`R1.4: seeded per-subsystem RNG streams (41 -> 55)` — plus a docs commit when a
tracker row ticks. One task, one commit, revertable.

## 8. Implementation fidelity — the anti-hallucination rules

The SDD layer only prevents invented code if deviation from it is *mechanically
awkward*. Four binding rules:

| # | Rule | Enforcement |
|---|---|---|
| F-1 | **Every public or internal member of an engine assembly is specified in a merged SDD before it is implemented.** A PR introducing an unspecified member is rejected — the fix is an SDD update first, reviewed on its own | PR template requires the SDD citation; reviewer checks it. Open item S000-4 explores automating the member-vs-SDD diff |
| F-2 | **No numeric literal in simulation code except 0, 1, and values imported from `PhysicalConstants` or content.** `PhysicalConstants` is one kernel file where every constant carries its SDD citation and unit (`// SDD-003 §6.3: r_c default 0.55, dimensionless`). **Scope: formula-bearing code only** — the domain modules implementing [05](../design/05_SIMULATION_MODELS.md)'s models, from R5 onward. **The kernel is not in scope**, and that was settled empirically at R1.12: applied literally to `OGSim.Kernel` the rule produced 38 violations, *all* false positives — enum member values, `Year * 12`, `vertices.Length < 3`, the shoelace `0.5`. None is a physical claim. A check that is wrong every time it fires gets disabled, not tightened. `DetMath`'s coefficients and the PCG64 multiplier are excluded for the same reason at a different level: they are algorithm definitions, not tunable parameters. What F-2 enforces *everywhere, today* is its positive half — every `PhysicalConstants` entry carries its SDD citation and unit | Citation half: architecture-tested from R1.12. Literal scan: Roslyn analyzer over model assemblies, from R5 ([R1](../phases/R1_KERNEL.md) §5b) |
| F-3 | **Every formula implementation cites the SDD section stating its form** (`// SDD-003 §6.1 — SI Darcy`), and an MX test pins it to an independently computed value. A formula without a pinned test is unreviewable and does not merge | Review + the MX suite |
| F-4 | **If implementation shows an SDD is wrong, stop.** Update the SDD (and the design doc if the conflict reaches it — [22](../design/22_DESIGN_COHERENCE.md) §6 rule 6), re-review, then code. The alternative — "fixing it in the code" — is how a design set and its codebase divorce | Process; deviations found later are coherence-log findings |

| F-5 | **An amendment EDITS the block it corrects; it never sits beneath it.** A correction appended as a note leaves two contradictory statements in one document with nothing saying which wins, and the reader implements whichever they reach first. Write the correction into the declaration, and keep the *reasoning* as the note | Review; the pass-10 audit found five instances (see below) |
| F-6 | **Identity is `EntityId<T>` over a marker interface. There are no per-entity id types.** `CompartmentId`, `PerforationId`, `OperationId` were each invented in an SDD, used across sections, and declared nowhere — a second identity scheme that no code ever had | Architecture test: no public **non-enum** type name ends in `Id` except `EntityId<T>`, `ContentId`, `TechnologyId`, `AuditId`, `EventId`, `MaterialId`, `PortId`. Enums are exempt by kind — membership fixed at compile time cannot be an identity scheme, which is what `StageId` and `StreamId` are (R2 — the test's first run named both) |

These six are the practical answer to "how do we avoid code hallucinations":
the coder is never the author of a contract, a constant, or a formula — only of
their implementation, against a pinned test.

**F-5 and F-6 were added at contract pass 10**, after a full SDD-versus-code
audit found each defect recurring across independent documents rather than once:

| Pattern | Where |
|---|---|
| Amendment beneath the block it corrects, block never edited | SDD-002 §6 (`FlowNetwork`/`FlowTopology`), SDD-004 §5 (`LoadResult`/`ContentLoadResult`), SDD-005 §3 (`EnvelopeContext`/`IEffectState`), SDD-010 §4 (two amendments disagreeing in one section), SDD-017 §1b (SDD-010's amendment never propagated across documents) |
| Phantom `XxxId` identity scheme | SDD-003 ×2, SDD-007 |

A third, subtler one is worth naming without a rule: **a claim that asserts its
own correctness stops anyone checking it.** SDD-017 §2 said "the exact
16-section ⇔ R21 §2.4b correspondence (V11)" above a record listing fourteen
members and omitting `FinanceView`. The note is what made the mismatch
invisible.

## 9. Open items

| # | Item | Trigger |
|---|---|---|
| S000-1 | Exact `DetMath` function list and required accuracy (correlations in [05](../design/05_SIMULATION_MODELS.md) need `exp`, `ln`, `pow`, and little else) | SDD-001 review |
| S000-2 | ~~Analyzer implementation: Roslyn vs reflection~~ **Resolved at R1.12: both, chosen per rule.** Reflection where the rule is about shape (a static field, an optional parameter, an assembly reference); Roslyn syntax where the rule is about what was written and has no metadata form (a `catch` clause, an `await`, a numeric literal — a compiled catch is indistinguishable from any other branch, and an `await` has been rewritten into a state machine). Neither is used where the other is the honest instrument | ✅ |
| S000-4 | Automating F-1: reflection dump of public/internal members diffed against an SDD-maintained manifest | R1.12 — start manual, script when the surface stabilises |
| S000-3 | Godot-host compatibility pin (if the old repo's client is reused, its SDK must load net10 assemblies — else the host targets netstandard-compatible surface) | Owner decision at R21 |
