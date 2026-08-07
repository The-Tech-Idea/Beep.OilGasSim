// R20c.6 — the state block (SDD-001 §10, SDD-013 §3).
//
// The rule under test throughout: a save that has quietly lost or reinterpreted
// a field must FAIL, not load. Every "throws" test here is a load that a
// forgiving reader would have accepted and then run wrong.

using OGSim.Kernel;
using OGSim.Persistence;

namespace OGSim.Persistence.Tests;

public sealed class StateBlockTests
{
    /// <summary>An owner over one value of each writable type.</summary>
    private sealed class Sample(int schemaVersion = 1) : IStateOwner
    {
        public StateKey Key { get; } = new("sample");

        public int SchemaVersion => schemaVersion;

        public string Text { get; set; } = "";
        public long Whole { get; set; }
        public double Real { get; set; }

        public void Capture(IStateWriter writer)
        {
            writer.WriteString("text", Text);
            writer.WriteInt64("whole", Whole);
            writer.WriteDouble("real", Real);
        }

        public void Restore(IStateReader reader)
        {
            Text = reader.ReadString("text");
            Whole = reader.ReadInt64("whole");
            Real = reader.ReadDouble("real");
        }
    }

    [Fact]
    public void A_captured_owner_restores_to_the_same_values()
    {
        var captured = new Sample { Text = "north-field", Whole = -42, Real = 0.1 + 0.2 };

        JsonValue written = StateBlock.Capture(captured).Written();

        var restored = new Sample();
        StateBlock.Restore(restored, written);

        Assert.Equal("north-field", restored.Text);
        Assert.Equal(-42, restored.Whole);

        // EXACT, not approximate: 0.1 + 0.2 must come back as the same bits, or
        // a reload diverges from the save it came from (PV1).
        Assert.Equal(captured.Real, restored.Real);
    }

    /// <summary>
    /// The canonical form must survive the text round trip too, not just the
    /// in-memory one — a block is only worth writing if it reads back.
    /// </summary>
    [Fact]
    public void A_block_survives_serialisation_to_text_and_back()
    {
        var captured = new Sample { Text = "a", Whole = 7, Real = 1.0 };

        string text = CanonicalJson.Write(StateBlock.Capture(captured).Written());

        var restored = new Sample();
        StateBlock.Restore(restored, CanonicalJson.Read(text));

        Assert.Equal(1.0, restored.Real);
        Assert.Equal(7, restored.Whole);
    }

    /// <summary>
    /// A whole-valued double writes without a fractional part, so the reader
    /// must accept an integer where a double is expected — otherwise the format
    /// cannot load what it itself wrote.
    /// </summary>
    [Fact]
    public void A_whole_valued_double_reads_back_as_a_double()
    {
        var captured = new Sample { Text = "x", Whole = 0, Real = 3.0 };

        var restored = new Sample();
        StateBlock.Restore(restored, StateBlock.Capture(captured).Written());

        Assert.Equal(3.0, restored.Real);
    }

    [Fact] // No TryRead, no defaults (SDD-001 §10)
    public void A_missing_field_refuses_the_load()
    {
        var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal)
        {
            ["$schema-version"] = new JsonInteger(1),
            ["text"] = new JsonString("a"),
            ["whole"] = new JsonInteger(1),
            // "real" is absent — a field the save has quietly lost.
        };

        SaveDataFault fault = Assert.Throws<SaveDataFault>(
            () => StateBlock.Restore(new Sample(), new JsonObject(members)));

        Assert.Contains("real", fault.Message, StringComparison.Ordinal);
    }

    [Fact] // A field of the wrong shape is a refusal, not a coercion
    public void A_field_of_the_wrong_type_refuses_the_load()
    {
        var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal)
        {
            ["$schema-version"] = new JsonInteger(1),
            ["text"] = new JsonString("a"),
            ["whole"] = new JsonString("not a number"),
            ["real"] = new JsonDouble(1.0),
        };

        Assert.Throws<SaveDataFault>(
            () => StateBlock.Restore(new Sample(), new JsonObject(members)));
    }

    /// <summary>
    /// A block written by a different schema version is refused rather than
    /// read. Interpreting one shape's bytes as another's is how a load produces
    /// a plausible, wrong world (SDD-013 §5).
    /// </summary>
    [Fact]
    public void A_block_from_another_schema_version_refuses_the_load()
    {
        JsonValue written = StateBlock.Capture(
            new Sample(schemaVersion: 1) { Text = "a", Whole = 1, Real = 1.0 }).Written();

        SaveDataFault fault = Assert.Throws<SaveDataFault>(
            () => StateBlock.Restore(new Sample(schemaVersion: 2), written));

        Assert.Contains("migration", fault.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Law L5 inside one block: a key written twice means two owners of one
    /// fact, and the second would silently win.
    /// </summary>
    [Fact]
    public void Writing_one_key_twice_is_refused()
    {
        Assert.Throws<InvariantFault>(() => StateBlock.Capture(new Twice()));
    }

    private sealed class Twice : IStateOwner
    {
        public StateKey Key { get; } = new("twice");

        public int SchemaVersion => 1;

        public void Capture(IStateWriter writer)
        {
            writer.WriteInt64("count", 1);
            writer.WriteInt64("count", 2);
        }

        public void Restore(IStateReader reader) => reader.ReadInt64("count");
    }

    /// <summary>
    /// NaN and infinity are unrepresentable in the canonical form because they
    /// were faults upstream. Refusing at the key names what went wrong.
    /// </summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_non_finite_double_is_refused_at_capture(double value)
    {
        InvariantFault fault = Assert.Throws<InvariantFault>(
            () => StateBlock.Capture(new Sample { Text = "a", Real = value }));

        Assert.Contains("real", fault.Message, StringComparison.Ordinal);
    }
}
