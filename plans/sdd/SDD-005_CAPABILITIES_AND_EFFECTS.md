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
public sealed record Requirements(
    IReadOnlyList<TechnologyId> Tech,            // all must be held or rented
    DetectClass? MinDetectClass,                 // surveys: spawn threshold (06 §2.3)
    IReadOnlyList<EnvelopeCheck> Envelopes);     // e.g. rig depth >= well TD (§4)

public static class Gating
{
    public static GateResult Check(in Requirements req, ICapabilitySet caps,
                                   IReadOnlyList<ServiceRental> rentals,
                                   in EnvelopeContext ctx);
    // GateResult = Pass | Fail(IReadOnlyList<MissingItem>)
    // MissingItem names the SPECIFIC tech/tier/envelope — the domain reason of
    // R17 §2.6b. ALL misses are reported, not the first (the R3-V2 principle).
}
```

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
public abstract record Effect;
public sealed record UnlockOption(ContentId What) : Effect;          // catalogue entry, activity template, drive mechanism…
public sealed record MoveEnvelope(EnvelopeKind Kind, double Value) : Effect;
public sealed record SetModelSelection(ModelSlot Slot, ContentId Plugin) : Effect;   // swap the registered implementation
public sealed record SetModelParameter(ModelSlot Slot, ParameterKey Key, double Value) : Effect;
// THERE IS NO MULTIPLIER RECORD. Architecture test: the Effect hierarchy is
// sealed to these four (R17-V13, 13 §2.1).
```

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
- Both land in one `EffectState` the models read; provenance kept per
  contribution so the audit can answer *"why is my max depth 4,200 m?"* with
  the list.

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
