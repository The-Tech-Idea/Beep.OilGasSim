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
}
