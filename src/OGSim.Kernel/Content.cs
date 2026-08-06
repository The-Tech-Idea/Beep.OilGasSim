// SDD-004 — the content pipeline surface: the front door of non-negotiable 11
// ("everything is definition-driven and moddable"). Third contract pass: these
// were pinned in the SDD but declared nowhere — the moddability rule had no type.
// The loader CLASS (six stages) is R3 implementation; these are its contracts.


namespace OGSim.Kernel;

/// <summary>The four technology eras (design 07 §2).</summary>
public enum Era { E1, E2, E3, E4 }

/// <summary>Every content entry derives from this — one id, one kind-specific record shape.</summary>
public abstract record ContentDefinition(ContentId Id);

/// <summary>
/// Base of every unlockable equipment kind (SDD-004 §6): well-component,
/// facility-unit, pipe-spec, information-source, lift-method. `Fits` is
/// REQUIRED on every unlockable (SDD-005 §4.0b) — it is how the system knows
/// where a new material or device plugs in. Datasheet blocks are kind-specific
/// and closed (no generic property bag), declared per catalogue sheet at R3.
/// </summary>
public abstract record GatedDefinition(
    ContentId Id,
    ContentId? RequiresTech,
    Era AvailableFromEra,
    SlotKind Fits) : ContentDefinition(Id);

/// <summary>SDD-004 §6 — ordinal = index into the id-sorted list; ordinals NEVER persist.</summary>
public interface ICatalog<TDef> where TDef : ContentDefinition
{
    /// <summary>Missing id is a content fault — never null (SDD-004 §6).</summary>
    TDef this[ContentId id] { get; }
    /// <summary>Ordinal-sorted by id string — save-stable.</summary>
    IReadOnlyList<TDef> All { get; }
    bool TryGet(ContentId id, out TDef def);
}

/// <summary>The typed catalogue set a successful load produces.</summary>
public interface ICatalogSet
{
    ICatalog<TDef> Of<TDef>() where TDef : ContentDefinition;
}

public sealed record ContentFile(string RelativePath, string Json);

/// <summary>One id an entry points at, with the path that named it (SDD-004 §5 stage 4).</summary>
public readonly record struct ContentReference(string Kind, ContentId Id, string JsonPath);

/// <summary>
/// SDD-004 §5 stage 2's dispatch table entry. THIS is what keeps the loader
/// type-agnostic: a content kind is registered, never coded into the loader, so
/// R3 §3's acceptance criterion — "if a later phase needs a loader change to add
/// a content kind, R3's design is wrong" — is a property of this interface
/// rather than a promise anyone has to keep.
///
/// There is deliberately no Write: content is authored by hand and by tooling
/// and never emitted by the engine, so a serialiser would be an unused member
/// (law L3) and an invitation to round-trip authored files through the engine,
/// which is how formatting and comments get destroyed.
/// </summary>
public interface IContentKind
{
    string Name { get; }

    /// <summary>Stages 2–3: shape and units. Throws; the loader turns the throw
    /// into a LoadFailure carrying file and JSON path.</summary>
    ContentDefinition Read(System.Text.Json.JsonElement element);

    IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition);

    /// <summary>Stage 5: EVERY problem, never just the first.</summary>
    IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition);
}

/// <summary>
/// Base content is order 0; mods declare order. Two sources overriding one
/// kind:id at the SAME order is a load failure naming both (SDD-004 §7).
/// An override replaces the whole entry — no partial merge.
/// </summary>
public interface IContentSource
{
    string Name { get; }
    int DeclaredOrder { get; }
    IReadOnlyList<ContentFile> Files { get; }
}

/// <summary>The six loader stages (SDD-004 §5); every file runs all stages, failures accumulate.</summary>
public enum LoadStage { Parse = 1, Shape, Units, References, Consistency, Binding }

public sealed record LoadFailure(
    string Source,
    string File,
    string JsonPath,
    LoadStage Stage,
    string Message);

/// <summary>Catalogues on success, failures otherwise — NEVER both; any failure means the engine does not start (10 §3, G2).</summary>
public abstract record ContentLoadResult;
public sealed record ContentLoaded(ICatalogSet Catalogues) : ContentLoadResult;
public sealed record ContentFailures(IReadOnlyList<LoadFailure> Failures) : ContentLoadResult;
