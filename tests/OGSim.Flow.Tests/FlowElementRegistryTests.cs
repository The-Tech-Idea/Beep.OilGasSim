// R20c — collecting the field across modules (SDD-002 §6, finding 130).
//
// The registry is what lets stage 5 see elements four different modules created.
// Its one subtle job is the per-segment VIEW: an unavailable element is ABSENT
// from the network, not present at zero rate (design 04 §4), and every
// connection touching it goes with it. A non-linear solve does not average, so
// "the compressor was down for half the month" is two segments, not one segment
// at half capacity — and this is the code that makes the halves different.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Flow.Tests;

public sealed class FlowElementRegistryTests
{
    private static EntityRef Ref(ulong id) => new(EntityKind.FlowElement, id);

    private static FlowConnection Wire(ulong from, int fromPort, ulong to, int toPort) =>
        new(new EntityId<IFlowElement>(from), new PortId(fromPort),
            new EntityId<IFlowElement>(to), new PortId(toPort));

    /// <summary>Ports 0 and 0 — enough for the registry's own checks, which
    /// never look at a port.</summary>
    private static FlowConnection Wire(ulong from, ulong to) => Wire(from, 0, to, 0);

    /// <summary>A well feeding a separator feeding a tank — the shortest chain
    /// that has a middle to remove.</summary>
    private static FlowElementRegistry Chain()
    {
        var registry = new FlowElementRegistry();

        registry.Add(new Source(1, oil: 10.0, gas: 1.0));
        registry.Add(new Restrictor(2, capacityKgPerSecond: 100.0));
        registry.Add(new Sink(3));

        // The restrictor's outlet is port 1, not port 0 — the solver validates
        // ports, so the chain has to be wired the way the elements declare.
        registry.Connect(Wire(1, 0, 2, 0));
        registry.Connect(Wire(2, 1, 3, 0));

        return registry;
    }

    [Fact]
    public void The_view_holds_every_available_element_and_its_wiring()
    {
        FlowTopology view = Chain().ViewFor([Ref(1), Ref(2), Ref(3)]);

        Assert.Equal(3, view.Elements.Count);
        Assert.Equal(2, view.Connections.Count);
    }

    /// <summary>
    /// The headline: an unavailable element is absent, and so is every
    /// connection that touched it. Leaving the edges behind would hand the
    /// solver a connection to an element the topology does not contain.
    /// </summary>
    [Fact]
    public void An_unavailable_element_and_its_connections_are_absent()
    {
        FlowTopology view = Chain().ViewFor([Ref(1), Ref(3)]);   // separator down

        Assert.Equal([1UL, 3UL], view.Elements.Select(e => e.Id.Value).ToArray());
        Assert.Empty(view.Connections);
    }

    /// <summary>
    /// A view does not modify the registry: the four segments of one tick each
    /// see the same field, and an abandoned tick has nothing to undo.
    /// </summary>
    [Fact]
    public void Taking_a_view_leaves_the_registry_untouched()
    {
        FlowElementRegistry registry = Chain();

        registry.ViewFor([Ref(1)]);
        FlowTopology full = registry.ViewFor([Ref(1), Ref(2), Ref(3)]);

        Assert.Equal(3, full.Elements.Count);
        Assert.Equal(2, full.Connections.Count);
    }

    [Fact] // Availability that names nothing registered yields an empty network
    public void An_empty_availability_set_yields_an_empty_view()
    {
        FlowTopology view = Chain().ViewFor([]);

        Assert.Empty(view.Elements);
        Assert.Empty(view.Connections);
    }

    /// <summary>
    /// Element order is registration order, which is issue order — never a
    /// Dictionary walk (rule D-5). The solver re-sorts topologically, but the
    /// input it re-sorts must not depend on hashing.
    /// </summary>
    [Fact]
    public void The_view_preserves_registration_order()
    {
        var registry = new FlowElementRegistry();
        for (ulong id = 5; id >= 1; id--) registry.Add(new Sink(id));

        FlowTopology view = registry.ViewFor(
            [Ref(1), Ref(2), Ref(3), Ref(4), Ref(5)]);

        Assert.Equal([5UL, 4UL, 3UL, 2UL, 1UL], view.Elements.Select(e => e.Id.Value).ToArray());
    }

    [Fact] // One id is one element (law L5)
    public void Registering_one_element_twice_is_refused()
    {
        var registry = new FlowElementRegistry();
        registry.Add(new Sink(1));

        Assert.Throws<InvariantFault>(() => registry.Add(new Sink(1)));
    }

    /// <summary>
    /// A connection to an unregistered element is caught HERE, naming the
    /// element — rather than deep inside a solve, naming nothing useful.
    /// </summary>
    [Fact]
    public void Connecting_an_unregistered_element_is_refused()
    {
        var registry = new FlowElementRegistry();
        registry.Add(new Sink(1));

        InvariantFault fault = Assert.Throws<InvariantFault>(() => registry.Connect(Wire(1, 99)));
        Assert.Contains("99", fault.Message, StringComparison.Ordinal);
    }

    [Fact] // The topology is a tree toward each sink (FD4)
    public void Connecting_an_element_to_itself_is_refused()
    {
        var registry = new FlowElementRegistry();
        registry.Add(new Sink(1));

        Assert.Throws<InvariantFault>(() => registry.Connect(Wire(1, 1)));
    }

    /// <summary>
    /// The whole point: a view of the real registry solves. If the topology it
    /// produced were not one the solver accepts, everything above would be
    /// checking the wrong shape.
    /// </summary>
    [Fact]
    public void A_view_from_the_registry_is_a_topology_the_solver_solves()
    {
        FlowTopology view = Chain().ViewFor([Ref(1), Ref(2), Ref(3)]);

        var solver = new FlowSolver(SolverSettings.Pinned, new NullAudit());
        SolveReport report = solver.Solve(
            new SegmentContext(DurationDays: 30, Temperature.FromCelsius(20.0), 0.0), view);

        Assert.Equal(3, report.Solutions.Count);
    }

    private sealed class NullAudit : IAuditTrail
    {
        private ulong _next;

        public AuditId Record(
            AuditCategory category, EntityRef? subject, AuditId? cause,
            IReadOnlyDictionary<string, AuditValue> data) => new(++_next);

        public IReadOnlyList<AuditEntry> Query(AuditQuery query) => [];
    }
}
