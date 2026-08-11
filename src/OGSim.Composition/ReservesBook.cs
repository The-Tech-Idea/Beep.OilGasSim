// R20d.14 — where reserves are worked out (SDD-009 §4, §2).
//
// TWO THINGS READ THEM and neither may compute its own. The read model reports
// what the company can still get out; the ledger accrues the abandonment
// provision per barrel against what the field will ultimately give. A month in
// which those two disagreed about the size of the field would post a provision
// against a number nobody was shown (law L5).
//
// COMPUTED, NEVER STORED. Reserves follow entirely from what the company
// believes, what the market pays and what lifting costs — so a stored figure
// would be a second owner of a derived fact, and it would go stale in exactly
// the month it mattered, which is the month the price moved.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>SDD-009 §4's reserves, restated on demand.</summary>
public sealed class ReservesBook
{
    private readonly IBeliefStore _beliefs;
    private readonly OGSim.Company.MarketState _market;
    private readonly OGSim.Company.ArpsReserves _curve;

    public ReservesBook(
        IBeliefStore beliefs,
        OGSim.Company.MarketState market,
        OGSim.Company.ArpsReserves curve)
    {
        ArgumentNullException.ThrowIfNull(beliefs);
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(curve);

        _beliefs = beliefs;
        _market = market;
        _curve = curve;
    }

    /// <summary>
    /// What the company will EVER get out of what it has found, at today's
    /// prices — before anything it has already produced is taken off.
    ///
    /// <para>This is the denominator the provision accrues against (SDD-009 §2):
    /// a field that produces its ultimate recovery accrues exactly its
    /// abandonment cost, which is the property that makes the accrual telescope
    /// rather than drift.</para>
    /// </summary>
    public ReservesEstimate Ultimate()
    {
        MassRate limit = OGSim.Company.ArpsReserves.EconomicLimit(
            _market.OilPrice,
            Defaults.Economics.LiftingCostPerTonne,
            Defaults.Economics.FixedOperatingCostPerTick);

        SurfaceVolume proved = default, probable = default, possible = default;

        // Walked in learning order (D-5): two runs of one save must book the
        // same reserves.
        IReadOnlyList<HeldBelief> held = _beliefs.Held;

        for (int i = 0; i < held.Count; i++)
        {
            HeldBelief entry = held[i];

            if (entry.Subject.Kind != EntityKind.Compartment) continue;
            if (entry.PropertyKind != Defaults.OilInPlaceKind) continue;

            ReservesEstimate field = _curve.From(
                OGSim.Information.Quantiles.P90(entry.Belief),
                OGSim.Information.Quantiles.P50(entry.Belief),
                OGSim.Information.Quantiles.P10(entry.Belief),
                limit,
                Defaults.SurfaceOilDensity);

            proved = new SurfaceVolume(proved.CubicMetres + field.Proved.CubicMetres);
            probable = new SurfaceVolume(probable.CubicMetres + field.Probable.CubicMetres);
            possible = new SurfaceVolume(possible.CubicMetres + field.Possible.CubicMetres);
        }

        return new ReservesEstimate(proved, probable, possible);
    }

    /// <summary>
    /// What is LEFT — the ultimate figure less what has already been lifted,
    /// floored at nothing.
    ///
    /// <para>The number a player is deciding against, and the one that falls as
    /// a field produces. Reporting the ultimate figure instead would leave a
    /// company's reserves unchanged on the day its last barrel came up.</para>
    /// </summary>
    public ReservesEstimate Remaining(SurfaceVolume produced)
    {
        ReservesEstimate ultimate = Ultimate();

        return new ReservesEstimate(
            Less(ultimate.Proved, produced),
            Less(ultimate.Probable, produced),
            Less(ultimate.Possible, produced));
    }

    private static SurfaceVolume Less(SurfaceVolume from, SurfaceVolume produced) =>
        new(Math.Max(0.0, from.CubicMetres - produced.CubicMetres));
}
