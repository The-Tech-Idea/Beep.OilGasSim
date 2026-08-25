// R20c.6 — acquired technology survives a save (SDD-001 §10, SDD-005 §2).
//
// The point throughout: the graph authorises a load exactly as it authorised the
// original acquisition. A save is not a way around the technology tree.

using OGSim.Capabilities;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Persistence;

namespace OGSim.Capabilities.Tests;

public sealed class CapabilityStateTests
{
    private static TechnologyId Tech(string id) => new(new ContentId(id));

    /// <summary>A chain — rotary needs cable, deviated needs rotary — so the
    /// replay's ordering has something real to get wrong.</summary>
    private static IReadOnlyList<TechnologyNode> Graph() =>
    [
        new(Tech("cable-tool"), Era.E1, 0, [], [], null, Researched, null, null),
        new(Tech("rotary"), Era.E1, 0, [Tech("cable-tool")], [], null, Researched, null, null),
        new(Tech("deviated"), Era.E2, 0, [Tech("rotary")], [], null, Researched, null, null),
    ];

    /// <summary>Acquired deliberately, never by diffusion — these tests are
    /// about what a save carries, so every grant here is an explicit one.</summary>
    private static readonly AcquisitionRoute[] Researched = [AcquisitionRoute.Research];

    /// <summary>
    /// A state in a given era — expressed as a CLOCK now, because the era is
    /// derived from the date rather than stored (SDD-005 §2's R20d.10
    /// amendment). The epoch is chosen to land inside the era asked for, which
    /// is the same statement the old `Era` argument made and one the calendar
    /// can now check.
    /// </summary>
    private static CapabilityState Fresh(Era era = Era.E2) =>
        Fresh(new GameDate(YearIn(era), 1));

    private static CapabilityState Fresh(GameDate today) =>
        new(Graph(), Calendar, () => today, today);

    private static readonly EraCalendar Calendar =
        new([(Era.E1, 1950), (Era.E2, 1970), (Era.E3, 1990), (Era.E4, 2010)]);

    private static int YearIn(Era era) => era switch
    {
        Era.E1 => 1960,
        Era.E2 => 1975,
        Era.E3 => 1995,
        Era.E4 => 2015,
        _ => throw new ArgumentOutOfRangeException(nameof(era)),
    };

    [Fact]
    public void Acquired_technology_restores_to_the_same_holdings()
    {
        CapabilityState captured = Fresh();
        captured.Technology.Acquire(Tech("cable-tool"), Era.E2, AcquisitionRoute.Diffusion);
        captured.Technology.Acquire(Tech("rotary"), Era.E2, AcquisitionRoute.Diffusion);

        JsonValue written = StateBlock.Capture(captured).Written();

        CapabilityState restored = Fresh();
        StateBlock.Restore(restored, written);

        Assert.True(restored.Technology.Has(Tech("cable-tool")));
        Assert.True(restored.Technology.Has(Tech("rotary")));
        Assert.False(restored.Technology.Has(Tech("deviated")));

        // The era is DERIVED and is not in the block (SDD-005 §2's R20d.10
        // amendment). This used to restore into a state built at E1 and assert
        // that E2 came back out of the save — a scenario the stored copy was the
        // only thing that made reachable. A save restores at the tick it was
        // taken at, so the restore target is at the same date and the era that
        // authorised these acquisitions is the era the calendar answers with.
        Assert.Equal(Era.E2, restored.Era);
    }

    /// <summary>
    /// Acquisition order is what makes the replay legal: prerequisites must be
    /// held before the nodes that need them. Capturing sorted would have
    /// produced a file that cannot load itself.
    /// </summary>
    [Fact]
    public void The_replay_follows_acquisition_order_not_alphabetical_order()
    {
        CapabilityState captured = Fresh();

        // "cable-tool" sorts before "deviated" and "rotary", but "rotary" must
        // be granted before "deviated" regardless of spelling.
        captured.Technology.Acquire(Tech("cable-tool"), Era.E2, AcquisitionRoute.Diffusion);
        captured.Technology.Acquire(Tech("rotary"), Era.E2, AcquisitionRoute.Diffusion);
        captured.Technology.Acquire(Tech("deviated"), Era.E2, AcquisitionRoute.Diffusion);

        // Restored at the same date, since that is where a save is taken from
        // and the era is now the calendar's answer rather than the block's.
        CapabilityState restored = Fresh();
        StateBlock.Restore(restored, StateBlock.Capture(captured).Written());

        Assert.Equal(
            ["cable-tool", "rotary", "deviated"],
            restored.Technology.Acquired.Select(t => t.Value.Value).ToArray());
    }

    [Fact]
    public void A_state_with_no_acquisitions_restores_empty()
    {
        CapabilityState restored = Fresh();
        StateBlock.Restore(restored, StateBlock.Capture(Fresh()).Written());

        Assert.Empty(restored.Technology.Acquired);
    }

    /// <summary>
    /// The graph authorises the load. A save claiming a technology whose
    /// prerequisite is absent is refused — a load that granted it anyway would
    /// be a route around the tree, which is the one thing the tree exists to
    /// prevent.
    /// </summary>
    [Fact]
    public void A_save_whose_prerequisites_are_missing_is_refused()
    {
        var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal)
        {
            ["$schema-version"] = new JsonInteger(2),
            ["era"] = new JsonInteger((long)Era.E2),
            ["acquired-count"] = new JsonInteger(1),
            ["acquired.000000"] = new JsonString("deviated"),   // rotary is not held
        };

        ModelFault fault = Assert.Throws<ModelFault>(
            () => StateBlock.Restore(Fresh(), new JsonObject(members)));

        Assert.Contains("rotary", fault.Message, StringComparison.Ordinal);
    }

    /// <summary>A technology the graph does not contain is refused rather than
    /// silently held — an id from a mod that is no longer installed.</summary>
    [Fact]
    public void A_save_naming_a_technology_outside_the_graph_is_refused()
    {
        var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal)
        {
            ["$schema-version"] = new JsonInteger(2),
            ["era"] = new JsonInteger((long)Era.E2),
            ["acquired-count"] = new JsonInteger(1),
            ["acquired.000000"] = new JsonString("fusion-drill"),
        };

        Assert.Throws<ModelFault>(() => StateBlock.Restore(Fresh(), new JsonObject(members)));
    }

    /// <summary>
    /// THE ERA IS THE CALENDAR'S ANSWER AND NOT THE SAVE'S (SDD-005 §2's R20d.10
    /// amendment). The same holdings read at a later date report a later era —
    /// which is the property that makes a campaign advance at all, and the one
    /// the stored field made impossible: it was written by the constructor and by
    /// `Restore` and by nothing else, so a 1965-to-2005 run stayed in E1 for
    /// forty years (finding 191).
    /// </summary>
    [Fact]
    public void The_era_follows_the_date_rather_than_the_save()
    {
        CapabilityState captured = Fresh();
        captured.Technology.Acquire(Tech("cable-tool"), Era.E2, AcquisitionRoute.Diffusion);

        JsonValue written = StateBlock.Capture(captured).Written();

        // Two decades on, from the same block.
        CapabilityState later = Fresh(new GameDate(1995, 6));
        StateBlock.Restore(later, written);

        Assert.Equal(Era.E3, later.Era);
        Assert.True(later.Technology.Has(Tech("cable-tool")));
    }

    /// <summary>And the block does not carry it, which is what stops the two
    /// answers ever disagreeing.</summary>
    [Fact]
    public void The_block_does_not_carry_an_era()
    {
        JsonValue written = StateBlock.Capture(Fresh()).Written();

        Assert.DoesNotContain("era", Assert.IsType<JsonObject>(written).Members.Keys);
    }
}
