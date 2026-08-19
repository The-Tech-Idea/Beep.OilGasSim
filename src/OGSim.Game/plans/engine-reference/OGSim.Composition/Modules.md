# Modules

Source: `src\OGSim.Composition\Modules.cs` · Lines: 984

## File intent

> Composition — the thirteen modules, declared (design 03 §3.1, §8).
> 
> THIS IS THE ONLY PROJECT THAT NAMES CONCRETE TYPES. Every other assembly
> depends downward on Kernel and Contracts alone; somebody has to know what
> implements what, and confining that knowledge to one project is exactly what
> keeps the rest honest.
> 
> A MODULE DECLARES BEFORE IT IS BUILT. Provides, Requires, OwnsState, Stages,

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L33` `internal abstract class EngineModule(ModuleManifest manifest) : IModule`
- `L85` `internal sealed class SubsurfaceModule() : EngineModule(Declare(`
- `L143` `internal sealed class WellsModule() : EngineModule(Declare(`
- `L174` `internal sealed class FlowModule() : EngineModule(Declare(`
- `L206` `internal sealed class FacilitiesModule() : EngineModule(Declare(`
- `L346` `internal sealed record SurfaceChain(`
- `L396` `internal sealed class OperationsModule() : EngineModule(Declare(`
- `L413` `internal sealed class CompanyModule() : EngineModule(Declare(`
- `L468` `internal sealed class FieldModule() : EngineModule(Declare(`
- `L731` `internal sealed class InformationModule() : EngineModule(Declare(`
- `L780` `internal sealed class WorldModule() : EngineModule(Declare(`
- `L811` `internal sealed class CapabilitiesModule() : EngineModule(Declare(`
- `L835` `internal sealed class IntegrityModule() : EngineModule(Declare(`
- `L860` `internal sealed class HseModule() : EngineModule(Declare(`
- `L880` `internal sealed class ObjectivesModule() : EngineModule(Declare(`
- `L898` `internal sealed class MaterialsModule(RealityProfile profile) : EngineModule(Declare(`
- `L963` `internal sealed class DiagnosticsModule(`

## Accessible members

- `L46` `protected static IReadOnlyList<StageParticipation> NoStagesYet { get; } = [];`
- `L59` `protected static IReadOnlyList<string> NothingOwnedYet { get; } = [];`
- `L61` `public ModuleManifest Manifest { get; } = manifest;`
- `L63` `public abstract void Compose(IModuleComposition composition);`
- `L66` `protected static ModuleManifest Declare(`
- `L100` `public override void Compose(IModuleComposition composition)`
- `L153` `public override void Compose(IModuleComposition composition)`
- `L189` `public override void Compose(IModuleComposition composition)`
- `L219` `public override void Compose(IModuleComposition composition)`
- `L357` `public int Slots => Manifold.Slots;`
- `L359` `public IReadOnlyList<EntityId<IFlowElement>> MeteredPoints => [Custody.Id];`
- `L371` `public string NameOf(EntityId<IFlowElement> element)`
- `L403` `public override void Compose(IModuleComposition composition) =>`
- `L420` `public override void Compose(IModuleComposition composition)`
- `L439` `private static bool IsCustodyTransfer(IAuditTrail audit, AuditId cause)`
- `L525` `public override void Compose(IModuleComposition composition)`
- `L744` `public override void Compose(IModuleComposition composition)`
- `L787` `public override void Compose(IModuleComposition composition)`
- `L818` `public override void Compose(IModuleComposition composition)`
- `L842` `public override void Compose(IModuleComposition composition)`
- `L867` `public override void Compose(IModuleComposition composition) =>`
- `L887` `public override void Compose(IModuleComposition composition) =>`
- `L905` `public override void Compose(IModuleComposition composition)`
- `L948` `private static IFluidPropertyModel Bound(BlackOilModel fluid, IMaterialCatalog catalogue)`
- `L976` `public override void Compose(IModuleComposition composition)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

