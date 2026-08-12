// SDD-012 §5 — the souring curve, on its own.
//
// What a unit suite can say about this and no more (finding 184's lesson in the
// other direction): that the shape is right and that bad content is refused.
// Whether souring MATTERS is a question about a forty-year field and is asked
// where it can be answered — R20d25V1 and V2 in the composition suite.

using OGSim.Contracts;
using OGSim.Integrity;
using OGSim.Kernel;

namespace OGSim.Integrity.Tests;

public sealed class SouringTests
{
    private static readonly ContentId Rock = new("sandstone-e1");

    private static SaturatingSourCurve Curve() =>
        new(new ContentId("sour-curve-test"), ultimatePpm: 2_000.0, halfRatio: 0.25);

    /// <summary>
    /// MX: the curve is what §5 says it is. Independently computed —
    /// 2000 · 0.25/(0.25 + 0.25) is exactly half the ultimate at the half-ratio,
    /// which is what "half-ratio" means and is the one point on the curve that
    /// can be checked without redoing its arithmetic (rule F-3).
    /// </summary>
    [Fact]
    public void R20d25V3_the_half_ratio_is_where_the_curve_is_half_way_up()
    {
        Assert.Equal(1_000.0, Curve().HydrogenSulphidePpm(Rock, 0.25), precision: 9);
    }

    /// <summary>
    /// MONOTONIC, which §5 pins in as many words: water already injected cannot
    /// un-sour a reservoir. Walked across the band a real flood covers.
    /// </summary>
    [Fact]
    public void R20d25V4_the_curve_never_falls()
    {
        SaturatingSourCurve curve = Curve();
        var previous = -1.0;

        for (var step = 0; step <= 100; step++)
        {
            double ppm = curve.HydrogenSulphidePpm(Rock, step / 100.0);

            Assert.True(ppm >= previous,
                $"the curve fell at a throughput of {step / 100.0} pore volumes");

            previous = ppm;
        }
    }

    /// <summary>
    /// AND IT SATURATES. A linear curve would put a long-flooded field at tens
    /// of thousands of ppm, which is not a reservoir — there is a finite amount
    /// of the rock's sulphate chemistry to work through, and the concentration
    /// flattens once it has been.
    /// </summary>
    [Fact]
    public void R20d25V5_the_curve_levels_off_instead_of_climbing_for_ever()
    {
        SaturatingSourCurve curve = Curve();

        Assert.True(curve.HydrogenSulphidePpm(Rock, 100.0) < 2_000.0,
            "the curve passed its own ultimate, so it is not saturating");

        // A hundred pore volumes is far past any flood, and it is still within a
        // per cent of the ultimate rather than beyond it.
        Assert.True(curve.HydrogenSulphidePpm(Rock, 100.0) > 1_980.0,
            "the curve has not approached its ultimate at a hundred pore volumes");
    }

    /// <summary>
    /// A SWEET FIELD IS SWEET, exactly. Not nearly: a field nobody has flooded
    /// has taken no sea water, and a curve that returned a whisker above zero
    /// would put every reservoir in the game into mildly sour service for ever.
    /// </summary>
    [Fact]
    public void R20d25V6_a_field_that_took_no_seawater_is_exactly_sweet()
    {
        Assert.Equal(0.0, Curve().HydrogenSulphidePpm(Rock, 0.0));
    }

    /// <summary>
    /// Content that would make the curve meaningless is refused where it is
    /// still cheap to say so. A half-ratio of zero is a STEP — the field is
    /// fully sour on its first cubic metre — which is not a fast curve but the
    /// absence of one, and it would delete the whole long-arc point of §5.
    /// </summary>
    [Fact]
    public void R20d25V7_content_that_is_not_a_curve_is_refused()
    {
        Assert.Throws<ContentFault>(() =>
            new SaturatingSourCurve(new ContentId("step"), ultimatePpm: 2_000.0, halfRatio: 0.0));

        Assert.Throws<ContentFault>(() =>
            new SaturatingSourCurve(new ContentId("flat"), ultimatePpm: 0.0, halfRatio: 0.25));
    }
}
