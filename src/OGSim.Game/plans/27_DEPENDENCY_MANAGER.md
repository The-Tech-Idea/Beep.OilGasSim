# 27 — DependencyManager

**Status:** implemented 2026-08-23 (W4) — `src/OGSim.Composition/DependencyManager.cs`.
Steps 1–6 of §6 are done; step 7 (S-values onward to content) is W8.
**Input:** [the condition review](modules/90_CONDITIONS.md) — 10 conditions, 5
static-number sites, 5 illegal neutrals, found by reading all sixteen modules.

---

## 1. The problem, in one sentence

**A module decides things it is not entitled to decide, and sometimes decides
them twice.**

`FacilitiesModule` compares two content ids to work out whether the company owns
a plant. `FieldModule` and `HseModule` each test the same term in the manifest
*and* in `Compose`, and a claimed stage slot must be filled — so if those two
ever disagree, composition refuses at run time for a reason neither line
explains. `MaterialsModule` silently falls back to a model when its slot is
unnamed.

There is no one place to look at what this build has switched on, and no one
place for a style to change it.

---

## 2. The shape

```csharp
public sealed class DependencyManager
{
    /// The build's dependency set: every entry ON at the engine's own value
    /// unless the style's terms, the starting state or the profile depart
    /// from it. Complete by construction; there is no partial manager.
    public static DependencyManager For(
        StyleTerms terms, ContentId startingState, RealityProfile profile);

    /// Is this mechanic present at all?
    public bool Has(DependencyId id);

    /// What value does it carry? Throws if it is not present.
    public T ValueOf<T>(DependencyId id);
}
```

Three rules, and they are what make it worth having:

1. **Complete by default.** `For` resolves every dependency, at the value the
   engine ships. A style states only what it changes, so a dependency added next
   year is ON everywhere and nobody has to remember it.
2. **A module asks; it never decides.** `FacilitiesModule` asks
   `Has(OpensHoldingAPlant)`. It does not know what a starting state is.
3. **Presence and value are separate questions.** Because of the five illegal
   neutrals (§C of the review), "off" cannot always mean "zero".

### Why presence must be separate

```
WorkingInterest.Validate  refuses a sellable cap of 0
Hedge.Validate            refuses a hedged fraction of 0
BowTie.Materialises       throws on a rate <= 0
ClimateProfile.Validate   refuses a climate closed all twelve months
CrewState ctor            throws if training buys nothing
```

Each of these deliberately refuses content that describes a mechanic doing
nothing — and each is right to. So `Has(id) == false` means **the stage is not
contributed and the value is never asked for**, not "the value is zero".

---

## 3. The entries

Ten. One per condition found, plus `Banking`, which the plan revision found
missing (W5's "no banks" rule needs a presence switch). Each carries a default.

| `DependencyId` | Default | Answers | Replaces |
|---|---|---|---|
| `OpensHoldingAPlant` | from `content/starting-states/` (`holdsPlant`) | presence | **C1** — content-owned since the balance pass |
| `OpeningCash` | from `content/starting-states/` (`openingCashMillions`) | value | **C7** — content-owned since the balance pass; `OpeningCashFor` and its literals are gone |
| `Hedging` | on, `Defaults.Hedge` | **both** | **C2, C3** |
| `Insurance` | on, `Defaults.Insurance` | **both** | **C4, C5, C6** |
| `Tenure` | on | presence | **C8, C9** — the licence OBJECT composes in every build (the read model promises one licence, always); presence switches the commitment stage and the tenure refusal |
| `FluidModel` | `black-oil-correlations` | value | **C10** — no fallback; an unnamed slot is a refusal |
| `FiscalRegime` | royalty 0.125, tax 0.40 | value | **S1** |
| `HazardRate` | 0.05/yr, exponent 4.0 | **both** | **S2** |
| `SolverSettings` | `Pinned` | value | **S5** |
| `Banking` | on | presence | *(found in revision)* — `BorrowCommand`/`RepayCommand`, the covenant, the sweep and the takeover are offered only when present |
| `TakeOrPay` | on | presence | *(found by the balance measurement)* — the offtake commitment, its state key and its Company-2 stage compose only when present; a Days company owed deliveries years before its first possible sale |

`Has` and `ValueOf` are answered from the same entry, which is what makes the
manifest and `Compose` agree by construction: both call `Has(Hedging)`.

---

## 4. What a module looks like afterwards

**Before** — `FacilitiesModule.Compose`:

```csharp
if (startingState == Defaults.OpeningPosition)
    works.Commission(chain);
else if (startingState != Defaults.BareGround)
    throw new InvariantFault(...);
```

**After**:

```csharp
if (dependencies.Has(Dependency.OpensHoldingAPlant))
    works.Commission(chain);
```

The refusal on an unknown starting state does not disappear — it moves to where
the manager is built, which is the one place that knows what starting states
exist. A module that has never heard of a starting state cannot misspell one.

**Before** — `FieldModule`, the same fact in two places:

```csharp
stages: [ ..., .. Slot(terms.Hedge is not null, StageId.Company, order: 3) ]
...
if (hedge is OGSim.Company.HedgeTerms collar) { Validate(collar); Contribute(...); }
```

**After**:

```csharp
stages: [ ..., .. Slot(dependencies.Has(Dependency.Hedging), StageId.Company, order: 3) ]
...
if (dependencies.Has(Dependency.Hedging))
    Contribute(order: 3, new HedgeStage(dependencies.ValueOf<HedgeTerms>(Dependency.Hedging), ...));
```

One entry, asked twice, cannot disagree with itself.

---

## 5. What it does not do

- **It is not a difficulty dial.** An entry is ON at its engine value or absent.
  There is no third position, and a style may not hand a mechanic gentler
  numbers — that is a rule set or a reality profile, not a dependency.
- **It does not shrink OGSim.** Every mechanic stays in the engine. The manager
  decides what a *build* composes, never what exists.
- **It does not replace content.** Values that belong in JSON should move to JSON;
  the manager is where a module *asks*, not where numbers *live*. S1–S5 are
  listed here because they are currently unreachable, not because a C# class is
  their right home.

---

## 6. Order of work

| Step | What |
|---|---|
| **1** | `DependencyId` and `DependencyManager` with the nine entries and their defaults |
| **2** | Departures derive from `StyleTerms` itself — a nullable term is absence, and `Banking` joins the record as the one presence with no expressible neutral terms |
| **3** | `EngineBuilder` builds the manager once and hands it to the modules that need it |
| **4** | Replace C1, C7, C10 — the single-site conditions |
| **5** | Replace C2–C5 — the duplicated ones, which is where the real defect is |
| **6** | Replace C6, C8, C9 — the per-tick and per-order tenure checks |
| **7** | S1–S5 — move the static numbers behind entries, then to content |

Steps 4–6 are behaviour-preserving for Oilfield Engineer. That is the acceptance
criterion: **not one test moves.**
