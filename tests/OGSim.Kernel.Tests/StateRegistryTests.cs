// R1.11 — state registration (SDD-001 §10). Registration only: the save format,
// module-block framing, canonical bytes and migrations are SDD-013 and belong to
// R19. What is proved here is that ownership is exclusive and the visit order is
// fixed, which are the two properties R19's byte-identity digest rests on.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class StateRegistryTests
{
    private sealed class Owner(string key, int schemaVersion = 1) : IStateOwner
    {
        public StateKey Key { get; } = new(key);
        public int SchemaVersion { get; } = schemaVersion;

        public IReadOnlyList<StateKey> RestoreAfter { get; init; } = [];
        public void Capture(IStateWriter writer) => writer.WriteString("k", Key.Value);
        public void Restore(IStateReader reader) => reader.ReadString("k");
    }

    [Fact] // Law L5 at the persistence boundary: one owner per fact
    public void L5_a_state_key_cannot_be_claimed_twice()
    {
        var registry = new StateRegistry();
        registry.Register(new Owner("subsurface"));

        var fault = Assert.Throws<InvariantFault>(() => registry.Register(new Owner("subsurface")));
        Assert.Equal("L5", fault.Fault.Rule);
        Assert.Contains("cannot claim one block", fault.Fault.Detail);
    }

    [Fact] // Visit order is a property of the keys, not of composition order
    public void R1V11_owners_are_visited_in_key_order_not_registration_order()
    {
        var registry = new StateRegistry();
        registry.Register(new Owner("wells"));
        registry.Register(new Owner("company"));
        registry.Register(new Owner("subsurface"));

        Assert.Equal(["company", "subsurface", "wells"],
                     registry.Owners.Select(o => o.Key.Value).ToArray());

        // Registering in a different order must produce the identical sequence,
        // or a save's bytes would depend on how the engine was composed.
        var other = new StateRegistry();
        other.Register(new Owner("subsurface"));
        other.Register(new Owner("wells"));
        other.Register(new Owner("company"));

        Assert.Equal(registry.Owners.Select(o => o.Key.Value).ToArray(),
                     other.Owners.Select(o => o.Key.Value).ToArray());
    }

    [Fact] // A block nobody owns means the module that owned it is gone
    public void R1V11_resolving_an_unowned_key_is_a_fault_not_an_empty_result()
    {
        var registry = new StateRegistry();
        registry.Register(new Owner("wells"));

        Assert.Throws<InvariantFault>(() => registry.Resolve(new StateKey("subsurface")));
        Assert.False(registry.TryGet(new StateKey("subsurface"), out IStateOwner? absent));
        Assert.Null(absent);

        Assert.True(registry.TryGet(new StateKey("wells"), out IStateOwner? found));
        Assert.NotNull(found);
    }

    [Fact] // Versions start at 1 so an unset field cannot pass for a valid one
    public void R1V11_a_malformed_owner_is_refused_at_registration()
    {
        var registry = new StateRegistry();

        Assert.Throws<InvariantFault>(() => registry.Register(new Owner("wells", schemaVersion: 0)));
        Assert.Throws<InvariantFault>(() => registry.Register(new Owner("", schemaVersion: 1)));
        Assert.Equal(0, registry.Count);
    }

    // -------------------------------------------- the restore order (S013-5)

    private static StateRegistry Registered(params IStateOwner[] owners)
    {
        var registry = new StateRegistry();
        foreach (IStateOwner owner in owners) registry.Register(owner);

        return registry;
    }

    private static List<string> Keys(IReadOnlyList<IStateOwner> owners)
    {
        var keys = new List<string>(owners.Count);
        for (var i = 0; i < owners.Count; i++) keys.Add(owners[i].Key.Value);

        return keys;
    }

    /// <summary>
    /// PV4b (SDD-013 §2b). CAPTURE ORDER AND RESTORE ORDER ARE DIFFERENT ORDERS,
    /// and only one of them can be the key's.
    ///
    /// <para>The real case, spelled with the real keys: `world` sorts after
    /// `wells` and has to be restored before it, because reopening a well
    /// measures its gathering line against a header that block holds. Key order
    /// gets that exactly backwards, which is what the loader did until this
    /// existed (finding 201).</para>
    /// </summary>
    [Fact]
    public void PV4b_an_owner_is_restored_after_what_it_declares()
    {
        StateRegistry registry = Registered(
            new Owner("wells") { RestoreAfter = [new StateKey("world"), new StateKey("subsurface")] },
            new Owner("world"),
            new Owner("subsurface"),
            new Owner("company"));

        Assert.Equal(["company", "subsurface", "wells", "world"], Keys(registry.Owners));
        Assert.Equal(["company", "subsurface", "world", "wells"], Keys(registry.RestoreOrder));
    }

    /// <summary>
    /// AND KEY ORDER IS THE TIE-BREAK, so the sort is total and deterministic
    /// (D-5) and adds an order only where a dependency states one. Without this
    /// the restore order would be an implementation detail of the sort, and two
    /// builds could disagree about it.
    /// </summary>
    [Fact]
    public void PV4b_owners_with_no_dependency_keep_key_order()
    {
        StateRegistry registry = Registered(
            new Owner("zulu"), new Owner("alpha"), new Owner("mike"));

        Assert.Equal(Keys(registry.Owners), Keys(registry.RestoreOrder));
    }

    /// <summary>
    /// PV4c. A CYCLE IS REFUSED AND EVERY KEY IN IT IS NAMED, because "there is a
    /// cycle" leaves the reader to find it.
    /// </summary>
    [Fact]
    public void PV4c_a_cyclic_restore_order_is_refused_naming_the_cycle()
    {
        StateRegistry registry = Registered(
            new Owner("wells") { RestoreAfter = [new StateKey("world")] },
            new Owner("world") { RestoreAfter = [new StateKey("wells")] });

        var fault = Assert.Throws<InvariantFault>(() => registry.RestoreOrder);

        Assert.Contains("wells", fault.Fault.Detail, StringComparison.Ordinal);
        Assert.Contains("world", fault.Fault.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// AND A KEY NAMING NO OWNER IS REFUSED RATHER THAN SKIPPED. Skipping would
    /// make a typo a silent re-ordering: the owner would restore early, in key
    /// order, exactly as though it had declared nothing — which is the failure
    /// this member exists to prevent, reintroduced by a spelling mistake.
    /// </summary>
    [Fact]
    public void PV4c_a_dependency_on_an_unowned_key_is_refused()
    {
        StateRegistry registry = Registered(
            new Owner("wells") { RestoreAfter = [new StateKey("wrold")] });

        var fault = Assert.Throws<InvariantFault>(() => registry.RestoreOrder);

        Assert.Contains("wrold", fault.Fault.Detail, StringComparison.Ordinal);
        Assert.Contains("no module owns", fault.Fault.Detail, StringComparison.Ordinal);
    }
}
