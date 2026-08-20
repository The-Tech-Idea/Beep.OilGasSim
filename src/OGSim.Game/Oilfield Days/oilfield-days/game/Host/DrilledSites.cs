#nullable enable

using System;
using System.Collections.Generic;
using OGSim.Composition;

namespace OilfieldDays.Host;

/// <summary>
/// Which structures this company has already put a hole into.
///
/// <para><b>Why the host has to remember this at all.</b> The read model's
/// prospect list is every structure the company knows about, and a structure
/// stays on it after being drilled — the projection filters on whether the
/// company <em>knows</em> the prospect, not on whether it has drilled it. So
/// "the best prospect" picked straight off the list is the same structure every
/// time, and an automatic picker drills it over and over.</para>
///
/// <para><b>That behaviour is not obviously wrong.</b> Drilling a second and
/// third well into a structure you have already found is appraisal, and it is
/// what a real company does after a discovery. What is wrong is only that
/// <c>FieldReadModel.Prospects</c> is documented as "every structure the world
/// placed that the company has <b>not drilled</b>", which is not what it
/// returns. That divergence belongs to the engine and is recorded in the plans;
/// the host does not paper over it.</para>
///
/// <para>What the host does instead is remember its own orders, so a picker
/// offering "the best prospect" can offer one that has not been drilled yet and
/// say so. That is the client recalling what it asked for — not a second opinion
/// about engine state.</para>
/// </summary>
public sealed class DrilledSites
{
    private readonly HashSet<ulong> _drilled = new();

    /// <summary>Forget everything: a new run has no history.</summary>
    public void Clear() => _drilled.Clear();

    /// <summary>Note that a hole was ordered into this structure.</summary>
    public void Record(ProspectView prospect)
    {
        ArgumentNullException.ThrowIfNull(prospect);

        _drilled.Add(prospect.Prospect.Value);
    }

    public bool WasDrilled(ProspectView prospect)
    {
        ArgumentNullException.ThrowIfNull(prospect);

        return _drilled.Contains(prospect.Prospect.Value);
    }

    /// <summary>
    /// The most promising structure, preferring one nothing has been sunk into.
    /// </summary>
    /// <remarks>
    /// Falls back to the best overall when every known structure has been
    /// drilled, because at that point another well is an appraisal well and is a
    /// legitimate thing to want. Returns null only when the company knows of no
    /// structures at all.
    /// </remarks>
    public ProspectView? BestUndrilled(FieldReadModel snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ProspectView? fresh = null;
        ProspectView? any = null;

        for (int i = 0; i < snapshot.Prospects.Count; i++)
        {
            ProspectView prospect = snapshot.Prospects[i];

            if (any is null || prospect.ProbabilityOfSuccess > any.ProbabilityOfSuccess)
                any = prospect;

            if (WasDrilled(prospect))
                continue;

            if (fresh is null || prospect.ProbabilityOfSuccess > fresh.ProbabilityOfSuccess)
                fresh = prospect;
        }

        return fresh ?? any;
    }
}
