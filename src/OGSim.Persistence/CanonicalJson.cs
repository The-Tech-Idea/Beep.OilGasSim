// R19.1 — canonical JSON (SDD-013 §3, PV1).
//
// WRITER AND READER LIVE IN ONE CLASS. There is no second serialisation path to
// drift — law L5 applied to bytes. Two paths would eventually disagree about a
// double's shortest representation or a key's sort order, and the disagreement
// would surface as a digest mismatch nobody could locate.
//
// The rules exist so that two runs producing the same STATE produce the same
// BYTES, which is what makes a per-module digest a usable divergence locator
// rather than a checksum that changes for no reason:
//
//   · object keys ORDINAL-sorted — not culture-sorted, which would order
//     differently under a Turkish locale and make a save machine-specific
//   · doubles as SHORTEST ROUND-TRIP, invariant — never localised, never
//     fixed-point, because "0,5" and "0.50000" both lose
//   · NaN and Infinity are UNREPRESENTABLE — they were faults upstream, and a
//     save that could carry one would let a broken tick be reloaded as valid
//   · Money as integer cents; ids as strings, because ordinals never persist
//     (SDD-004 §6)

using System.Globalization;
using System.Text;
using OGSim.Kernel;

namespace OGSim.Persistence;

/// <summary>
/// A canonical JSON value. Deliberately a tiny closed model rather than a
/// general JSON library: the save format is a contract, and a type that could
/// represent something the format forbids would push the checking to runtime.
/// </summary>
public abstract record JsonValue;

public sealed record JsonString(string Value) : JsonValue;

/// <summary>Integers — ids, ticks, counts, and <b>Money in cents</b>.</summary>
public sealed record JsonInteger(long Value) : JsonValue;

public sealed record JsonDouble(double Value) : JsonValue;

public sealed record JsonBoolean(bool Value) : JsonValue;

/// <summary>Ordered by entity id at construction (D-5), never by insertion.</summary>
public sealed record JsonArray(IReadOnlyList<JsonValue> Items) : JsonValue;

/// <summary>Keys are ordinal-sorted when written; the dictionary's order is irrelevant.</summary>
public sealed record JsonObject(IReadOnlyDictionary<string, JsonValue> Members) : JsonValue;

/// <summary>
/// SDD-013 §3. The one writer and the one reader.
/// </summary>
public static class CanonicalJson
{
    public static string Write(JsonValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder();
        WriteTo(builder, value);
        return builder.ToString();
    }

    private static void WriteTo(StringBuilder builder, JsonValue value)
    {
        switch (value)
        {
            case JsonString text:
                WriteString(builder, text.Value);
                break;

            case JsonInteger integer:
                builder.Append(integer.Value.ToString(CultureInfo.InvariantCulture));
                break;

            case JsonDouble number:
                WriteDouble(builder, number.Value);
                break;

            case JsonBoolean flag:
                builder.Append(flag.Value ? "true" : "false");
                break;

            case JsonArray array:
                WriteArray(builder, array);
                break;

            case JsonObject obj:
                WriteObject(builder, obj);
                break;

            default:
                throw new SaveDataFault("SDD-013 §3", null,
                    $"unknown JSON value {value.GetType().Name}; the canonical model is " +
                    "closed and every member must have a writer");
        }
    }

    /// <summary>
    /// Shortest round-trip, invariant. <c>"R"</c> guarantees that parsing the
    /// text returns the identical double.
    ///
    /// <para>NaN and Infinity are refused rather than written. They were faults
    /// upstream — every model in the engine treats a non-finite value as a
    /// MODEL FAULT — and a save that could carry one would let a broken tick be
    /// reloaded as a valid game.</para>
    /// </summary>
    private static void WriteDouble(StringBuilder builder, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new SaveDataFault("SDD-013 §3", null,
                $"cannot persist {value.ToString(CultureInfo.InvariantCulture)}: non-finite " +
                "values are faults upstream and are unrepresentable in a save");

        string text = value.ToString("R", CultureInfo.InvariantCulture);

        // A double whose value is integral renders as "1", which the reader
        // would take for an INTEGER — and integers are ids and Money cents,
        // which must stay exact above 2^53 and so cannot be read as doubles.
        // Writing "1.0" keeps the two kinds distinguishable and keeps
        // Write(Read(s)) == s true, which is the property the digest rests on.
        if (text.AsSpan().IndexOfAny('.', 'e', 'E') < 0) text += ".0";

        builder.Append(text);
    }

    private static void WriteArray(StringBuilder builder, JsonArray array)
    {
        builder.Append('[');

        for (int i = 0; i < array.Items.Count; i++)
        {
            if (i > 0) builder.Append(',');
            WriteTo(builder, array.Items[i]);
        }

        builder.Append(']');
    }

    private static void WriteObject(StringBuilder builder, JsonObject obj)
    {
        // ORDINAL sort, not culture. A culture-sensitive sort orders differently
        // under a Turkish locale, which would make a save machine-specific and
        // every cross-platform digest comparison meaningless.
        var keys = new List<string>(obj.Members.Keys);
        keys.Sort(StringComparer.Ordinal);

        builder.Append('{');

        for (int i = 0; i < keys.Count; i++)
        {
            if (i > 0) builder.Append(',');

            WriteString(builder, keys[i]);
            builder.Append(':');
            WriteTo(builder, obj.Members[keys[i]]);
        }

        builder.Append('}');
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;

                default:
                    if (c < 0x20)
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        builder.Append(c);
                    break;
            }
        }

        builder.Append('"');
    }

    // ---------------------------------------------------------- reading

    /// <summary>
    /// The reader, in the same class as the writer.
    ///
    /// <para>Round-tripping is the property that matters: <c>Read(Write(v))</c>
    /// must equal <c>v</c>, and <c>Write(Read(s))</c> must equal <c>s</c>. The
    /// second is the stronger one — it says the format has exactly one
    /// representation per value, which is what makes a digest meaningful.</para>
    /// </summary>
    public static JsonValue Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int position = 0;
        JsonValue value = ReadValue(text, ref position);

        SkipWhitespace(text, ref position);
        if (position != text.Length)
            throw new SaveDataFault("SDD-013 §3", null,
                $"trailing content at position {position}; a canonical document is one value");

        return value;
    }

    private static JsonValue ReadValue(string text, ref int position)
    {
        SkipWhitespace(text, ref position);

        if (position >= text.Length)
            throw new SaveDataFault("SDD-013 §3", null, "unexpected end of document");

        return text[position] switch
        {
            '"' => new JsonString(ReadString(text, ref position)),
            '{' => ReadObject(text, ref position),
            '[' => ReadArray(text, ref position),
            't' or 'f' => ReadBoolean(text, ref position),
            _ => ReadNumber(text, ref position),
        };
    }

    private static JsonValue ReadObject(string text, ref int position)
    {
        position++;      // '{'
        var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);

        SkipWhitespace(text, ref position);
        if (position < text.Length && text[position] == '}') { position++; return new JsonObject(members); }

        while (true)
        {
            SkipWhitespace(text, ref position);
            string key = ReadString(text, ref position);

            SkipWhitespace(text, ref position);
            Expect(text, ref position, ':');

            members[key] = ReadValue(text, ref position);

            SkipWhitespace(text, ref position);
            if (position >= text.Length)
                throw new SaveDataFault("SDD-013 §3", null, "unterminated object");

            if (text[position] == ',') { position++; continue; }

            Expect(text, ref position, '}');
            return new JsonObject(members);
        }
    }

    private static JsonValue ReadArray(string text, ref int position)
    {
        position++;      // '['
        var items = new List<JsonValue>();

        SkipWhitespace(text, ref position);
        if (position < text.Length && text[position] == ']') { position++; return new JsonArray(items); }

        while (true)
        {
            items.Add(ReadValue(text, ref position));

            SkipWhitespace(text, ref position);
            if (position >= text.Length)
                throw new SaveDataFault("SDD-013 §3", null, "unterminated array");

            if (text[position] == ',') { position++; continue; }

            Expect(text, ref position, ']');
            return new JsonArray(items);
        }
    }

    private static JsonValue ReadBoolean(string text, ref int position)
    {
        if (text.AsSpan(position).StartsWith("true")) { position += 4; return new JsonBoolean(true); }
        if (text.AsSpan(position).StartsWith("false")) { position += 5; return new JsonBoolean(false); }

        throw new SaveDataFault("SDD-013 §3", null, $"malformed literal at position {position}");
    }

    private static JsonValue ReadNumber(string text, ref int position)
    {
        int start = position;

        while (position < text.Length && (char.IsAsciiDigit(text[position])
               || text[position] is '-' or '+' or '.' or 'e' or 'E'))
            position++;

        ReadOnlySpan<char> span = text.AsSpan(start, position - start);

        // An INTEGER stays an integer. Money is integer cents and ids are
        // integers; parsing them as doubles would silently lose precision above
        // 2^53, which for cents is a perfectly reachable balance.
        if (span.IndexOfAny('.', 'e', 'E') < 0
            && long.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
            return new JsonInteger(integer);

        if (!double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            throw new SaveDataFault("SDD-013 §3", null,
                $"malformed number '{span}' at position {start}");

        return new JsonDouble(value);
    }

    private static string ReadString(string text, ref int position)
    {
        Expect(text, ref position, '"');

        var builder = new StringBuilder();

        while (position < text.Length && text[position] != '"')
        {
            char c = text[position++];

            if (c != '\\') { builder.Append(c); continue; }

            if (position >= text.Length)
                throw new SaveDataFault("SDD-013 §3", null, "unterminated escape");

            char escape = text[position++];
            builder.Append(escape switch
            {
                '"' => '"',
                '\\' => '\\',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'u' => ReadUnicode(text, ref position),
                _ => throw new SaveDataFault("SDD-013 §3", null,
                    $"unknown escape '\\{escape}'"),
            });
        }

        Expect(text, ref position, '"');
        return builder.ToString();
    }

    private static char ReadUnicode(string text, ref int position)
    {
        if (position + 4 > text.Length)
            throw new SaveDataFault("SDD-013 §3", null, "truncated unicode escape");

        char value = (char)int.Parse(
            text.AsSpan(position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        position += 4;
        return value;
    }

    private static void Expect(string text, ref int position, char expected)
    {
        if (position >= text.Length || text[position] != expected)
            throw new SaveDataFault("SDD-013 §3", null,
                $"expected '{expected}' at position {position}");

        position++;
    }

    /// <summary>Canonical output has none, but a hand-edited or migrated
    /// document may — and refusing to read what we would never write would make
    /// every migration fixture unreadable.</summary>
    private static void SkipWhitespace(string text, ref int position)
    {
        while (position < text.Length && text[position] is ' ' or '\t' or '\n' or '\r') position++;
    }
}
