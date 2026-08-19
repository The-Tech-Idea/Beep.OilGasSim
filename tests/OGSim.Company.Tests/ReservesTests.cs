// R20d.13 — reserves (SDD-009 §4).
//
// The two properties that make a reserves number worth having: it is the volume
// that can be produced PROFITABLY, and it therefore moves when the market does.
// A figure that only counted oil would be an oil-in-place estimate wearing a
// different name.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company.Tests;

public sealed class ReservesTests
{
    private static readonly Density Oil = Density.FromSpecificGravity(0.85);

    private static ArpsReserves Curve(double b = 0.5) =>
        new(declinePerYear: 0.18, exponent: b, recoveryFactor: 0.35);

    private static MassRate Limit(double pricePerTonne, double liftPerTonne = 0.0001) =>
        ArpsReserves.EconomicLimit(
            Money.FromMillions(pricePerTonne),
            Money.FromMillions(liftPerTonne),
            Money.FromMillions(0.3));

    /// <summary>
    /// Reserves are a FRACTION of what is down there, and less than the recovery
    /// factor alone would give, because the tail below the economic limit never
    /// comes out.
    /// </summary>
    [Fact]
    public void R20d13V1_reserves_are_less_than_the_oil_that_is_there()
    {
        const double inPlace = 100.0e6;

        ReservesEstimate booked = Curve().From(
            inPlace, inPlace, inPlace, Limit(0.0004435), Oil);

        Assert.True(booked.Probable.CubicMetres > 0.0, "a good field booked nothing");

        // Below the recovery factor: the truncation costs something.
        Assert.True(booked.Probable.CubicMetres < inPlace * 0.35,
            "reserves equal the full recoverable volume; the economic limit is not biting");
    }

    /// <summary>
    /// THE THREE CASES ARE ORDERED, and in the petroleum convention: 1P is the
    /// low one. Reading them the statistical way round would let a host render a
    /// possible case as a proved one, which is the single most expensive
    /// mistake this vocabulary can make.
    /// </summary>
    [Fact]
    public void R20d13V1_proved_is_the_low_case()
    {
        ReservesEstimate booked = Curve().From(
            60.0e6, 100.0e6, 170.0e6, Limit(0.0004435), Oil);

        Assert.True(booked.Proved.CubicMetres < booked.Probable.CubicMetres);
        Assert.True(booked.Probable.CubicMetres < booked.Possible.CubicMetres);
    }

    /// <summary>
    /// SC6, AND IT NEEDS NO CODE OF ITS OWN. A crash raises the rate at which a
    /// field stops paying, the tail of the decline falls below it, and barrels
    /// beyond that point stop being reserves without having gone anywhere.
    ///
    /// <para>This is the property that makes reserves worth reporting rather
    /// than production: it is the only number on the surface that says a company
    /// got poorer because the market moved.</para>
    /// </summary>
    [Fact]
    public void R20d13V1_a_price_crash_writes_reserves_down()
    {
        const double inPlace = 100.0e6;

        SurfaceVolume rich = Curve().From(
            inPlace, inPlace, inPlace, Limit(0.0004435), Oil).Probable;

        SurfaceVolume poor = Curve().From(
            inPlace, inPlace, inPlace, Limit(0.0004435 / 3.0), Oil).Probable;

        Assert.True(poor.CubicMetres < rich.CubicMetres,
            $"a market at a third of the price booked {poor.CubicMetres:0} against " +
            $"{rich.CubicMetres:0}; the truncation does not move with price");
    }

    /// <summary>
    /// AND A FIELD CAN HAVE NONE. If the opening rate is already below what it
    /// costs to run the place, there is oil down there and no reserves — the
    /// honest and common case for a marginal discovery, and the reason "we found
    /// oil" and "we found a field" are different sentences.
    /// </summary>
    [Fact]
    public void R20d13V1_a_field_that_cannot_pay_has_no_reserves()
    {
        Assert.Equal(0.0, Curve().From(2.0e5, 2.0e5, 2.0e5, Limit(0.0004435), Oil)
                                 .Probable.CubicMetres);

        // And nothing is worth producing at a price below what lifting costs.
        Assert.Equal(0.0, Curve().From(
            100.0e6, 100.0e6, 100.0e6,
            ArpsReserves.EconomicLimit(
                Money.FromMillions(0.00001), Money.FromMillions(0.0001), Money.FromMillions(0.3)),
            Oil).Probable.CubicMetres);
    }

    /// <summary>
    /// The exponential case (b = 0) is the one with an independent closed form —
    /// remaining volume is <c>q_lim / D</c> — so it pins the algebra against a
    /// number computed a different way (rule F-3).
    /// </summary>
    [Fact]
    public void R20d13V1_the_exponential_case_matches_its_own_closed_form()
    {
        const double inPlace = 100.0e6;
        const double recovery = 0.35;
        const double decline = 0.18;

        MassRate limit = Limit(0.0004435);

        double ultimate = inPlace * recovery;
        double limitPerYear = limit.KgPerSecond * 360.0 * 86400.0 / Oil.KgPerCubicMetre;

        // Exponential: EUR = qi/D, and what remains below q_lim is q_lim/D.
        double expected = ultimate - (limitPerYear / decline);

        Assert.Equal(
            expected,
            Curve(b: 0.0).From(inPlace, inPlace, inPlace, limit, Oil).Probable.CubicMetres,
            precision: 3);
    }

    /// <summary>
    /// A curve that never runs out is a content error, not a lucky field.
    /// Refused at construction, because b ≥ 1 makes the integral diverge and the
    /// reserves infinite.
    /// </summary>
    [Fact]
    public void R20d13V1_a_curve_that_never_depletes_is_refused()
    {
        Assert.Throws<ContentFault>(() => Curve(b: 1.0));
        Assert.Throws<ContentFault>(() => new ArpsReserves(0.0, 0.5, 0.35));
        Assert.Throws<ContentFault>(() => new ArpsReserves(0.18, 0.5, 1.5));
    }
}
