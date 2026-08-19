// R20d.9 — IWell and IWellbore, implemented (SDD-003 §5's R20d.9 amendment).
//
// Both contracts have been declared since the contract layer and implemented
// by nothing. Built against the REACHABLE subset of design 02 §3.4's twelve-
// state lifecycle, not the full diagram — the amendment states exactly which
// nine states this composition's command surface cannot produce and why, so
// this file does not re-argue it.
//
// PURE VALUE OBJECTS, deliberately: neither type holds a module reference of
// its own. The caller (FieldControl, in composition) reads WellsState's
// completions and the abandoned set, and hands over already-computed values —
// which is what keeps a Wellbore's own dependency on its Completion (below) a
// same-module reference rather than a cross-module one (law L1).

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Wells;

/// <summary>
/// SDD-003 §5's R20d.9 amendment. Identity and the reachable status machine;
/// no physics.
/// </summary>
public sealed class Well : IWell
{
    public Well(
        EntityId<IWell> id,
        WellStatus status,
        WellClassification classification,
        EntityId<ILicence> licence,
        Coordinate surface,
        IReadOnlyList<EntityId<IWellbore>> wellbores)
    {
        ArgumentNullException.ThrowIfNull(wellbores);

        Id = id;
        Status = status;
        Classification = classification;
        Licence = licence;
        Surface = surface;
        Wellbores = wellbores;
    }

    public EntityId<IWell> Id { get; }
    public WellStatus Status { get; }
    public WellClassification Classification { get; }
    public EntityId<ILicence> Licence { get; }
    public Coordinate Surface { get; }
    public IReadOnlyList<EntityId<IWellbore>> Wellbores { get; }
}

/// <summary>
/// SDD-003 §5's R20d.9 amendment. One wellbore per well, since no command in
/// this composition produces a second (no sidetrack).
///
/// <para><see cref="ContactLengthIn"/> reads the completion's OWN perforations
/// rather than re-deriving an interval from <see cref="Path"/>: a perforation
/// already states its own <c>TopMd</c>/<c>BottomMd</c> — the fact flow
/// calculations actually use — and a compartment carries no depth interval of
/// its own to check a geometry-only derivation against. Summing the
/// perforations draining the named compartment is the one owner already
/// established (law L5); this reads it rather than adding a second.</para>
/// </summary>
public sealed class Wellbore : IWellbore
{
    private readonly Completion _completion;

    public Wellbore(
        EntityId<IWellbore> id, EntityId<IWell> well, Trajectory path, Completion completion)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(completion);

        Id = id;
        Well = well;
        Path = path;
        _completion = completion;
    }

    public EntityId<IWellbore> Id { get; }
    public EntityId<IWell> Well { get; }
    public Trajectory Path { get; }

    public Length ContactLengthIn(EntityId<IReservoirCompartmentEntity> compartment)
    {
        IReadOnlyList<Perforation> perforations = _completion.Perforations;
        double totalMetres = 0.0;

        for (int i = 0; i < perforations.Count; i++)
        {
            Perforation perforation = perforations[i];
            if (perforation.Drains != compartment) continue;

            totalMetres += perforation.BottomMd.Metres - perforation.TopMd.Metres;
        }

        return new Length(totalMetres);
    }

    /// <summary>Computed from the completion itself rather than stored a
    /// second time (law L5) — the completion already carries its own id.</summary>
    public IReadOnlyList<EntityId<ICompletion>> Completions => [_completion.CompletionId];
}
