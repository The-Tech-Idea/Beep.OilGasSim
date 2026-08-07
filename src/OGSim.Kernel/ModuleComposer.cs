// R1.10 / R1.11 — composition (SDD-001 §9-10, design 03 §3.1, R1 §2.9).
//
// Composition either fully succeeds or refuses to start, naming EVERY unmet
// requirement. There is no partially-composed engine and no degraded mode: that
// is law L2 applied at the module level, so a missing implementation is a
// startup error naming exactly what is missing rather than a silently absent
// behaviour discovered in month 300 of a playthrough.
//
// Concrete by design. Composition is the one layer where concrete types are
// named (design 03 §2), so there is nothing for this to sit behind. It is NOT
// called IModuleRegistry: that name belongs to the content plugin binder in
// Modules.cs, and glossary rule N1 is one concept, one name.

namespace OGSim.Kernel;

public enum CompositionProblemKind
{
    UnmetRequirement,
    DuplicateProvider,
    DuplicateStateKey,
    DependencyCycle,
    StageConflict,
}

public sealed record CompositionProblem(
    CompositionProblemKind Kind,
    ModuleName Module,
    string Detail);

public abstract record CompositionResult;

/// <summary>
/// What the modules provided, after composition. Read-only and resolve-only: the
/// set is closed the moment composition finishes, so a caller can ask for a
/// contract and can never add one.
///
/// <para>Named for CONTRACTS rather than services because "service" is one of
/// glossary rule N3's banned words, and the rule is right here: what this holds
/// is exactly the interfaces design 03 §3.2 calls contracts.</para>
/// </summary>
public interface IResolvedContracts
{
    T Resolve<T>() where T : class;
}

/// <summary>
/// One declared command, with the handlers a module gave it, waiting for the bus
/// (finding 139).
///
/// <para>Deferred rather than registered during Compose because a
/// <see cref="CommandBus"/> needs an audit trail and an event bus, which are
/// themselves composed — so the bus cannot exist until composition is over. What
/// the composer validates is that the pairs EXIST and match what was declared;
/// binding them is the one step left afterwards.</para>
/// </summary>
public sealed record CommandRegistration(Type CommandType, Action<CommandBus> BindTo);

/// <summary>
/// Modules in the order their stages run, every stage they contributed —
/// ordered by (StageId, Order), so what composition validated IS what the tick
/// runs (SDD-001 §9, finding 125) — and what they provided.
/// </summary>
public sealed record Composed(
    IReadOnlyList<IModule> OrderedModules,
    IReadOnlyList<ITickStage> Stages,
    StateRegistry State,
    IResolvedContracts Provided,
    IReadOnlyList<CommandRegistration> Commands) : CompositionResult
{
    // Finding 131.
    public bool Equals(Composed? other) =>
        other is not null
        && Structural.Equal(OrderedModules, other.OrderedModules)
        && Structural.Equal(Stages, other.Stages)
        && ReferenceEquals(State, other.State);

    public override int GetHashCode() =>
        HashCode.Combine(Structural.HashOf(OrderedModules), Structural.HashOf(Stages), State);
}

/// <summary>Every problem, never just the first (R1 §2.9).</summary>
public sealed record CompositionRefused(IReadOnlyList<CompositionProblem> Problems) : CompositionResult
{
    // Finding 131.
    public bool Equals(CompositionRefused? other) =>
        other is not null && Structural.Equal(Problems, other.Problems);

    public override int GetHashCode() => Structural.HashOf(Problems);
}

public sealed class ModuleComposer
{
    /// <summary>
    /// Runs all five checks of R1 §2.9 before constructing anything, then
    /// composes. Validation is complete before any module is built, so a module
    /// can never observe a half-built world.
    /// </summary>
    public CompositionResult Compose(IReadOnlyList<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var problems = new List<CompositionProblem>();

        // Providers are collected first because checks 1 and 2 both read them.
        var providers = new Dictionary<Type, ModuleName>();
        foreach (IModule module in modules)
        {
            ModuleManifest manifest = module.Manifest;
            for (int i = 0; i < manifest.Provides.Count; i++)
            {
                Type contract = manifest.Provides[i];
                // Check 2: no contract provided twice.
                if (providers.TryGetValue(contract, out ModuleName existing))
                    problems.Add(new CompositionProblem(
                        CompositionProblemKind.DuplicateProvider, manifest.Name,
                        $"{contract.Name} is already provided by {existing.Value}."));
                else
                    providers.Add(contract, manifest.Name);
            }
        }

        // Check 1: every Requires is Provided.
        foreach (IModule module in modules)
        {
            ModuleManifest manifest = module.Manifest;
            for (int i = 0; i < manifest.Requires.Count; i++)
            {
                Type contract = manifest.Requires[i];
                if (!providers.ContainsKey(contract))
                    problems.Add(new CompositionProblem(
                        CompositionProblemKind.UnmetRequirement, manifest.Name,
                        $"{contract.Name} is required but no module provides it."));
            }
        }

        // Check 3: no state key owned twice — law L5 at composition time.
        var stateOwners = new Dictionary<StateKey, ModuleName>();
        foreach (IModule module in modules)
        {
            ModuleManifest manifest = module.Manifest;
            for (int i = 0; i < manifest.OwnsState.Count; i++)
            {
                StateKey key = manifest.OwnsState[i];
                if (stateOwners.TryGetValue(key, out ModuleName existing))
                    problems.Add(new CompositionProblem(
                        CompositionProblemKind.DuplicateStateKey, manifest.Name,
                        $"State key '{key.Value}' is already owned by {existing.Value}; " +
                        "two modules cannot own one fact."));
                else
                    stateOwners.Add(key, manifest.Name);
            }
        }

        // Check 5: no two modules claim the same (stage, order).
        var stageSlots = new Dictionary<(StageId, int), ModuleName>();
        foreach (IModule module in modules)
        {
            ModuleManifest manifest = module.Manifest;
            for (int i = 0; i < manifest.Stages.Count; i++)
            {
                StageParticipation participation = manifest.Stages[i];
                var slot = (participation.Stage, participation.Order);
                if (stageSlots.TryGetValue(slot, out ModuleName existing))
                    problems.Add(new CompositionProblem(
                        CompositionProblemKind.StageConflict, manifest.Name,
                        $"Stage {participation.Stage} order {participation.Order} is already " +
                        $"claimed by {existing.Value}; within-stage order must be unambiguous."));
                else
                    stageSlots.Add(slot, manifest.Name);
            }
        }

        // Check 4: the dependency graph is acyclic.
        DetectCycles(modules, providers, problems);

        if (problems.Count > 0) return new CompositionRefused(problems);

        // DEPENDENCY order, not the caller's (finding 126). The cycle check just
        // proved a construction order exists; using the argument order instead
        // would throw from Require whenever a provider happened to sit later in
        // the list, putting knowledge of who-builds-first back in every caller.
        var composition = new Composition(providers);
        foreach (IModule module in ResolutionOrder(modules, providers))
            composition.Composing(module).Compose(composition);

        composition.AssertEveryProviderDelivered(modules, problems);
        composition.AssertEverySlotFilled(modules, problems);
        composition.AssertEveryFactOwned(modules, problems);
        composition.AssertEveryCommandHandled(modules, problems);

        if (problems.Count > 0) return new CompositionRefused(problems);

        return new Composed(
            OrderByStage(modules), composition.OrderedStages(), composition.State,
            composition, composition.Registrations());
    }

    /// <summary>
    /// Topological order over "module A requires a contract module B provides":
    /// DFS post-order, so every provider is composed before its consumer.
    /// Reached only after <see cref="DetectCycles"/> passed, so the recursion
    /// terminates and the order is total.
    /// </summary>
    private static IReadOnlyList<IModule> ResolutionOrder(
        IReadOnlyList<IModule> modules, Dictionary<Type, ModuleName> providers)
    {
        var byName = new Dictionary<ModuleName, IModule>();
        foreach (IModule module in modules) byName[module.Manifest.Name] = module;

        var ordered = new List<IModule>(modules.Count);
        var settled = new HashSet<ModuleName>();

        foreach (IModule module in modules) Walk(module);

        return ordered;

        void Walk(IModule module)
        {
            if (!settled.Add(module.Manifest.Name)) return;

            IReadOnlyList<Type> requires = module.Manifest.Requires;
            for (int i = 0; i < requires.Count; i++)
                if (providers.TryGetValue(requires[i], out ModuleName provider)
                    && provider != module.Manifest.Name
                    && byName.TryGetValue(provider, out IModule? dependency))
                    Walk(dependency);

            ordered.Add(module);
        }
    }

    /// <summary>
    /// Depth-first over "module A requires a contract module B provides". A cycle
    /// means no construction order exists, which the engine must discover now
    /// rather than as a stack overflow during composition.
    /// </summary>
    private static void DetectCycles(
        IReadOnlyList<IModule> modules,
        Dictionary<Type, ModuleName> providers,
        List<CompositionProblem> problems)
    {
        var byName = new Dictionary<ModuleName, IModule>();
        foreach (IModule module in modules) byName[module.Manifest.Name] = module;

        var settled = new HashSet<ModuleName>();
        var onPath = new HashSet<ModuleName>();
        var reported = new HashSet<ModuleName>();

        foreach (IModule module in modules)
            Walk(module.Manifest.Name, []);

        void Walk(ModuleName name, List<ModuleName> path)
        {
            if (settled.Contains(name)) return;

            if (!onPath.Add(name))
            {
                if (reported.Add(name))
                {
                    var cycle = new List<string>();
                    int start = path.IndexOf(name);
                    for (int i = start < 0 ? 0 : start; i < path.Count; i++) cycle.Add(path[i].Value);
                    cycle.Add(name.Value);
                    problems.Add(new CompositionProblem(
                        CompositionProblemKind.DependencyCycle, name,
                        $"Dependency cycle: {string.Join(" -> ", cycle)}."));
                }
                return;
            }

            path.Add(name);
            if (byName.TryGetValue(name, out IModule? module))
            {
                IReadOnlyList<Type> requires = module.Manifest.Requires;
                for (int i = 0; i < requires.Count; i++)
                    if (providers.TryGetValue(requires[i], out ModuleName provider) && provider != name)
                        Walk(provider, path);
            }
            path.RemoveAt(path.Count - 1);

            onPath.Remove(name);
            settled.Add(name);
        }
    }

    /// <summary>
    /// Modules sorted by their earliest (stage, order) claim, so the pipeline
    /// receives them in the sequence they run. Modules with no stage participation
    /// sort last, by name, so the result is total.
    /// </summary>
    private static IReadOnlyList<IModule> OrderByStage(IReadOnlyList<IModule> modules)
    {
        var ordered = new List<IModule>(modules);
        ordered.Sort((left, right) =>
        {
            (int Stage, int Order) a = EarliestSlot(left);
            (int Stage, int Order) b = EarliestSlot(right);
            int byStage = a.Stage.CompareTo(b.Stage);
            if (byStage != 0) return byStage;
            int byOrder = a.Order.CompareTo(b.Order);
            if (byOrder != 0) return byOrder;
            return string.CompareOrdinal(left.Manifest.Name.Value, right.Manifest.Name.Value);
        });
        return ordered;
    }

    private static (int Stage, int Order) EarliestSlot(IModule module)
    {
        IReadOnlyList<StageParticipation> stages = module.Manifest.Stages;
        if (stages.Count == 0) return (int.MaxValue, int.MaxValue);

        int stage = int.MaxValue;
        int order = int.MaxValue;
        for (int i = 0; i < stages.Count; i++)
        {
            int candidateStage = (int)stages[i].Stage;
            if (candidateStage < stage || (candidateStage == stage && stages[i].Order < order))
            {
                stage = candidateStage;
                order = stages[i].Order;
            }
        }
        return (stage, order);
    }

    /// <summary>
    /// The Provide/Require surface handed to each module. Resolution happens
    /// AFTER validation of the whole set (SDD-001 §9), so Require can only ever
    /// see contracts that were proven present.
    /// </summary>
    private sealed class Composition(Dictionary<Type, ModuleName> providers)
        : IModuleComposition, IResolvedContracts
    {
        /// <summary>The same lookup <c>Require</c> uses — after composition, when
        /// the set can no longer change.</summary>
        T IResolvedContracts.Resolve<T>() => Require<T>();


        private readonly Dictionary<Type, object> _implementations = [];
        private readonly List<(StageId Stage, int Order, ITickStage Work)> _stages = [];
        private readonly Dictionary<(StageId, int), ModuleName> _contributors = [];
        private readonly HashSet<StateKey> _owned = [];
        private readonly HashSet<Type> _handled = [];
        private readonly List<CommandRegistration> _commands = [];

        /// <summary>The registry the save walks — populated here and nowhere
        /// else, so a fact reaches a save only by being declared and owned.</summary>
        public StateRegistry State { get; } = new();

        /// <summary>
        /// Which module is being composed right now — needed so a contribution
        /// can be checked against the manifest that claimed the slot. Set by the
        /// composer immediately before each <c>Compose</c> call, which is the
        /// only sequencing this class does.
        /// </summary>
        private IModule? _current;

        public IModule Composing(IModule module)
        {
            _current = module;
            return module;
        }

        public void Provide<T>(T implementation) where T : class
        {
            ArgumentNullException.ThrowIfNull(implementation);
            if (!_implementations.TryAdd(typeof(T), implementation))
                throw new InvariantFault("L5", null,
                    $"{typeof(T).Name} was provided twice during composition.");
        }

        public T Require<T>() where T : class
        {
            if (_implementations.TryGetValue(typeof(T), out object? implementation))
                return (T)implementation;

            // Validation proved a provider exists, so reaching here means it
            // declared the contract and then did not Provide it.
            throw new InvariantFault("SDD-001 §9", null,
                providers.TryGetValue(typeof(T), out ModuleName owner)
                    ? $"{owner.Value} declared {typeof(T).Name} in Provides but never provided it."
                    : $"{typeof(T).Name} is required but was never declared.");
        }

        public void Contribute(int order, ITickStage work)
        {
            ArgumentNullException.ThrowIfNull(work);

            if (_current is null)
                throw new InvariantFault("SDD-001 §9", null,
                    "A stage was contributed outside any module's Compose.");

            ModuleManifest manifest = _current.Manifest;
            var slot = (work.Id, order);

            // A module may only fill a slot it DECLARED. Without this the
            // manifest's stage list would be decorative — check 5 would police
            // an order the tick was free to ignore.
            bool declared = false;
            for (int i = 0; i < manifest.Stages.Count; i++)
                if (manifest.Stages[i].Stage == work.Id && manifest.Stages[i].Order == order)
                {
                    declared = true;
                    break;
                }

            if (!declared)
                throw new InvariantFault("SDD-001 §9", null,
                    $"{manifest.Name.Value} contributed to stage {work.Id} order {order}, " +
                    "which its manifest never declared.");

            if (_contributors.TryGetValue(slot, out ModuleName existing))
                throw new InvariantFault("SDD-001 §9", null,
                    $"Stage {work.Id} order {order} was already filled by {existing.Value}; " +
                    "check 5 should have refused this set.");

            _contributors.Add(slot, manifest.Name);
            _stages.Add((work.Id, order, work));
        }

        public void HandleCommand<TCommand>(
            ICommandValidator<TCommand> validator,
            ICommandApplier<TCommand> applier)
            where TCommand : Command
        {
            ArgumentNullException.ThrowIfNull(validator);
            ArgumentNullException.ThrowIfNull(applier);

            if (_current is null)
                throw new InvariantFault("SDD-001 §9", null,
                    "A command handler was registered outside any module's Compose.");

            ModuleManifest manifest = _current.Manifest;

            // A module may only handle a command it DECLARED — the same rule as
            // stages and state. Without it the engine's input surface would sit
            // outside the set the composer validates.
            if (!manifest.Commands.Contains(typeof(TCommand)))
                throw new InvariantFault("SDD-001 §9", null,
                    $"{manifest.Name.Value} handles {typeof(TCommand).Name}, " +
                    "which its manifest never declared.");

            if (!_handled.Add(typeof(TCommand)))
                throw new InvariantFault("SDD-001 §7", null,
                    $"{typeof(TCommand).Name} already has a handler; a command with two " +
                    "would be applied by whichever registered first.");

            _commands.Add(new CommandRegistration(
                typeof(TCommand), bus => bus.Register(validator, applier)));
        }

        public IReadOnlyList<CommandRegistration> Registrations() => _commands;

        /// <summary>
        /// A command declared and left unhandled. It would reach the bus, find
        /// nothing listening, and fail at the moment a player used it — which is
        /// the latest possible moment to discover it.
        /// </summary>
        public void AssertEveryCommandHandled(
            IReadOnlyList<IModule> modules, List<CompositionProblem> problems)
        {
            foreach (IModule module in modules)
            {
                IReadOnlyList<Type> commands = module.Manifest.Commands;
                for (int i = 0; i < commands.Count; i++)
                    if (!_handled.Contains(commands[i]))
                        problems.Add(new CompositionProblem(
                            CompositionProblemKind.UnmetRequirement, module.Manifest.Name,
                            $"{commands[i].Name} was declared in Commands but no handler " +
                            "was registered for it."));
            }
        }

        public void Own(IStateOwner state)
        {
            ArgumentNullException.ThrowIfNull(state);

            if (_current is null)
                throw new InvariantFault("SDD-001 §9", null,
                    "A state owner was registered outside any module's Compose.");

            ModuleManifest manifest = _current.Manifest;

            // A module may only own a fact it DECLARED. Otherwise OwnsState
            // would police uniqueness among claims while the actual owners went
            // unchecked, and law L5 would hold on paper only.
            bool declared = false;
            for (int i = 0; i < manifest.OwnsState.Count; i++)
                if (manifest.OwnsState[i] == state.Key)
                {
                    declared = true;
                    break;
                }

            if (!declared)
                throw new InvariantFault("L5", null,
                    $"{manifest.Name.Value} owns state '{state.Key.Value}', " +
                    "which its manifest never declared.");

            _owned.Add(state.Key);

            // StateRegistry enforces write-once per key itself; this adds the
            // manifest half, which it cannot see.
            State.Register(state);
        }

        /// <summary>
        /// Every contributed stage, in (stage, order). The pipeline sorts by
        /// <c>StageId</c> alone and is stable, so within-stage order is decided
        /// here — where the declared <c>Order</c> is still in hand.
        /// </summary>
        public IReadOnlyList<ITickStage> OrderedStages()
        {
            var sorted = new List<(StageId Stage, int Order, ITickStage Work)>(_stages);
            sorted.Sort((left, right) =>
            {
                int byStage = ((int)left.Stage).CompareTo((int)right.Stage);
                return byStage != 0 ? byStage : left.Order.CompareTo(right.Order);
            });

            var stages = new List<ITickStage>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++) stages.Add(sorted[i].Work);
            return stages;
        }

        /// <summary>
        /// A module may declare a contract in Provides and then fail to hand one
        /// over. Validation cannot see that — only running Compose can — so it is
        /// checked afterwards and reported the same way.
        /// </summary>
        public void AssertEveryProviderDelivered(
            IReadOnlyList<IModule> modules, List<CompositionProblem> problems)
        {
            foreach (IModule module in modules)
            {
                IReadOnlyList<Type> provides = module.Manifest.Provides;
                for (int i = 0; i < provides.Count; i++)
                    if (!_implementations.ContainsKey(provides[i]))
                        problems.Add(new CompositionProblem(
                            CompositionProblemKind.UnmetRequirement, module.Manifest.Name,
                            $"{provides[i].Name} was declared in Provides but never provided."));
            }
        }

        /// <summary>
        /// The same omission on the stage side: a slot claimed and left empty.
        /// Law L3 — a declaration with no behaviour behind it — and the tick
        /// would silently skip it, which is the failure this refuses to ship.
        /// </summary>
        public void AssertEverySlotFilled(
            IReadOnlyList<IModule> modules, List<CompositionProblem> problems)
        {
            foreach (IModule module in modules)
            {
                IReadOnlyList<StageParticipation> stages = module.Manifest.Stages;
                for (int i = 0; i < stages.Count; i++)
                    if (!_contributors.ContainsKey((stages[i].Stage, stages[i].Order)))
                        problems.Add(new CompositionProblem(
                            CompositionProblemKind.UnmetRequirement, module.Manifest.Name,
                            $"Stage {stages[i].Stage} order {stages[i].Order} was declared " +
                            "in Stages but no work was contributed for it."));
            }
        }

        /// <summary>
        /// The same omission on the state side: a fact declared and left
        /// unowned. It would be validated for uniqueness, never registered, and
        /// silently absent from every save — which is worse than refusing to
        /// save at all, because the loss surfaces only on load.
        /// </summary>
        public void AssertEveryFactOwned(
            IReadOnlyList<IModule> modules, List<CompositionProblem> problems)
        {
            foreach (IModule module in modules)
            {
                IReadOnlyList<StateKey> keys = module.Manifest.OwnsState;
                for (int i = 0; i < keys.Count; i++)
                    if (!_owned.Contains(keys[i]))
                        problems.Add(new CompositionProblem(
                            CompositionProblemKind.DuplicateStateKey, module.Manifest.Name,
                            $"State key '{keys[i].Value}' was declared in OwnsState but " +
                            "no owner was registered for it."));
            }
        }
    }
}
