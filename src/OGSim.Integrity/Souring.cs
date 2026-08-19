// R20d.25 — the reservoir turns sour (SDD-012 §5, finding 182).
//
// THE BILL FOR A DECISION TAKEN TWENTY YEARS AGO. A company floods to hold the
// pressure up and gets its oil; the sea water it bought carries sulphate, the
// bacteria in the formation reduce it, and the reservoir starts making hydrogen
// sulphide. By the time it shows up the flood is long since paid for and the
// plant it is eating was installed before anybody thought about metallurgy.
//
// SATURATING, because souring does. There is a finite amount of the rock's
// carbon and sulphate chemistry to work through, so the concentration climbs
// fast on the first pore volumes and then flattens — a linear curve would have a
// field at ten thousand ppm after a long enough flood, which is not a reservoir,
// it is an industrial accident.
//
// MONOTONIC, and the constructor enforces what it can. Water already injected
// cannot un-sour a reservoir (SDD-012 §5's own words), so the form is chosen to
// be monotonic by construction rather than checked sample by sample: with both
// parameters positive, r/(half + r) rises everywhere on r ≥ 0.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Integrity;

/// <summary>
/// SDD-012 §5's curve: <c>ppm(r) = ultimate · r / (half + r)</c>, where r is the
/// imported water a compartment has taken as a fraction of its pore volume.
/// </summary>
public sealed class SaturatingSourCurve : ISouringModel
{
    private readonly double _ultimatePpm;
    private readonly double _halfRatio;

    /// <param name="ultimatePpm">Where the curve levels off — what this rock
    /// will make if a flood runs for ever.</param>
    /// <param name="halfRatio">The throughput at which it is half way there.
    /// This is the parameter that decides whether souring is a late-life event
    /// or an immediate one, and it is the one a content author tunes.</param>
    public SaturatingSourCurve(ContentId id, double ultimatePpm, double halfRatio)
    {
        if (ultimatePpm <= 0.0 || !double.IsFinite(ultimatePpm))
            throw new ContentFault("SDD-012 §5", null,
                $"sour curve '{id.Value}' declares an ultimate of {Format(ultimatePpm)} ppm; " +
                "a curve that tends to nothing is a rock that does not sour, which is said " +
                "by not giving the compartment a curve at all");

        // A half-ratio of zero is a STEP: the field is fully sour on its first
        // barrel of imported water. That is not a fast curve, it is the absence
        // of one, and it would make the whole long-arc point of §5 vanish.
        if (halfRatio <= 0.0 || !double.IsFinite(halfRatio))
            throw new ContentFault("SDD-012 §5", null,
                $"sour curve '{id.Value}' declares a half-ratio of {Format(halfRatio)} pore " +
                "volumes; souring takes throughput, and a curve that arrives at the first " +
                "cubic metre is a step function wearing a curve's name");

        Id = id;
        _ultimatePpm = ultimatePpm;
        _halfRatio = halfRatio;
    }

    public ContentId Id { get; }

    /// <summary>
    /// ROCK TYPE IS ACCEPTED AND NOT YET READ, and that is the honest state
    /// rather than a stub: §5 says the curve is content per rock type, and this
    /// composition ships one curve because it ships one rock. The parameter is
    /// in the signature because the day a second rock exists the selection
    /// happens at composition — a curve per rock, registered by id — and not by
    /// a branch appearing inside this method.
    /// </summary>
    public double HydrogenSulphidePpm(ContentId rockType, double importedWaterOverPoreVolume)
    {
        double r = importedWaterOverPoreVolume;

        // A field nobody has flooded is sweet. Said rather than left to the
        // arithmetic because a negative ratio is a caller defect and returning
        // a negative concentration would hide it.
        if (r <= 0.0) return 0.0;

        return _ultimatePpm * r / (_halfRatio + r);
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
