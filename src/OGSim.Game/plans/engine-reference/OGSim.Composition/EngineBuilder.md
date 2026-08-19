# EngineBuilder

Source: `src\OGSim.Composition\EngineBuilder.cs` · Lines: 1277

## File intent

> Composition — building the engine (design 03 §3.1, §8).
> 
> COMPOSITION IS ALL-OR-NOTHING. ModuleComposer validates the whole set before
> anything is constructed: every Requires met, no contract provided twice, no
> state key owned twice, no dependency cycle, no two modules in one stage slot,
> every declared slot filled. Either the engine builds, or it refuses naming
> EVERY problem.
> 

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L31` `internal static class Defaults`
- `L976` `internal sealed class RegionalObservationModel : IObservationModel`
- `L1037` `public enum FaultHandling`
- `L1049` `public sealed record EngineSettings(`
- `L1073` `public sealed record Engine(`
- `L1112` `public abstract record BuildResult;`
- `L1114` `public sealed record Built(Engine Engine) : BuildResult;`
- `L1117` `public sealed record BuildRefused(EngineCompositionRefused Refusal) : BuildResult`
- `L1123` `public static class EngineBuilder`

## Accessible members

- `L33` `public static BlackOilInputs Fluid { get; } = new(`
- `L40` `public static ValidityRange Validity { get; } = new(`
- `L44` `public static Wells.InflowConditions Inflow { get; } = new(`
- `L48` `public static Wells.TubingGeometry Tubing { get; } = new(`
- `L57` `public const double MaxTickPressureDropFraction = 0.2;`
- `L60` `public static Money OpeningCash { get; } = Money.FromMillions(50.0);`
- `L74` `public static FieldEconomics Economics { get; } = new(`
- `L97` `public static IReadOnlyList<(ContentId Id, PhaseAtStandardConditions Phase,`
- `L111` `public static MaterialId OilOrdinal { get; } = new(0);`
- `L113` `public static MaterialId GasOrdinal { get; } = new(1);`
- `L117` `public static MaterialId WaterOrdinal { get; } = new(2);`
- `L124` `public static IReadOnlyList<int> LiquidOrdinals { get; } =`
- `L132` `public static Density GasSurfaceDensity { get; } = new(`
- `L141` `public const int MaterialCount = 3;`
- `L150` `public static EntityId<IRig> TheRig { get; } = new(1);`
- `L162` `public static OutcomeTable DrillingOutcomes { get; } = new(`
- `L182` `public static OutcomeTable WellTestOutcomes { get; } = new(`
- `L197` `public static Length MaximumDrillingDepth { get; } = new(4000.0);`
- `L204` `public static OutcomeTable SurveyOutcomes { get; } = new(`
- `L220` `public static ContentId PressureKind { get; } = new("reservoir-pressure");`
- `L222` `public static ContentId PorosityKind { get; } = new("porosity");`
- `L224` `public static ContentId PermeabilityKind { get; } = new("permeability");`
- `L226` `public static ContentId OilInPlaceKind { get; } = new("oil-in-place");`
- `L233` `public static ContentId StructureCapacityKind { get; } = new("structure-capacity");`
- `L242` `public static ContentId WellTestSource { get; } = new("well-test");`
- `L244` `public static ContentId WellLogSource { get; } = new("well-log");`
- `L246` `public static ContentId CoreSource { get; } = new("core");`
- `L248` `public static ContentId SeismicSource { get; } = new("seismic-3d");`
- `L259` `public static BeliefSpace SpaceOf(ContentId kind) =>`
- `L284` `public static double SigmaFloorFor(ContentId kind) =>`
- `L320` `public static ActivityTerms DrillWellTerms { get; } = new(`
- `L327` `public static ActivityTerms WellTestTerms { get; } = new(`
- `L335` `public static ActivityTerms WirelineLogTerms { get; } = new(`
- `L344` `public static ActivityTerms CoringTerms { get; } = new(`
- `L358` `public static ActivityTerms SeismicSurveyTerms { get; } = new(`
- `L375` `public static FormationVolumeFactor CompletionBo { get; } = new(1.2);`
- `L382` `public static Wells.Completion CompletionFor(`
- `L433` `public static IReadOnlyList<ProjectedPath> ProjectedPaths { get; } =`
- `L463` `private static Money TargetCash { get; } = Money.FromMillions(600.0);`
- `L482` `public static Scenario FirstField { get; } = new(`
- `L517` `public static Temperature ReservoirTemperature { get; } = Temperature.FromCelsius(93.3);`
- `L527` `public static EntityId<IFlowElement> TheManifold { get; } = new(1_000_001);`
- `L529` `public static EntityId<IFlowElement> TheSeparator { get; } = new(1_000_002);`
- `L531` `public static EntityId<IFlowElement> TheCustodyPoint { get; } = new(1_000_003);`
- `L533` `public static EntityId<IFlowElement> TheFlare { get; } = new(1_000_004);`
- `L535` `public static EntityId<ICompletion> TheDisposalWell { get; } = new(1_000_005);`
- `L537` `public static EntityId<IFlowElement> TheFlowline { get; } = new(1_000_006);`
- `L539` `public static EntityId<IFlowElement> TheTank { get; } = new(1_000_007);`
- `L546` `public const ulong FirstGatheringLine = 2_000_000UL;`
- `L563` `public static Facilities.TankTier TankTier { get; } = new(`
- `L579` `public static MassRate ExportOfftake { get; } = new(20.0);`
- `L590` `public static IReadOnlyList<Facilities.ExportTier> ExportLadder { get; } =`
- `L603` `public static ActivityTerms ExpandExportTerms { get; } = new(`
- `L626` `public static PipeGeometry Flowline { get; } = new(`
- `L632` `public static Pressure FlowlineRating { get; } = Pressure.FromBar(100.0);`
- `L640` `public static Wells.InjectionConditions Disposal { get; } = new(`
- `L652` `public static Pressure DisposalPressure { get; } = new(28.0e6);`
- `L665` `public static Pressure DisposalFormationPressure { get; } = new(20.0e6);`
- `L672` `public static MassRate FlareCapacity { get; } = new(200.0);`
- `L677` `public const double FlareCombustionEfficiency = 0.98;`
- `L685` `public static Facilities.ManifoldTier ManifoldTier { get; } =`
- `L711` `public static Facilities.SeparatorTier SeparatorTier { get; } = new(`
- `L729` `public static IReadOnlyList<Facilities.SeparatorTier> SeparatorLadder { get; } =`
- `L758` `public static ActivityTerms AbandonWellTerms { get; } = new(`
- `L770` `public static Money AbandonmentCostOf(ContentId template) =>`
- `L777` `public static ActivityTerms InstallSeparatorTerms { get; } = new(`
- `L795` `public static Specification SalesSpec { get; } = new([]);`
- `L801` `public static Facilities.StreamProperties MeasureStream(MaterialStream stream) =>`
- `L812` `public static Temperature SurfaceAmbient { get; } = Temperature.FromCelsius(15.0);`
- `L816` `public static Density SurfaceOilDensity { get; } = Density.FromSpecificGravity(0.85);`
- `L820` `public static Density WaterSurfaceDensity { get; } = Density.FromSpecificGravity(1.05);`
- `L838` `public static ContentId Drive { get; } = new("water-drive");`
- `L864` `public static FactorBelief ExplorationPrior { get; } = new(Alpha: 2.8, Beta: 1.2);`
- `L873` `public static double TrapConfidenceOf(DetectClass subtlety) => subtlety switch`
- `L884` `public static Pressure InitialReservoirPressure { get; } = new(30.0e6);`
- `L900` `public const double AquiferStrength = 4.0;`
- `L912` `public static Duration AquiferResponseTime { get; } = Duration.FromTicks(40.0 * 12.0);`
- `L914` `public static RelativePermeabilityCurve Wettability { get; } =`
- `L923` `public static ModelSlot FluidSlot { get; } = new("fluid-properties");`
- `L929` `public static RealityProfile Simulation { get; } = new(new ContentId("simulation"), []);`
- `L937` `public static RealityProfile Arcade { get; } = new(`
- `L946` `public static IReadOnlyList<RealityProfile> Profiles { get; } = [Simulation, Arcade];`
- `L948` `public static RealityProfile ProfileNamed(ContentId id)`
- `L958` `public static Integrity.DegradationCoefficients Decay { get; } =`
- `L978` `public ContentId Id { get; } = new("regional-observation");`
- `L980` `public double? SigmaFor(ContentId source, ContentId propertyKind, EntityRef subject) =>`
- `L1087` `public FieldReadModel? ReadModel => Provided.Resolve<CloseStage>().Published;`
- `L1091` `public bool Equals(Engine? other) =>`
- `L1098` `public override int GetHashCode() =>`
- `L1119` `public IReadOnlyList<CompositionProblem> Problems => Refusal.Problems;`
- `L1134` `internal static IReadOnlyList<IModule> ShippedModules(`
- `L1172` `public static BuildResult CreateNew(EngineSettings settings, WorldParameters world)`
- `L1203` `public static BuildResult Build(EngineSettings settings)`
- `L1221` `public static BuildResult Build(EngineSettings settings, IReadOnlyList<IModule> modules)`
- `L1232` `private static BuildResult Build(`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

