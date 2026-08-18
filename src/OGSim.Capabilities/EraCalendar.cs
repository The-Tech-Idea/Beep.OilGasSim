// R20d.10 — when an era starts (SDD-005 §2's R20d.10 amendment).
//
// §2 prices diffusion at `era start + diffusionLag(node)` and no document said
// when an era starts, so the one input that formula needs had no source and the
// whole technology arc was inert: `CapabilityState.Era` was written only by its
// constructor, a 1965-to-2005 campaign stayed in E1 throughout, and every
// `availableFromEra` in the catalogue gated nothing (finding 191).
//
// The boundaries are TECH_TREE's own — "E1 1950s–60s · E2 70s–80s · E3 90s–2000s
// · E4 2010s+" — transcribed rather than chosen, because the registry that
// assigns each node an era is the document entitled to say when those eras are.

using OGSim.Kernel;

namespace OGSim.Capabilities;

/// <summary>
/// The four eras against the calendar (SDD-005 §2's R20d.10 amendment).
///
/// <para>A TABLE and not a rule: the boundaries belong to content, and this is
/// the shape they arrive in until R20c.9's remaining half loads them from
/// <c>content/</c> like the facility ladders. Passed as a dependency rather than
/// read from a static, so law L2 holds and a scenario can set a game in a
/// different decade without editing an engine assembly.</para>
/// </summary>
public sealed class EraCalendar
{
    private readonly (Era Era, int FromYear)[] _boundaries;

    public EraCalendar(IReadOnlyList<(Era Era, int FromYear)> boundaries)
    {
        ArgumentNullException.ThrowIfNull(boundaries);

        if (boundaries.Count == 0)
            throw new ContentFault("SDD-005 §2", null,
                "an era calendar with no boundaries cannot answer what era any date " +
                "is in, and every gate reading it would silently pass");

        _boundaries = [.. boundaries];

        // ASCENDING AND STRICT, checked once here rather than trusted at every
        // lookup: `At` walks forward and takes the last boundary it has passed,
        // so an out-of-order table would answer a plausible wrong era instead of
        // failing, and a repeated year would make the answer depend on which row
        // was written first.
        for (var i = 1; i < _boundaries.Length; i++)
            if (_boundaries[i].FromYear <= _boundaries[i - 1].FromYear)
                throw new ContentFault("SDD-005 §2", null,
                    $"era {_boundaries[i].Era} starts in {_boundaries[i].FromYear}, " +
                    $"which is not after {_boundaries[i - 1].Era}'s " +
                    $"{_boundaries[i - 1].FromYear}; eras are a calendar and run forwards");
    }

    /// <summary>
    /// The era a date falls in.
    ///
    /// <para>A date BEFORE the first boundary is that first era rather than a
    /// fault: a scenario set in 1930 is playing the earliest technology the
    /// registry describes, which is the honest answer — there is no era zero, and
    /// refusing would make the calendar decide what dates a game may start on.</para>
    /// </summary>
    public Era At(GameDate date)
    {
        Era found = _boundaries[0].Era;

        for (var i = 0; i < _boundaries.Length; i++)
            if (date.Year >= _boundaries[i].FromYear) found = _boundaries[i].Era;

        return found;
    }

    /// <summary>
    /// The tick an era began, relative to a run's epoch — what
    /// <c>ApplyDiffusion</c> measures its lag from.
    ///
    /// <para>Negative when the era began BEFORE the game did, and that is the
    /// case that matters: a 1965 start is fifteen years into E1, so E1's
    /// diffusion lags are already partly spent and a node whose lag ran out in
    /// 1958 is standard practice on the first morning rather than something the
    /// company waits for.</para>
    /// </summary>
    public Tick StartOf(Era era, GameDate epoch)
    {
        for (var i = 0; i < _boundaries.Length; i++)
            if (_boundaries[i].Era == era)
                return new Tick((_boundaries[i].FromYear - epoch.Year) * MonthsPerYear
                                - (epoch.Month - 1));

        throw new ContentFault("SDD-005 §2", null,
            $"era {era} is not in the calendar; a node assigned to it could never diffuse");
    }

    private const int MonthsPerYear = 12;
}
