// R4.9 — randomised network generation for FV1 (design 04 §9).
//
// FV1 asks for conservation over RANDOMISED networks, not over the three shapes
// a person happened to think of. Hand-written topologies test the cases their
// author already believed in; a generator finds the merge-under-a-split nobody
// drew.
//
// Every network produced is valid by construction: built bottom-up from a
// frontier of unconsumed outlets, so it is always a DAG toward a single sink,
// every port is used exactly once, and no cycle is representable. A generator
// that emitted invalid networks would spend the trial budget on refusals rather
// than on solves.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Flow.Tests;

internal sealed record RandomNetwork(
    FlowTopology Topology,
    IReadOnlyList<ICompletion> Completions)
{
    private const int MinCompletions = 1;
    private const int MaxCompletions = 5;
    private const int MaxGrowthSteps = 12;

    public static RandomNetwork Generate(IRandomStream stream, int trial)
    {
        var elements = new List<IFlowElement>();
        var connections = new List<FlowConnection>();
        var completions = new List<ICompletion>();

        ulong nextId = 1;

        // The frontier is every outlet not yet consumed: (element id, port).
        var frontier = new List<(EntityId<IFlowElement> Element, PortId Port)>();

        int wells = MinCompletions + stream.NextInt(MaxCompletions - MinCompletions + 1);
        for (int i = 0; i < wells; i++)
        {
            // PI spread over two decades so some wells bind capacity and some
            // do not — a generator where every well behaves the same tests one
            // case many times.
            var well = new SyntheticCompletion(
                nextId++,
                productivityIndex: 1e-10 + stream.NextUnit() * 5e-9,
                reservoirBar: 50.0 + stream.NextUnit() * 250.0)
            {
                Compartment = (ulong)(1 + stream.NextInt(3)),
            };

            elements.Add(well);
            completions.Add(well);
            frontier.Add((well.Id, new PortId(0)));
        }

        for (int step = 0; step < MaxGrowthSteps && frontier.Count > 0; step++)
        {
            // Merge while more than one branch is open, otherwise extend.
            bool canMerge = frontier.Count >= 2;
            int choice = stream.NextInt(canMerge ? 3 : 2);

            if (canMerge && choice == 0)
            {
                var manifold = new Manifold(nextId++);
                elements.Add(manifold);

                (EntityId<IFlowElement> Element, PortId Port) a = Take(frontier, stream);
                (EntityId<IFlowElement> Element, PortId Port) b = Take(frontier, stream);

                connections.Add(new FlowConnection(a.Element, a.Port, manifold.Id, new PortId(0)));
                connections.Add(new FlowConnection(b.Element, b.Port, manifold.Id, new PortId(1)));
                frontier.Add((manifold.Id, new PortId(2)));
            }
            else if (choice == 1 || !canMerge)
            {
                // A restrictor with a capacity that sometimes binds — FV1 must
                // hold WHILE throttling, not only on an unconstrained network.
                var restrictor = new Restrictor(nextId++, 5.0 + stream.NextUnit() * 500.0)
                {
                    DropBar = stream.NextUnit() * 20.0,
                };
                elements.Add(restrictor);

                (EntityId<IFlowElement> Element, PortId Port) feed = Take(frontier, stream);
                connections.Add(new FlowConnection(
                    feed.Element, feed.Port, restrictor.Id, new PortId(0)));
                frontier.Add((restrictor.Id, new PortId(1)));
            }
            else
            {
                var splitter = new Splitter(nextId++, 0.1 + stream.NextUnit() * 0.8);
                elements.Add(splitter);

                (EntityId<IFlowElement> Element, PortId Port) feed = Take(frontier, stream);
                connections.Add(new FlowConnection(
                    feed.Element, feed.Port, splitter.Id, new PortId(0)));

                frontier.Add((splitter.Id, new PortId(1)));
                frontier.Add((splitter.Id, new PortId(2)));
            }
        }

        // Every remaining open outlet gets its own sink, so nothing dangles and
        // the network-wide balance closes.
        for (int i = 0; i < frontier.Count; i++)
        {
            var sink = new Sink(nextId++);
            elements.Add(sink);
            connections.Add(new FlowConnection(
                frontier[i].Element, frontier[i].Port, sink.Id, new PortId(0)));
        }

        return new RandomNetwork(new FlowTopology(elements, connections), completions);
    }

    /// <summary>Removes and returns one frontier entry. Index chosen from the
    /// stream, so the shape varies but is reproducible from the seed.</summary>
    private static (EntityId<IFlowElement> Element, PortId Port) Take(
        List<(EntityId<IFlowElement> Element, PortId Port)> frontier, IRandomStream stream)
    {
        int index = stream.NextInt(frontier.Count);
        (EntityId<IFlowElement> Element, PortId Port) taken = frontier[index];
        frontier.RemoveAt(index);
        return taken;
    }
}
