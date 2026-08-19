# SDD-004 — Content Pipeline and Catalogue

**Status:** drafted · **Serves:** R3 · **Design docs:** [10](../design/10_CONTENT_AND_UNITS.md), [07](../design/07_TECHNOLOGY.md) §4b, [catalog/](../catalog/CATALOG_INDEX.md), [R3](../phases/R3_CONTENT.md)

Everything the loader does, pinned: the file grammar, the unit-string grammar,
how "unknown keys are errors" is actually achieved, id policy, catalogue
building order, plugin binding, and mod layering. The content author's contract
and the implementer's, in one place.

---

## 1. Scope

`OGSim.Kernel.Content`. Zero external packages (SDD-000 §1): parsing is
`System.Text.Json` **source-generated** contexts, which is also how unknown-key
rejection works without a schema engine.

## 2. File grammar

One entry per file. Required envelope, in this order by convention:

```jsonc
{
  "kind": "well-component",          // REQUIRED, first — the explicit type (10 §3.1)
  "id": "esp-c",                     // REQUIRED — kebab-case, unique per kind
  "name": "$loc:equip.esp-c",        // localisation id, never inline text (CD5)
  "era": "E3",                       // availableFromEra; omitted = E1
  "requiresTech": "esp-ht-gassy",    // omitted = ungated, EXPLICITLY (10 §3)
  // ... kind-specific body
}
```

- Files under `content/**` with extension `.json`; directory layout is
  organisational only — **the loader never infers anything from paths**.
- `id` charset: `[a-z0-9-]`, 1–64 chars. Uniqueness is per `kind`.
- **`ContentId`, pinned here because every SDD uses it and none declared it:**
  `readonly record struct ContentId(string Value)` — charset-validated at
  construction, compared and sorted **ordinal** (D-8), interned at load.
  Never a bare string in a signature.
- Cross-references are always by `"kind:id"` when cross-kind
  (`"tech:esp-ht-gassy"` is legal and equivalent for `requiresTech`, which is
  implicitly `tech:`).

> **Pass-7 amendment (finding 80):** `terrain-class` joins the kind table — a
> plain `ContentDefinition` (world fact: no `requiresTech`, no `Fits`, no era).
> Authoring spec: [C16](../catalog/C16_TERRAIN_CLASSES.md). Consumed by the
> world generator's terrain step (SDD-010 §3) and read back for construction /
> transport / access factors.

## 3. Unknown keys, without a schema engine

> **R3.0 layering correction.** This section places the definition records in
> `OGSim.Kernel.*` while `ContentDefinition` and `GatedDefinition` — the bases
> they derive from — were declared in `OGSim.Contracts`. Kernel cannot reference
> Contracts, so no definition record could have derived from its own base. The
> whole content surface (`ContentDefinition`, `GatedDefinition`, `Era`,
> `ICatalog<T>`, `ICatalogSet`, `IContentSource`, `ContentFile`, `LoadStage`,
> `LoadFailure`, `ContentLoadResult`) now lives in `OGSim.Kernel/Content.cs`,
> which also matches [R3](../phases/R3_CONTENT.md) §3's own deliverable name,
> `OGSim.Kernel.Content`. Second instance of the pattern R2.1 found in R2 §3 —
> a deliverables table specifying a build that cannot compile.

**Decision:** every content kind has a C# *definition record* in
`OGSim.Kernel.Content.Definitions`, deserialised via a source-generated
`JsonSerializerContext` with:

```csharp
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,   // unknown key -> error
    NumberHandling = JsonNumberHandling.Strict)]
```

`Disallow` gives R3-V3 natively; the load report wraps the thrown path into
*file · JSON path · offending key · nearest valid key* (Levenshtein over the
record's property names — small, worth it for authors).

**JSON Schema files still ship** (R3 deliverable, for editor tooling) but are
**generated from the definition records at build** — one source of truth, so
R3-V13 (schema currency) becomes a build step, not a discipline.

## 4. The unit-string grammar (CD2)

```ebnf
quantity   = number , " " , unit ;
number     = [ "-" ] , digits , [ "." , digits ] , [ ("e"|"E") , [ "-" ] , digits ] ;
unit       = token , { ("/" | "." ) , token } , [ "^" , digit ] ;   // "kg/m^3", "m3/d"
```

- Parsed with `InvariantCulture`; **decimal point only** — a comma is a load
  error with a pointed message, not a locale guess.
- The unit vocabulary is **closed**: a table mapping unit token →
  (dimension, factor-to-SI, offset for temperature). Unknown token → load error
  naming the nearest known token.

  > **R3.1 correction — the table is `UnitGrammar`, not `PhysicalConstants`.**
  > This bullet placed it in `PhysicalConstants`, which is rule F-2's home: one
  > file where every entry is a *physical constant* carrying its SDD citation and
  > unit. A token-to-dimension map contains no physics, and putting it there
  > would dissolve the single property that makes F-2 checkable at all.
- Dimension check happens at bind time: the definition record property is a
  quantity type (`Pressure`), and a `"3200 psi"` deserialising into a
  `Temperature` property fails stage 3 with both dimensions named (R3-V5).
- Volume tokens carry their condition: `stb`, `rb`, `scf`, `sm3`, `rm3` map to
  the distinct volume types of SDD-001 §1.1 — the double-count protection
  reaches all the way into content.

## 5. The six stages, as code

```csharp
public enum LoadStage { Parse = 1, Shape, Units, References, Consistency, Binding }

public sealed record LoadFailure(
    string Source,
    string File,
    string JsonPath,
    LoadStage Stage,
    string Message);

// Catalogues on success, failures otherwise — NEVER both. Any failure at all
// means the engine does not start (10 §3, G2), which is why this is a closed
// choice and not a result with an errors list hanging off it.
public abstract record ContentLoadResult;
public sealed record ContentLoaded(ICatalogSet Catalogues) : ContentLoadResult;
public sealed record ContentFailures(IReadOnlyList<LoadFailure> Failures) : ContentLoadResult;

// Stage 2's "dispatch by table", declared (R3.2). THIS is what keeps the loader
// type-agnostic: a content kind is REGISTERED, never coded into the loader, so
// R3 §3's real acceptance criterion — "if a later phase needs a loader change to
// add a content kind, R3's design is wrong" — is a property of this interface
// rather than a promise. All 27 kinds of design 10 §2 arrive this way.
public readonly record struct ContentReference(string Kind, ContentId Id, string JsonPath);

// Stage 6. The CONTRACT is part of the binding because a plugin name alone
// cannot be checked usefully: "is anything registered under this name" would
// pass a price model named where a separation model belongs, and only fail at
// the tick that first used it — with a saved game already built on it.
public readonly record struct PluginBinding(ContentId Plugin, Type Contract, string JsonPath);

public interface IContentKind
{
    string Name { get; }                                    // the JSON "kind" value

    // Stages 2-3: shape and units. Throws ContentUnitFault / JsonException;
    // the loader converts either into a LoadFailure carrying file and path.
    ContentDefinition Read(JsonElement element);

    // Stage 4: every id this entry points at, for resolution against the index.
    IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition);

    // Stage 5: per-kind rules — ranges, monotone curves, DAG membership.
    // Returns EVERY problem, not the first.
    IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition);

    // Stage 6: which model plugins this entry names, and against what contract.
    // The KIND reports these because only it knows its own datasheet — the
    // loader must not learn what a lift-method's plugin field is called.
    IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition);
}

public sealed class ContentLoader   // one public entry point
{
    public ContentLoader(IReadOnlyList<IContentKind> kinds, IModuleRegistry plugins);
    public ContentLoadResult LoadAll(IReadOnlyList<IContentSource> sources);
}
```

**`IContentKind` has no `Write`, and that is deliberate.** Content is authored by
hand and by tooling, never emitted by the engine — a serialiser here would be an
unused member (law L3) and an invitation to round-trip content through the engine,
which is how authored formatting and comments get destroyed.

> **Contract pass 10.** This block declared the return as `LoadResult` while the
> pass-3 amendment below it — and the committed code — say `ContentLoadResult`.
> One document, two names for its own load result, exactly as SDD-002 §6 named
> `FlowNetwork` against its own §7. The pattern in both cases is an amendment
> appended beneath a code block that was never edited to agree with it.
>
> `LoadStage` and `LoadFailure` are declared here rather than left to the table
> below, because the stage numbers are load-bearing: failures are reported in
> stage order, and `Parse = 1` fixes that ordering against the enum rather than
> against declaration accident.

| Stage | Implementation | Failure carries |
|---|---|---|
| 1 Parse | `JsonDocument.Parse` per file | file, byte offset, parser message |
| 2 Shape | Source-gen deserialise to the kind's record (`kind` read first, dispatch by table) | file, JSON path, unknown/missing key, nearest-key hint |
| 3 Units | Quantity properties bound via the grammar (§4) | file, path, token, expected dimension |
| 4 References | Every id-typed property resolved against the id index built in stage 2 across **all** files (two passes: index, then resolve) | file, path, dangling `kind:id` |
| 5 Consistency | Per-kind validators (ranges from property kinds, duplicate ids, `requiresTech` DAG acyclicity, era ∈ {E1..E4}) | file, path, rule id, values |
| 6 Binding | Model-plugin names resolved against `IModuleRegistry`'s factories | file, path, unbound plugin name |

**All files run all stages**; failures accumulate (R3-V2). Stage order within a
file is fixed; file order is `ordinal sort of relative path` — determinism even
in diagnostics.

> **R3.2 refinement — "all files" and "all stages" are different promises, and
> only the first is unconditional.** Stages 1–3 are *per-file*: every file is
> parsed, shaped and unit-bound regardless of what any other file did, which is
> what R3-V2 is actually protecting — never stopping at the first bad file.
>
> Stages 4–6 are *cross-file*: they resolve against an index built from every
> entry that survived 1–3. If a file failed to parse, its entry is simply
> absent, and every reference to it would then report a dangling reference —
> **a cascade of spurious failures from one root cause**, burying the real error
> in consequences of it. So 4–6 run only once 1–3 have produced a complete
> index.
>
> The practical shape: a first run reports the malformed files; fixing those and
> re-running reports the reference and consistency problems. Two rounds, each
> reporting everything it can honestly see — rather than one round reporting a
> true error and five false ones.

> **R3.7 — `property-kind` is a BOOTSTRAP kind, loaded in its own pass first.**
> Stage 3 binds a quantity string against the dimension its property kind
> declares (`"850 kg/m3"` is a density because `density` says so). But stage 3
> runs before stage 4, so it cannot resolve a reference to reach that dimension —
> the ordering is not an oversight, it is what stops unit binding depending on
> arbitrary other content.
>
> Property kinds are the vocabulary everything else is *written in*, which makes
> them schema rather than data: loading schema before data is the normal shape,
> and property kinds have no references of their own by construction, so the
> bootstrap pass can never itself need a second one.
>
> Concretely: load `property-kind` alone, build the id → dimension map, then load
> every other kind with that map in hand. Two `LoadAll` calls, and the loader
> stays type-agnostic — it is the *caller* that knows property kinds come first,
> not the loader.

> **Pass-3 amendment (finding 69):** the surface of this section now exists in
> `OGSim.Contracts/ContentContracts.cs`: `ContentDefinition`, `GatedDefinition`,
> `Era`, `ICatalog<TDef>`, `ICatalogSet.Of<TDef>()`, `IContentSource`
> (`Name`, `DeclaredOrder`, `Files`), `ContentFile`, `LoadStage`, `LoadFailure`,
> `ContentLoadResult = ContentLoaded(ICatalogSet) | ContentFailures`. Stage-6
> binding resolves against `IModuleRegistry` (Kernel):
> `CanBind(ContentId, Type)` / `Bind<T>(ContentId)`. The `ContentLoader` class
> itself remains R3 implementation.

## 6. Catalogues

```csharp
public enum Era { E1, E2, E3, E4 }                 // the four technology eras (07 §2)

// Every content entry derives from this: one id, one kind-specific record shape.
public abstract record ContentDefinition(ContentId Id);

// Base of every unlockable equipment kind. `Fits` is REQUIRED on every one of
// them (SDD-005 §4.0b) — it is how the system knows where a new device or
// material plugs in without anyone branching on what it is.
public abstract record GatedDefinition(
    ContentId Id,
    ContentId? RequiresTech,
    Era AvailableFromEra,
    SlotKind Fits) : ContentDefinition(Id);        // SlotKind: SDD-005 §4.0b

public interface ICatalog<TDef> where TDef : ContentDefinition
{
    TDef this[ContentId id] { get; }               // missing -> content fault (never null)
    IReadOnlyList<TDef> All { get; }               // ordinal-sorted by id string — save-stable
    bool TryGet(ContentId id, out TDef def);
}

// What a successful load produces: catalogues addressed by definition type.
public interface ICatalogSet
{
    ICatalog<TDef> Of<TDef>() where TDef : ContentDefinition;
}
```

- **Ordinal assignment** (e.g. `MaterialId.Ordinal`, SDD-002 §2): index into the
  id-sorted list. Sorted-by-id means adding a material *changes ordinals* — so
  **ordinals never persist**: saves store the id string; ordinals rebuild at
  load. Pinned here because persisting an ordinal is exactly the subtle bug an
  implementer would commit in week two.
- Equipment kinds (`well-component`, `facility-unit`, `pipe-spec`,
  `information-source`, `lift-method`) share a `GatedDefinition` base:
  `RequiresTech : ContentId?`, `Era`, **`Fits : SlotKind`** (required on every
  unlockable — [SDD-005](SDD-005_CAPABILITIES_AND_EFFECTS.md) §4.0b), optional
  `ScopedEffects` + `ConsumptionRate`/`UnitCost` for treatments, plus the
  catalogue-sheet fields (capex, install operation template id, datasheet
  block). **The datasheet block is
  kind-specific and closed** — an ESP datasheet is `headCurve` (piecewise
  points, monotone-decreasing validated), `powerCurve`, `maxFreeGasFraction`,
  `maxTemperature`, `rateRange`; there is no generic properties bag, because a
  bag is where unknown-key safety goes to die.

> **R20c.9 amendment. Facility units are SIX kinds, and a ladder declares its
> own order.**
>
> §6 lists `facility-unit` among the equipment kinds sharing `GatedDefinition`.
> Implementing it showed one kind cannot carry six datasheets and stay closed: a
> separator states two independent leg capacities, a vessel volume, a rated
> efficiency and the rate that efficiency holds at; a manifold states a slot
> count; a treater states a water-removal fraction. A single record spanning
> those is the generic bag this section forbids, wearing a record's clothes —
> every field optional, every reader branching on which ones arrived.
>
> So `facility-unit` resolves into six kinds — `separator`, `tank`, `treater`,
> `gas-plant`, `export-line`, `manifold` — each a closed record over a shared
> `FacilityUnitDefinition(Id, RequiresTech, AvailableFromEra, Rung)`. They share
> the gate and the rung and nothing else, which is what §6's "the datasheet block
> is kind-specific and closed" already says; the correction is only that the KIND
> is what varies, not a block inside one.
>
> **They do NOT derive from `GatedDefinition`, and the missing member is the
> reason.** That base requires `Fits : SlotKind`, which SDD-005 §4.0b defines as
> how the system knows where a device plugs in *without branching on what it is*
> — and `SlotKind` has no facility member. Adding one would make a `separator`
> definition declare that it fits the separator slot, which is its own kind
> restated: a second owner of one fact, which law L5 forbids. **For a facility
> unit the KIND IS THE SLOT.** So these carry the gate pair explicitly and leave
> `Fits` to the unlockables that genuinely need it — a lift method, a completion
> fluid, a chemical — where one socket accepts several kinds of thing and the
> answer cannot be read off the record's type.
>
> **A ladder is ORDERED, and `ICatalog.All` is not.** Catalogue order is the
> id-sorted list, which is right for save stability and wrong for a progression:
> `gas-plant-e1` sorts before `gas-plant-none`, and an upgrade path read off that
> would let a player install a plant by buying nothing. Nor can era stand in —
> two rungs of one ladder routinely share an era. **So every facility-unit
> declares `rung`**, a non-negative integer, unique within its kind, and the
> ladder is the kind's definitions sorted by it. Rung 0 is the absent state that
> every ladder in this engine starts at: a field with no gas plant flares, and
> "no gas plant" is a rung rather than a null.
>
> **Content loads BEFORE modules compose**, which §5 implies and nothing stated.
> A module's `Compose` reads catalogues to build its equipment, so the catalogues
> must exist first; `PluginRegistry`'s own header already says a plugin must not
> be built during content load "which happens before the engine exists". The
> sources therefore arrive on `EngineSettings`, beside the seed and the retention
> policy — a host supplies them, and the engine never touches a disk. A load
> failure is a composition refusal carrying the same `LoadFailure` list §5
> defines, because a game that cannot read its own equipment has nothing to
> start (G2).

## 7. Mods

```csharp
public sealed record ContentFile(string RelativePath, string Json);

public interface IContentSource
{
    string Name { get; }
    int DeclaredOrder { get; }                     // base content is 0
    IReadOnlyList<ContentFile> Files { get; }
}
```

**A source carries its files rather than a directory path**, which is what lets
base content, a mod folder, a zip and a test fixture all be the same thing to
the loader — and is why the six stages need no file-system access at all.

Base content is source order 0. Mods declare order; **two sources overriding one
`kind:id` at the same order → load failure naming both** (10 §4). An override
replaces the whole entry (no partial merge — merge semantics are unspecifiable
for datasheets). The load report lists every override applied (R3-V11).

## 8. TECH_TREE and sheets as fixtures

The [TECH_TREE registry](../catalog/TECH_TREE.md) and
[catalogue sheets](../catalog/CATALOG_INDEX.md) are the authoring spec (10 §2b).
The mapping rule, pinned: **content id = deterministic slug of the registry
display name** — lowercase, `/`, spaces and `·` to `-`, diacritics/₂-class
subscripts folded (`CO₂ flood` → `co2-flood`). One function, used by the
fixture and by authors. Enforced mechanically here: a **fixture test** parses the registry's node tables
and asserts (a) every shipped `tech` content id appears in the registry, (b)
every `requiresTech` in shipped content names a registry node, (c) era and
prereqs agree. The plans-side gate check that already runs over the sheets gets
a code-side twin.

## 9. Error surface

All loader failures are **content faults** ([09](../design/09_DIAGNOSTICS.md)
§5.1 C1): reported in batch, engine refuses to start. There are no warnings —
a tolerated oddity today is a silent wrong game later.

## 10. Test mapping

R3-V1..V14 map one-to-one; §3 gives V3/V13 their mechanism, §4 gives V4/V5, §5
gives V2/V6..V10, §7 gives V11/V12, §8 adds the registry fixture test
(new: R3-V15, sheet/tree/content agreement).

## 11. Open items

| # | Item | Trigger |
|---|---|---|
| S004-1 | Schema generation tooling (records → JSON Schema) — build-time generator or a checked-in generated set with a drift test | R3.1 |
| S004-2 | Localisation file format for `$loc:` ids | R21 (host needs it first) |
| S004-3 | Content hot-reload for balancing sessions (out of scope for the engine proper; a dev-host feature) | post-R20 |
