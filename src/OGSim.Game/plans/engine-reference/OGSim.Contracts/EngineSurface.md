# EngineSurface

Source: `src\OGSim.Contracts\EngineSurface.cs` · Lines: 474

## File intent

> SDD-017 — the complete public surface. Nothing else exists: commands in,
> read model out, sealed events polled, audit queried. The read model is an
> immutable record tree built from beliefs — never truth (R21-V4).
> NO AdvisorView: the Advisor is a client (SDD-015 §1).
> <summary>The renderable world — what a map screen draws under the entity layers.</summary>

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L11` `public sealed record WorldView(`
- `L44` `public interface IEngine`
- `L73` `public sealed record EngineSetup(`
- `L95` `public abstract record EngineStartResult;`
- `L96` `public sealed record EngineStarted(IEngine Engine) : EngineStartResult;`
- `L99` `public sealed record EngineRefused(IReadOnlyList<LoadFailure> Reasons) : EngineStartResult`
- `L118` `public sealed record EngineCompositionRefused(`
- `L133` `public interface IEngineFactory`
- `L144` `public sealed record CompanyView(`
- `L157` `public sealed record FieldView(`
- `L184` `public sealed record CompartmentView(`
- `L190` `public sealed record WellView(`
- `L218` `public sealed record FacilityView(`
- `L241` `public sealed record OperationView(`
- `L249` `public sealed record LogisticsView(`
- `L266` `public sealed record MarketView(`
- `L279` `public sealed record HseView(`
- `L300` `public sealed record EnvironmentView(`
- `L318` `public sealed record BeliefEntryView(`
- `L327` `public sealed record BeliefView(`
- `L344` `public sealed record ExplorationView(`
- `L365` `public sealed record FinanceView(`
- `L379` `public sealed record ObjectiveView(`
- `L395` `public sealed record ReadModel(`
- `L450` `public sealed record ProductionLossReport(`
- `L468` `public interface IAuditQuery`

## Accessible members

- `L23` `public bool Equals(WorldView? other) =>`
- `L32` `public override int GetHashCode() =>`
- `L81` `public bool Equals(EngineSetup? other) =>`
- `L87` `public override int GetHashCode() =>`
- `L102` `public bool Equals(EngineRefused? other) =>`
- `L105` `public override int GetHashCode() => Structural.HashOf(Reasons);`
- `L122` `public bool Equals(EngineCompositionRefused? other) =>`
- `L125` `public override int GetHashCode() => Structural.HashOf(Problems);`
- `L168` `public bool Equals(FieldView? other) =>`
- `L177` `public override int GetHashCode() =>`
- `L202` `public bool Equals(WellView? other) =>`
- `L212` `public override int GetHashCode() =>`
- `L228` `public bool Equals(FacilityView? other) =>`
- `L236` `public override int GetHashCode() =>`
- `L255` `public bool Equals(LogisticsView? other) =>`
- `L261` `public override int GetHashCode() =>`
- `L271` `public bool Equals(MarketView? other) =>`
- `L275` `public override int GetHashCode() =>`
- `L287` `public bool Equals(HseView? other) =>`
- `L295` `public override int GetHashCode() =>`
- `L307` `public bool Equals(EnvironmentView? other) =>`
- `L313` `public override int GetHashCode() =>`
- `L333` `public bool Equals(BeliefView? other) =>`
- `L339` `public override int GetHashCode() =>`
- `L351` `public bool Equals(ExplorationView? other) =>`
- `L358` `public override int GetHashCode() =>`
- `L370` `public bool Equals(FinanceView? other) =>`
- `L375` `public override int GetHashCode() =>`
- `L385` `public bool Equals(ObjectiveView? other) =>`
- `L390` `public override int GetHashCode() =>`
- `L415` `public bool Equals(ReadModel? other) =>`
- `L426` `public override int GetHashCode()`
- `L458` `public bool Equals(ProductionLossReport? other) =>`
- `L463` `public override int GetHashCode() =>`

## Imports

- `using OGSim.Kernel;`

