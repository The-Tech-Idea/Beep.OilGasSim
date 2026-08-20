# Time

Source: `src\OGSim.Kernel\Time.cs` · Lines: 114

## File intent

> SDD-001 §3 — time. THE 360-DAY YEAR, pinned: every month is exactly 30 days
> (the industry's own 30/360 convention). GameDate labels stay real (year,
> month names, eras); day arithmetic is 30/360; leap years do not exist.
> This is what makes the /30ths segment grid exact for EVERY tick.
> <summary>One simulation step = one month (design 15, TM-D1). 0-based, monotonic.</summary>

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L9` `public readonly record struct Tick(int Value) : IComparable<Tick>`
- `L18` `public readonly record struct TickRange(Tick From, Tick To);`
- `L20` `public enum Quarter { Q1 = 1, Q2 = 2, Q3 = 3, Q4 = 4 }`
- `L22` `public enum Season { Winter, Spring, Summer, Autumn }`
- `L24` `public enum ClimateHemisphere { Northern, Southern }`
- `L27` `public readonly record struct GameDate(int Year, int Month)`
- `L110` `public interface ISimulationClock`

## Accessible members

- `L11` `public Tick Next => new(Value + 1);`
- `L12` `public int CompareTo(Tick other) => Value.CompareTo(other.Value);`
- `L13` `public static bool operator >(Tick a, Tick b) => a.Value > b.Value;`
- `L14` `public static bool operator <(Tick a, Tick b) => a.Value < b.Value;`
- `L31` `public int Month { get; init; } = Month is >= 1 and <= 12`
- `L36` `public Quarter Quarter => (Quarter)((Guarded() - 1) / 3 + 1);`
- `L43` `public GameDate AddMonths(int months)`
- `L58` `public bool StartsQuarter => Guarded() is 1 or 4 or 7 or 10;`
- `L61` `public bool StartsYear => Guarded() == 1;`
- `L68` `public bool StartsSeason => Guarded() is 12 or 3 or 6 or 9;`
- `L71` `public int MonthsUntil(GameDate other) =>`
- `L79` `private int Guarded() =>`
- `L86` `public Season SeasonAt(ClimateHemisphere hemisphere)`

