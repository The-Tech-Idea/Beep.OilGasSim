# ExportTerminal

Source: `src\OGSim.Facilities\ExportTerminal.cs` · Lines: 57

## File intent

> R20d.8 — export capacity (SDD-006 §7b, §0c).
> 
> THE FIELD'S LAST CEILING. Everything upstream of here can be debottlenecked —
> a bigger vessel, another well, a wider line — and none of it produces a barrel
> more if the export line will not take it. The tank fills, the ullage
> constraint reaches back down the chain, and wells shut themselves in.
> 
> IT WAS A CONSTANT, and that was the reason a big field played exactly like a

## Namespaces

- `OGSim.Facilities`

## Type declarations

- `L18` `public sealed record ExportTier(ContentId Id, MassRate Offtake);`
- `L25` `public sealed class ExportTerminal`

## Accessible members

- `L27` `public ExportTerminal(EntityRef id, ExportTier fitted)`
- `L42` `public EntityRef Id { get; }`
- `L46` `public ExportTier Tier { get; private set; }`
- `L52` `public void Fit(ExportTier tier)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

