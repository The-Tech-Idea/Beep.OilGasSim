// R3.5 — model-plugin binding by name (SDD-001 §9, SDD-004 §5 stage 6).
//
// This is the front door of non-negotiable 11's second half: rebalancing is a
// content edit, and genuinely new behaviour is A NEW PLUGIN PLUS THE JSON THAT
// NAMES IT. A content entry naming `mean-reverting` gets that price model
// because a module registered one under that name — not because the engine has
// a switch on the string.
//
// Registration lives on the concrete class, not on IModuleRegistry: modules
// register at composition time, and a module handed the interface must be able
// to resolve a plugin without being able to substitute another module's. Same
// shape as CommandBus.Register and SimulationClock.Advance (SDD-001 §12b).

namespace OGSim.Kernel;

public sealed class PluginRegistry : IModuleRegistry
{
    // Keyed by (name, contract): one name may legitimately serve two different
    // contracts — `simple` is a reasonable id for both a hydraulic model and a
    // separation model, and they are different plugins.
    private readonly Dictionary<(ContentId Name, Type Contract), Func<object>> _factories = [];

    /// <summary>
    /// A factory rather than an instance: some plugins are per-element (a
    /// pipeline's hydraulic model) and some are singletons for a run. Deferring
    /// construction lets composition decide, and keeps a plugin from being built
    /// during content load — which happens before the engine exists.
    /// </summary>
    public void Register<TContract>(ContentId name, Func<TContract> factory)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        var key = (name, typeof(TContract));
        if (_factories.ContainsKey(key))
            throw new InvariantFault("L5", null,
                $"Plugin '{name}' is already registered for {typeof(TContract).Name}; " +
                "two modules cannot provide one plugin under one contract.");

        _factories.Add(key, factory);
    }

    public bool CanBind(ContentId plugin, Type contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return _factories.ContainsKey((plugin, contract));
    }

    /// <summary>
    /// Unbound is an INVARIANT fault, not a null: stage 6 already proved every
    /// named plugin resolves, so reaching here means composition and content
    /// disagree about what exists — and continuing would mean running a
    /// simulation whose models are not the ones the content asked for.
    /// </summary>
    public T Bind<T>(ContentId plugin) where T : class
    {
        if (!_factories.TryGetValue((plugin, typeof(T)), out Func<object>? factory))
            throw new InvariantFault("SDD-004 §5", null,
                $"No plugin '{plugin}' for {typeof(T).Name}; content load should have " +
                "refused this at stage 6.");

        return (T)factory();
    }

    // A `RegisteredFor(contract)` helper was written here and removed: nothing
    // called it, and it enumerated the factory Dictionary — which rule D-5
    // forbids even though the result was sorted afterwards. The rule's value is
    // that it needs no reasoning about whether a particular enumeration happens
    // to be safe, and "I sorted it after" is exactly the argument that stops
    // being true when someone edits the method later. If a load report ever
    // wants to list the available plugin names, that is the point to add the
    // ordered structure it needs (law L3: no member without a consumer).
}
