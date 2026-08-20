# ProductionLoop

Source: `src\OGSim.Composition\ProductionLoop.cs` · Lines: 1078

## File intent

> R20c.7 — the loop (design 03 §6 stages 5, 6 and 8).
> 
> A well produces, the compartment it drained loses pressure, the oil is sold
> and the cash lands in the ledger. Next month the same well produces less
> because of what this month took. That circle is the game; everything else in
> the engine exists to make it interesting.
> 
> It lives in COMPOSITION because it is the one place entitled to know that

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L27` `public sealed record FieldEconomics(`
- `L64` `public sealed record ChainElementView(`
- `L91` `internal sealed class ChainElement(EntityId<IFlowElement> element)`
- `L132` `internal sealed class TickProduction`
- `L153` `internal sealed class ProductionLoop`
- `L733` `internal delegate OGSim.Facilities.Pipeline GatheringLine(Length run);`
- `L735` `public sealed class FieldControl`
- `L1021` `internal sealed class SegmentationStage(IFlowElementRegistry network) : ITickStage`
- `L1045` `internal sealed class SolveFlowStage(ProductionLoop loop) : ITickStage`
- `L1053` `internal sealed class CustodyStage(ProductionLoop loop) : ITickStage`
- `L1069` `internal sealed class EconomicsStage(ProductionLoop loop) : ITickStage`

## Accessible members

- `L71` `public bool Equals(ChainElementView? other) =>`
- `L76` `public override int GetHashCode() =>`
- `L81` `public bool IsBottleneck => Deferred.Count > 0;`
- `L93` `private readonly List<(ConstraintKind Kind, Mass Deferred)> _deferred = [];`
- `L95` `public EntityId<IFlowElement> Element { get; } = element;`
- `L97` `public double Throughput { get; set; }`
- `L102` `public void Refuse(ConstraintKind kind, Mass deferred)`
- `L115` `public ChainElementView Published(Func<EntityId<IFlowElement>, string> nameOf) =>`
- `L134` `private readonly List<CompartmentWithdrawal> _withdrawals = [];`
- `L136` `public IReadOnlyList<CompartmentWithdrawal> Withdrawals => _withdrawals;`
- `L140` `public void Set(IReadOnlyList<CompartmentWithdrawal> withdrawals)`
- `L155` `private readonly SubsurfaceState _subsurface;`
- `L156` `private readonly WellsState _wells;`
- `L157` `private readonly CompanyState _company;`
- `L158` `private readonly TickProduction _production;`
- `L159` `private readonly IFluidPropertyModel _fluid;`
- `L160` `private readonly IAuditTrail _audit;`
- `L161` `private readonly FieldEconomics _economics;`
- `L162` `private readonly Temperature _reservoirTemperature;`
- `L163` `private readonly IFlowSolver _solver;`
- `L164` `private readonly IFlowElementRegistry _network;`
- `L165` `private readonly Temperature _ambient;`
- `L166` `private readonly Density _surfaceDensity;`
- `L167` `private readonly int _materialCount;`
- `L172` `private readonly HashSet<EntityId<IFlowElement>> _meters = [];`
- `L176` `private readonly Dictionary<EntityId<IReservoirCompartmentEntity>, double> _byCompartment = [];`
- `L179` `private readonly List<ChainElement> _chain = [];`
- `L181` `private readonly Func<EntityId<IFlowElement>, string> _names;`
- `L183` `private readonly OGSim.Facilities.Tank _tank;`
- `L184` `private readonly OGSim.Facilities.ExportTerminal _terminal;`
- `L186` `private OGSim.Kernel.Composition _stored;`
- `L187` `private Allocation _tankProvenance;`
- `L191` `private readonly double[] _handled;`
- `L193` `private readonly IFiscalRegime _regime;`
- `L194` `private readonly IReadOnlyList<int> _liquidOrdinals;`
- `L195` `private readonly Func<bool> _isAbandoned;`
- `L197` `public ProductionLoop(`
- `L266` `public SurfaceVolume ProducedThisTick { get; private set; } = new(0.0);`
- `L270` `public OGSim.Kernel.Composition Delivered { get; private set; }`
- `L288` `public void SolveFlow(TickContext context)`
- `L342` `private void Accumulate(SolveReport report, double seconds, double[] delivered)`
- `L430` `private ChainElement Flowing(EntityId<IFlowElement> element)`
- `L441` `public IReadOnlyList<ChainElementView> Chain()`
- `L458` `private const int OnSpecLeg = 0;`
- `L460` `private const double SecondsPerDay = 86_400.0;`
- `L467` `private void PublishWithdrawals()`
- `L556` `public void StoreAndExport(Duration tick)`
- `L576` `public Mass Exported { get; private set; }`
- `L578` `public void RecordCustody()`
- `L594` `private AuditId? _sale;`
- `L596` `private static string Format(double value) =>`
- `L599` `public void PostEconomics(Tick tick)`
- `L657` `private static Money Scale(Money unitPrice, double quantity) =>`
- `L676` `private Money OperatingCost()`
- `L698` `private const double KilogramsPerTonne = 1000.0;`
- `L706` `private static Viscosity WaterViscosity { get; } = new(0.5e-3);`
- `L737` `private readonly SubsurfaceState _subsurface;`
- `L738` `private readonly WellsState _wells;`
- `L739` `private readonly IFlowElementRegistry _network;`
- `L740` `private readonly SurfaceChain _chain;`
- `L741` `private readonly IObligationRegistry _obligations;`
- `L742` `private readonly ContentId _abandonmentTemplate;`
- `L743` `private readonly WorldState _world;`
- `L744` `private readonly GatheringLine _gatheringLine;`
- `L746` `private int _slotsTaken;`
- `L748` `internal FieldControl(`
- `L776` `public bool HasFreeSlot => _slotsTaken < _chain.Slots;`
- `L778` `public int FreeSlots => _chain.Slots - _slotsTaken;`
- `L780` `public EntityId<IReservoirCompartmentEntity> AddCompartment(`
- `L808` `public EntityId<ICompletion> OpenWell(`
- `L882` `private static PortId WellheadOutlet { get; } = new(0);`
- `L884` `private static PortId PipelineInlet { get; } = new(0);`
- `L886` `private static PortId PipelineOutlet { get; } = new(1);`
- `L894` `private static Length MinimumGatheringRun { get; } = new(200.0);`
- `L898` `public Completion? WellNamed(EntityId<ICompletion> well) => _wells.Find(well);`
- `L902` `public int LiveWellCount => _wells.Count - _abandoned.Count;`
- `L912` `public bool IsAbandoned => _wells.Count > 0 && _abandoned.Count == _wells.Count;`
- `L925` `public IReadOnlyList<WellStatusView> Wells()`
- `L959` `public void Abandon(EntityId<ICompletion> well, AuditId cause)`
- `L975` `private readonly HashSet<EntityId<ICompletion>> _abandoned = [];`
- `L983` `public void SetChoke(EntityId<ICompletion> well, ChokeSetting choke)`
- `L993` `public int CompartmentCount => _subsurface.Count;`
- `L995` `public int WellCount => _wells.Count;`
- `L1005` `public ulong NextWellId() => (ulong)_wells.Count + 1;`
- `L1023` `public StageId Id => StageId.Availability;`
- `L1025` `public void Execute(TickContext context)`
- `L1047` `public StageId Id => StageId.SolveFlow;`
- `L1049` `public void Execute(TickContext context) => loop.SolveFlow(context);`
- `L1055` `public StageId Id => StageId.Custody;`
- `L1057` `public void Execute(TickContext context)`
- `L1071` `public StageId Id => StageId.Economics;`
- `L1073` `public void Execute(TickContext context)`

## Imports

- `using OGSim.Company;`
- `using OGSim.Contracts;`
- `using OGSim.Kernel;`
- `using OGSim.Subsurface;`
- `using OGSim.Wells;`

