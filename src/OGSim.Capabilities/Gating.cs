// R17.2 / R17.7 — the one gating validator and the shared effect path
// (SDD-005 §3–4, design 07).
//
// EXACTLY ONE VALIDATOR evaluates every gated thing: operation template,
// equipment tier, well command. One implementation means one place where a
// refusal is worded, one place where rentals are honoured, and one thing to
// point an architecture test at.
//
// ALL MISSES ARE REPORTED, never just the first. A player who fixes one reason,
// resubmits, and hits the next has been made to pay twice for one piece of
// information — and in a game where acquiring a technology takes years, that is
// a very expensive lesson in interface design.
//
// AND ONE EFFECT PATH serves technology and environment alike. A cold climate
// restricting a rig's operability and a technology extending it are the same
// kind of statement, so they combine by one rule rather than two that have to
// be kept agreeing.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Capabilities;

/// <summary>SDD-005 §3's validator. The only one.</summary>
public sealed class GatingValidator : IGatingValidator
{
    public GateResult Check(
        Requirements requirements,
        ICapabilitySet capabilities,
        IReadOnlyList<ServiceRental> rentals,
        IEffectState effects)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(rentals);
        ArgumentNullException.ThrowIfNull(effects);

        var missing = new List<MissingItem>();

        // 1. Technology — held OR rented. A rental is scoped to one operation
        //    and never touches the capability set (SDD-005 §2), which is why it
        //    arrives here as an argument rather than as state.
        for (int i = 0; i < requirements.Tech.Count; i++)
        {
            TechnologyId required = requirements.Tech[i];
            if (capabilities.Has(required)) continue;
            if (IsRented(rentals, required)) continue;

            // Names the SPECIFIC technology, so the refusal renders straight to
            // the player as a domain reason.
            missing.Add(new MissingTechnology(required));
        }

        // 2. Detect tier — the survey spawn threshold.
        if (requirements.MinDetectClass is DetectClass needed
            && capabilities.MaxDetectClass < needed)
            missing.Add(new MissingDetectTier(needed, capabilities.MaxDetectClass));

        // 3. Envelopes — compared against EFFECTIVE values, which only the
        //    effect state can supply: a rig's depth rating is its base plus what
        //    technology extended, capped by what the climate restricts.
        for (int i = 0; i < requirements.Envelopes.Count; i++)
        {
            EnvelopeCheck check = requirements.Envelopes[i];
            double effective = effects.EffectiveEnvelope(check.Kind);

            if (effective >= check.RequiredValue) continue;

            missing.Add(new EnvelopeExceeded(check.Kind, check.RequiredValue, effective));
        }

        return missing.Count == 0 ? new GatePass() : new GateFail(missing);
    }

    private static bool IsRented(IReadOnlyList<ServiceRental> rentals, TechnologyId tech)
    {
        for (int i = 0; i < rentals.Count; i++)
            if (rentals[i].Capability == tech) return true;

        return false;
    }
}

/// <summary>
/// SDD-005 §4.2's combined effect state. Environment and technology apply
/// through ONE path.
///
/// <para>Envelope combination is <c>Min(Max(base, extensions…), restrictions…)</c>
/// — <b>restrictions win</b>. Technology extends what is possible; the
/// environment caps what is permitted; and a rig that can technically drill to
/// 6 000 m in a sea state it cannot work in still cannot work.</para>
///
/// <para>DERIVED, never saved (SDD-013 §4): recomputed from the held nodes and
/// the current environment, so a save cannot carry a stale envelope and a
/// loaded game cannot disagree with a fresh one.</para>
/// </summary>
public sealed class EffectState : IEffectState
{
    private readonly Dictionary<EnvelopeKind, double> _base = [];
    private readonly Dictionary<EnvelopeKind, double> _extensions = [];
    private readonly Dictionary<EnvelopeKind, double> _restrictions = [];
    private readonly Dictionary<ModelSlot, ContentId> _plugins = [];
    private readonly Dictionary<(ModelSlot, ParameterKey), double> _parameters = [];
    private readonly HashSet<ContentId> _unlocked = [];
    private readonly List<ContentId> _unlockedOrder = [];

    public EffectState(IReadOnlyDictionary<EnvelopeKind, double> baseEnvelopes)
    {
        ArgumentNullException.ThrowIfNull(baseEnvelopes);

        foreach (EnvelopeKind kind in AllKinds)
            _base[kind] = baseEnvelopes.TryGetValue(kind, out double value) ? value : 0.0;
    }

    /// <summary>
    /// Applies a set of effects. Called with technology's at acquisition and
    /// with the environment's at stage 2 — the SAME method, because they are the
    /// same kind of statement.
    /// </summary>
    public void Apply(IReadOnlyList<Effect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        for (int i = 0; i < effects.Count; i++)
        {
            switch (effects[i])
            {
                case UnlockOption unlock:
                    if (_unlocked.Add(unlock.What)) _unlockedOrder.Add(unlock.What);
                    break;

                case MoveEnvelope move:
                    Move(move);
                    break;

                case SetModelSelection selection:
                    _plugins[selection.Slot] = selection.Plugin;
                    break;

                case SetModelParameter parameter:
                    _parameters[(parameter.Slot, parameter.Key)] = parameter.Value;
                    break;

                default:
                    // The vocabulary is a SEALED four-record set (SDD-001 §7).
                    // An unrecognised effect means the vocabulary grew without
                    // this path growing with it, which is a fault rather than
                    // something to ignore.
                    throw new ModelFault("SDD-005 §4", null,
                        $"unknown effect {effects[i].GetType().Name}; the effect vocabulary " +
                        "is sealed and every member must be applied somewhere");
            }
        }
    }

    private void Move(MoveEnvelope move)
    {
        if (double.IsNaN(move.Value))
            throw new ModelFault("SDD-005 §4", null,
                $"envelope contribution to {move.Kind} is not a number");

        if (move.Contribution == EnvelopeContributionKind.Extension)
        {
            // The BEST extension, not the sum: two technologies that each raise
            // a rig to 5 000 m do not reach 10 000. Extensions are claims about
            // a ceiling, and the highest claim is the ceiling.
            _extensions[move.Kind] = Math.Max(
                _extensions.GetValueOrDefault(move.Kind, double.NegativeInfinity), move.Value);
            return;
        }

        // The TIGHTEST restriction, for the mirror-image reason.
        _restrictions[move.Kind] = Math.Min(
            _restrictions.GetValueOrDefault(move.Kind, double.PositiveInfinity), move.Value);
    }

    /// <summary><c>Min(Max(base, extensions), restrictions)</c>.</summary>
    public double EffectiveEnvelope(EnvelopeKind kind)
    {
        double extended = Math.Max(
            _base.GetValueOrDefault(kind),
            _extensions.GetValueOrDefault(kind, double.NegativeInfinity));

        return Math.Min(extended, _restrictions.GetValueOrDefault(kind, double.PositiveInfinity));
    }

    public ContentId SelectedPlugin(ModelSlot slot) =>
        _plugins.TryGetValue(slot, out ContentId plugin)
            ? plugin
            : throw new ModelFault("SDD-005 §4", null,
                $"no plugin is selected for slot {slot}; composition must register one " +
                "before anything can ask — there is no default implementation");

    public double Parameter(ModelSlot slot, ParameterKey key) =>
        _parameters.TryGetValue((slot, key), out double value)
            ? value
            : throw new ModelFault("SDD-005 §4", null,
                $"no value for parameter {key} of slot {slot}; a model parameter with no " +
                "declared value is content that was never written, not a zero");

    /// <summary>Whether a catalogue entry, template or mechanism is available
    /// (R17.7's catalogue gating).</summary>
    public bool IsUnlocked(ContentId what) => _unlocked.Contains(what);

    public IReadOnlyList<ContentId> Unlocked => _unlockedOrder;

    /// <summary>Declared order, so a walk is identical every run (D-5).</summary>
    private static readonly EnvelopeKind[] AllKinds =
    [
        EnvelopeKind.MaxDrillingDepth, EnvelopeKind.MaxWaterDepth,
        EnvelopeKind.MaxWaveHeight, EnvelopeKind.MaxAmbientTemperature,
        EnvelopeKind.MaxH2SFraction, EnvelopeKind.MaxCompressionRatio,
        EnvelopeKind.ArcticOperability, EnvelopeKind.MaxLoadBearing,
    ];
}
