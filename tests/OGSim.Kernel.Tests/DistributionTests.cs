// SDD-002 §2b — the distribution family (contract pass 9, finding 82).
// R2-V5 log-normal propagation is the load-bearing one; the P90/P10 ordering is
// the one most likely to be got backwards by a future contributor.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class DistributionTests
{
    [Fact] // SDD-002 §2b: petroleum convention — P90 is the LOW case, P10 the HIGH
    public void P90_is_the_low_estimate_and_P10_the_high_across_every_kind()
    {
        Distribution[] spreads =
        [
            new NormalDistribution(100.0, 15.0),
            new LogNormalDistribution(DetMath.Ln(100.0), 0.5),
            new TriangularDistribution(50.0, 90.0, 200.0),
            new UniformDistribution(50.0, 150.0),
        ];

        foreach (Distribution d in spreads)
        {
            Assert.True(d.P90 < d.P50, $"{d.GetType().Name}: P90 should be the low case");
            Assert.True(d.P50 < d.P10, $"{d.GetType().Name}: P10 should be the high case");
        }
    }

    [Fact] // A point value is a distribution with zero spread, not a special case
    public void a_point_value_has_no_spread()
    {
        var measured = new PointValue(42.0);
        Assert.Equal(42.0, measured.Mean);
        Assert.Equal(42.0, measured.P90);
        Assert.Equal(42.0, measured.P50);
        Assert.Equal(42.0, measured.P10);
    }

    [Fact] // Normal: symmetric about the centre at z = 1.281552
    public void a_normal_distribution_is_symmetric_about_its_centre()
    {
        var normal = new NormalDistribution(100.0, 15.0);
        Assert.Equal(100.0, normal.Mean, 9);
        Assert.Equal(100.0, normal.P50, 9);
        Assert.Equal(100.0 - 1.281552 * 15.0, normal.P90, 9);
        Assert.Equal(100.0 + 1.281552 * 15.0, normal.P10, 9);
        Assert.Equal(normal.P10 - normal.P50, normal.P50 - normal.P90, 9);
    }

    [Fact] // Log-normal is right-skewed — the reason volumes use it (design 05 §1.4)
    public void a_log_normal_distribution_is_right_skewed()
    {
        var volume = new LogNormalDistribution(DetMath.Ln(100.0), 0.6);

        Assert.Equal(100.0, volume.P50, 6);            // median is exp(mu)
        Assert.True(volume.Mean > volume.P50);          // mean exceeds median
        Assert.True(volume.P10 - volume.P50 > volume.P50 - volume.P90,
                    "the upside tail must be the longer one");
    }

    [Fact] // R2-V5: a product of log-normals is log-normal, analytically
    public void R2V5_the_product_of_log_normals_is_log_normal()
    {
        // Volumetrics: area x thickness, both uncertain.
        var area = new LogNormalDistribution(DetMath.Ln(500.0), 0.4);
        var thickness = new LogNormalDistribution(DetMath.Ln(20.0), 0.3);

        LogNormalDistribution product = LogNormalDistribution.Product(area, thickness);

        // Log parameters add; variances add (not standard deviations).
        Assert.Equal(area.LogMean + thickness.LogMean, product.LogMean, 12);
        Assert.Equal(DetMath.Sqrt(0.4 * 0.4 + 0.3 * 0.3), product.LogStandardDeviation, 12);

        // The median of the product is the product of the medians — the property
        // that makes closed-form volumetrics possible instead of sampling.
        Assert.Equal(500.0 * 20.0, product.P50, 6);

        // And the spread genuinely widens rather than averaging.
        Assert.True(product.LogStandardDeviation > area.LogStandardDeviation);
    }

    [Fact] // Triangular quantiles are analytic, so they stay D-1 safe
    public void a_triangular_distribution_quantile_is_analytic_and_bounded()
    {
        var estimate = new TriangularDistribution(50.0, 90.0, 200.0);

        Assert.Equal((50.0 + 90.0 + 200.0) / 3.0, estimate.Mean, 9);
        Assert.InRange(estimate.P90, 50.0, 200.0);
        Assert.InRange(estimate.P50, 50.0, 200.0);
        Assert.InRange(estimate.P10, 50.0, 200.0);
    }

    [Fact] // Uniform: the deciles sit where arithmetic says they do
    public void a_uniform_distribution_has_linear_quantiles()
    {
        var range = new UniformDistribution(50.0, 150.0);
        Assert.Equal(100.0, range.Mean, 9);
        Assert.Equal(60.0, range.P90, 9);
        Assert.Equal(100.0, range.P50, 9);
        Assert.Equal(140.0, range.P10, 9);
    }

    [Fact] // The hierarchy is closed to the five kinds of R2 §2.1
    public void the_distribution_hierarchy_is_closed_to_five_kinds()
    {
        Type[] kinds =
        [
            typeof(PointValue), typeof(NormalDistribution), typeof(LogNormalDistribution),
            typeof(TriangularDistribution), typeof(UniformDistribution),
        ];

        Type[] declared = [.. typeof(Distribution).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Distribution)))];

        Assert.Equal(kinds.Length, declared.Length);
        foreach (Type kind in kinds)
        {
            Assert.Contains(kind, declared);
            Assert.True(kind.IsSealed, $"{kind.Name} must be sealed");
        }
    }
}
