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

/// <summary>Modules in the order their stages run — the tick pipeline's input.</summary>
public sealed record Composed(IReadOnlyList<IModule> OrderedModules) : CompositionResult;

/// <summary>Every problem, never just the first (R1 §2.9).</summary>
public sealed record CompositionRefused(IReadOnlyList<CompositionProblem> Problems) : CompositionResult;

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

        var composition = new Composition(providers);
        foreach (IModule module in modules) module.Compose(composition);
        composition.AssertEveryProviderDelivered(modules, problems);

        if (problems.Count > 0) return new CompositionRefused(problems);

        return new Composed(OrderByStage(modules));
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
    private sealed class Composition(Dictionary<Type, ModuleName> providers) : IModuleComposition
    {
        private readonly Dictionary<Type, object> _implementations = [];

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
    }
}
