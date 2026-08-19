# StateBlock

Source: `src\OGSim.Persistence\StateBlock.cs` · Lines: 165

## File intent

> R20c.6 — the bridge between a state owner and a save block
> (SDD-001 §10, SDD-013 §3).
> 
> WRITER AND READER LIVE IN ONE CLASS (SDD-013 §3). Two classes would be two
> places for the key spelling, the number format and the missing-value rule to
> drift apart, and the drift would surface as a save that writes correctly and
> loads wrong — the L5 principle applied to bytes.
> 

## Namespaces

- `OGSim.Persistence`

## Type declarations

- `L26` `public sealed class StateBlock : IStateWriter, IStateReader`

## Accessible members

- `L31` `private readonly SortedDictionary<string, JsonValue> _values =`
- `L34` `private readonly bool _reading;`
- `L36` `private StateBlock(bool reading) => _reading = reading;`
- `L39` `public static StateBlock Capture(IStateOwner owner)`
- `L55` `public static void Restore(IStateOwner owner, JsonValue state)`
- `L78` `private const string SchemaVersionKey = "$schema-version";`
- `L81` `public JsonValue Written()`
- `L91` `public void WriteString(string key, string value)`
- `L97` `public void WriteInt64(string key, long value) => Put(key, new JsonInteger(value));`
- `L99` `public void WriteDouble(string key, double value)`
- `L112` `private void Put(string key, JsonValue value)`
- `L129` `public string ReadString(string key) => Get<JsonString>(key).Value;`
- `L131` `public long ReadInt64(string key) => Get<JsonInteger>(key).Value;`
- `L138` `public double ReadDouble(string key) => Get<JsonValue>(key) switch`
- `L151` `private T Get<T>(string key) where T : JsonValue`

## Imports

- `using OGSim.Kernel;`

