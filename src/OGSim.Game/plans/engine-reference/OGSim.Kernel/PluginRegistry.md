# PluginRegistry

Source: `src\OGSim.Kernel\PluginRegistry.cs` · Lines: 93

## File intent

> R3.5 — model-plugin binding by name (SDD-001 §9, SDD-004 §5 stage 6).
> 
> This is the front door of non-negotiable 11's second half: rebalancing is a
> content edit, and genuinely new behaviour is A NEW PLUGIN PLUS THE JSON THAT
> NAMES IT. A content entry naming `mean-reverting` gets that price model
> because a module registered one under that name — not because the engine has
> a switch on the string.
> 

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L16` `public sealed class PluginRegistry : IModuleRegistry`

## Accessible members

- `L21` `private readonly Dictionary<(ContentId Name, Type Contract), Func<object>> _factories = [];`
- `L27` `private readonly List<(ContentId Name, Type Contract)> _registered = [];`
- `L35` `public void Register<TContract>(ContentId name, Func<TContract> factory)`
- `L50` `public bool CanBind(ContentId plugin, Type contract)`
- `L62` `public T Bind<T>(ContentId plugin) where T : class`
- `L82` `public IReadOnlyList<ContentId> RegisteredFor(Type contract)`

