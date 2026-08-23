// R20d.15 — borrowing against reserves (SDD-009 §5).
//
// HOW AN OIL COMPANY ACTUALLY FUNDS A DEVELOPMENT. Not out of cash flow — the
// field that will pay for it does not produce until it is built. A bank lends
// against the oil in the ground, and the amount is what that oil is worth today
// after everything it will cost to get out and everyone who is paid before the
// lender.
//
// `IReserveBasedLending` was declared at finding 147 and implemented by nobody,
// which had a good reason until R20d.13: there were no reserves to lend against.
// There are now.
//
// THE BANK NEVER CALLS INSTANTLY (SDD-009 §5's pin). A breach opens a cure
// window, and the window is the player's warning — a loan that was called the
// month reserves were written down would make a price crash an instant loss
// rather than a thing to survive, and surviving it is the game.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>
/// SDD-009 §5's borrowing base and its covenant.
/// </summary>
public sealed class ReserveBasedLending : IReserveBasedLending
{
    private readonly double _advanceRate;
    private readonly double _discountPerYear;
    private readonly int _years;
    private readonly double _baseRate;
    private readonly double _esgSpreadAtWorst;
    private readonly ArpsReserves _curve;
    private readonly Func<Money> _netback;

    public ReserveBasedLending(
        double advanceRate,
        double discountPerYear,
        int years,
        double baseRate,
        double esgSpreadAtWorst,
        ArpsReserves curve,

        /// <summary>
        /// What a cubic metre is worth to the lender after everything paid
        /// before it. ASKED rather than held, because it moves with the market
        /// — a borrowing base computed against last year's netback would be the
        /// bank lending on a price that has gone.
        /// </summary>
        Func<Money> netback)
    {
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentNullException.ThrowIfNull(netback);

        // STRICTLY BELOW ONE. A bank that advanced the full present value would
        // have no margin for the reserves being wrong, and reserves are an
        // estimate by construction — the advance rate IS that margin.
        if (advanceRate is <= 0.0 or >= 1.0)
            throw new ContentFault("SDD-009 §5", null,
                "an advance rate is a fraction in (0, 1); at one the bank carries the whole " +
                "of the estimate being wrong");

        if (discountPerYear is <= 0.0 or >= 1.0)
            throw new ContentFault("SDD-009 §5", null,
                "a discount rate is a fraction in (0, 1); at zero a barrel in twenty years " +
                "is worth a barrel today and no development is ever urgent");

        if (years <= 0)
            throw new ContentFault("SDD-009 §5", null,
                "a lending horizon is a positive number of years");

        if (baseRate < 0.0 || !double.IsFinite(baseRate))
            throw new ContentFault("SDD-009 §5", null, "a base rate is finite and non-negative");

        if (esgSpreadAtWorst < 0.0 || !double.IsFinite(esgSpreadAtWorst))
            throw new ContentFault("SDD-009 §5", null, "an ESG spread is finite and non-negative");

        _advanceRate = advanceRate;
        _discountPerYear = discountPerYear;
        _years = years;
        _baseRate = baseRate;
        _esgSpreadAtWorst = esgSpreadAtWorst;
        _curve = curve;
        _netback = netback;
    }

    public ContentId Id { get; } = new("reserve-based-lending");

    /// <summary>
    /// What the bank will lend, and at what price (SDD-009 §5).
    ///
    /// <para>Against 1P — the PROVED case, not the expected one. A lender books
    /// the number it is confident of rather than the number the company hopes
    /// for, which is why a field can be worth developing and still not be worth
    /// lending much against.</para>
    /// </summary>
    public BorrowingTerms Redetermine(SurfaceVolume provedReserves, Money debt, double esgStanding)
    {
        if (esgStanding is < 0.0 or > 1.0)
            throw new ModelFault("SDD-009 §5", null,
                "ESG standing is a fraction in [0, 1]; it scales a spread and a value outside " +
                "it would price a loan off a scale nobody defined");

        // A WORSE STANDING IS A DEARER LOAN, carried separately from the base
        // rate so a company can see WHY its debt got more expensive. A number
        // that only appeared inside the total would be a penalty nobody could
        // act on.
        double spread = _esgSpreadAtWorst * (1.0 - esgStanding);

        // ONE WALK, TWO ROUNDINGS (SDD-009 §5's finding-262 amendment) — never
        // a rounded ReserveValue scaled again into BorrowingBase, which would
        // compound two roundings into one and could move BorrowingBase by a
        // cent against every test already pinned to it.
        double raw = PresentValueCents(provedReserves);
        Money reserveValue = Money.RoundHalfEven(raw);
        Money borrowingBase = Money.RoundHalfEven(raw * _advanceRate);

        return new BorrowingTerms(borrowingBase, reserveValue, _baseRate + spread, spread);
    }

    /// <summary>
    /// The present value of the proved case, in cents and BEFORE the
    /// advance-rate haircut: each year's production from the decline curve, at
    /// today's netback, discounted back.
    ///
    /// <para>The curve is what makes this a PV rather than a multiple. Reserves
    /// that arrive over twenty years are worth materially less than the same
    /// volume over five, which is the difference between a shallow decline and a
    /// steep one and exactly what a bank is paid to notice.</para>
    /// </summary>
    private double PresentValueCents(SurfaceVolume proved)
    {
        Money netback = _netback();

        if (proved.CubicMetres <= 0.0 || netback <= Money.Zero) return 0.0;

        double remaining = proved.CubicMetres;
        double value = 0.0;
        double discount = 1.0;

        for (int year = 1; year <= _years && remaining > 0.0; year++)
        {
            discount /= 1.0 + _discountPerYear;

            double produced = _curve.ProducedInYear(proved.CubicMetres, year);

            if (produced > remaining) produced = remaining;

            remaining -= produced;
            value += produced * netback.Cents * discount;
        }

        return value;
    }

    /// <summary>
    /// SDD-009 §5's covenant, and its whole point is the CURE WINDOW. Debt above
    /// the borrowing base does not call the loan; it starts a clock, and the
    /// clock is the player's warning.
    ///
    /// <para>A bank that called instantly would make a price crash an immediate
    /// loss rather than something to survive — and surviving it, by shutting in,
    /// selling down or simply waiting for the market, is the decision the whole
    /// mechanism exists to create.</para>
    /// </summary>
    public CovenantStatus Assess(BorrowingTerms terms, Money debt, CovenantStatus previous)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(previous);

        // BACK INSIDE. Curing ends the moment the breach does, whether the
        // company paid debt down or the market lifted the base back over it —
        // the covenant tests a ratio, and it does not care which side moved.
        if (debt <= terms.BorrowingBase) return new CovenantStatus(CovenantState.Clear, 0);

        return previous.State switch
        {
            CovenantState.Clear => new CovenantStatus(CovenantState.Curing, CureWindowTicks),

            // The clock runs whether or not anything is done about it.
            CovenantState.Curing when previous.TicksRemaining > 1 =>
                new CovenantStatus(CovenantState.Curing, previous.TicksRemaining - 1),

            // Out of time. Amortising is not a call either — it is a schedule,
            // and a company that starts producing its way out of it recovers.
            _ => new CovenantStatus(CovenantState.Amortising, 0),
        };
    }

    /// <summary>
    /// Six months to cure. Long enough to shut in a bad well, sell something or
    /// wait out a short dip; short enough that a company cannot simply ignore
    /// it (SDD-009 §5's IR-consistent pin).
    /// </summary>
    private const int CureWindowTicks = 6;
}
