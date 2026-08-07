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
        new(Tech("cable-tool"), Era.E1, 0, [], [], null),
        new(Tech("rotary"), Era.E1, 0, [Tech("cable-tool")], [], null),
        new(Tech("deviated"), Era.E2, 0, [Tech("rotary")], [], null),
    ];

    private static CapabilityState Fresh(Era era = Era.E2) => new(Graph(), era);

    [Fact]
    public void Acquired_technology_restores_to_the_same_holdings()
    {
        CapabilityState captured = Fresh();
        captured.Technology.Acquire(Tech("cable-tool"), Era.E2);
        captured.Technology.Acquire(Tech("rotary"), Era.E2);

        JsonValue written = StateBlock.Capture(captured).Written();

        CapabilityState restored = Fresh(Era.E1);
        StateBlock.Restore(restored, written);

        Assert.True(restored.Technology.Has(Tech("cable-tool")));
        Assert.True(restored.Technology.Has(Tech("rotary")));
        Assert.False(restored.Technology.Has(Tech("deviated")));

        // The era comes back too — Acquire checks it, so a restored era of E1
        // would have refused the E2 holdings above.
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
        captured.Technology.Acquire(Tech("cable-tool"), Era.E2);
        captured.Technology.Acquire(Tech("rotary"), Era.E2);
        captured.Technology.Acquire(Tech("deviated"), Era.E2);

        CapabilityState restored = Fresh(Era.E1);
        StateBlock.Restore(restored, StateBlock.Capture(captured).Written());

        Assert.Equal(
            ["cable-tool", "rotary", "deviated"],
            restored.Technology.Acquired.Select(t => t.Value.Value).ToArray());
    }

    [Fact]
    public void A_state_with_no_acquisitions_restores_empty()
    {
        CapabilityState restored = Fresh(Era.E1);
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
            ["$schema-version"] = new JsonInteger(1),
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
            ["$schema-version"] = new JsonInteger(1),
            ["era"] = new JsonInteger((long)Era.E2),
            ["acquired-count"] = new JsonInteger(1),
            ["acquired.000000"] = new JsonString("fusion-drill"),
        };

        Assert.Throws<ModelFault>(() => StateBlock.Restore(Fresh(), new JsonObject(members)));
    }
}
