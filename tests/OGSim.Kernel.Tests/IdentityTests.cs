// R1.2 — the entity registry (SDD-001 §2). R1 goal G2: "identity is stable and
// total — every id resolves or raises a fault; no reference-equality keying
// anywhere". R1-V7: an unregistered id raises a resolution fault, never null.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class IdentityTests
{
    private sealed class Well { public string Name { get; init; } = ""; }
    private sealed class Facility { public string Name { get; init; } = ""; }

    // ------------------------------------------------------------- issue / register

    [Fact] // Ids are sequential per type and start at 1
    public void R1V7_ids_are_sequential_per_type_and_begin_at_one()
    {
        var registry = new EntityRegistry();

        Assert.Equal(1UL, registry.Issue<Well>().Value);
        Assert.Equal(2UL, registry.Issue<Well>().Value);
        Assert.Equal(3UL, registry.Issue<Well>().Value);

        // A different type has its own sequence — ids are typed, not global.
        Assert.Equal(1UL, registry.Issue<Facility>().Value);
        Assert.Equal(4UL, registry.Issue<Well>().Value);
    }

    [Fact] // Beginning at 1 is what makes the default id detectable
    public void R1V7_default_id_is_never_valid()
    {
        var registry = new EntityRegistry();
        registry.Register(registry.Issue<Well>(), new Well { Name = "A-01" });

        EntityId<Well> uninitialised = default;
        Assert.Equal(0UL, uninitialised.Value);

        var fault = Assert.Throws<InvariantFault>(() => registry.Resolve(uninitialised));
        Assert.Equal(FaultClass.Invariant, fault.Fault.Class);
        Assert.Equal("INV3", fault.Fault.Rule);

        Assert.False(registry.TryResolve(uninitialised, out Well? absent));
        Assert.Null(absent);
    }

    [Fact] // The round trip the registry exists for
    public void R1V7_a_registered_entity_resolves()
    {
        var registry = new EntityRegistry();
        var well = new Well { Name = "A-01" };

        EntityId<Well> id = registry.Issue<Well>();
        registry.Register(id, well);

        Assert.Same(well, registry.Resolve(id));
        Assert.True(registry.TryResolve(id, out Well? found));
        Assert.Same(well, found);
    }

    // ------------------------------------------------------------- faults

    [Fact] // R1-V7: never null. An id that does not resolve halts (design 09 §5.1 C5)
    public void R1V7_an_unissued_id_is_an_invariant_fault()
    {
        var registry = new EntityRegistry();
        registry.Issue<Well>();

        var fault = Assert.Throws<InvariantFault>(() => registry.Resolve(new EntityId<Well>(99)));
        Assert.Equal("INV3", fault.Fault.Rule);
        Assert.Contains("never issued", fault.Fault.Detail);

        Assert.False(registry.TryResolve(new EntityId<Well>(99), out Well? absent));
        Assert.Null(absent);
    }

    [Fact] // The gap between Issue and Register is loud, not silent
    public void R1V7_an_issued_but_unregistered_id_is_an_invariant_fault()
    {
        var registry = new EntityRegistry();
        EntityId<Well> id = registry.Issue<Well>();

        var fault = Assert.Throws<InvariantFault>(() => registry.Resolve(id));
        Assert.Contains("never registered", fault.Fault.Detail);

        // TryResolve is the one place absence is a legitimate answer.
        Assert.False(registry.TryResolve(id, out Well? absent));
        Assert.Null(absent);
    }

    [Fact] // Law L5: one owner per fact — a reference cannot be repointed
    public void L5_registration_is_write_once()
    {
        var registry = new EntityRegistry();
        EntityId<Well> id = registry.Issue<Well>();
        registry.Register(id, new Well { Name = "A-01" });

        var fault = Assert.Throws<InvariantFault>(
            () => registry.Register(id, new Well { Name = "A-02" }));
        Assert.Contains("already registered", fault.Fault.Detail);
        Assert.Equal("A-01", registry.Resolve(id).Name);
    }

    // ------------------------------------------------------------- enumeration

    [Fact] // D-5: All() is ordered by id, so iteration is deterministic
    public void D5_all_enumerates_in_id_order()
    {
        var registry = new EntityRegistry();
        var ids = new List<EntityId<Well>>();
        for (int i = 0; i < 5; i++) ids.Add(registry.Issue<Well>());

        // Registered deliberately out of order: order must follow the ID, not
        // the insertion sequence, or a save would replay differently.
        for (int i = 4; i >= 0; i--)
            registry.Register(ids[i], new Well { Name = $"W-{i}" });

        IReadOnlyList<Well> all = registry.All<Well>();
        Assert.Equal(5, all.Count);
        for (int i = 0; i < 5; i++) Assert.Equal($"W-{i}", all[i].Name);
    }

    [Fact] // Typed registries do not leak into each other
    public void R1V7_types_have_independent_tables()
    {
        var registry = new EntityRegistry();
        registry.Register(registry.Issue<Well>(), new Well { Name = "A-01" });
        registry.Register(registry.Issue<Facility>(), new Facility { Name = "CPF" });

        Assert.Single(registry.All<Well>());
        Assert.Single(registry.All<Facility>());
        Assert.Equal("A-01", registry.All<Well>()[0].Name);
        Assert.Equal("CPF", registry.All<Facility>()[0].Name);
    }

    // ------------------------------------------------------------- save stability

    [Fact] // Ids must continue after a load, never restart onto live entities
    public void R1V7_the_high_water_mark_survives_a_restore()
    {
        var before = new EntityRegistry();
        for (int i = 0; i < 7; i++) before.Register(before.Issue<Well>(), new Well());
        Assert.Equal(7UL, before.HighWaterMark<Well>());

        var after = new EntityRegistry();
        after.RestoreHighWaterMark<Well>(7);
        Assert.Equal(8UL, after.Issue<Well>().Value);   // continues, does not collide

        // Restoring onto a registry that has already issued would silently
        // renumber live entities.
        Assert.Throws<InvariantFault>(() => after.RestoreHighWaterMark<Well>(20));
    }
}
