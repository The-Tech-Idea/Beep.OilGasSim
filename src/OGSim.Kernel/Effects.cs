// SDD-005 §4 — the SEALED effect vocabulary. There is no multiplier record,
// and the hierarchy is closed to these four (architecture test R17-V13).
// Technology and environment speak this one language (design 07 §1 = 13 §2.1).

namespace OGSim.Kernel;

/// <summary>A named model slot the composition can rebind (design 03 §3.2).</summary>
public readonly record struct ModelSlot(string Name);

public readonly record struct ParameterKey(string Name);

/// <summary>
/// Envelope kinds with their combinator semantics fixed by SDD-005 §4.1:
/// effective = Min( Max(base, extensions…), restrictions… ) — extensions raise
/// what is possible, restrictions cap what is permitted, restrictions win.
/// </summary>
public enum EnvelopeKind
{
    MaxDrillingDepth, MaxWaterDepth, MaxWaveHeight, MaxAmbientTemperature,
    MaxH2SFraction, MaxCompressionRatio, ArcticOperability, MaxLoadBearing
}

/// <summary>Extension raises the base; Restriction caps the result (SDD-005 §4.1).</summary>
public enum EnvelopeContributionKind { Extension, Restriction }

public abstract record Effect;

/// <summary>Makes a content entry (catalogue tier, activity template, drive mechanism…) available.</summary>
public sealed record UnlockOption(ContentId What) : Effect;

public sealed record MoveEnvelope(
    EnvelopeKind Kind,
    EnvelopeContributionKind Contribution,
    double Value) : Effect;

/// <summary>Swap the registered implementation of a model slot.</summary>
public sealed record SetModelSelection(ModelSlot Slot, ContentId Plugin) : Effect;

public sealed record SetModelParameter(ModelSlot Slot, ParameterKey Key, double Value) : Effect;

/// <summary>
/// The combined effect state (SDD-005 §4.2): environment recomputed at stage 2,
/// technology applied next-tick on acquisition. DERIVED — never saved (SDD-013
/// §4). Envelope combination: Min(Max(base, extensions…), restrictions…).
/// Per-contribution provenance answers "why is this value what it is?".
/// </summary>
public interface IEffectState
{
    double EffectiveEnvelope(EnvelopeKind kind);
    ContentId SelectedPlugin(ModelSlot slot);
    double Parameter(ModelSlot slot, ParameterKey key);
}

/// <summary>
/// Where an unlockable fits (design 07 §4b.3b, SDD-005 §4.0b). Meters are
/// ComponentSocket entries — the enum matches 07's table exactly, by test.
/// </summary>
public enum SlotKind
{
    ComponentSocket, LiftSocket, DrillingFluid, CompletionFluid,
    ChemicalInjection, ProcessAdditive, InjectionStream, DriveMechanism, ModelSlot
}
