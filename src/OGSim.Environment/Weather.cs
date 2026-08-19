// R22.1 — the weather a region has, and the state it carries (SDD-016 §1–§4).
//
// THE CURVES ARE CONTENT AND THE PROCESS IS A PLUGIN. `IWeatherModel` advances a
// standardised x; everything a player experiences — how rough a January is, how
// warm a July — is a curve over that same x, per climate profile. Two curves,
// one x, which is why a hot spell and a calm sea arrive together (SDD-016 §1).
//
// THIRTY DAYS PER TICK, region order then day order. Weather is daily because
// operations lose DAYS (design 13 EV1) and segment boundaries live on the /30ths
// grid (SDD-001 §9) — so "the storm arrived on day 11" is an exact grid fact and
// nothing has to interpolate a month of weather into a day of drilling.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Persistence;

namespace OGSim.Environment;

/// <summary>
/// A climate, as content states it: how persistent its weather is, and the
/// twelve monthly curves severity and temperature are read from.
/// </summary>
public sealed record ClimateProfile(
    ContentId Id,
    double Persistence,
    IReadOnlyList<double> Baseline,
    IReadOnlyList<double> Amplitude,
    IReadOnlyList<double> TemperatureBaseline,
    double TemperatureAmplitude,

    /// <summary>
    /// Which months the site can be REACHED (SDD-016 §3, and §5b's R22.6
    /// amendment) — an ice road that carries a rig only while the ground is
    /// frozen, a coast a monsoon shuts for a season.
    ///
    /// <para><b>Twelve booleans and not a severity threshold</b>, which is the
    /// distinction §3 draws and the one that is easy to lose. A window is a
    /// CALENDAR fact and takes no draw: deriving it from <c>severity &gt; L</c>
    /// would make it a consequence of the dice, and a lucky calm February would
    /// then open an ice road in a thaw.</para>
    /// </summary>
    IReadOnlyList<bool> AccessOpen)
{
    /// <summary>Twelve of everything, checked here rather than at the first
    /// December of a forty-year game.</summary>
    public void Validate()
    {
        Twelve(Baseline, nameof(Baseline));
        Twelve(Amplitude, nameof(Amplitude));
        Twelve(TemperatureBaseline, nameof(TemperatureBaseline));
        TwelveOf(AccessOpen, nameof(AccessOpen));

        // A climate no month can reach is content that has closed the field
        // permanently, which is a state the game reaches by abandonment and
        // never by a calendar.
        var open = 0;
        for (var month = 0; month < MonthsPerYear; month++) if (AccessOpen[month]) open++;

        if (open == 0)
            throw new ContentFault("SDD-016 §3", null,
                $"climate '{Id.Value}' is closed in all twelve months; a site that " +
                "can never be reached cannot be developed, and a permanent closure " +
                "is abandonment rather than a season");

        for (var month = 0; month < MonthsPerYear; month++)
            if (!double.IsFinite(Baseline[month])
                || !double.IsFinite(Amplitude[month])
                || Amplitude[month] < 0.0
                || !double.IsFinite(TemperatureBaseline[month]))
                throw new ContentFault("SDD-016 §1", null,
                    $"climate '{Id.Value}' month {month + 1} carries a non-finite or " +
                    "negative curve value; amplitude is a spread and cannot be negative");

        if (!double.IsFinite(TemperatureAmplitude))
            throw new ContentFault("SDD-016 §3", null,
                $"climate '{Id.Value}' has a non-finite temperature amplitude");
    }

    private void Twelve(IReadOnlyList<double> curve, string named)
    {
        ArgumentNullException.ThrowIfNull(curve);

        if (curve.Count != MonthsPerYear)
            throw new ContentFault("SDD-016 §1", null,
                $"climate '{Id.Value}' declares {curve.Count} months of {named}; a " +
                "seasonal curve has twelve, one per month of the 30/360 year");
    }

    private void TwelveOf(IReadOnlyList<bool> window, string named)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.Count != MonthsPerYear)
            throw new ContentFault("SDD-016 §3", null,
                $"climate '{Id.Value}' declares {window.Count} months of {named}; a " +
                "window is a season and the year has twelve");
    }

    internal const int MonthsPerYear = 12;
}

/// <summary>The analytic predictive distribution of the process (SDD-016 §4).</summary>
public readonly record struct Forecast(double Expected, double Sigma);

/// <summary>
/// Where every region's weather stands, and the thirty days it just had.
/// </summary>
public sealed class WeatherState : IStateOwner
{
    private readonly ClimateProfile[] _climates;

    // x(d) for each region's last thirty days: severity and temperature are both
    // curves over these, so one array serves both and they cannot disagree.
    private readonly double[] _daily;

    // What the next tick's first day builds on. Held separately from the daily
    // array because it is the only value that CROSSES a tick — and therefore the
    // only one a save has to carry (SDD-013 §4).
    private readonly double[] _carry;

    private int _month = 1;

    public WeatherState(IReadOnlyList<ClimateProfile> climates)
    {
        ArgumentNullException.ThrowIfNull(climates);

        if (climates.Count == 0)
            throw new ContentFault("SDD-016 §1", null,
                "a world has at least one climate region; with none, every location " +
                "would map to no weather and §3's day-lost rule would silently never fire");

        _climates = new ClimateProfile[climates.Count];

        for (int i = 0; i < climates.Count; i++)
        {
            climates[i].Validate();
            _climates[i] = climates[i];
        }

        _daily = new double[climates.Count * (int)Duration.DaysPerTick];
        _carry = new double[climates.Count];
    }

    public int Regions => _climates.Length;

    /// <summary>
    /// One month of weather: thirty states per region, drawn in REGION ORDER then
    /// day order so that adding a region cannot shift an existing one's position
    /// in the stream (SDD-016 §6's EN8).
    /// </summary>
    public void Advance(GameDate month, IWeatherModel model, IRandomStream weather)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(weather);

        _month = month.Month;

        for (var region = 0; region < _climates.Length; region++)
        {
            double x = _carry[region];
            int at = region * (int)Duration.DaysPerTick;

            for (var day = 0; day < (int)Duration.DaysPerTick; day++)
            {
                x = model.NextState(x, weather);
                _daily[at + day] = x;
            }

            _carry[region] = x;
        }
    }

    /// <summary>How rough day <paramref name="day"/> was (SDD-016 §1).</summary>
    public double SeverityOn(int region, int day)
    {
        int at = Index(region, day);
        int month = _month - 1;

        return _climates[region].Baseline[month]
             + (_climates[region].Amplitude[month] * _daily[at]);
    }

    /// <summary>
    /// Ambient temperature on the same day, from the SAME x — which is what makes
    /// a hot spell and a calm correlate without either curve knowing about the
    /// other (SDD-016 §3).
    /// </summary>
    public Temperature TemperatureOn(int region, int day)
    {
        int at = Index(region, day);
        int month = _month - 1;

        return Temperature.FromCelsius(
            _climates[region].TemperatureBaseline[month]
            + (_climates[region].TemperatureAmplitude * _daily[at]));
    }

    /// <summary>
    /// Whether the site can be reached this month (SDD-016 §5b's R22.6
    /// amendment).
    ///
    /// <para>Reads the CALENDAR and nothing else — not the daily severities, not
    /// the carry, and no draw. So it answers the same for a given month of a
    /// given region in every game, which is what lets a player plan against it a
    /// year ahead.</para>
    /// </summary>
    public bool AccessOpenIn(int region, GameDate month)
    {
        return _climates[Region(region)].AccessOpen[month.Month - 1];
    }

    /// <summary>
    /// How many months are left before the window shuts — 0 when it is already
    /// shut, and the count INCLUDING this one when it is open.
    ///
    /// <para>This is the question a player actually has (§5b's R22.6 amendment).
    /// A window gates STARTING, so the decision is a deadline: the work has to
    /// be committed before the road closes, and a month spent deciding can cost a
    /// year. "Is it open?" is answerable from the calendar by anyone; "how long
    /// have I got?" is what turns it into a decision.</para>
    ///
    /// <para>Bounded by the twelve months of the year: a window that never shuts
    /// answers twelve rather than looping, because a year of notice and a
    /// permanent opening are the same thing to anyone planning a job.</para>
    /// </summary>
    public int MonthsUntilAccessCloses(int region, GameDate from)
    {
        IReadOnlyList<bool> window = _climates[Region(region)].AccessOpen;

        int start = from.Month - 1;

        if (!window[start]) return 0;

        for (var ahead = 1; ahead < ClimateProfile.MonthsPerYear; ahead++)
            if (!window[(start + ahead) % ClimateProfile.MonthsPerYear]) return ahead;

        return ClimateProfile.MonthsPerYear;
    }

    /// <summary>
    /// Days this month too rough to work (SDD-016 §3). This is the whole of
    /// weather's coupling to operations: the count is STANDBY days, which
    /// <c>Operation.Advance</c> already prices as cost without progress.
    /// </summary>
    public int DaysAbove(int region, double limit)
    {
        if (!double.IsFinite(limit))
            throw new ModelFault("SDD-016 §3", null,
                "a weather limit is a finite severity; a non-finite one would make " +
                "every day either lost or safe with nothing in between");

        var lost = 0;

        for (var day = 0; day < (int)Duration.DaysPerTick; day++)
            if (SeverityOn(region, day) > limit) lost++;

        return lost;
    }

    /// <summary>
    /// The forecast, which is a THEOREM about the generator rather than a second
    /// model (SDD-016 §4): E[x(d+h)] = ρ^h·x(d) and σ(h) = sqrt(1 − ρ^(2h)).
    ///
    /// <para>Consumes no draws, so looking at the forecast can never change the
    /// weather — and its honesty is structural: the error grows with horizon
    /// because ρ^h falls, not because anything was tuned to make it.</para>
    /// </summary>
    public Forecast Look(int region, int horizonDays)
    {
        if (horizonDays < 1)
            throw new ModelFault("SDD-016 §4", null,
                "a forecast horizon is at least one day ahead; zero days ahead is " +
                "today, which is an observation rather than a forecast");

        double rho = _climates[Region(region)].Persistence;

        // Repeated multiplication rather than a power: the horizon is a whole
        // number of days, and D-2 keeps transcendentals out of simulation code
        // where an exact form exists.
        double decay = 1.0;
        for (var h = 0; h < horizonDays; h++) decay *= rho;

        return new Forecast(
            decay * _carry[Region(region)],
            DetMath.Sqrt(1.0 - (decay * decay)));
    }

    private int Index(int region, int day)
    {
        if (day < 0 || day >= (int)Duration.DaysPerTick)
            throw new ModelFault("SDD-016 §1", null,
                $"day {day} is outside the month; the /30 grid has days 0 to 29");

        return (Region(region) * (int)Duration.DaysPerTick) + day;
    }

    private int Region(int region) =>
        region >= 0 && region < _climates.Length
            ? region
            : throw new ModelFault("SDD-016 §1", null,
                $"region {region} does not exist; the world declares {_climates.Length}");

    // ------------------------------------------------------- SDD-013 §4

    public StateKey Key { get; } = new("environment.weather");

    public int SchemaVersion => 1;

    /// <summary>Nothing: a carry per region depends on no other owner.</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    /// <summary>
    /// THE CARRY, AND ONLY THE CARRY. The thirty daily states are regenerated by
    /// the next `Advance` from this value and the restored stream position, so
    /// writing them would store a value beside the inputs that produce it.
    ///
    /// <para>The carry itself is not derivable from anything else: a reload that
    /// resumed from zero would restart every region at its seasonal mean, and the
    /// weather would visibly calm at the moment a game was loaded. That is
    /// finding 192's lesson in a new place — the right dice from the wrong
    /// place.</para>
    /// </summary>
    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("month", _month);
        writer.WriteInt64("regions", _carry.Length);

        for (var region = 0; region < _carry.Length; region++)
            writer.WriteDouble(
                "x" + region.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _carry[region]);
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _month = (int)reader.ReadInt64("month");

        var regions = (int)reader.ReadInt64("regions");

        if (regions != _carry.Length)
            throw new ContentFault("SDD-016 §1", null,
                $"the save carries {regions} weather regions and this world composed " +
                $"{_carry.Length}; the climate content has changed under the save");

        for (var region = 0; region < _carry.Length; region++)
            _carry[region] = reader.ReadDouble(
                "x" + region.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
