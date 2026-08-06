// R4.2 — network construction and validation (SDD-002 §6).
//
// The solver knows only IFlowElement (design 04 §1): nothing here names a
// separator, a pipeline or a well, which is what makes adding equipment a
// content edit rather than a solver change.
//
// Validation happens ONCE, at build, and every failure is reported together —
// a network that fails is a COMPOSITION fault and the engine refuses to start
// (SDD-002 §10), so there is no value in stopping at the first problem.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Flow;

public sealed record NetworkProblem(EntityId<IFlowElement>? Element, string Detail);

public abstract record NetworkBuildResult;
public sealed record NetworkBuilt(FlowNetwork Network) : NetworkBuildResult;
public sealed record NetworkRefused(IReadOnlyList<NetworkProblem> Problems) : NetworkBuildResult;

/// <summary>
/// A validated, per-segment view of the elements that are available and how they
/// are wired. Built from (all elements) ∩ (segment availability set) — an
/// unavailable element is ABSENT from the network rather than present-and-off,
/// which is why <see cref="IFlowElement"/> carries no availability flag.
/// </summary>
public sealed class FlowNetwork
{
    private readonly Dictionary<EntityId<IFlowElement>, IFlowElement> _byId;
    private readonly Dictionary<EntityId<IFlowElement>, List<FlowConnection>> _outgoing;
    private readonly Dictionary<EntityId<IFlowElement>, List<FlowConnection>> _incoming;

    private FlowNetwork(
        IReadOnlyList<IFlowElement> ordered,
        Dictionary<EntityId<IFlowElement>, IFlowElement> byId,
        Dictionary<EntityId<IFlowElement>, List<FlowConnection>> outgoing,
        Dictionary<EntityId<IFlowElement>, List<FlowConnection>> incoming,
        IReadOnlyList<FlowConnection> connections)
    {
        Ordered = ordered;
        Connections = connections;
        _byId = byId;
        _outgoing = outgoing;
        _incoming = incoming;
    }

    /// <summary>
    /// Topological order, ties broken by ascending element id — "the single
    /// deterministic order every pass uses" (SDD-002 §6). Sources first.
    /// </summary>
    public IReadOnlyList<IFlowElement> Ordered { get; }

    public IReadOnlyList<FlowConnection> Connections { get; }

    public IFlowElement this[EntityId<IFlowElement> id] => _byId[id];

    public IReadOnlyList<FlowConnection> Downstream(EntityId<IFlowElement> id) =>
        _outgoing.TryGetValue(id, out List<FlowConnection>? edges) ? edges : [];

    public IReadOnlyList<FlowConnection> Upstream(EntityId<IFlowElement> id) =>
        _incoming.TryGetValue(id, out List<FlowConnection>? edges) ? edges : [];

    /// <summary>An element with no inlet ports — where mass enters the network.</summary>
    public bool IsSource(IFlowElement element)
    {
        IReadOnlyList<PortSpec> ports = element.Ports;
        for (int i = 0; i < ports.Count; i++)
            if (ports[i].Direction == PortDirection.Inlet) return false;
        return true;
    }

    public static NetworkBuildResult Build(FlowTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);

        var problems = new List<NetworkProblem>();

        // The dictionary answers "who is this id?"; the list answers "in what
        // order?". Keeping both means nothing ever enumerates the dictionary
        // (rule D-5) — the ordered walk starts from the caller's list, which has
        // an order, rather than from a hash table, which does not.
        var byId = new Dictionary<EntityId<IFlowElement>, IFlowElement>();
        var ordered = new List<IFlowElement>(topology.Elements.Count);

        for (int i = 0; i < topology.Elements.Count; i++)
        {
            IFlowElement element = topology.Elements[i];
            if (byId.TryAdd(element.Id, element))
                ordered.Add(element);
            else
                problems.Add(new NetworkProblem(element.Id, "element id appears twice"));
        }

        ordered.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

        var outgoing = new Dictionary<EntityId<IFlowElement>, List<FlowConnection>>();
        var incoming = new Dictionary<EntityId<IFlowElement>, List<FlowConnection>>();
        var claimedInlets = new HashSet<(EntityId<IFlowElement>, PortId)>();
        var claimedOutlets = new HashSet<(EntityId<IFlowElement>, PortId)>();

        for (int i = 0; i < topology.Connections.Count; i++)
        {
            FlowConnection edge = topology.Connections[i];

            if (!byId.TryGetValue(edge.From, out IFlowElement? from))
            {
                problems.Add(new NetworkProblem(edge.From, "connection from an element not in the network"));
                continue;
            }
            if (!byId.TryGetValue(edge.To, out IFlowElement? to))
            {
                problems.Add(new NetworkProblem(edge.To, "connection to an element not in the network"));
                continue;
            }

            if (!HasPort(from, edge.FromPort, PortDirection.Outlet))
            {
                problems.Add(new NetworkProblem(edge.From,
                    $"port {edge.FromPort.Index} is not an outlet"));
                continue;
            }
            if (!HasPort(to, edge.ToPort, PortDirection.Inlet))
            {
                problems.Add(new NetworkProblem(edge.To,
                    $"port {edge.ToPort.Index} is not an inlet"));
                continue;
            }

            // TREE toward each sink (FD4): one edge per port, both ends. Two
            // streams into one inlet would be an undeclared commingle — mixing
            // is an ELEMENT's job (a manifold), never an emergent property of
            // the wiring, or provenance would blend with nothing recording it.
            if (!claimedOutlets.Add((edge.From, edge.FromPort)))
                problems.Add(new NetworkProblem(edge.From,
                    $"outlet port {edge.FromPort.Index} feeds more than one element"));
            if (!claimedInlets.Add((edge.To, edge.ToPort)))
                problems.Add(new NetworkProblem(edge.To,
                    $"inlet port {edge.ToPort.Index} is fed by more than one element"));

            Index(outgoing, edge.From, edge);
            Index(incoming, edge.To, edge);
        }

        // A spec gate with nowhere to send refused mass is a network that cannot
        // conserve (SDD-002 §5): the stream fails the spec, does not pass, and
        // has no Reject port to leave by.
        for (int i = 0; i < ordered.Count; i++)
        {
            IFlowElement element = ordered[i];
            if (element is not ICustodyTransferPoint) continue;

            bool hasReject = false;
            IReadOnlyList<PortSpec> ports = element.Ports;
            for (int p = 0; p < ports.Count; p++)
                if (ports[p].Role == PortRole.Reject && ports[p].Direction == PortDirection.Outlet)
                    hasReject = true;

            if (!hasReject)
                problems.Add(new NetworkProblem(element.Id,
                    "a spec gate must declare a Reject outlet port"));
        }

        IReadOnlyList<IFlowElement>? order =
            TopologicalOrder(ordered, byId, outgoing, incoming, problems);

        if (problems.Count > 0 || order is null)
        {
            problems.Sort((a, b) => string.CompareOrdinal(a.Detail, b.Detail));
            return new NetworkRefused(problems);
        }

        return new NetworkBuilt(new FlowNetwork(
            order, byId, outgoing, incoming, topology.Connections));
    }

    // ------------------------------------------------------------- internals

    private static bool HasPort(IFlowElement element, PortId port, PortDirection direction)
    {
        IReadOnlyList<PortSpec> ports = element.Ports;
        for (int i = 0; i < ports.Count; i++)
            if (ports[i].Id == port && ports[i].Direction == direction) return true;
        return false;
    }

    private static void Index(
        Dictionary<EntityId<IFlowElement>, List<FlowConnection>> index,
        EntityId<IFlowElement> key,
        FlowConnection edge)
    {
        if (!index.TryGetValue(key, out List<FlowConnection>? edges))
        {
            edges = [];
            index.Add(key, edges);
        }
        edges.Add(edge);
    }

    /// <summary>
    /// Kahn's algorithm with the ready set kept in ascending id order, so the
    /// result is not merely *a* topological order but always the SAME one — two
    /// runs of one tick must visit elements identically or the solve is not
    /// reproducible (SDD-002 §6).
    /// </summary>
    private static IReadOnlyList<IFlowElement>? TopologicalOrder(
        IReadOnlyList<IFlowElement> ordered,
        Dictionary<EntityId<IFlowElement>, IFlowElement> byId,
        Dictionary<EntityId<IFlowElement>, List<FlowConnection>> outgoing,
        Dictionary<EntityId<IFlowElement>, List<FlowConnection>> incoming,
        List<NetworkProblem> problems)
    {
        var remaining = new Dictionary<EntityId<IFlowElement>, int>();
        var ready = new List<EntityId<IFlowElement>>();

        // Seeded from the id-sorted list, so the ready set starts in the order
        // the tie-break below relies on.
        for (int i = 0; i < ordered.Count; i++)
        {
            EntityId<IFlowElement> id = ordered[i].Id;
            int inDegree = incoming.TryGetValue(id, out List<FlowConnection>? edges)
                ? edges.Count : 0;
            remaining.Add(id, inDegree);
            if (inDegree == 0) ready.Add(id);
        }

        var order = new List<IFlowElement>(byId.Count);
        while (ready.Count > 0)
        {
            ready.Sort((a, b) => a.Value.CompareTo(b.Value));
            EntityId<IFlowElement> next = ready[0];
            ready.RemoveAt(0);
            order.Add(byId[next]);

            if (!outgoing.TryGetValue(next, out List<FlowConnection>? downstream)) continue;
            for (int i = 0; i < downstream.Count; i++)
            {
                EntityId<IFlowElement> target = downstream[i].To;
                remaining[target] -= 1;
                if (remaining[target] == 0) ready.Add(target);
            }
        }

        if (order.Count == byId.Count) return order;

        // Whatever is left is in or behind a cycle. Named, because "there is a
        // cycle" without saying where is a diagnostic nobody can act on.
        var stuck = new List<string>();
        foreach (KeyValuePair<EntityId<IFlowElement>, int> pair in remaining)
            if (pair.Value > 0) stuck.Add(pair.Key.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        stuck.Sort(StringComparer.Ordinal);

        problems.Add(new NetworkProblem(null,
            $"cycle: elements {string.Join(", ", stuck)} are not reachable in topological order " +
            "— recycle streams close with a one-tick lag, never as in-tick cycles (SDD-002 §6)"));
        return null;
    }
}
