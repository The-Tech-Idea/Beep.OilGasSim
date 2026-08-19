// R20d.12.34 — the reserve replacement ratio (SDD-009 §4, SDD-013 §4, finding 208).
//
// RRR IS DERIVED AND ITS HISTORY IS NOT, which is the whole reason this class
// exists. The ratio is computed from reserves the belief store already carries
// and production the loop already counts, so no block writes the number — but it
// is defined over a TRAILING TWELVE MONTHS, and what proved reserves stood at a
// year ago is recoverable from nothing else in the save. Beliefs restore as they
// are today; a reloaded company would have no past to measure against.
//
// The pattern that has now appeared twice in one phase: a quantity recomputed
// each tick is derived, and a quantity recomputed each tick FROM ITS OWN PAST is
// not. The covenant clock was the first (finding 210).
//
// A ring rather than a growing list, on the shape `company.market` already uses:
// a forty-year game is 480 ticks and nothing here needs the other 468.

using OGSim.Kernel;
using OGSim.Persistence;

namespace OGSim.Composition;

/// <summary>
/// What proved reserves and cumulative production stood at, at each of the last
/// twelve month-ends.
/// </summary>
public sealed class ReserveHistory : IStateOwner
{
    // Twelve month-ends, so a trailing year is the OLDEST entry rather than a
    // thirteenth slot: with twelve recorded, index `_months % MonthsPerYear` is
    // simultaneously where the next one goes and where the year-ago one is.
    private readonly double[] _proved = new double[MonthsPerYear];
    private readonly double[] _produced = new double[MonthsPerYear];

    private int _months;

    /// <summary>
    /// Record where the company stood at the close of a month.
    /// </summary>
    public void Record(SurfaceVolume proved, SurfaceVolume cumulativeProduced)
    {
        int at = _months % MonthsPerYear;

        _proved[at] = proved.CubicMetres;
        _produced[at] = cumulativeProduced.CubicMetres;

        _months++;
    }

    /// <summary>
    /// Additions over the trailing twelve months against what was produced in
    /// them (SDD-009 §4).
    ///
    /// <para>NULL RATHER THAN A NUMBER in the two cases where the ratio is not
    /// defined, because both would otherwise be reported as a result the company
    /// achieved. Under twelve months of history there is no window to measure —
    /// a company nine months old has not failed to replace anything. And a
    /// denominator of zero is the company not having produced, so 0.0 would state
    /// a replacement failure that did not happen and 1.0 would state a success.
    /// A host shows "—" (SDD-009 §4's R20d.12.34 amendment).</para>
    /// </summary>
    public double? Ratio(SurfaceVolume proved, SurfaceVolume cumulativeProduced)
    {
        if (_months < MonthsPerYear) return null;

        int oldest = _months % MonthsPerYear;

        double producedBetween = cumulativeProduced.CubicMetres - _produced[oldest];

        if (producedBetween <= 0.0) return null;

        // NOTHING BOOKED AT EITHER END IS NOT A REPLACEMENT OF ONE. With proved
        // at zero throughout, additions equal production identically and the
        // ratio comes out at exactly 1.0 — the healthiest reading there is,
        // reported for a company with no reserves at all, which is the position
        // IR2 exists to warn about.
        //
        // The state is impossible in the world (nothing produces barrels it
        // never booked) and reachable here, because a field developed without
        // the discovery path never puts an oil-in-place belief in the store. So
        // the honest answer is the one the null already means: not measured.
        if (proved.CubicMetres <= 0.0 && _proved[oldest] <= 0.0) return null;

        return (proved.CubicMetres - _proved[oldest] + producedBetween) / producedBetween;
    }

    // ------------------------------------------------------- SDD-013 §4

    public StateKey Key { get; } = new("company.reserve-history");

    public int SchemaVersion => 1;

    /// <summary>Nothing: two rings of doubles that depend on no other owner. The
    /// reserves they record are a MEASUREMENT already taken, not a reading of the
    /// belief store, so this does not wait on beliefs being back.</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("months", _months);

        for (var i = 0; i < MonthsPerYear; i++)
        {
            writer.WriteDouble("proved" + Slot(i), _proved[i]);
            writer.WriteDouble("produced" + Slot(i), _produced[i]);
        }
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _months = (int)reader.ReadInt64("months");

        for (var i = 0; i < MonthsPerYear; i++)
        {
            _proved[i] = reader.ReadDouble("proved" + Slot(i));
            _produced[i] = reader.ReadDouble("produced" + Slot(i));
        }
    }

    private static string Slot(int i) =>
        i.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private const int MonthsPerYear = 12;
}
