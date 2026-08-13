// R20c.6 — the bridge between a state owner and a save block
// (SDD-001 §10, SDD-013 §3).
//
// WRITER AND READER LIVE IN ONE CLASS (SDD-013 §3). Two classes would be two
// places for the key spelling, the number format and the missing-value rule to
// drift apart, and the drift would surface as a save that writes correctly and
// loads wrong — the L5 principle applied to bytes.
//
// A block is a FLAT map of key to scalar. Nesting is spelled into the key
// ("belief.3.mu") rather than structured, because the canonical form sorts keys
// ordinally and a flat map has exactly one sorted form. Arrays-of-objects would
// have had two.

using OGSim.Kernel;

namespace OGSim.Persistence;

/// <summary>
/// One module's state, as written and as read.
///
/// <para>The same instance is never both: <see cref="Written"/> produces a block
/// from an owner and <see cref="Read"/> consumes one, and each is a distinct
/// object with its own direction. What they share — and the reason they are one
/// class — is the key and value rules between them.</para>
/// </summary>
public sealed class StateBlock : IStateWriter, IStateReader
{
    // SortedDictionary: SDD-013 §3 requires ordinal-sorted keys, and sorting at
    // write time rather than on the way out means the order is a property of the
    // block rather than of the code that serialises it.
    private readonly SortedDictionary<string, JsonValue> _values =
        new(StringComparer.Ordinal);

    private readonly bool _reading;

    private StateBlock(bool reading) => _reading = reading;

    /// <summary>Captures an owner into a block.</summary>
    public static StateBlock Capture(IStateOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var block = new StateBlock(reading: false);

        // The schema version is written by US rather than by the owner, so an
        // owner cannot forget it and a load cannot meet a block that does not
        // say what shape it is.
        block._values[SchemaVersionKey] = new JsonInteger(owner.SchemaVersion);
        owner.Capture(block);

        return block;
    }

    /// <summary>Restores an owner from a block, refusing a version it cannot read.</summary>
    public static void Restore(IStateOwner owner, JsonValue state)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(state);

        if (state is not JsonObject json)
            throw new SaveDataFault("SDD-013 §3", null,
                $"State for '{owner.Key.Value}' is not an object.");

        var block = new StateBlock(reading: true);
        foreach (KeyValuePair<string, JsonValue> member in json.Members)
            block._values[member.Key] = member.Value;

        long version = block.ReadInt64(SchemaVersionKey);
        if (version != owner.SchemaVersion)
            throw new SaveDataFault("SDD-013 §5", null,
                $"State for '{owner.Key.Value}' is schema version {version}; this build " +
                $"owns version {owner.SchemaVersion}. A migration must run first — reading " +
                "it anyway would interpret one shape's bytes as another's.");

        owner.Restore(block);
    }

    /// <summary>
    /// A block as something to READ, without restoring an owner from it.
    ///
    /// <para>What a loader needs (design 11 §2.1): the field has to be rebuilt
    /// before its owner can be told what it holds, and the instructions for
    /// rebuilding it are in the block. Reading them is not restoring — the owner
    /// still gets its own <see cref="Restore"/>, version check and all, once the
    /// thing it describes exists.</para>
    ///
    /// <para>NO VERSION CHECK HERE, deliberately: the owner performs it on the
    /// same block moments later, and doing it twice would put the rule in two
    /// places to disagree about.</para>
    /// </summary>
    public static IStateReader ReaderFor(JsonValue state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state is not JsonObject json)
            throw new SaveDataFault("SDD-013 §3", null, "a state block is not an object.");

        var block = new StateBlock(reading: true);

        foreach (KeyValuePair<string, JsonValue> member in json.Members)
            block._values[member.Key] = member.Value;

        return block;
    }

    private const string SchemaVersionKey = "$schema-version";

    /// <summary>The block as canonical JSON.</summary>
    public JsonValue Written()
    {
        var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonValue> pair in _values)
            members[pair.Key] = pair.Value;
        return new JsonObject(members);
    }

    // ------------------------------------------------------------- writing

    public void WriteString(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Put(key, new JsonString(value));
    }

    public void WriteInt64(string key, long value) => Put(key, new JsonInteger(value));

    public void WriteDouble(string key, double value)
    {
        // NaN and infinity are unrepresentable in the canonical form (SDD-013
        // §3) because they were faults upstream. Refusing HERE names the key
        // that went wrong; refusing at serialisation would name the file.
        if (!double.IsFinite(value))
            throw new InvariantFault("SDD-013 §3", null,
                $"State key '{key}' holds {value}, which the canonical form cannot " +
                "represent; a non-finite value was a fault before it reached a save.");

        Put(key, new JsonDouble(value));
    }

    private void Put(string key, JsonValue value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        if (_reading)
            throw new InvariantFault("SDD-001 §10", null,
                "A block being read cannot be written to.");

        // Write-once: a key written twice means two owners of one fact inside
        // one block, and the second would silently win (law L5).
        if (!_values.TryAdd(key, value))
            throw new InvariantFault("L5", null,
                $"State key '{key}' was written twice in one block.");
    }

    // ------------------------------------------------------------- reading

    public string ReadString(string key) => Get<JsonString>(key).Value;

    public long ReadInt64(string key) => Get<JsonInteger>(key).Value;

    /// <summary>
    /// An integer in the file is a legitimate double: the canonical form writes
    /// a whole-valued double without a fractional part, so a reader that refused
    /// one would fail to load what it had itself written.
    /// </summary>
    public double ReadDouble(string key) => Get<JsonValue>(key) switch
    {
        JsonDouble value => value.Value,
        JsonInteger value => value.Value,
        JsonValue other => throw new SaveDataFault("SDD-013 §3", null,
            $"State key '{key}' holds {other.GetType().Name}, not a number."),
    };

    /// <summary>
    /// No TryRead and no defaults (SDD-001 §10): a missing field is a
    /// <see cref="SaveDataFault"/>, because a save that has quietly lost a field
    /// is not a save that should load.
    /// </summary>
    private T Get<T>(string key) where T : JsonValue
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        if (!_values.TryGetValue(key, out JsonValue? value))
            throw new SaveDataFault("SDD-001 §10", null,
                $"State key '{key}' is missing from the save.");

        if (typeof(T) == typeof(JsonValue)) return (T)value;

        return value as T
            ?? throw new SaveDataFault("SDD-013 §3", null,
                $"State key '{key}' holds {value.GetType().Name}, not {typeof(T).Name}.");
    }
}
