# ModuleComposer

Source: `src\OGSim.Kernel\ModuleComposer.cs` · Lines: 551

## File intent

> R1.10 / R1.11 — composition (SDD-001 §9-10, design 03 §3.1, R1 §2.9).
> 
> Composition either fully succeeds or refuses to start, naming EVERY unmet
> requirement. There is no partially-composed engine and no degraded mode: that
> is law L2 applied at the module level, so a missing implementation is a
> startup error naming exactly what is missing rather than a silently absent
> behaviour discovered in month 300 of a playthrough.
> 

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L16` `public enum CompositionProblemKind`
- `L25` `public sealed record CompositionProblem(`
- `L30` `public abstract record CompositionResult;`
- `L41` `public interface IResolvedContracts`
- `L56` `public sealed record CommandRegistration(Type CommandType, Action<CommandBus> BindTo);`
- `L76` `internal sealed class ManifestPromise<TKey>(`
- `L124` `public sealed record Composed(`
- `L143` `public sealed record CompositionRefused(IReadOnlyList<CompositionProblem> Problems) : CompositionResult`
- `L152` `public sealed class ModuleComposer`
- `L389` `private sealed class Composition(Dictionary<Type, ModuleName> providers)`

## Accessible members

- `L81` `private readonly Dictionary<TKey, ModuleName> _delivered = [];`
- `L83` `public void Deliver(ModuleManifest manifest, TKey key, string described)`
- `L98` `public bool WasDelivered(TKey key) => _delivered.ContainsKey(key);`
- `L100` `public void AssertComplete(`
- `L132` `public bool Equals(Composed? other) =>`
- `L138` `public override int GetHashCode() =>`
- `L146` `public bool Equals(CompositionRefused? other) =>`
- `L149` `public override int GetHashCode() => Structural.HashOf(Problems);`
- `L159` `public CompositionResult Compose(IReadOnlyList<IModule> modules)`
- `L262` `private static IReadOnlyList<IModule> ResolutionOrder(`
- `L295` `private static void DetectCycles(`
- `L349` `private static IReadOnlyList<IModule> OrderByStage(IReadOnlyList<IModule> modules)`
- `L365` `private static (int Stage, int Order) EarliestSlot(IModule module)`
- `L397` `private readonly Dictionary<Type, object> _implementations = [];`
- `L398` `private readonly List<(StageId Stage, int Order, ITickStage Work)> _stages = [];`
- `L399` `private readonly List<CommandRegistration> _commands = [];`
- `L403` `private readonly ManifestPromise<Type> _provides =`
- `L406` `private readonly ManifestPromise<StateKey> _ownsState =`
- `L409` `private readonly ManifestPromise<(StageId Stage, int Order)> _stageSlots =`
- `L413` `private readonly ManifestPromise<Type> _commandTypes =`
- `L418` `public StateRegistry State { get; } = new();`
- `L426` `private IModule? _current;`
- `L428` `public IModule Composing(IModule module)`
- `L438` `private IModule Current =>`
- `L442` `public void Provide<T>(T implementation) where T : class`
- `L456` `public T Require<T>() where T : class`
- `L469` `public void Contribute(int order, ITickStage work)`
- `L479` `public void HandleCommand<TCommand>(`
- `L493` `public IReadOnlyList<CommandRegistration> Registrations() => _commands;`
- `L495` `public void Own(IStateOwner state)`
- `L512` `public void AssertEveryPromiseKept(`
- `L537` `public IReadOnlyList<ITickStage> OrderedStages()`

