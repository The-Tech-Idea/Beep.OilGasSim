// R4.2 — network construction and validation (SDD-002 §6).
// Every refusal the build owes. These are NOT part of design 04 §9's FV suite —
// a network that fails validation never reaches a solve, so nothing here is a
// statement about conservation or convergence.

using OGSim.Contracts;
using OGSim.Flow;
using OGSim.Kernel;

namespace OGSim.Flow.Tests;

public class NetworkTests
{
    private static FlowConnection Edge(ulong from, int fromPort, ulong to, int toPort) =>
        new(new EntityId<IFlowElement>(from), new PortId(fromPort),
            new EntityId<IFlowElement>(to), new PortId(toPort));

    [Fact] // A well-formed chain builds, in topological order
    public void Build_a_valid_chain_builds_in_topological_order()
    {
        var topology = new FlowTopology(
            // Declared out of order deliberately — the ORDER must come from the
            // wiring, not from how the caller listed the elements.
            [new Sink(3), new Restrictor(2, 100.0), new Source(1, 40.0, 10.0)],
            [Edge(1, 0, 2, 0), Edge(2, 1, 3, 0)]);

        var built = Assert.IsType<NetworkBuilt>(FlowNetwork.Build(topology));
        FlowNetwork network = built.Network;

        Assert.Equal(3, network.Ordered.Count);
        Assert.Equal(1UL, network.Ordered[0].Id.Value);   // source first
        Assert.Equal(2UL, network.Ordered[1].Id.Value);
        Assert.Equal(3UL, network.Ordered[2].Id.Value);   // sink last

        Assert.True(network.IsSource(network.Ordered[0]));
        Assert.False(network.IsSource(network.Ordered[2]));
    }

    [Fact] // The order must be THE same one every time, not merely a valid one
    public void Build_topological_order_is_stable_across_input_orderings()
    {
        static ulong[] Order(params IFlowElement[] elements)
        {
            var built = Assert.IsType<NetworkBuilt>(FlowNetwork.Build(new FlowTopology(
                elements, [Edge(1, 0, 3, 0), Edge(2, 0, 3, 1), Edge(3, 2, 4, 0)])));
            return [.. built.Network.Ordered.Select(e => e.Id.Value)];
        }

        IFlowElement a = new Source(1, 10.0, 0.0);
        IFlowElement b = new Source(2, 20.0, 0.0);
        IFlowElement manifold = new Manifold(3);
        IFlowElement sink = new Sink(4);

        // Two sources are tied in the ready set; the tie-break by ascending id
        // is what makes the result deterministic (SDD-002 §6).
        Assert.Equal(Order(a, b, manifold, sink), Order(sink, manifold, b, a));
        Assert.Equal(Order(a, b, manifold, sink), Order(b, a, sink, manifold));
    }

    [Fact] // FD4: topology is a TREE toward each sink
    public void Build_an_inlet_fed_twice_is_refused()
    {
        var topology = new FlowTopology(
            [new Source(1, 10.0, 0.0), new Source(2, 10.0, 0.0), new Sink(3)],
            [Edge(1, 0, 3, 0), Edge(2, 0, 3, 0)]);      // both into inlet 0

        var refused = Assert.IsType<NetworkRefused>(FlowNetwork.Build(topology));
        Assert.Contains(refused.Problems, p => p.Detail.Contains("fed by more than one"));
    }

    [Fact] // A recycle must close with a one-tick lag, never as an in-tick cycle
    public void Build_a_cycle_is_refused_and_the_elements_are_named()
    {
        var topology = new FlowTopology(
            [new Restrictor(1, 100.0), new Restrictor(2, 100.0)],
            [Edge(1, 1, 2, 0), Edge(2, 1, 1, 0)]);

        var refused = Assert.IsType<NetworkRefused>(FlowNetwork.Build(topology));
        NetworkProblem cycle = Assert.Single(refused.Problems);

        Assert.Contains("cycle", cycle.Detail);
        Assert.Contains("1", cycle.Detail);
        Assert.Contains("2", cycle.Detail);
        Assert.Contains("one-tick lag", cycle.Detail);
    }

    [Fact] // Wiring must match declared ports, in both direction and existence
    public void Build_a_connection_to_a_wrong_or_missing_port_is_refused()
    {
        // Port 0 on a Source is an OUTLET; using it as an inlet is wrong.
        var backwards = new FlowTopology(
            [new Source(1, 10.0, 0.0), new Source(2, 10.0, 0.0)],
            [Edge(1, 0, 2, 0)]);
        Assert.IsType<NetworkRefused>(FlowNetwork.Build(backwards));

        var missing = new FlowTopology(
            [new Source(1, 10.0, 0.0), new Sink(2)],
            [Edge(1, 0, 2, 9)]);
        var refused = Assert.IsType<NetworkRefused>(FlowNetwork.Build(missing));
        Assert.Contains(refused.Problems, p => p.Detail.Contains("not an inlet"));
    }

    [Fact] // An element referenced by an edge but absent from the network
    public void Build_an_edge_to_an_absent_element_is_refused()
    {
        var topology = new FlowTopology(
            [new Source(1, 10.0, 0.0)],
            [Edge(1, 0, 99, 0)]);

        var refused = Assert.IsType<NetworkRefused>(FlowNetwork.Build(topology));
        Assert.Contains(refused.Problems, p => p.Detail.Contains("not in the network"));
    }

    [Fact] // Every problem at once — a network is a composition fault, so a
           // developer should get the whole list (SDD-002 §10)
    public void Build_every_problem_is_reported_together()
    {
        var topology = new FlowTopology(
            [new Source(1, 10.0, 0.0), new Source(1, 20.0, 0.0), new Sink(3)],
            [Edge(1, 0, 99, 0), Edge(1, 0, 3, 9)]);

        var refused = Assert.IsType<NetworkRefused>(FlowNetwork.Build(topology));
        Assert.True(refused.Problems.Count >= 3, $"only {refused.Problems.Count} reported");
        Assert.Contains(refused.Problems, p => p.Detail.Contains("appears twice"));
    }

    [Fact] // An isolated element is legal: a network need not be connected
    public void Build_a_disconnected_element_is_not_an_error()
    {
        var topology = new FlowTopology(
            [new Source(1, 10.0, 0.0), new Sink(2), new Sink(3)],
            [Edge(1, 0, 2, 0)]);

        var built = Assert.IsType<NetworkBuilt>(FlowNetwork.Build(topology));
        Assert.Equal(3, built.Network.Ordered.Count);
    }
}
