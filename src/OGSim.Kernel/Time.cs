// SDD-001 §3 — time. THE 360-DAY YEAR, pinned: every month is exactly 30 days
// (the industry's own 30/360 convention). GameDate labels stay real (year,
// month names, eras); day arithmetic is 30/360; leap years do not exist.
// This is what makes the /30ths segment grid exact for EVERY tick.

namespace OGSim.Kernel;

/// <summary>One simulation step = one month (design 15, TM-D1). 0-based, monotonic.</summary>
public readonly record struct Tick(int Value) : IComparable<Tick>
{
    public Tick Next => new(Value + 1);
    public int CompareTo(Tick other) => Value.CompareTo(other.Value);
    public static bool operator >(Tick a, Tick b) => a.Value > b.Value;
    public static bool operator <(Tick a, Tick b) => a.Value < b.Value;
}

/// <summary>An inclusive tick range for queries.</summary>
public readonly record struct TickRange(Tick From, Tick To);

public enum Quarter { Q1 = 1, Q2 = 2, Q3 = 3, Q4 = 4 }

public enum Season { Winter, Spring, Summer, Autumn }

public enum ClimateHemisphere { Northern, Southern }

/// <summary>Real calendar labels over 30/360 arithmetic (SDD-001 §3, TM-D5).</summary>
public readonly record struct GameDate(int Year, int Month)
{
    /// <summary>Validated at construction: a month outside 1–12 would silently
    /// yield a wrong quarter and a wrong season (R1.3).</summary>
    public int Month { get; init; } = Month is >= 1 and <= 12
        ? Month
        : throw new ArgumentOutOfRangeException(nameof(Month), Month,
            "GameDate month must be 1-12 (SDD-001 §3).");

    public Quarter Quarter => (Quarter)((Guarded() - 1) / 3 + 1);

    /// <summary>
    /// Months forward (or back) on the 30/360 calendar. This is the whole of
    /// date arithmetic: with every month exactly 30 days there is no day-length
    /// case analysis and no leap year to carry.
    /// </summary>
    public GameDate AddMonths(int months)
    {
        int total = Year * 12 + (Guarded() - 1) + months;
        // Floor division, so dates before the epoch move back correctly rather
        // than truncating toward zero across the year boundary.
        int year = total >= 0 ? total / 12 : (total - 11) / 12;
        return new GameDate(year, total - year * 12 + 1);
    }

    // R1.15 — calendar boundaries. Quarter, year and season closes are when
    // reporting, licence clocks, reserves booking and seasonal access windows
    // fire, so "did this tick cross one?" is asked every tick by several
    // subsystems and is answered in exactly one place.

    /// <summary>This tick opens a new quarter (design 15 §9).</summary>
    public bool StartsQuarter => Guarded() is 1 or 4 or 7 or 10;

    /// <summary>This tick opens a new year — the reporting and reserves boundary.</summary>
    public bool StartsYear => Guarded() == 1;

    /// <summary>
    /// This tick opens a new meteorological season. The boundary months are the
    /// same in both hemispheres — only the season's NAME flips — so this needs no
    /// hemisphere argument and deliberately does not take one.
    /// </summary>
    public bool StartsSeason => Guarded() is 12 or 3 or 6 or 9;

    /// <summary>Whole months from this date to another — the inverse of AddMonths.</summary>
    public int MonthsUntil(GameDate other) =>
        (other.Year * 12 + other.Guarded() - 1) - (Year * 12 + Guarded() - 1);

    /// <summary>
    /// default(GameDate) bypasses the constructor and carries month 0. Every
    /// member checks rather than quietly reporting Q0 (the Polygon rule, applied
    /// at the other value-type boundary that has no valid zero).
    /// </summary>
    private int Guarded() =>
        Month is >= 1 and <= 12
            ? Month
            : throw new InvariantFault("SDD-001 §3", null,
                $"default(GameDate) has month {Month}; a date must be constructed.");

    /// <summary>Meteorological season by month, flipped for the southern hemisphere.</summary>
    public Season SeasonAt(ClimateHemisphere hemisphere)
    {
        var northern = Guarded() switch
        {
            12 or 1 or 2 => Season.Winter,
            3 or 4 or 5 => Season.Spring,
            6 or 7 or 8 => Season.Summer,
            _ => Season.Autumn,
        };
        if (hemisphere == ClimateHemisphere.Northern) return northern;
        return northern switch
        {
            Season.Winter => Season.Summer,
            Season.Spring => Season.Autumn,
            Season.Summer => Season.Winter,
            _ => Season.Spring,
        };
    }
}

/// <summary>
/// The only source of "now". No setters, no Advance() — only the engine's tick
/// pipeline moves time (SDD-001 §3). Wall-clock APIs are banned (D-6).
/// </summary>
public interface ISimulationClock
{
    Tick CurrentTick { get; }
    GameDate Date { get; }
}
