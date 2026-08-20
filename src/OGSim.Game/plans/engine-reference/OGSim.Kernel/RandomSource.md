# RandomSource

Source: `src\OGSim.Kernel\RandomSource.cs` · Lines: 215

## File intent

> R1.4 — seeded, per-subsystem randomness (SDD-001 §4). Design 11 §3.1: adding a
> draw in one subsystem must never shift another's sequence, which is what keeps
> world seeds stable across engine versions. Eight independent PCG64 streams,
> each seeded from the world seed and its own name.
> 
> PCG64 rather than any BCL generator: System.Random is banned outright (D-6),
> its algorithm is an implementation detail that has changed between .NET
> versions, and it has no seek. A counter-based generator makes save/restore an

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L19` `public sealed class RandomSource : IRandomSource`
- `L65` `private sealed class PcgStream : IRandomStream`

## Accessible members

- `L21` `private readonly PcgStream[] _streams;`
- `L23` `public RandomSource(ulong worldSeed)`
- `L31` `public IRandomStream Stream(StreamId id)`
- `L46` `private static string NameOf(StreamId id) => id switch`
- `L67` `private static readonly UInt128 Multiplier =`
- `L70` `private readonly UInt128 _increment;   // per-stream, always odd`
- `L71` `private readonly UInt128 _origin;      // state at position 0`
- `L72` `private UInt128 _state;`
- `L74` `public PcgStream(ulong worldSeed, string streamName)`
- `L96` `public ulong Position { get; private set; }`
- `L98` `public void Seek(ulong position)`
- `L107` `public double NextUnit() => (Next64() >> 11) * (1.0 / 9007199254740992.0);`
- `L120` `public double NextNormal()`
- `L140` `public int NextInt(int exclusiveMax)`
- `L153` `private ulong Next64()`
- `L168` `private static UInt128 Advance(UInt128 state, UInt128 delta, UInt128 multiplier, UInt128 increment)`
- `L196` `private static ulong Fnv1a64(string text)`
- `L207` `private static ulong SplitMix64(ulong value)`

## Imports

- `using System.Numerics;`

