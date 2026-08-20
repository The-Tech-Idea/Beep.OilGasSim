# ReservoirCompartment

Source: `src\OGSim.Subsurface\ReservoirCompartment.cs` · Lines: 219

## File intent

> R20c.7 — the compartment as a living entity (SDD-003 §3, design 02 §2.1).
> 
> IReservoirCompartment was declared at R5.1 and never implemented: the material
> balance was proven against inputs a test assembled, and nothing held a
> reservoir between two ticks. This is that thing — the first entity in the
> engine that persists across a tick and changes because of what happened.
> 
> STILL INTERNAL, and that is the point. The player's belief about a reservoir

## Namespaces

- `OGSim.Subsurface`

## Type declarations

- `L25` `internal sealed class ReservoirCompartment : IReservoirCompartment`

## Accessible members

- `L27` `private readonly List<CompartmentLink> _links;`
- `L29` `public ReservoirCompartment(`
- `L56` `public EntityId<IReservoirCompartmentEntity> Id { get; }`
- `L58` `public Pressure Pr { get; private set; }`
- `L60` `public InPlace InPlace { get; private set; }`
- `L62` `public ContactSet Contacts { get; private set; }`
- `L64` `public RockTruth Rock { get; }`
- `L66` `public IDriveMechanism Drive { get; }`
- `L68` `public IReadOnlyList<CompartmentLink> Links => _links;`
- `L70` `public InitialConditions Initial { get; }`
- `L72` `public CumulativeProduction Cumulative { get; private set; }`
- `L90` `public double WaterSaturation`
- `L123` `private const double WaterFormationVolumeFactor = 1.0;`
- `L134` `public void CommitWithdrawal(`
- `L183` `public void RestoreTo(`
- `L218` `public void MoveContacts(ContactSet contacts) => Contacts = contacts;`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`
- `using InPlace = OGSim.Kernel.MaterialInventory;`

