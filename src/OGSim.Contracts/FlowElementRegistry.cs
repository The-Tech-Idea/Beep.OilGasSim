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

    /// <summary>
    /// The available set with everything that has lost its route removed
    /// (SDD-002 §5, R20d.22).
    ///
    /// <para><b>An element is available only if everything it feeds is.</b>
    /// <see cref="ViewFor"/> drops the connections touching an absent element,
    /// which leaves whatever fed it flowing into a pipe that now ends nowhere —
    /// and the solver accepts that: the source still sources, and the mass
    /// leaves the network by the back door. In this composition that is the
    /// reservoir being drained for nothing, because stage 6 publishes the
    /// withdrawal whether or not a barrel reached custody.</para>
    ///
    /// <para>So the shut-in propagates upstream until it stops, and every
    /// element becomes a single point of failure for everything behind it. That
    /// is the gain rather than the cost: the tank goes and the field shuts in
    /// because there is nowhere to put oil; the disposal well goes and it shuts
    /// in because there is nowhere to put water. Redundancy is something a
    /// player buys.</para>
    /// </summary>
    IReadOnlyList<EntityRef> Routed(IReadOnlyCollection<EntityRef> available);

    /// <summary>
    /// The same law, answering what it REMOVED as well as what survived
    /// (SDD-002 §5's R22.4 amendment). <see cref="Routed"/> is this without the
    /// casualties — one fixed point, not two.
    /// </summary>
    RouteClosure Close(IReadOnlyCollection<EntityRef> available);
}

/// <summary>
/// Why one element left the available set: the connection that removed it
/// (SDD-002 §5, finding 202).
///
/// <para><c>Because</c> is the element downstream whose absence shut this one
/// in — the IMMEDIATE reason. An element removed late in the closure names one
/// removed earlier, and following the entries walks the outage back to the
/// thing that actually failed.</para>
/// </summary>
public sealed record RouteExclusion(EntityRef Element, EntityRef Because);

/// <summary>
/// What the route law found: the fixed point, and every element it shut in
/// getting there, in the order the outage spread.
///
/// <para>Elements absent from <c>available</c> on entry are NOT in
/// <see cref="Excluded"/>. They are the roots — a hazard draw or an operation
/// took them out and audited that with its own reason — so the two records meet
/// rather than overlap.</para>
/// </summary>
public sealed record RouteClosure(
    IReadOnlyList<EntityRef> Routed,
    IReadOnlyList<RouteExclusion> Excluded)
{
    // Finding 131: the compiler's record equality is reference equality for a
    // collection member, so two closures with identical contents would compare
    // unequal — and a projection that carried one would report a difference
    // every tick.
    public bool Equals(RouteClosure? other) =>
        other is not null
        && Structural.Equal(Routed, other.Routed)
        && Structural.Equal(Excluded, other.Excluded);

    public override int GetHashCode() =>
        HashCode.Combine(Structural.HashOf(Routed), Structural.HashOf(Excluded));
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

    /// <summary>
    /// SDD-002 §5's fixed point, by removal: drop every element that feeds
    /// something absent, and keep dropping until a pass removes nothing.
    ///
    /// <para>CLOSURE AND NOT REACHABILITY, which sound like the same rule.
    /// "Can this element reach a sink?" cannot tell a genuine terminal element
    /// from one left dangling — in the view neither has an outgoing connection.
    /// This asks about each element's OWN declared connections, which the
    /// registry holds whether or not a segment uses them, so the flare stays a
    /// sink and the orphaned flowline does not become one.</para>
    ///
    /// <para>Bounded by the element count: each pass removes at least one
    /// element or is the last, so a chain of n shuts in within n passes. Written
    /// as repeated passes rather than a reverse walk because the connection list
    /// is the only index there is and this needs no second one (law L5).</para>
    /// </summary>
    public IReadOnlyList<EntityRef> Routed(IReadOnlyCollection<EntityRef> available) =>
        Close(available).Routed;

    /// <summary>
    /// The law's full answer: what survived, and what each casualty was removed
    /// BY (SDD-002 §5's R22.4 amendment, finding 202).
    ///
    /// <para>Every removal happens at one connection — <c>c.From</c> goes because
    /// <c>c.To</c> is absent — so the causal edge is already in hand here and was
    /// simply discarded. It is what lets a deferral cite the failure behind it
    /// instead of recording <c>cause: null</c>.</para>
    ///
    /// <para><c>Because</c> is the IMMEDIATE reason: an element removed in the
    /// third pass names the one removed in the second, which names the one that
    /// failed. Following the entries walks the outage back; recording a root here
    /// would flatten the path a player is asking about.</para>
    /// </summary>
    public RouteClosure Close(IReadOnlyCollection<EntityRef> available)
    {
        ArgumentNullException.ThrowIfNull(available);

        var present = new HashSet<EntityId<IFlowElement>>();
        foreach (EntityRef reference in available)
            if (reference.Kind == EntityKind.FlowElement)
                present.Add(new EntityId<IFlowElement>(reference.Value));

        // Kept in removal order, which is the order the outage SPREAD: the first
        // entry names what the failure took directly. A set keyed by element
        // would lose that, and the order is the half of the record that reads
        // like an explanation.
        var excluded = new List<RouteExclusion>();

        bool removedSomething = true;

        while (removedSomething)
        {
            removedSomething = false;

            for (int i = 0; i < _connections.Count; i++)
            {
                FlowConnection connection = _connections[i];

                // The upstream end goes when the downstream end is gone. Not the
                // other way round: an element with an unfed inlet is a normal
                // thing — a gas plant with no gas arriving takes none — and
                // shutting it in would spread an outage forwards through a chain
                // that is merely idle.
                if (present.Contains(connection.From) && !present.Contains(connection.To))
                {
                    present.Remove(connection.From);
                    removedSomething = true;

                    excluded.Add(new RouteExclusion(
                        new EntityRef(EntityKind.FlowElement, connection.From.Value),
                        new EntityRef(EntityKind.FlowElement, connection.To.Value)));
                }
            }
        }

        // Rebuilt from the REGISTRATION order rather than filtered from the
        // caller's list, so the result's order is the field's order and does not
        // inherit whatever order the hazard pass happened to hand over (D-5).
        var routed = new List<EntityRef>(_elements.Count);
        for (int i = 0; i < _elements.Count; i++)
            if (present.Contains(_elements[i].Id)) routed.Add(ReferenceTo(_elements[i]));

        return new RouteClosure(routed, excluded);
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
