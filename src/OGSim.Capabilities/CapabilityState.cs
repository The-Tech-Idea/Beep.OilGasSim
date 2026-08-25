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
/// <para><b>The era is DERIVED and is not captured</b> (SDD-005 §2's R20d.10
/// amendment). It was a stored field written only by the constructor and by
/// <c>Restore</c> — law L5's mirrored derived value — and that is exactly why a
/// 1965-to-2005 campaign stayed in E1 for forty years: nothing ever wrote it,
/// and nothing could, because there was no calendar to write it from.</para>
///
/// <para>The note this replaces said the era is captured "because <c>Acquire</c>
/// checks it: replaying a late-era technology against a restored early era would
/// refuse a save that was legitimate when written." Deriving it REMOVES that
/// hazard rather than creating it — a save restores at the tick it was taken at,
/// so the derived era is the era that authorised the acquisition, by
/// construction. The stored copy was guarding against a divergence only the
/// stored copy could produce.</para>
/// </summary>
public sealed class CapabilityState : IStateOwner
{
    private readonly IReadOnlyList<TechnologyNode> _graph;
    private readonly EraCalendar _calendar;
    private readonly Func<GameDate> _today;
    private readonly GameDate _epoch;

    /// <summary>
    /// <paramref name="today"/> is a DELEGATE and not the clock, because law L1
    /// forbids a module a concrete type from another assembly — composition holds
    /// the clock and hands over a function from it. The same door
    /// <c>WellsState.RefreshFromReservoir</c> crosses the truth boundary through.
    /// </summary>
    public CapabilityState(
        IReadOnlyList<TechnologyNode> graph,
        EraCalendar calendar,
        Func<GameDate> today,
        GameDate epoch)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(today);

        _graph = graph;
        _calendar = calendar;
        _today = today;
        _epoch = epoch;
        Technology = new TechnologyState(graph);
    }

    public TechnologyState Technology { get; private set; }

    /// <summary>What era the game is in, read from the calendar every time it is
    /// asked. Computed, never mirrored (law L5).</summary>
    public Era Era => _calendar.At(_today());

    /// <summary>The tick the current era began, which is what a diffusion lag is
    /// measured from (SDD-005 §2).</summary>
    public Tick EraStart => _calendar.StartOf(Era, _epoch);

    public StateKey Key { get; } = new("capabilities.technology");

    /// <summary>v3 (finding 293): each acquisition carries its route — the
    /// licence fee bills only what was licensed — and in-flight research
    /// programmes travel with their months remaining.</summary>
    public int SchemaVersion => 3;

    /// <summary>Nothing has to be back before this is (SDD-013 §2b).</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // The era is NOT written: it is a function of the date, and the date is
        // the clock's (SDD-005 §2's R20d.10 amendment). Writing it would put a
        // second answer in the container that could disagree with the first.
        IReadOnlyList<TechnologyId> acquired = Technology.Acquired;
        writer.WriteInt64("acquired-count", acquired.Count);

        for (int i = 0; i < acquired.Count; i++)
            writer.WriteString(Prefix(i) + ".route", Technology.RouteOf(acquired[i]).ToString());

        IReadOnlyList<(TechnologyId Tech, int Remaining)> researching = Technology.Researching;
        writer.WriteInt64("research-count", researching.Count);

        for (int i = 0; i < researching.Count; i++)
        {
            writer.WriteString(ResearchPrefix(i) + ".tech", researching[i].Tech.Value.Value);
            writer.WriteInt64(ResearchPrefix(i) + ".remaining", researching[i].Remaining);
        }

        // ACQUISITION order, not sorted order: prerequisites were satisfied in
        // this sequence and the replay needs the same one. Sorting by id here
        // would produce a canonical-looking file that cannot be loaded.
        for (int i = 0; i < acquired.Count; i++)
            writer.WriteString(Prefix(i), acquired[i].Value.Value);
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        // The era the replay authorises against is the one the restored CLOCK
        // implies — which is the era the acquisitions were made in, since a save
        // restores at the tick it was taken at.
        Era era = Era;
        var rebuilt = new TechnologyState(_graph);

        long count = reader.ReadInt64("acquired-count");
        if (count < 0)
            throw new SaveDataFault("SDD-005 §2", null,
                $"The technology state declares {count} acquisitions.");

        for (long i = 0; i < count; i++)
            rebuilt.Acquire(
                new TechnologyId(new ContentId(reader.ReadString(Prefix(i)))), era,
                System.Enum.Parse<AcquisitionRoute>(reader.ReadString(Prefix(i) + ".route")));

        long researchCount = reader.ReadInt64("research-count");

        for (long i = 0; i < researchCount; i++)
            rebuilt.RestoreResearch(
                new TechnologyId(new ContentId(reader.ReadString(ResearchPrefix(i) + ".tech"))),
                (int)reader.ReadInt64(ResearchPrefix(i) + ".remaining"));

        Technology = rebuilt;
    }

    /// <summary>Zero-padded so ordinal key order and acquisition order agree
    /// (SDD-013 §3); otherwise the tenth grant sorts before the second.</summary>
    private static string Prefix(long index) =>
        "acquired." + index.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    private static string ResearchPrefix(long index) =>
        "research." + index.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
}
