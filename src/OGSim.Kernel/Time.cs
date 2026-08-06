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
    public Quarter Quarter => (Quarter)((Month - 1) / 3 + 1);

    /// <summary>Meteorological season by month, flipped for the southern hemisphere.</summary>
    public Season SeasonAt(ClimateHemisphere hemisphere)
    {
        var northern = Month switch
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
