// R20d.16 — a company's record, and what it costs (SDD-012 §4, design 08 §5).
//
// THE SLOWEST LOOP IN THE GAME (08 §5's own words). Nothing a company does about
// its record pays back this quarter; it pays back in the rate it borrows at, for
// years, and a decade of running clean genuinely rehabilitates because the
// intensity is a rate rather than a tally.
//
// FLARING IS THE ONE TERM WITH A SUBJECT TODAY. The separator's gas leg goes to
// the flare, and a field with no gas handling burns everything that comes up
// with the oil. Emissions and methane need equipment that vents rather than
// burns; incident points need incidents, which is R18's. The formula is not
// truncated — the missing terms contribute nothing because nothing has happened
// yet, which is the correct answer rather than a placeholder for one.
//
// PER UNIT PRODUCED, which is what makes this a record rather than a size. A
// large field flares more gas than a small one in absolute terms and may be the
// better-run of the two; a lender charging it more for that would be pricing
// scale instead of behaviour.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>SDD-012 §4's ESG standing, as a fraction.</summary>
public sealed class EsgRecord
{
    private readonly double _cleanIntensity;
    private readonly double _worstIntensity;

    /// <summary>
    /// The band edges, in kilograms flared per cubic metre produced.
    ///
    /// <para>Below <paramref name="cleanIntensity"/> a company is doing what a
    /// well-run field does — some flaring is unavoidable, and a scale where
    /// perfection was the only clean score would make the whole measure
    /// unreachable and therefore ignorable. Above <paramref
    /// name="worstIntensity"/> there is nothing further to lose, which matters:
    /// a penalty with no floor would keep punishing a company that had already
    /// stopped being able to respond to it.</para>
    /// </summary>
    public EsgRecord(double cleanIntensity, double worstIntensity)
    {
        if (cleanIntensity < 0.0 || !double.IsFinite(cleanIntensity))
            throw new ContentFault("SDD-012 §4", null,
                "a clean flaring intensity is finite and non-negative");

        if (worstIntensity <= cleanIntensity)
            throw new ContentFault("SDD-012 §4", null,
                "the worst band must sit above the clean one; equal edges would make the " +
                "scale a step and every company either spotless or ruined");

        _cleanIntensity = cleanIntensity;
        _worstIntensity = worstIntensity;
    }

    /// <summary>
    /// Standing from what has been flared against what has been produced.
    ///
    /// <para>ONE over the company's whole history rather than this month's: a
    /// record is what a lender has watched, and a single bad month should not
    /// re-price a facility any more than a single good one should clear it.
    /// That is also what makes the loop slow enough to be strategic — a company
    /// that cleans up waits years to be believed.</para>
    /// </summary>
    public double Standing(Mass flared, SurfaceVolume produced)
    {
        // NOTHING PRODUCED, NOTHING TO JUDGE. A company that has not started is
        // spotless rather than unrated — an unrated one would pay the worst
        // spread on its first loan, which is the loan it most needs.
        if (produced.CubicMetres <= 0.0) return Spotless;

        double intensity = flared.Kilograms / produced.CubicMetres;

        if (intensity <= _cleanIntensity) return Spotless;
        if (intensity >= _worstIntensity) return Ruined;

        // Linear between the edges. A band table is what SDD-012 §4 specifies and
        // what content will carry; a straight line between two edges is the same
        // shape with two bands, and it moves smoothly rather than lurching when a
        // company crosses a boundary by a kilogram.
        return Spotless
             - ((intensity - _cleanIntensity) / (_worstIntensity - _cleanIntensity));
    }

    private const double Spotless = 1.0;

    private const double Ruined = 0.0;
}
