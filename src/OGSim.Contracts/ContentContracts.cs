// SDD-004 — the content pipeline surface: the front door of non-negotiable 11
// ("everything is definition-driven and moddable"). Third contract pass: these
// were pinned in the SDD but declared nowhere — the moddability rule had no type.
// The loader CLASS (six stages) is R3 implementation; these are its contracts.

using OGSim.Kernel;

namespace OGSim.Contracts;

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
