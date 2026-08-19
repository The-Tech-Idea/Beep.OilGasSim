# Effects

Source: `src\OGSim.Kernel\Effects.cs` · Lines: 62

## File intent

> SDD-005 §4 — the SEALED effect vocabulary. There is no multiplier record,
> and the hierarchy is closed to these four (architecture test R17-V13).
> Technology and environment speak this one language (design 07 §1 = 13 §2.1).
> <summary>A named model slot the composition can rebind (design 03 §3.2).</summary>

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L8` `public readonly record struct ModelSlot(string Name);`
- `L10` `public readonly record struct ParameterKey(string Name);`
- `L17` `public enum EnvelopeKind`
- `L24` `public enum EnvelopeContributionKind { Extension, Restriction }`
- `L26` `public abstract record Effect;`
- `L29` `public sealed record UnlockOption(ContentId What) : Effect;`
- `L31` `public sealed record MoveEnvelope(`
- `L37` `public sealed record SetModelSelection(ModelSlot Slot, ContentId Plugin) : Effect;`
- `L39` `public sealed record SetModelParameter(ModelSlot Slot, ParameterKey Key, double Value) : Effect;`
- `L47` `public interface IEffectState`
- `L58` `public enum SlotKind`

## Accessible members

_No public/internal/protected/private member lines matched the extractor._

