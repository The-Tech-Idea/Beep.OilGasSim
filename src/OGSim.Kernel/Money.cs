// SDD-001 §8 — Money is a checked scaled integer: cash conservation (INV2) is
// EXACT, with no tolerance term. SDD-009 §1: every double→Money crossing rounds
// half-even, exactly once, at the Movement that enters the ledger.

namespace OGSim.Kernel;

public readonly record struct Money(long Cents) : IComparable<Money>
{
    public static readonly Money Zero = new(0);

    /// <summary>The one double→Money rounding rule: half-even, once (SDD-009 §1).</summary>
    public static Money RoundHalfEven(double cents) =>
        new(checked((long)Math.Round(cents, MidpointRounding.ToEven)));

    public static Money FromMillions(double m) => RoundHalfEven(m * 100_000_000.0);

    // checked arithmetic: overflow is an invariant fault, not a wrap (SDD-001 §8)
    public static Money operator +(Money a, Money b) => new(checked(a.Cents + b.Cents));
    public static Money operator -(Money a, Money b) => new(checked(a.Cents - b.Cents));
    public static Money operator -(Money a) => new(checked(-a.Cents));

    /// <summary>EXACT integer scaling (day-rate × days, unit cost × count) —
    /// pass 4, finding 74: without this, exact math was forced through the
    /// lossy double door.</summary>
    public static Money operator *(Money a, long factor) => new(checked(a.Cents * factor));
    public static Money operator *(long factor, Money a) => new(checked(a.Cents * factor));
    public static bool operator >(Money a, Money b) => a.Cents > b.Cents;
    public static bool operator <(Money a, Money b) => a.Cents < b.Cents;
    public static bool operator >=(Money a, Money b) => a.Cents >= b.Cents;
    public static bool operator <=(Money a, Money b) => a.Cents <= b.Cents;

    public int CompareTo(Money other) => Cents.CompareTo(other.Cents);
}
