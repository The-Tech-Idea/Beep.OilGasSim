# WorldSink

Source: `src\OGSim.Composition\WorldSink.cs` · Lines: 400

## File intent

> R20d.8 — the generated world reaches the engine (SDD-010 §4).
> 
> THE GENERATOR HAS EXISTED AND NEVER RUN. `BasinWorldGenerator` draws traps,
> charges some and leaves others empty, sizes accumulations log-normally and
> derives depth, pressure, temperature and access from where each one sits — and
> the only thing that ever called it was its own test. `IWorldSink` had exactly
> one implementation in the repository and it was a recording double.
> 

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L40` `public sealed class WorldState`
- `L260` `public sealed class WorldSink : IWorldSink`

## Accessible members

- `L42` `private readonly List<EntityId<IProspect>> _prospects = [];`
- `L43` `private readonly List<Coordinate> _at = [];`
- `L49` `private readonly Dictionary<EntityId<IProspect>,`
- `L51` `private readonly List<ClimateRegion> _climate = [];`
- `L52` `private readonly List<Jurisdiction> _jurisdictions = [];`
- `L54` `private IReadOnlyList<Harbour> _harbours = [];`
- `L55` `private GeneratedSurface? _surface;`
- `L63` `public WorldView? View => _surface is null ? null : new WorldView(`
- `L77` `public IReadOnlyList<EntityId<IProspect>> Prospects => _prospects;`
- `L81` `public int Count => _prospects.Count;`
- `L85` `internal EntityId<IProspect> Place(Coordinate at, ReservoirVolume capacity)`
- `L96` `private readonly List<ReservoirVolume> _capacity = [];`
- `L104` `internal ReservoirVolume CapacityOf(EntityId<IProspect> prospect)`
- `L122` `public EntityId<IProspect> DeclareKnownField(`
- `L132` `internal void Found(EntityId<IProspect> prospect, EntityId<IReservoirCompartmentEntity> in_)`
- `L138` `private readonly Dictionary<EntityId<IReservoirCompartmentEntity>,`
- `L153` `internal void HeaderAt(Coordinate site) => _header ??= site;`
- `L155` `private Coordinate? _header;`
- `L162` `public Length? DistanceToHeaderOf(EntityId<IReservoirCompartmentEntity> compartment)`
- `L177` `public Length? DistanceToMarketOf(EntityId<IReservoirCompartmentEntity> compartment) =>`
- `L188` `public EntityId<IProspect> ProspectFor(EntityId<IReservoirCompartmentEntity> compartment) =>`
- `L201` `internal EntityId<IReservoirCompartmentEntity>? Beneath(EntityId<IProspect> prospect) =>`
- `L206` `internal void Surface(GeneratedSurface surface)`
- `L212` `internal void Climate(ClimateRegion region) => _climate.Add(region);`
- `L214` `internal void Jurisdiction(Jurisdiction jurisdiction) => _jurisdictions.Add(jurisdiction);`
- `L229` `public Coordinate PositionOf(EntityId<IProspect> prospect)`
- `L236` `public Length? DistanceToMarket(EntityId<IProspect> prospect)`
- `L262` `private readonly FieldControl _field;`
- `L263` `private readonly IBeliefStore _beliefs;`
- `L264` `private readonly WorldState _world;`
- `L265` `private readonly OGSim.Information.ProspectRisks _risks;`
- `L267` `public WorldSink(`
- `L284` `private ulong _prospectsPlaced;`
- `L286` `public void AddAccumulation(GeneratedAccumulation accumulation)`
- `L350` `private const double RockCompressibility = 4.5e-10;`
- `L357` `private const double ContactStandoff = 100.0;`
- `L359` `public void SetSurface(GeneratedSurface surface)`
- `L365` `public void AddClimateRegion(ClimateRegion region)`
- `L371` `public void AddJurisdiction(Jurisdiction jurisdiction)`
- `L384` `public void DeliverRegionalObservation(Observation observation)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

