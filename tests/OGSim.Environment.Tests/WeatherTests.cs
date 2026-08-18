// R22.1 — the weather process, measured rather than asserted.
//
// Every check here is against a property of the AR(1) the SDD pins, not against
// a number this suite recorded from a run: the lag-1 correlation IS ρ, the
// stationary variance IS 1, and the forecast IS ρ^h·x. A test that only compared
// today's draws with yesterday's would pass for any process at all.

using OGSim.Contracts;
using OGSim.Environment;
using OGSim.Kernel;
using OGSim.Persistence;

namespace OGSim.Environment.Tests;

public class WeatherTests
{
    private static ClimateProfile Climate(
        double persistence, double amplitude = 1.0, IReadOnlyList<bool>? access = null) =>
        new(new ContentId("north-sea"),
            persistence,
            Baseline: Flat(3.0),
            Amplitude: Flat(amplitude),
            TemperatureBaseline: Flat(8.0),
            TemperatureAmplitude: -2.0,
            AccessOpen: access ?? Open());

    /// <summary>A year a boat can reach in every month.</summary>
    private static IReadOnlyList<bool> Open() =>
        [.. Enumerable.Range(0, 12).Select(_ => true)];

    /// <summary>
    /// An ICE ROAD: reachable only while the ground is frozen. December through
    /// March, which is four months of the twelve and the shape that makes a
    /// window a deadline rather than a tax.
    /// </summary>
    private static IReadOnlyList<bool> IceRoad() =>
        [.. Enumerable.Range(1, 12).Select(month => month <= 3 || month == 12)];

    private static double[] Flat(double value)
    {
        var months = new double[12];
        for (var i = 0; i < months.Length; i++) months[i] = value;

        return months;
    }

    private static IRandomStream Weather(ulong seed = 20260816UL) =>
        new RandomSource(seed).Stream(StreamId.Weather);

    [Fact] // R22-V16. Autocorrelated at the DECLARED strength, which is the whole
           // claim of the model: measured over a long run, the lag-1 correlation
           // of the standardised state is ρ.
    public void R22V16_weather_is_autocorrelated_at_the_declared_strength()
    {
        const double rho = 0.7;

        var model = new Ar1Weather(rho);
        IRandomStream stream = Weather();

        double x = 0.0;
        double sumXy = 0.0, sumX = 0.0, sumY = 0.0, sumXx = 0.0, sumYy = 0.0;
        const int samples = 200_000;

        for (var i = 0; i < samples; i++)
        {
            double next = model.NextState(x, stream);

            sumXy += x * next;
            sumX += x;
            sumY += next;
            sumXx += x * x;
            sumYy += next * next;

            x = next;
        }

        double n = samples;
        double covariance = (sumXy / n) - ((sumX / n) * (sumY / n));
        double sdX = DetMath.Sqrt((sumXx / n) - ((sumX / n) * (sumX / n)));
        double sdY = DetMath.Sqrt((sumYy / n) - ((sumY / n) * (sumY / n)));

        double correlation = covariance / (sdX * sdY);

        Assert.True(Math.Abs(correlation - rho) < 0.01,
            $"declared persistence {rho}, measured lag-1 correlation {correlation}");
    }

    [Fact] // The pair of coefficients is what keeps the process STATIONARY: without
           // sqrt(1 - rho^2) on the innovation the variance would drift, and every
           // content curve written over x would slowly stop meaning what it said.
    public void The_process_holds_unit_variance_however_persistent_it_is()
    {
        foreach (double rho in new[] { 0.0, 0.5, 0.9 })
        {
            var model = new Ar1Weather(rho);
            IRandomStream stream = Weather();

            double x = 0.0, sum = 0.0, sumSquares = 0.0;
            const int samples = 200_000;

            // Burn in, so the measurement is of the stationary distribution rather
            // than of the walk out from a cold start at the mean.
            for (var i = 0; i < 1_000; i++) x = model.NextState(x, stream);

            for (var i = 0; i < samples; i++)
            {
                x = model.NextState(x, stream);
                sum += x;
                sumSquares += x * x;
            }

            double variance = (sumSquares / samples) - ((sum / samples) * (sum / samples));

            Assert.True(Math.Abs(variance - 1.0) < 0.03,
                $"persistence {rho} gave variance {variance}; the process is not stationary N(0,1)");
        }
    }

    [Fact] // A random walk is not weather: it never returns to the season, so the
           // monthly curves would describe nothing after the first year.
    public void A_persistence_of_one_is_refused()
    {
        Assert.Throws<ContentFault>(() => new Ar1Weather(1.0));
        Assert.Throws<ContentFault>(() => new Ar1Weather(-0.1));
    }

    [Fact] // SDD-016 §4 — the forecast is a theorem about the generator, so it is
           // checked against the closed form rather than against a recorded run.
    public void R22V15_the_forecast_is_the_analytic_prediction_and_consumes_no_draws()
    {
        const double rho = 0.8;

        var state = new WeatherState([Climate(rho)]);
        var model = new Ar1Weather(rho);
        IRandomStream stream = Weather();

        state.Advance(new GameDate(2026, 6), model, stream);

        // Two identical looks either side of a third: if forecasting drew from the
        // stream, the second would differ from the first.
        Forecast first = state.Look(region: 0, horizonDays: 3);
        state.Look(region: 0, horizonDays: 7);
        Forecast again = state.Look(region: 0, horizonDays: 3);

        Assert.Equal(first, again);

        // E[x(d+h)] = rho^h * x(d), so successive horizons are in the ratio rho —
        // which pins the form without the carry having to be exposed on the
        // surface for a test's convenience.
        Assert.Equal(rho, state.Look(0, 2).Expected / state.Look(0, 1).Expected, 9);
        Assert.Equal(rho * rho, state.Look(0, 3).Expected / state.Look(0, 1).Expected, 9);

        double decay = rho * rho * rho;

        Assert.Equal(DetMath.Sqrt(1.0 - (decay * decay)), first.Sigma, 9);

        // Honestly degrading: further out is less certain, always.
        Assert.True(state.Look(0, 30).Sigma > state.Look(0, 1).Sigma);
    }

    [Fact] // SDD-016 §3 — the day-lost count IS the coupling to operations, so it
           // is counted against the same severity a reader would see.
    public void Days_above_the_limit_are_the_days_an_operation_stands_by()
    {
        var state = new WeatherState([Climate(0.6, amplitude: 2.0)]);

        state.Advance(new GameDate(2026, 1), new Ar1Weather(0.6), Weather());

        const double limit = 3.0;                 // the flat baseline

        var counted = 0;
        for (var day = 0; day < 30; day++)
            if (state.SeverityOn(0, day) > limit) counted++;

        Assert.Equal(counted, state.DaysAbove(0, limit));

        // A limit nothing can exceed loses no days; one nothing can meet loses all
        // thirty. Both ends stated, because a count that was always zero would
        // satisfy the equality above.
        Assert.Equal(0, state.DaysAbove(0, double.MaxValue / 2.0));
        Assert.Equal(30, state.DaysAbove(0, double.MinValue / 2.0));
    }

    [Fact] // Both curves read the SAME x, which is what makes a hot spell arrive
           // with a calm. A temperature drawn separately would decorrelate them
           // and nothing in the content would say it had.
    public void Severity_and_temperature_are_two_curves_over_one_state()
    {
        var state = new WeatherState([Climate(0.5)]);

        state.Advance(new GameDate(2026, 3), new Ar1Weather(0.5), Weather());

        for (var day = 0; day < 30; day++)
        {
            // Amplitude 1.0 and baseline 3.0 → x = severity - 3; temperature is
            // 8 - 2x, so the two are exactly linearly related through that x.
            double x = state.SeverityOn(0, day) - 3.0;

            Assert.Equal(8.0 + (-2.0 * x), state.TemperatureOn(0, day).ToCelsius(), 9);
        }
    }

    [Fact] // R22-V14. Adding a region must not shift an existing one's position in
           // the stream: the draw order is region then day, so region 0's month is
           // the same thirty draws whether or not a second region exists.
    public void R22V14_a_second_region_does_not_move_the_first_ones_weather()
    {
        var alone = new WeatherState([Climate(0.7)]);
        var beside = new WeatherState([Climate(0.7), Climate(0.4)]);

        alone.Advance(new GameDate(2026, 5), new Ar1Weather(0.7), Weather());
        beside.Advance(new GameDate(2026, 5), new Ar1Weather(0.7), Weather());

        for (var day = 0; day < 30; day++)
            Assert.Equal(alone.SeverityOn(0, day), beside.SeverityOn(0, day), 12);
    }

    [Fact] // The carry is the only value that crosses a tick, and a reload that
           // resumed from zero would restart every region at its seasonal mean —
           // the weather would visibly calm at the moment a game was loaded.
    public void A_restored_region_continues_the_weather_it_was_having()
    {
        var original = new WeatherState([Climate(0.85)]);
        var model = new Ar1Weather(0.85);
        IRandomStream stream = Weather();

        for (var month = 1; month <= 6; month++)
            original.Advance(new GameDate(2026, month), model, stream);

        var restored = new WeatherState([Climate(0.85)]);
        StateBlock.Restore(restored, StateBlock.Capture(original).Written());

        // The next month must agree, which is what "continues" means — comparing
        // the captured carry with itself would pass for a block that wrote a
        // constant.
        IRandomStream one = Weather(7UL);
        IRandomStream two = Weather(7UL);

        original.Advance(new GameDate(2026, 7), model, one);
        restored.Advance(new GameDate(2026, 7), model, two);

        for (var day = 0; day < 30; day++)
            Assert.Equal(original.SeverityOn(0, day), restored.SeverityOn(0, day), 12);
    }

    [Fact] // Content that changed under a save is refused rather than silently
           // read into the wrong regions.
    public void A_save_from_a_world_with_other_regions_is_refused()
    {
        var two = new WeatherState([Climate(0.5), Climate(0.5)]);
        JsonValue written = StateBlock.Capture(two).Written();

        Assert.Throws<ContentFault>(
            () => StateBlock.Restore(new WeatherState([Climate(0.5)]), written));
    }

    [Fact] // A climate is twelve months of curve. Eleven is content that would
           // fail in December of the first year and pass every test before it.
    public void A_climate_missing_a_month_is_refused()
    {
        var eleven = new double[11];

        var profile = new ClimateProfile(
            new ContentId("short-year"), 0.5, eleven, Flat(1.0), Flat(8.0), -2.0, Open());

        Assert.Throws<ContentFault>(profile.Validate);
    }

    // ------------------------------------------- access windows (R22.6)

    /// <summary>
    /// A window is a CALENDAR fact and takes no draw (SDD-016 §5b's R22.6
    /// amendment) — so it answers the same in every game, which is what lets a
    /// player plan a mobilisation against it a year ahead.
    ///
    /// <para>Asserted by advancing the weather between the two questions: if the
    /// answer came from the daily severities, thirty fresh draws would have moved
    /// it. It does not move, because the road is frozen or it is not.</para>
    /// </summary>
    [Fact]
    public void EN3_an_access_window_is_a_calendar_fact_and_takes_no_draw()
    {
        var state = new WeatherState([Climate(0.75, access: IceRoad())]);
        var stream = new RandomSource(11UL).Stream(StreamId.Weather);
        var model = new Ar1Weather(0.75);

        Assert.True(state.AccessOpenIn(0, new GameDate(1965, 1)));
        Assert.False(state.AccessOpenIn(0, new GameDate(1965, 7)));

        for (var month = 0; month < 24; month++)
            state.Advance(new GameDate(1965 + (month / 12), (month % 12) + 1), model, stream);

        // Two years of weather later, the calendar says exactly what it said.
        Assert.True(state.AccessOpenIn(0, new GameDate(1967, 1)));
        Assert.False(state.AccessOpenIn(0, new GameDate(1967, 7)));
    }

    /// <summary>
    /// How long is left to commit — the question a player actually has, since a
    /// window gates STARTING and the decision it creates is a deadline.
    ///
    /// <para>Independently counted against the ice road above: open in December,
    /// January, February and March. Asked in January, two months remain after
    /// this one, so the answer is 3 — the count INCLUDING the month asked about,
    /// because a job committed in January is committed inside the window.</para>
    /// </summary>
    [Fact]
    public void The_window_says_how_long_is_left_to_commit()
    {
        var state = new WeatherState([Climate(0.75, access: IceRoad())]);

        Assert.Equal(3, state.MonthsUntilAccessCloses(0, new GameDate(1965, 1)));
        Assert.Equal(2, state.MonthsUntilAccessCloses(0, new GameDate(1965, 2)));
        Assert.Equal(1, state.MonthsUntilAccessCloses(0, new GameDate(1965, 3)));

        // Shut already: nothing is left, and the honest answer is not "eight
        // months until it opens" — that is a different question.
        Assert.Equal(0, state.MonthsUntilAccessCloses(0, new GameDate(1965, 4)));

        // December opens it, and the count runs across the year boundary.
        Assert.Equal(4, state.MonthsUntilAccessCloses(0, new GameDate(1965, 12)));
    }

    /// <summary>A year that never opens is content that has closed the field for
    /// ever, which is abandonment rather than a season.</summary>
    [Fact]
    public void A_climate_no_month_can_reach_is_refused()
    {
        ClimateProfile shut = Climate(
            0.5, access: [.. Enumerable.Range(0, 12).Select(_ => false)]);

        Assert.Throws<ContentFault>(shut.Validate);
    }

    /// <summary>A window is twelve months, like every other seasonal curve.</summary>
    [Fact]
    public void A_window_of_the_wrong_length_is_refused()
    {
        ClimateProfile eleven = Climate(
            0.5, access: [.. Enumerable.Range(0, 11).Select(_ => true)]);

        Assert.Throws<ContentFault>(eleven.Validate);
    }
}
