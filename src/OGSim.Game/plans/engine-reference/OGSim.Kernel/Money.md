# Money

Source: `src\OGSim.Kernel\Money.cs` · Lines: 33

## File intent

> SDD-001 §8 — Money is a checked scaled integer: cash conservation (INV2) is
> EXACT, with no tolerance term. SDD-009 §1: every double→Money crossing rounds
> half-even, exactly once, at the Movement that enters the ledger.

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L7` `public readonly record struct Money(long Cents) : IComparable<Money>`

## Accessible members

- `L9` `public static readonly Money Zero = new(0);`
- `L12` `public static Money RoundHalfEven(double cents) =>`
- `L15` `public static Money FromMillions(double m) => RoundHalfEven(m * 100_000_000.0);`
- `L18` `public static Money operator +(Money a, Money b) => new(checked(a.Cents + b.Cents));`
- `L19` `public static Money operator -(Money a, Money b) => new(checked(a.Cents - b.Cents));`
- `L20` `public static Money operator -(Money a) => new(checked(-a.Cents));`
- `L25` `public static Money operator *(Money a, long factor) => new(checked(a.Cents * factor));`
- `L26` `public static Money operator *(long factor, Money a) => new(checked(a.Cents * factor));`
- `L27` `public static bool operator >(Money a, Money b) => a.Cents > b.Cents;`
- `L28` `public static bool operator <(Money a, Money b) => a.Cents < b.Cents;`
- `L29` `public static bool operator >=(Money a, Money b) => a.Cents >= b.Cents;`
- `L30` `public static bool operator <=(Money a, Money b) => a.Cents <= b.Cents;`
- `L32` `public int CompareTo(Money other) => Cents.CompareTo(other.Cents);`

