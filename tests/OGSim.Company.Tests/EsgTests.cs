// R20d.16 — a company's record (SDD-012 §4).

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company.Tests;

public sealed class EsgTests
{
    private static EsgRecord Record() => new(cleanIntensity: 0.10, worstIntensity: 0.30);

    private static double At(double kgFlaredPerCubicMetre) =>
        Record().Standing(new Mass(kgFlaredPerCubicMetre * 1.0e6), new SurfaceVolume(1.0e6));

    /// <summary>
    /// A RECORD, NOT A TALLY. Intensity is per unit produced, so a big field
    /// that flares more gas in absolute terms than a small one can still be the
    /// better-run of the two — and a lender charging it more for that would be
    /// pricing size instead of behaviour.
    /// </summary>
    [Fact]
    public void R20d16V1_standing_measures_intensity_not_volume()
    {
        // Ten times the field, ten times the flare, same behaviour.
        double small = Record().Standing(new Mass(2.0e5), new SurfaceVolume(1.0e6));
        double large = Record().Standing(new Mass(2.0e6), new SurfaceVolume(1.0e7));

        Assert.Equal(small, large, precision: 12);
    }

    /// <summary>Worse flaring is a worse record, monotonically.</summary>
    [Fact]
    public void R20d16V1_more_flaring_is_a_worse_record()
    {
        Assert.Equal(1.0, At(0.05), precision: 12);      // inside the clean band
        Assert.True(At(0.15) < At(0.12));
        Assert.True(At(0.25) < At(0.15));
        Assert.Equal(0.0, At(0.40), precision: 12);      // nothing further to lose
    }

    /// <summary>
    /// A FLOOR MATTERS. Without one the penalty would keep growing on a company
    /// that had already lost everything it could lose by it — and a cost that
    /// cannot be responded to is not a decision, it is a tax on having failed
    /// once.
    /// </summary>
    [Fact]
    public void R20d16V1_a_ruined_record_cannot_get_worse()
    {
        Assert.Equal(At(0.30), At(3.0), precision: 12);
    }

    /// <summary>
    /// A COMPANY THAT HAS NOT STARTED IS SPOTLESS, not unrated. An unrated one
    /// would pay the worst spread on its first loan, which is the loan it most
    /// needs — and it would be paying for a record it has had no chance to
    /// earn.
    /// </summary>
    [Fact]
    public void R20d16V1_a_company_that_has_produced_nothing_is_spotless()
    {
        Assert.Equal(1.0, Record().Standing(new Mass(0.0), new SurfaceVolume(0.0)), precision: 12);
    }

    /// <summary>A scale with no width is a step, and would make every company
    /// either spotless or ruined.</summary>
    [Fact]
    public void R20d16V1_a_scale_with_no_width_is_refused()
    {
        Assert.Throws<ContentFault>(() => new EsgRecord(0.10, 0.10));
        Assert.Throws<ContentFault>(() => new EsgRecord(0.30, 0.10));
    }
}
