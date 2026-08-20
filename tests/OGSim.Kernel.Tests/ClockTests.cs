// R1.3 — the clock (SDD-001 §3). R1 goal G9: the engine is turn-based and
// pacing-agnostic; AdvanceTick is the only way time moves and no wall-clock API
// is reachable. R1-V19 covers the calendar itself: quarters and years land on
// the right ticks under 30/360, where leap years do not exist.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class ClockTests
{
    private static SimulationClock StartOf(int year, int month) =>
        new(new GameDate(year, month));

    // ------------------------------------------------------------- advance

    [Fact] // One tick is one month, always (design 15, TM-D1)
    public void TM1_a_tick_is_one_month()
    {
        var clock = StartOf(1965, 1);
        Assert.Equal(0, clock.CurrentTick.Value);
        Assert.Equal(new GameDate(1965, 1), clock.Date);

        clock.Advance();
        Assert.Equal(1, clock.CurrentTick.Value);
        Assert.Equal(new GameDate(1965, 2), clock.Date);

        for (int i = 0; i < 11; i++) clock.Advance();
        Assert.Equal(12, clock.CurrentTick.Value);
        Assert.Equal(new GameDate(1966, 1), clock.Date);   // the year rolled
    }

    [Fact] // The clock is read-only through its interface — capability follows the handle
    public void L2_the_interface_cannot_move_time()
    {
        ISimulationClock readOnly = StartOf(1965, 1);

        // ISimulationClock has no Advance and no setters: this is asserted by
        // the type system, and the compile-failure corpus (R1-V2) pins it. What
        // is checked here is that the read surface reports what it should.
        Assert.Equal(0, readOnly.CurrentTick.Value);
        Assert.Equal(1965, readOnly.Date.Year);
    }

    // ------------------------------------------------------------- calendar

    [Fact] // R1-V19: quarter boundaries land on the right ticks
    public void R1V19_quarters_fall_on_the_correct_ticks()
    {
        var clock = StartOf(1965, 1);
        Quarter[] expected =
        [
            Quarter.Q1, Quarter.Q1, Quarter.Q1, Quarter.Q2, Quarter.Q2, Quarter.Q2,
            Quarter.Q3, Quarter.Q3, Quarter.Q3, Quarter.Q4, Quarter.Q4, Quarter.Q4,
        ];

        for (int tick = 0; tick < 12; tick++)
        {
            Assert.Equal(expected[tick], clock.Date.Quarter);
            clock.Advance();
        }
        Assert.Equal(Quarter.Q1, clock.Date.Quarter);   // wrapped into the new year
    }

    [Fact] // R1-V19: 30/360 means no leap year exists to be handled
    public void R1V19_the_calendar_is_30_360_with_no_leap_year()
    {
        Assert.Equal(30.0, Duration.DaysPerTick);
        Assert.Equal(360.0, Duration.FromTicks(12.0).Days);

        // 1964 was a leap year in the real calendar. Here February is 30 days
        // like every other month, so a run through it is arithmetically
        // identical to a run through any other twelve months.
        var leapYear = StartOf(1964, 1);
        var ordinaryYear = StartOf(1965, 1);
        for (int i = 0; i < 12; i++) { leapYear.Advance(); ordinaryYear.Advance(); }

        Assert.Equal(new GameDate(1965, 1), leapYear.Date);
        Assert.Equal(new GameDate(1966, 1), ordinaryYear.Date);
        Assert.Equal(leapYear.CurrentTick.Value, ordinaryYear.CurrentTick.Value);
    }

    [Fact] // Seasons flip across the equator — R22's weather model depends on it
    public void R1V19_seasons_are_hemisphere_aware()
    {
        var january = new GameDate(1965, 1);
        Assert.Equal(Season.Winter, january.SeasonAt(ClimateHemisphere.Northern));
        Assert.Equal(Season.Summer, january.SeasonAt(ClimateHemisphere.Southern));

        var july = new GameDate(1965, 7);
        Assert.Equal(Season.Summer, july.SeasonAt(ClimateHemisphere.Northern));
        Assert.Equal(Season.Winter, july.SeasonAt(ClimateHemisphere.Southern));
    }

    // ------------------------------------------------------------- date arithmetic

    [Fact] // AddMonths is the whole of date arithmetic under 30/360
    public void R1V19_add_months_crosses_years_in_both_directions()
    {
        var march = new GameDate(1970, 3);
        Assert.Equal(new GameDate(1970, 12), march.AddMonths(9));
        Assert.Equal(new GameDate(1971, 1), march.AddMonths(10));
        Assert.Equal(new GameDate(1972, 3), march.AddMonths(24));

        // Backwards must floor, not truncate toward zero, or the year is wrong.
        Assert.Equal(new GameDate(1970, 1), march.AddMonths(-2));
        Assert.Equal(new GameDate(1969, 12), march.AddMonths(-3));
        Assert.Equal(new GameDate(1969, 1), march.AddMonths(-14));
        Assert.Equal(new GameDate(1968, 3), march.AddMonths(-24));
    }

    [Fact] // MonthsUntil is AddMonths' inverse — licence terms are counted with it
    public void R1V19_months_until_inverts_add_months()
    {
        var start = new GameDate(1970, 3);
        foreach (int offset in new[] { -30, -14, -1, 0, 1, 9, 24, 121 })
            Assert.Equal(offset, start.MonthsUntil(start.AddMonths(offset)));
    }

    // ------------------------------------------------------------- validation

    [Fact] // A month outside 1-12 would silently produce a wrong quarter
    public void R1V19_an_out_of_range_month_is_refused_at_construction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(1965, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(1965, 13));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(1965, -1));
    }

    [Fact] // default(GameDate) skips the constructor and must not read as Q1
    public void R1V19_default_date_faults_rather_than_reporting_a_quarter()
    {
        GameDate uninitialised = default;
        Assert.Throws<InvariantFault>(() => uninitialised.Quarter);
        Assert.Throws<InvariantFault>(() => uninitialised.SeasonAt(ClimateHemisphere.Northern));
        Assert.Throws<InvariantFault>(() => uninitialised.AddMonths(1));
    }

    // ------------------------------------------------------------- printing

    /// <summary>
    /// Finding 248: this build's compiler mis-synthesises <c>Tick</c>'s
    /// record <c>PrintMembers</c> because <c>Next</c> is an instance property
    /// of the struct's OWN type — <c>ToString</c> called <c>PrintMembers</c>
    /// called <c>ToString</c> on <c>Next</c>, which has its own <c>Next</c>,
    /// forever, a genuine stack overflow rather than a slow test. Nothing in
    /// the engine ever called it (every site formats <c>Value</c> directly),
    /// which is exactly how it went unnoticed — until a FAILING assertion
    /// involving a <c>Tick</c> tried to render one for its own message and
    /// took the whole test host down instead of reporting the failure. The
    /// fix is <c>Tick</c>'s own hand-written override; this proves it holds.
    /// </summary>
    [Fact]
    public void Finding248_ToString_terminates_and_does_not_take_the_host_down_with_it()
    {
        Assert.Equal("Tick { Value = 5 }", new Tick(5).ToString());
    }

    // ------------------------------------------------------------- restore

    [Fact] // A load resumes mid-run; it does not replay from tick 0
    public void R1V19_restore_resumes_the_clock_and_the_date()
    {
        var clock = StartOf(1965, 1);
        clock.RestoreTo(new Tick(30));

        Assert.Equal(30, clock.CurrentTick.Value);
        Assert.Equal(new GameDate(1967, 7), clock.Date);

        // Restoring is not advancing: it happens before the engine ticks.
        Assert.Throws<InvariantFault>(() => clock.RestoreTo(new Tick(40)));
        Assert.Throws<InvariantFault>(() => StartOf(1965, 1).RestoreTo(new Tick(-1)));
    }
}
