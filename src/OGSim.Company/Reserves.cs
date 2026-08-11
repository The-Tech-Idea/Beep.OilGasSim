// R20d.13 — reserves (SDD-009 §4).
//
// WHAT AN OIL COMPANY IS ACTUALLY VALUED ON. Not its cash, not its production —
// what it can still get out of the ground and sell at a profit. A game that
// reported the first two and not this would let a player run a field to
// exhaustion and see the balance sheet improve every month right up to the end.
//
// THE SOLVER NEVER RUNS IN HERE (SDD-009 §4's own words, and the trap that line
// exists to forbid). Reserves are re-stated quarterly and after every material
// event; a full-field re-simulation each time would cost more than the game
// does. A decline curve is what a reserves auditor actually uses, and it is a
// closed form.
//
// AND IT MOVES WITH THE MARKET FOR FREE. Reserves stop where production stops
// paying, so the economic limit is a rate — and that rate depends on the oil
// price and on what it costs to lift a tonne. A crash raises the limit, the tail
// of the curve falls below it, and the reserves are written down without a line
// of code that mentions a crash (SDD-009 §4's SC6).

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>
/// SDD-009 §4's Arps decline, truncated at the economic limit.
///
/// <para><c>q(t) = qi · (1 + b·D·t)^(−1/b)</c> — the hyperbolic form, which is
/// what a type-curve is quoted as. The initial rate is not content: it FOLLOWS
/// from the recoverable volume and the decline, because a curve and a volume
/// that disagreed would be two owners of one fact (law L5).</para>
/// </summary>
public sealed class ArpsReserves
{
    private readonly double _decline;      // D, per year
    private readonly double _exponent;     // b, dimensionless
    private readonly double _recovery;     // what fraction of oil in place comes out

    public ArpsReserves(double declinePerYear, double exponent, double recoveryFactor)
    {
        if (declinePerYear is <= 0.0 or >= 1.0)
            throw new ContentFault("SDD-009 §4", null,
                "an annual decline is a fraction in (0, 1); zero would make a field produce " +
                "at its opening rate for ever");

        // STRICTLY BELOW ONE. At b = 1 the harmonic case has infinite reserves —
        // the integral does not converge — and above it the curve is not a
        // decline at all. Refused rather than clamped, because a field with
        // unbounded oil in it is a content error worth being told about.
        if (exponent is < 0.0 or >= 1.0)
            throw new ContentFault("SDD-009 §4", null,
                "the Arps exponent is in [0, 1); at 1 the curve integrates to infinity and " +
                "the field would never run out");

        if (recoveryFactor is <= 0.0 or > 1.0)
            throw new ContentFault("SDD-009 §4", null,
                "a recovery factor is a fraction in (0, 1]; no reservoir gives up all of it, " +
                "and one that gives up none is not a reservoir");

        _decline = declinePerYear;
        _exponent = exponent;
        _recovery = recoveryFactor;
    }

    /// <summary>
    /// The three cases from the company's belief about oil in place.
    ///
    /// <para>P90 is the LOW case and P10 the high — the petroleum convention,
    /// which is the opposite way round from the statistical reading and the
    /// reason a host must never be handed the raw quantiles to name for
    /// itself.</para>
    /// </summary>
    public ReservesEstimate From(
        double oilInPlaceP90,
        double oilInPlaceP50,
        double oilInPlaceP10,
        MassRate economicLimit,
        Density oilDensity)
    {
        return new ReservesEstimate(
            Recoverable(oilInPlaceP90, economicLimit, oilDensity),
            Recoverable(oilInPlaceP50, economicLimit, oilDensity),
            Recoverable(oilInPlaceP10, economicLimit, oilDensity));
    }

    /// <summary>
    /// What comes out before the field stops paying.
    ///
    /// <para>The ultimate recovery is the whole area under the curve; the
    /// economic limit cuts its tail off. Both are closed forms, so this is
    /// arithmetic rather than a simulation — which is what lets it be re-stated
    /// every quarter without the game slowing down.</para>
    /// </summary>
    private SurfaceVolume Recoverable(
        double oilInPlace, MassRate economicLimit, Density oilDensity)
    {
        if (oilInPlace <= 0.0) return new SurfaceVolume(0.0);

        double ultimate = oilInPlace * _recovery;

        // qi follows from the volume: EUR = qi / (D·(1 − b)) for b < 1, so a
        // bigger field starts at a higher rate rather than producing for longer
        // at the same one — which is what a development plan actually does.
        double initial = ultimate * _decline * (1.0 - _exponent);

        double limit = economicLimit.KgPerSecond * SecondsPerYear / oilDensity.KgPerCubicMetre;

        // Already below the limit at day one: nothing here is worth producing,
        // so nothing here is a reserve. A field can be discovered and still have
        // no reserves at today's prices, which is the honest and common case.
        if (limit >= initial) return new SurfaceVolume(0.0);

        // THE TAIL BELOW THE LIMIT, from the same closed form.
        //
        //   Q(t)  = EUR · [1 − (1 + b·D·t)^(1 − 1/b)]
        //   q(t)  = qi · (1 + b·D·t)^(−1/b)
        //
        // Eliminating t between them — (1 + b·D·t) = (qi/q)^b — leaves
        // Q = EUR · [1 − (q/qi)^(1−b)], so what remains beyond the limit rate is
        // EUR · (q_lim/qi)^(1−b). At b = 0 that is the exponential case's
        // q_lim/D, which is the check that the algebra came out right.
        double tail = ultimate * DetMath.Pow(limit / initial, 1.0 - _exponent);

        return new SurfaceVolume(ultimate - tail);
    }

    /// <summary>
    /// What a field gives in one year of its life, from the decline curve
    /// (SDD-009 §4). The difference of the cumulative at each end of the year.
    ///
    /// <para>Used by the lender to discount a volume over the shape it arrives
    /// in (SDD-009 §5): reserves that come over twenty years are worth
    /// materially less than the same volume over five, which is the difference
    /// between a shallow decline and a steep one.</para>
    ///
    /// <para><paramref name="ultimate"/> is the volume the profile integrates
    /// to. A lender passes its BOOKABLE volume rather than the untruncated
    /// recovery — the shape is the field's, the quantity is the one the bank
    /// will actually count.</para>
    /// </summary>
    public double ProducedInYear(double ultimate, int year) =>
        Cumulative(ultimate, year) - Cumulative(ultimate, year - 1);

    private double Cumulative(double ultimate, int year)
    {
        if (year <= 0 || ultimate <= 0.0) return 0.0;

        // The exponential case has its own closed form: the hyperbolic one
        // divides by b, which is zero here.
        if (_exponent == 0.0)
            return ultimate * (1.0 - DetMath.Exp(-_decline * year));

        return ultimate * (1.0 - DetMath.Pow(
            1.0 + (_exponent * _decline * year), 1.0 - (1.0 / _exponent)));
    }

    /// <summary>
    /// The rate below which a month costs more than it earns (SDD-009 §4 step
    /// 3). Fixed costs divided by the margin on a tonne — and if there is no
    /// margin, no rate is high enough and the field is uneconomic at any size.
    /// </summary>
    public static MassRate EconomicLimit(
        Money oilPricePerTonne, Money liftingCostPerTonne, Money fixedCostPerTick)
    {
        Money margin = oilPricePerTonne - liftingCostPerTonne;

        if (margin <= Money.Zero) return new MassRate(double.PositiveInfinity);

        double tonnesPerTick = fixedCostPerTick.Cents / (double)margin.Cents;

        return new MassRate(tonnesPerTick * KilogramsPerTonne / SecondsPerTick);
    }

    private const double SecondsPerYear = 360.0 * 86400.0;

    private const double SecondsPerTick = 30.0 * 86400.0;

    private const double KilogramsPerTonne = 1000.0;
}
