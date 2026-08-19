# SDD-005 — Capabilities, Effects and Gating

**Status:** drafted · **Serves:** R17, R22 (shared path), and every phase that validates a gated command · **Design docs:** [07](../design/07_TECHNOLOGY.md) §1–§4b, [13](../design/13_ENVIRONMENT.md) §2.1, [06](../design/06_WORLD_AND_EXPLORATION.md) §2.3, [catalog/TECH_TREE](../catalog/TECH_TREE.md)

The cross-cutting system three design passes built: what a company *can do*
(capabilities), how technology and environment *change the world's parameters*
(effects, one shared path), and how every gated command is validated. Pinned
now because five phases consume it before R17 builds the real capability state.

---

## 1. Scope

Contracts in `OGSim.Contracts`; the capability state lives in `OGSim.Company`
(R17); the effect-application path lives in `OGSim.Kernel` (it is
domain-neutral); consumers everywhere via command validators.

## 2. Capabilities

```csharp
public readonly record struct TechnologyId(ContentId Value);   // a `tech` content id

public interface ICapabilitySet
{
    bool Has(TechnologyId tech);
    DetectClass MaxDetectClass { get; }        // D0..D3, derived from held observation nodes (06 §2.3)
    // NOTHING else. Tiers/envelopes are NOT queried here — they are content
    // gated BY these answers. Keeping this interface two members wide is what
    // keeps gating explainable.
}
```

**Implementations:**

| Impl | Behaviour | Status |
|---|---|---|
| `TechnologyState` (R17) | The company's acquired nodes; era-filtered; per-company | The real one |
| `AllCapabilities` | `Has` ⇒ true; `MaxDetectClass` ⇒ D3 | **A shipped mode** — the sandbox all-tech modifier ([18](../design/18_GAME_MODES.md) §5) and the composition every pre-R17 phase runs under (R17 §2.6c). Complete, tested, not scaffolding |

**Diffusion timing** (07 §3's free route): a node auto-grants at
`era start + diffusionLag(node)` — content, deterministic, no draws; the same
mechanism rivals use with their personality lag (SDD-011 §2.1). "Everything
eventually becomes standard practice" is a date, not an event.

> **R20d.10b amendment. The era gate is a CALENDAR check and is not part of
> `Requirements`.**
>
> §2's `Requirements` block carries `Tech`, `MinDetectClass` and `Envelopes` —
> and no era. That is correct and worth stating, because the obvious reading is
> that `IGatingValidator` should answer the whole question and it cannot: a
> requirement is something a company can go and GET, and every `MissingItem` the
> validator returns names an action — acquire this node, rent it, get a bigger
> rig. **An era has not arrived yet and no amount of spending changes that**, so
> folding it into the validator would produce a "missing item" with no remedy and
> teach a player that a refusal is a shopping list when this one is a date.
>
> So an equipment tier is purchasable when **both** hold, checked in two places
> because they are two kinds of fact:
>
> - its `availableFromEra` has begun — a comparison against the calendar of the
>   amendment below, made where the purchase is refused;
> - its `requiresTech` is held or rented — `Requirements.Tech` through the one
>   validator, as §2 already specifies.
>
> **The refusal names the era and the year**, per R17 §2.6b's rule that a domain
> reason renders straight to the player: *this equipment is not invented yet, and
> here is when it will be* is actionable — a player waits, or plans around it —
> where *requirements not met* is not.

> **R20d.10 amendment. What "era start" IS, and why the era is DERIVED.**
>
> §2 above prices diffusion at `era start + diffusionLag(node)` and no document
> says when an era starts, so the one input the formula needs had no source.
> [TECH_TREE](../catalog/TECH_TREE.md) states it and always has — *Eras: E1
> 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+* — so the boundaries are
> **1950, 1970, 1990, 2010**, transcribed from the registry rather than chosen
> here. They are a calendar, passed as a dependency like every other content
> table; a game that starts in 1965 begins in E1 and reaches E3 inside a
> forty-year run.
>
> **The era is a FUNCTION OF THE DATE and is therefore not state.** It was a
> stored field on `CapabilityState`, captured into the save and set only by the
> constructor and by `Restore` — which is law L5's mirrored derived value, and it
> is why a 1965→2005 campaign stayed in E1 for forty years: nothing ever wrote
> it, and nothing could, because there was no calendar to write from.
>
> This section's own note said the era is captured "because `Acquire` checks it:
> replaying a late-era technology against a restored early era would refuse a save
> that was legitimate when written." **Deriving it removes that hazard rather than
> creating it** — a save restores at the tick it was taken at, so the derived era
> is the era that authorised the acquisition, by construction. The stored copy was
> protecting against a divergence only the stored copy could produce.
>
> **Diffusion is a per-tick pass, and it is what makes the calendar visible.**
> Nothing called `ApplyDiffusion`, so the third acquisition route existed for its
> unit tests alone. It runs with the era and its start tick, and grants what the
> registry's Routes column marks **D** once the lag has elapsed — no draws, and
> the same date in every game with the same start.
>
> **It runs at `StageId.Company` (11), NOT stage 2 — corrected here after
> shipping it at the wrong one.** This amendment first placed `DiffusionStage`
> beside weather at stage 2, reasoning by analogy — "the world does this to the
> company" — without checking it against §4.2 two sections below, which already
> states the correct answer and the reason for it: *"applied when acquisition
> completes (a stage-11 state change), taking effect next tick — technology
> never creates a segment boundary (R17 §2.7)."* At stage 2 a node diffusing
> this month would be visible to stage 4's segmentation THIS SAME month; at
> stage 11, after the solve has already run on last month's holdings, a newly
> diffused node is genuinely a NEXT-tick fact, which is what "next tick" in
> §4.2 has always meant. Currently unobservable — nothing yet reads
> `EffectiveEnvelope` from inside a tick — which is exactly why it went
> uncaught rather than evidence it did not matter: the first envelope-reading
> consumer would have inherited the wrong timing silently.
>
> **The scheduler is told what the company actually holds.** It took
> `availableCapabilities: []` at both call sites: a hardcoded empty list standing
> in for `TechnologyState.Acquired`, so `Requirements.RequiredCapabilities` could
> neither refuse nor permit anything. No shipped template declares a requirement
> today, so this changes no refusal now and is the difference between a gate that
> is open and a gate that is not connected.

> **R20c.9 review corrections (findings 128, 129).** Writing the registry as
> loadable content found the `tech` kind unspecified and diffusion ignoring the
> one column that limits it.
>
> - **The `tech` content kind was never declared** (finding 129). §2 above says
>   `TechnologyId` wraps "a `tech` content id" and no SDD states what a `tech`
>   entry contains, so `TechnologyState` was constructed from a
>   `TechnologyNode` that only tests could build. The shape, mapping one-to-one
>   onto [TECH_TREE](../catalog/TECH_TREE.md)'s registry columns:
>
>   ```csharp
>   public enum AcquisitionRoute { Research, Licence, ServiceRental, Diffusion }
>
>   public sealed record TechnologyDefinition(
>       ContentId Id,                                  // slug of the display name (SDD-004 §8)
>       Era AvailableFrom,                             // the registry's Era column
>       int DiffusionLagTicks,                         // months after era start
>       IReadOnlyList<ContentId> Prerequisites,        // the Prereqs column
>       IReadOnlyList<AcquisitionRoute> Routes,        // the Routes column, R L S D
>       DetectClass? GrantsDetectClass,                // the Opens column, where it opens a D-class
>       IReadOnlyList<Effect> Effects) : ContentDefinition(Id);
>   ```
>
>   `Effects` is empty for most nodes and that is correct rather than missing:
>   the registry's `Opens` column is overwhelmingly *gating* — a tier or
>   activity becomes purchasable — which content expresses as a `requiresTech`
>   on the equipment, not as an effect on the node. A node carries an effect
>   only where it changes a number nobody bought.
>
> - **Diffusion granted nodes that have no diffusion route** (finding 128).
>   `ApplyDiffusion` granted everything whose era had started and whose lag had
>   elapsed, but the registry's Routes column lists **D** for only some nodes:
>   Horizontal is `R L S`, hydraulic fracturing is `R L S`. Every such node was
>   being handed to the player free, on a timer, which erases the difference
>   between "eventually standard" and "you must go and get this" — the whole
>   point of having four routes. Diffusion now requires
>   `Routes.Contains(AcquisitionRoute.Diffusion)`. It was invisible while the
>   graph was only ever built by tests, because no test fixture carried a route
>   list to contradict.

**Rentals** (07 §4b.4): a rental is **not** a capability-set change. It is a
field on the *operation*:

```csharp
public sealed record ServiceRental(TechnologyId Capability, Money Premium);
// carried by the IOperation; the gating validator accepts
// caps.Has(t) OR operation.Rentals.Contains(t). Scope = that operation only,
// which is why rentals never touch ICapabilitySet or persistence.
```

## 3. Requirements and the one gating validator

> **Pass-2 amendment (finding 63):** `Check` takes a fourth argument,
> `IEffectState effects` — envelope checks compare against *effective* values,
> which only the effect state can supply. `IEffectState` (§4.2) is declared in
> the Kernel: `EffectiveEnvelope(kind)`, `SelectedPlugin(slot)`, `Parameter(slot, key)`.


Every gated thing — operation template, equipment tier, well command — declares
a `Requirements` block in content, and **exactly one** validator evaluates it:

```csharp
public sealed record EnvelopeCheck(EnvelopeKind Kind, double RequiredValue);

public sealed record Requirements(
    IReadOnlyList<TechnologyId> Tech,            // all must be held or rented
    DetectClass? MinDetectClass,                 // surveys: spawn threshold (06 §2.3)
    IReadOnlyList<EnvelopeCheck> Envelopes);     // e.g. rig depth >= well TD (§4)

// MissingItem names the SPECIFIC tech, tier or envelope — the domain reason of
// R17 §2.6b, renderable straight to the player rather than "requirements not met".
public abstract record MissingItem;
public sealed record MissingTechnology(TechnologyId Tech) : MissingItem;
public sealed record MissingDetectTier(DetectClass Required, DetectClass Held) : MissingItem;
public sealed record EnvelopeExceeded(EnvelopeKind Kind, double Required, double Effective) : MissingItem;

// ALL misses are reported, never just the first (the R3-V2 principle).
public abstract record GateResult;
public sealed record GatePass : GateResult;
public sealed record GateFail(IReadOnlyList<MissingItem> Missing) : GateResult;

public interface IGatingValidator
{
    GateResult Check(
        Requirements requirements,
        ICapabilitySet capabilities,
        IReadOnlyList<ServiceRental> rentals,
        IEffectState effects);                   // §4.2 — envelopes compare EFFECTIVE values
}
```

> **Contract pass 10 — two corrections here, one of them architectural.**
>
> - **`public static class Gating` → `interface IGatingValidator`.** A static
>   class cannot be supplied at construction, so nothing could substitute it and
>   nothing could see that a module depended on it — law L1 says a collaborator
>   is an interface handed over, and L2 says omitting one must not compile. A
>   static gate satisfies neither. It would also have put the "exactly one
>   validator" rule beyond the reach of the architecture test that now checks it.
> - **`in EnvelopeContext ctx` → `IEffectState effects`.** `EnvelopeContext`
>   existed in no SDD and no code; the pass-2 amendment directly above this block
>   already said `IEffectState`, and the block was never updated to match — the
>   same amendment-versus-block drift found in SDD-002 §6 and SDD-004 §5.
> - `EnvelopeCheck`, the `MissingItem` family and the `GateResult` family were
>   all referenced by this signature and declared nowhere.

**Two timing rules, both architectural:**
1. Gating runs at **command validation / operation scheduling only** — never at
   execution, never inside the solver (07 §2c; R12-V11; SDD-003 §7).
2. The solver and every model are **capability-blind**: they read installed
   tiers and applied effects, never `ICapabilitySet` (GM14's cousin — an
   architecture test bans `ICapabilitySet` references outside validators and
   the Advisor).

## 4. Effects — one path for technology and environment

The shared vocabulary (07 §1 = 13 §2.1), as data:

```csharp
public readonly record struct ModelSlot(string Name);      // a rebindable model slot (03 §3.2)
public readonly record struct ParameterKey(string Name);

public enum EnvelopeKind
{
    MaxDrillingDepth, MaxWaterDepth, MaxWaveHeight, MaxAmbientTemperature,
    MaxH2SFraction, MaxCompressionRatio, ArcticOperability, MaxLoadBearing
}

// Extension raises the base; Restriction caps the result (§4.1).
public enum EnvelopeContributionKind { Extension, Restriction }

public abstract record Effect;
public sealed record UnlockOption(ContentId What) : Effect;          // catalogue entry, activity template, drive mechanism…
public sealed record MoveEnvelope(
    EnvelopeKind Kind,
    EnvelopeContributionKind Contribution,
    double Value) : Effect;
public sealed record SetModelSelection(ModelSlot Slot, ContentId Plugin) : Effect;   // swap the registered implementation
public sealed record SetModelParameter(ModelSlot Slot, ParameterKey Key, double Value) : Effect;
// THERE IS NO MULTIPLIER RECORD. Architecture test: the Effect hierarchy is
// sealed to these four (R17-V13, 13 §2.1).
```

> **Contract pass 10 — this block contradicted §4.1 below it.** `MoveEnvelope`
> was declared as `(EnvelopeKind, double)`, with no contribution kind. But §4.1
> settles the combination rule as
> `Min( Max(base, Extensions…), Restrictions… )`, which is only computable if
> each contribution says *which* it is. As declared, a winterisation extension
> and an ice-season restriction were the same shape, and the combinator had
> nothing to dispatch on — the rule §4.1 calls "the rule that had to be pinned"
> could not be implemented from the type that carries it. `EnvelopeKind`,
> `EnvelopeContributionKind`, `ModelSlot` and `ParameterKey` were likewise used
> throughout §4 and declared nowhere.

### 4.0b Slots and scoped effects (07 §4b.3b)

```csharp
public enum SlotKind { ComponentSocket, LiftSocket, DrillingFluid,
    CompletionFluid, ChemicalInjection, ProcessAdditive, InjectionStream,
    DriveMechanism, ModelSlot }
// Meters are ComponentSocket entries (07 §4b.3b) — one socket kind for all
// completion-mounted components; an earlier draft had MeterSocket and drifted
// from 07's table. The enum matches the design table exactly, by test.

// On gated content (SDD-004 GatedDefinition):
//   Fits : SlotKind                      — REQUIRED on every unlockable entry
//   ScopedEffects : IReadOnlyList<Effect> — treatments/materials only; applied to
//                                           the owning instance while assigned
//   ConsumptionRate, UnitCost             — the OPEX line, metered by the owner
```

- Slot assignment is a **command** (validated: SlotKind match + Gating.Check),
  so "install", "select mud", "start inhibitor injection" are one mechanism.
- Scoped effects enter the SAME `EffectState` (§4.2) with **instance scope**;
  combination follows §4.1 unchanged; per-contribution provenance answers
  "why is this value what it is?" at every scope.
- `InjectionStream` entries that are stream materials (polymer, CO₂) are
  ordinary `IMaterial`s — they flow, they conserve (04 §7), and their reservoir
  behaviour comes from the `DriveMechanism` plugin the flood plan selects; the
  ScopedEffects on the material cover surface-side handling only. **No
  material-identity branch anywhere** — the drive plugin declares which
  injectants it accepts.
- Architecture test: every `UnlockOption` target has `Fits`; every slot picker
  filters on it; an entry assigned to a mismatched SlotKind cannot be
  expressed (typed command parameters).

### 4.1 Envelope combination — the rule that had to be pinned

Environment *restricts* envelopes; technology *extends* them; both submit
`MoveEnvelope` values. Resolution:

```text
Each EnvelopeKind declares its combinator in content: Min or Max.
  effective(kind) = combinator( base(kind), all active contributions(kind) )
For a "maximum-type" envelope (max operating depth, max wave height, max H2S):
  combinator = Min over restrictions, then Max with extensions is WRONG and
  order-dependent. Instead: contributions are TYPED —
     Restriction(value)  participates as an upper bound (min-combined)
     Extension(value)    RAISES the base before restrictions apply
  effective = min( base + Σ? no — max(base, all Extensions) , then min with all Restrictions )
  i.e.  effective = Min( Max(base, Extensions…), Restrictions… )
```

That final line is the pinned form: **extensions raise what is possible;
restrictions cap what is permitted; restrictions always win.** Winterisation
(extension) raises arctic operability; the ice-road season (restriction) still
caps it. Deterministic, order-free, and it answers the composition question
13 §2.1 raised but never resolved — logged as a design refinement, not a
contradiction.

### 4.2 Application timing

- Environment effects: recomputed at **tick stage 2** from the location's
  profile and weather (R22).
- Technology effects: applied when acquisition completes (a stage-11
  state change), taking effect **next tick** — technology never creates a
  segment boundary (R17 §2.7).
- Both land in one effect state the models read; provenance kept per
  contribution so the audit can answer *"why is my max depth 4,200 m?"* with
  the list.

```csharp
// The combined state, in the Kernel so every module can read it without
// depending on the technology or environment modules that write it.
// DERIVED — never saved (SDD-013 §4): it is rebuilt at stage 2 from the
// profile and weather, and on acquisition for technology.
public interface IEffectState
{
    double EffectiveEnvelope(EnvelopeKind kind);        // §4.1's combination, already applied
    ContentId SelectedPlugin(ModelSlot slot);
    double Parameter(ModelSlot slot, ParameterKey key);
}
```

**The three readers are the whole surface**, and that is what keeps models
capability-blind (§3 rule 2): a model asks what its effective envelope, plugin
or parameter *is*, and has no way to ask who contributed it or whether the
company owns a technology.

## 5. Detectability consumption

```csharp
// In OGSim.Information's observation pipeline (R14):
if (accumulation.TrapSubtlety > survey.Tier.MaxDetectClass) 
    yield nothing;   // no lead, no belief entry, no read-model trace (R14-V14)
```

`survey.Tier.MaxDetectClass` comes from the **information-source content entry**
(gated equipment), not from `ICapabilitySet` directly — you re-screen with a
*kit*, which you could only buy because of the node. Consistent with §3 rule 2.

## 6. Persistence

`TechnologyState` persists: acquired node ids (strings), in-progress R&D, and
per-node acquisition route (for running costs). Rentals persist inside their
operations. `EffectState` is **derived — never saved** (law L5; rebuilt from
nodes + profiles at load, PV2 verifies behavioural identity).

## 7. Error surface

| Situation | Response |
|---|---|
| Gated command without capability | Command rejection listing every missing item |
| Content `requiresTech` naming an unknown node | Load failure (SDD-004 stage 4) |
| Effect targeting an unknown slot/envelope kind | Load failure (stage 6) |
| Two `SetModelSelection` on one slot from different active technologies | Content consistency failure at load — precedence is authored, never implicit |

## 7b. Reality profiles — the fidelity axis

> **R25.1 declaration (finding 163).** `RealityProfile` is a `ContentId` on
> `EngineSetup` ([SDD-017](SDD-017_HOST_SURFACE.md) §1b), on `Scenario`
> ([SDD-014](SDD-014_OBJECTIVES_AND_SCENARIOS.md) §5) and on `ObjectiveView`,
> and SDD-014 cites *"modifiers (SDD-005, 18 §5b)"* — **this document.** It said
> nothing about them. Three contracts carried an id that nothing could turn into
> behaviour, which is findings 129/141/154's shape for the fifth time.

[18](../design/18_GAME_MODES.md) §5b.1 makes fidelity "per-model plugin
selection ([03](../design/03_ARCHITECTURE.md) §3.2) — arcade / standard /
simulation implementations per subsystem". **Every mechanism that needs already
exists**: `ModelSlot`, `SetModelSelection` (§4's sealed effect vocabulary) and
`PluginRegistry`, keyed by (name, contract). A profile is therefore not a new
mechanism — it is a named bundle of the selections technology already makes.

```csharp
/// 18 §5b's fidelity axis, as content. A named bundle of model selections
/// applied at composition, before any tick runs.
public sealed record RealityProfile(
    ContentId Id,
    IReadOnlyList<SetModelSelection> Fidelity);
```

**Composed, not commanded.** Technology issues `SetModelSelection` mid-game
through §4's effect pipeline; a profile applies the same selections *at
composition*, because fidelity is what a run is played at rather than something
earned during it. Changing preset mid-game is allowed (18 §5b.5) and is a
recompose, which is why it is "allowed and logged" rather than free.

**The same slot may be named by a profile and by a technology**, and §7's error
table already settles the collision: two `SetModelSelection` on one slot is a
content consistency failure with authored precedence, never implicit. A profile
selecting a plugin a technology later replaces is the designed case — the
player earned a better model — and a profile and an *active* technology naming
one slot at the same instant is the authored-precedence one.

**A profile names slots, never all of them.** An unnamed slot keeps whatever the
module composed, so `standard` is legitimately the empty profile: it is the
shipped set, and only a departure from it needs stating.

## 8. Test mapping

R17-V11 (gate names the missing tech) · R17-V12 (rental path) · R17-V13/13 §2.1
(sealed effect hierarchy) · R17-V14 (`AllCapabilities` retrofit — physics
unchanged) · R17-V15 + R14-V14 (detectability unlock end-to-end) · R12-V11
(scheduling-time gating) · R22-V13 (shared vocabulary) · new **R17-V16**:
envelope combination matches §4.1's pinned form on a matrix of
extension/restriction cases, order-shuffled.

## 9. Open items

| # | Item | Trigger |
|---|---|---|
| S005-1 | Rival capability state: rivals hold their own `TechnologyState` (TD2) — same type, no special path; confirm memory footprint is trivial | R16 |
| S005-2 | Whether `MaxDetectClass` should be per-basin (regional expertise) rather than global | Post-R20 balance — start global |
| S005-3 | Which of §3.2's eleven slots ship an arcade implementation. Fluid properties first (the correlations a player never sees); inflow and hazards are the next candidates. A slot with one implementation is not a defect — it is a slot whose fidelity does not yet vary | R25.1, as each arcade model is written |
