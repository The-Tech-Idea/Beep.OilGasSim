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
/// One list on a manifest, and what modules actually delivered against it
/// (finding 140).
///
/// <para>A manifest makes five promises — Provides, Requires, OwnsState, Stages,
/// Commands — and for most of the project's life only Requires was checked
/// against anything. Findings 125, 127 and 139 fixed three of the others, one at
/// a time, each with its own near-identical pair of loops; the fourth, Provides,
/// was still checked in one direction only, so a module could hand over a
/// contract it never declared. Four bespoke implementations of one rule is how
/// the fifth list would have gone unchecked too.</para>
///
/// <para>The rule, stated once: <b>a module may deliver only what it declared,
/// may deliver it only once, and must deliver everything it declared.</b> The
/// first two throw — they are the module's own mistake, visible at the line that
/// made it — and the third accumulates into the refusal, because a whole set's
/// missing pieces should be reported together.</para>
/// </summary>
internal sealed class ManifestPromise<TKey>(
    string noun,
    Func<ModuleManifest, IReadOnlyList<TKey>> declaredIn)
    where TKey : notnull
{
    private readonly Dictionary<TKey, ModuleName> _delivered = [];

    public void Deliver(ModuleManifest manifest, TKey key, string described)
    {
        if (!declaredIn(manifest).Contains(key))
            throw new InvariantFault("SDD-001 §9", null,
                $"{manifest.Name.Value} delivered {noun} {described}, " +
                "which its manifest never declared.");

        if (_delivered.TryGetValue(key, out ModuleName existing))
            throw new InvariantFault("SDD-001 §9", null,
                $"{noun} {described} was already delivered by {existing.Value}; " +
                "two of one is one too many.");

        _delivered.Add(key, manifest.Name);
    }

    public bool WasDelivered(TKey key) => _delivered.ContainsKey(key);

    public void AssertComplete(
        IReadOnlyList<IModule> modules,
        List<CompositionProblem> problems,
        CompositionProblemKind kind,
        Func<TKey, string> describe)
    {
        foreach (IModule module in modules)
        {
            IReadOnlyList<TKey> declared = declaredIn(module.Manifest);

            for (int i = 0; i < declared.Count; i++)
                if (!_delivered.ContainsKey(declared[i]))
                    problems.Add(new CompositionProblem(
                        kind, module.Manifest.Name,
                        $"{noun} {describe(declared[i])} was declared but never delivered."));
        }
    }
}

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

        composition.AssertEveryPromiseKept(modules, problems);

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
        private readonly List<CommandRegistration> _commands = [];

        // One rule, four lists. Adding a sixth promise to the manifest means
        // adding one of these, not another pair of loops (finding 140).
        private readonly ManifestPromise<Type> _provides =
            new("contract", manifest => manifest.Provides);

        private readonly ManifestPromise<StateKey> _ownsState =
            new("state key", manifest => manifest.OwnsState);

        private readonly ManifestPromise<(StageId Stage, int Order)> _stageSlots =
            new("stage slot",
                manifest => [.. manifest.Stages.Select(s => (s.Stage, s.Order))]);

        private readonly ManifestPromise<Type> _commandTypes =
            new("command", manifest => manifest.Commands);

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

        /// <summary>
        /// The module being composed. Every delivery goes through here, so
        /// "outside any module's Compose" is one message rather than four.
        /// </summary>
        private IModule Current =>
            _current ?? throw new InvariantFault("SDD-001 §9", null,
                "A module delivered something outside any module's Compose.");

        public void Provide<T>(T implementation) where T : class
        {
            ArgumentNullException.ThrowIfNull(implementation);

            // Checked in BOTH directions now (finding 140). Providing a contract
            // the manifest never declared used to be allowed, so a consumer's
            // Require could be satisfied by something no manifest mentions — and
            // the dependency graph the composer orders modules by would have a
            // missing edge.
            _provides.Deliver(Current.Manifest, typeof(T), typeof(T).Name);

            _implementations.Add(typeof(T), implementation);
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

            _stageSlots.Deliver(
                Current.Manifest, (work.Id, order), $"{work.Id} order {order}");

            _stages.Add((work.Id, order, work));
        }

        public void HandleCommand<TCommand>(
            ICommandValidator<TCommand> validator,
            ICommandApplier<TCommand> applier)
            where TCommand : Command
        {
            ArgumentNullException.ThrowIfNull(validator);
            ArgumentNullException.ThrowIfNull(applier);

            _commandTypes.Deliver(Current.Manifest, typeof(TCommand), typeof(TCommand).Name);

            _commands.Add(new CommandRegistration(
                typeof(TCommand), bus => bus.Register(validator, applier)));
        }

        public IReadOnlyList<CommandRegistration> Registrations() => _commands;

        public void Own(IStateOwner state)
        {
            ArgumentNullException.ThrowIfNull(state);

            _ownsState.Deliver(Current.Manifest, state.Key, state.Key.Value);

            // StateRegistry enforces write-once per key itself; this adds the
            // manifest half, which it cannot see.
            State.Register(state);
        }

        /// <summary>
        /// Every promise on every manifest, checked in one pass (finding 140).
        /// A declared contract nobody provided, a stage slot nobody filled, a
        /// fact nobody owns, a command nobody handles — one rule, one loop, and
        /// a sixth list would join it by being added to this array.
        /// </summary>
        public void AssertEveryPromiseKept(
            IReadOnlyList<IModule> modules, List<CompositionProblem> problems)
        {
            _provides.AssertComplete(
                modules, problems, CompositionProblemKind.UnmetRequirement,
                contract => contract.Name);

            _stageSlots.AssertComplete(
                modules, problems, CompositionProblemKind.UnmetRequirement,
                slot => $"{slot.Stage} order {slot.Order}");

            _ownsState.AssertComplete(
                modules, problems, CompositionProblemKind.DuplicateStateKey,
                key => key.Value);

            _commandTypes.AssertComplete(
                modules, problems, CompositionProblemKind.UnmetRequirement,
                command => command.Name);
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
    }
}
