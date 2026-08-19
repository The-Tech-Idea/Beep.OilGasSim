# SubsurfaceState

Source: `src\OGSim.Subsurface\SubsurfaceState.cs` · Lines: 448

## File intent

> R20c.7 — the subsurface module's own state and its stage
> (SDD-001 §9-10, SDD-003 §3, design 03 §6 stage 6).
> 
> This is the first module in the engine that owns entities, saves them, and
> does per-tick work on them. The three arrive together on purpose: a stage with
> nothing to act on is law L3's declaration-with-no-behaviour, and state with no
> stage is a fact nothing ever changes.
> 

## Namespaces

- `OGSim.Subsurface`

## Type declarations

- `L28` `internal sealed class SubsurfaceState : IStateOwner`
- `L424` `internal sealed record CompartmentWithdrawal(`
- `L437` `internal sealed class MaterialBalanceStage(`

## Accessible members

- `L33` `private readonly List<ReservoirCompartment> _compartments = [];`
- `L34` `private readonly Dictionary<EntityId<IReservoirCompartmentEntity>, ReservoirCompartment> _byId = [];`
- `L36` `private readonly IFluidPropertyModel _fluid;`
- `L37` `private readonly IDriveMechanism _defaultDrive;`
- `L38` `private readonly double _maxTickPressureDropFraction;`
- `L40` `private ulong _nextId;`
- `L42` `public SubsurfaceState(`
- `L61` `public StateKey Key { get; } = new("subsurface.compartments");`
- `L63` `public int SchemaVersion => 1;`
- `L65` `public int Count => _compartments.Count;`
- `L88` `internal static IDriveMechanism DriveNamed(ContentId drive) => drive.Value switch`
- `L101` `public EntityId<IReservoirCompartmentEntity> Create(`
- `L184` `public ReservoirVolume InfluxFor(`
- `L199` `private readonly Dictionary<EntityId<IReservoirCompartmentEntity>, IAquiferModel?> _aquifers = [];`
- `L206` `private const double WaterCompressibility = 4.4e-10;`
- `L217` `public void CommitTick(IReadOnlyList<CompartmentWithdrawal> withdrawals)`
- `L251` `internal Pressure TruePressureOf(EntityId<IReservoirCompartmentEntity> compartment) =>`
- `L256` `internal double TruePorosityOf(EntityId<IReservoirCompartmentEntity> compartment) =>`
- `L259` `internal Permeability TruePermeabilityOf(EntityId<IReservoirCompartmentEntity> compartment) =>`
- `L272` `internal double TrueWaterCutOf(`
- `L289` `internal SurfaceVolume TrueOilInPlaceOf(EntityId<IReservoirCompartmentEntity> compartment) =>`
- `L292` `private ReservoirCompartment Find(EntityId<IReservoirCompartmentEntity> compartment) =>`
- `L298` `public void Capture(IStateWriter writer)`
- `L349` `public void Restore(IStateReader reader)`
- `L416` `private static string Prefix(long index) =>`
- `L441` `public StageId Id => StageId.MaterialBalance;`
- `L443` `public void Execute(TickContext context)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`
- `using InPlace = OGSim.Kernel.MaterialInventory;`

