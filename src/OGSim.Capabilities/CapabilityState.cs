// R20c.6 — acquired technology as an owned, saved fact (SDD-001 §10, SDD-005 §2).
//
// ACQUISITION IS REPLAYED IN ORDER, not restored as a set. Acquire checks
// prerequisites, so replaying in the order they were granted rebuilds the same
// holdings through the same graph that authorised them. A save listing a
// technology whose prerequisite is missing is refused rather than loaded, which
// is the point: the graph is what makes the tree a sequence rather than a menu,
// and a load that bypassed it would be a route around the whole system.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Capabilities;

/// <summary>
/// Owner of <c>capabilities.technology</c>.
///
/// <para>The era is captured with the holdings because <c>Acquire</c> checks it:
/// replaying a late-era technology against a restored early era would refuse a
/// save that was legitimate when written.</para>
/// </summary>
public sealed class CapabilityState : IStateOwner
{
    private readonly IReadOnlyList<TechnologyNode> _graph;

    public CapabilityState(IReadOnlyList<TechnologyNode> graph, Era era)
    {
        ArgumentNullException.ThrowIfNull(graph);

        _graph = graph;
        Era = era;
        Technology = new TechnologyState(graph);
    }

    public TechnologyState Technology { get; private set; }

    public Era Era { get; private set; }

    public StateKey Key { get; } = new("capabilities.technology");

    public int SchemaVersion => 1;

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("era", (long)Era);

        IReadOnlyList<TechnologyId> acquired = Technology.Acquired;
        writer.WriteInt64("acquired-count", acquired.Count);

        // ACQUISITION order, not sorted order: prerequisites were satisfied in
        // this sequence and the replay needs the same one. Sorting by id here
        // would produce a canonical-looking file that cannot be loaded.
        for (int i = 0; i < acquired.Count; i++)
            writer.WriteString(Prefix(i), acquired[i].Value.Value);
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var era = (Era)reader.ReadInt64("era");
        var rebuilt = new TechnologyState(_graph);

        long count = reader.ReadInt64("acquired-count");
        if (count < 0)
            throw new SaveDataFault("SDD-005 §2", null,
                $"The technology state declares {count} acquisitions.");

        for (long i = 0; i < count; i++)
            rebuilt.Acquire(new TechnologyId(new ContentId(reader.ReadString(Prefix(i)))), era);

        Technology = rebuilt;
        Era = era;
    }

    /// <summary>Zero-padded so ordinal key order and acquisition order agree
    /// (SDD-013 §3); otherwise the tenth grant sorts before the second.</summary>
    private static string Prefix(long index) =>
        "acquired." + index.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
}
