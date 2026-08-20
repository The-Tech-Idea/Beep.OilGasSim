# CanonicalJson

Source: `src\OGSim.Persistence\CanonicalJson.cs` · Lines: 395

## File intent

> R19.1 — canonical JSON (SDD-013 §3, PV1).
> 
> WRITER AND READER LIVE IN ONE CLASS. There is no second serialisation path to
> drift — law L5 applied to bytes. Two paths would eventually disagree about a
> double's shortest representation or a key's sort order, and the disagreement
> would surface as a digest mismatch nobody could locate.
> 
> The rules exist so that two runs producing the same STATE produce the same

## Namespaces

- `OGSim.Persistence`

## Type declarations

- `L32` `public abstract record JsonValue;`
- `L34` `public sealed record JsonString(string Value) : JsonValue;`
- `L37` `public sealed record JsonInteger(long Value) : JsonValue;`
- `L39` `public sealed record JsonDouble(double Value) : JsonValue;`
- `L41` `public sealed record JsonBoolean(bool Value) : JsonValue;`
- `L44` `public sealed record JsonArray(IReadOnlyList<JsonValue> Items) : JsonValue`
- `L56` `public sealed record JsonObject(IReadOnlyDictionary<string, JsonValue> Members) : JsonValue`
- `L71` `public static class CanonicalJson`

## Accessible members

- `L49` `public bool Equals(JsonArray? other) =>`
- `L52` `public override int GetHashCode() => Structural.HashOf(Items);`
- `L62` `public bool Equals(JsonObject? other) =>`
- `L65` `public override int GetHashCode() => Structural.HashOf(Members);`
- `L73` `public static string Write(JsonValue value)`
- `L82` `private static void WriteTo(StringBuilder builder, JsonValue value)`
- `L126` `private static void WriteDouble(StringBuilder builder, double value)`
- `L145` `private static void WriteArray(StringBuilder builder, JsonArray array)`
- `L158` `private static void WriteObject(StringBuilder builder, JsonObject obj)`
- `L180` `private static void WriteString(StringBuilder builder, string value)`
- `L218` `public static JsonValue Read(string text)`
- `L233` `private static JsonValue ReadValue(string text, ref int position)`
- `L250` `private static JsonValue ReadObject(string text, ref int position)`
- `L279` `private static JsonValue ReadArray(string text, ref int position)`
- `L302` `private static JsonValue ReadBoolean(string text, ref int position)`
- `L310` `private static JsonValue ReadNumber(string text, ref int position)`
- `L334` `private static string ReadString(string text, ref int position)`
- `L367` `private static char ReadUnicode(string text, ref int position)`
- `L379` `private static void Expect(string text, ref int position, char expected)`
- `L391` `private static void SkipWhitespace(string text, ref int position)`

## Imports

- `using System.Globalization;`
- `using System.Text;`
- `using OGSim.Kernel;`

