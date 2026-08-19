# UnitGrammar

Source: `src\OGSim.Kernel\UnitGrammar.cs` · Lines: 230

## File intent

> R3.1 — the content unit-string grammar (SDD-004 §4, decision CD2).
> 
> Content writes "3200 psi", not 3200 with a comment saying psi. The engine is
> SI throughout, so every quantity crosses one conversion at load and never
> again — which is what makes the unit-error class the design 05 §2 note calls
> "a classic source of unit errors" structurally absent rather than tested for.
> 
> THE TABLE DOES NOT LIVE IN PhysicalConstants, though SDD-004 §4 said it did.

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L21` `public readonly record struct UnitConversion(Dimension Dimension, double Factor, double Offset);`
- `L28` `public static class UnitGrammar`
- `L218` `public sealed class ContentUnitFault : FaultException`

## Accessible members

- `L32` `private static readonly Dictionary<string, UnitConversion> Tokens =`
- `L126` `public static double ParseToSi(string text, Dimension expected)`
- `L161` `public static bool IsKnownUnit(string token) =>`
- `L169` `private static string NearestHint(string unknown)`
- `L191` `private static int EditDistance(string a, string b)`
- `L220` `public ContentUnitFault(string text, string reason)`
- `L228` `public string Text { get; }`
- `L229` `public string Reason { get; }`

## Imports

- `using System.Globalization;`

