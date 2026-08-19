# PowerBalance

Source: `src\OGSim.Facilities\PowerBalance.cs` · Lines: 150

## File intent

> R8.8 — the facility power balance (SDD-006 §3, R8 §2.5, design 03 §6.1).
> 
> Units declare demand; sources declare supply. A shortfall takes units OFFLINE
> by a declared priority order — at tick stage 4, BEFORE the flow solve at stage
> 5. That ordering is the point: a power shortfall is decided before the solve
> rather than discovered inside it, and an element taken offline is simply
> absent from the segment's network (design 04 §4).
> 

## Namespaces

- `OGSim.Facilities`

## Type declarations

- `L23` `public enum PowerPriority`
- `L40` `public sealed record PowerDemand(`
- `L46` `public sealed record PowerBalanceResult(`
- `L67` `public static class PowerBalance`

## Accessible members

- `L53` `public bool Equals(PowerBalanceResult? other) =>`
- `L58` `public override int GetHashCode() =>`
- `L61` `public bool Shortfall => Offline.Count > 0;`
- `L84` `public static PowerBalanceResult Balance(`
- `L148` `private static string Format(double value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

