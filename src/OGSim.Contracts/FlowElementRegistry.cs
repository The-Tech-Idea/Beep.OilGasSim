// R20c — where the field's elements and wiring are collected (SDD-002 §6).
//
// Elements are created by four different modules — Wells makes completions,
// Facilities makes separators and tanks, Transport makes pipelines — and the
// solver needs all of them at once. Without this there is no way for stage 5 to
// see across modules, and the solver is reachable only by a test that hand-builds
// its input.
//
// This lives in Contracts because every module must be able to name it and no
// module may name another (design 03 §2). The FLOW module provides it, since it
// is the solver's input; the modules that create elements require it.

using OGSim.Kernel;

namespace OGSim.Contracts;

/// <summary>
/// The field as registered: every flow element and every tie-in between them.
///
/// <para>The registry holds the EDGES; the modules keep their equipment. "The
/// state behind an edge" — a flowline's length, diameter and condition — stays
/// with the module that owns the pipeline, so law L5 is not strained: a
/// <see cref="FlowConnection"/> is an immutable statement about which port feeds
/// which, registered once and held in one place.</para>
/// </summary>
public interface IFlowElementRegistry
{
    /// <summary>Registered by the module that created it, once.</summary>
    void Add(IFlowElement element);

    void Connect(FlowConnection connection);

    /// <summary>
    /// Every registered element, available or not.
    ///
    /// <para>Stage 4 has to say which elements are available this segment, and
    /// the only thing that knows what exists is the registry — so without this a
    /// caller holding the contract could not build the set
    /// <see cref="ViewFor"/> filters by. It is also what a whole-field view
    /// reports: what is present, not what one segment happened to solve.</para>
    /// </summary>
    IReadOnlyList<IFlowElement> Registered { get; }

    /// <summary>
    /// One segment's view: the registered elements that are available, and the
    /// connections among them. A VIEW — the registry is not modified, so the
    /// four segments of a tick each see one unchanging field and an abandoned
    /// tick has nothing to undo (SDD-002 §9).
    /// </summary>
    FlowTopology ViewFor(IReadOnlyCollection<EntityRef> available);
}

/// <summary>The shipped registry. Concrete because there is nothing to vary:
/// collecting registrations is not a modelling decision.</summary>
public sealed class FlowElementRegistry : IFlowElementRegistry
{
    // Issue order, not a Dictionary walk: D-5 forbids enumerating a Dictionary,
    // and the view's element order must not depend on hashing.
    private readonly List<IFlowElement> _elements = [];
    private readonly HashSet<EntityId<IFlowElement>> _registered = [];
    private readonly List<FlowConnection> _connections = [];

    public void Add(IFlowElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        // Write-once. A second registration of one id would put two objects
        // behind one identity, and the solve would use whichever it met first.
        if (!_registered.Add(element.Id))
            throw new InvariantFault("SDD-002 §6", null,
                $"flow element {element.Id.Value} is registered twice; " +
                "one id is one element (law L5)");

        _elements.Add(element);
    }

    public void Connect(FlowConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        // Both ends must exist. A connection to an unregistered element would
        // surface as a missing key deep inside a solve, naming nothing useful.
        if (!_registered.Contains(connection.From))
            throw new InvariantFault("SDD-002 §6", null,
                $"connection from unregistered element {connection.From.Value}");

        if (!_registered.Contains(connection.To))
            throw new InvariantFault("SDD-002 §6", null,
                $"connection to unregistered element {connection.To.Value}");

        if (connection.From == connection.To)
            throw new InvariantFault("SDD-002 §6", null,
                $"element {connection.From.Value} is connected to itself; " +
                "the topology is a tree toward each sink (FD4)");

        _connections.Add(connection);
    }

    public FlowTopology ViewFor(IReadOnlyCollection<EntityRef> available)
    {
        ArgumentNullException.ThrowIfNull(available);

        var present = new HashSet<EntityId<IFlowElement>>();
        foreach (EntityRef reference in available)
            if (reference.Kind == EntityKind.FlowElement)
                present.Add(new EntityId<IFlowElement>(reference.Value));

        var elements = new List<IFlowElement>(_elements.Count);
        for (int i = 0; i < _elements.Count; i++)
            if (present.Contains(_elements[i].Id)) elements.Add(_elements[i]);

        // A connection touching an absent element goes with it. Design 04 §4:
        // an unavailable element is ABSENT from the network, not present at zero
        // rate — the difference is what makes segmenting availability worth
        // doing, because a non-linear solve does not average.
        var connections = new List<FlowConnection>(_connections.Count);
        for (int i = 0; i < _connections.Count; i++)
        {
            FlowConnection connection = _connections[i];
            if (present.Contains(connection.From) && present.Contains(connection.To))
                connections.Add(connection);
        }

        return new FlowTopology(elements, connections);
    }

    public IReadOnlyList<IFlowElement> Registered => _elements;

    /// <summary>An element's availability reference, so a caller building a
    /// segment's set does not have to know how the two identities relate.</summary>
    public static EntityRef ReferenceTo(IFlowElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return new EntityRef(EntityKind.FlowElement, element.Id.Value);
    }
}
