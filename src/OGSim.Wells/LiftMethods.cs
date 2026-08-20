// R7.1–R7.5 — artificial lift (SDD-003 §6.2, design 02 §3.3, R7).
//
// Every well starts flowing naturally and every well stops. R6-V6 is that
// moment; this is the answer to it. Each method has a different cost, a
// different capability envelope and a different failure mode, and choosing
// between them is one of the game's best decisions precisely because the
// envelopes overlap only partly.
//
// NO METHOD HAS ITS OWN INTERFACE. §6.2's three hooks — a pressure boost, a
// density reduction, a displacement cap — cover all four, and each fills the
// fields it uses. The outflow model applies all of them uniformly, so nothing
// anywhere asks which method is installed (design 02 §4.1).

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Wells;

/// <summary>
/// What every method shares: identity as a component, an envelope, and the
/// envelope's consequences. What it does NOT share is the effect — that is the
/// one thing a lift method is.
/// </summary>
public abstract class LiftMethod : ILiftMethod
{
    /// <summary>How much performance is lost per unit of relative exceedance.
    /// R7 §2.2: outside the envelope still works, just worse.</summary>
    private const double DegradationPerExceedance = 0.35;

    /// <summary>And how much sooner it fails. Consumed by R18's hazard model;
    /// declared here because the envelope is what knows about it.</summary>
    private const double HazardPerExceedance = 3.0;

    protected LiftMethod(
        EntityId<IWellComponent> id, ContentId tier, LiftEnvelope envelope, GameDate installed)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        Id = id;
        InstalledTier = tier;
        Tier = tier;
        Envelope = envelope;
        Installed = installed;
        Condition = 1.0;
    }

    public EntityId<IWellComponent> Id { get; }
    public ContentId Tier { get; }
    public ContentId InstalledTier { get; }
    public LiftEnvelope Envelope { get; }
    public GameDate Installed { get; }
    public double Condition { get; }

    /// <summary>
    /// R7 §2.2 / R7-V5. Every breach is NAMED, because the player is meant to be
    /// able to read the envelope before installing and diagnose the degradation
    /// afterwards. An assessment that reported only "degraded" would make the
    /// trap unavoidable rather than avoidable.
    /// </summary>
    public EnvelopeAssessment Assess(LiftConditions conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        var exceeded = new List<ContentId>();
        double worst = 0.0;

        Check("rate-below-minimum",
            Envelope.MinRate.CubicMetresPerSecond - conditions.Rate.CubicMetresPerSecond,
            Math.Max(Envelope.MinRate.CubicMetresPerSecond, RelativeFloor));

        Check("rate-above-maximum",
            conditions.Rate.CubicMetresPerSecond - Envelope.MaxRate.CubicMetresPerSecond,
            Envelope.MaxRate.CubicMetresPerSecond);

        Check("too-deep",
            conditions.Depth.Metres - Envelope.MaxDepth.Metres,
            Envelope.MaxDepth.Metres);

        Check("too-deviated",
            conditions.DeviationDegrees - Envelope.MaxDeviationDegrees,
            Math.Max(Envelope.MaxDeviationDegrees, RelativeFloor));

        Check("too-gassy",
            conditions.GasFraction - Envelope.MaxGasFraction,
            Math.Max(Envelope.MaxGasFraction, RelativeFloor));

        Check("too-hot",
            conditions.Temperature.Kelvin - Envelope.MaxTemperature.Kelvin,
            Envelope.MaxTemperature.Kelvin);

        Check("too-much-solids",
            conditions.SolidsFraction - Envelope.MaxSolidsFraction,
            Math.Max(Envelope.MaxSolidsFraction, RelativeFloor));

        if (exceeded.Count == 0)
            return new EnvelopeAssessment(
                Within: true, PerformanceFactor: 1.0, HazardMultiplier: 1.0, Exceeded: []);

        // Driven by the WORST single breach rather than by their sum: a pump
        // half a decade too hot and marginally too gassy fails from the heat,
        // and adding the two would make a trivial second breach look decisive.
        return new EnvelopeAssessment(
            Within: false,
            PerformanceFactor: Math.Max(0.0, 1.0 - DegradationPerExceedance * worst),
            HazardMultiplier: 1.0 + HazardPerExceedance * worst,
            Exceeded: exceeded);

        void Check(string name, double overBy, double scale)
        {
            if (overBy <= 0.0) return;

            exceeded.Add(new ContentId(name));

            double relative = overBy / scale;
            if (relative > worst) worst = relative;
        }
    }

    /// <summary>SDD-003 §6.2's hooks. The one thing that differs per method.</summary>
    public abstract LiftEffect EffectAt(ReservoirRate rate, Density mixtureDensity);

    /// <summary>Guards a division when an envelope field is legitimately zero
    /// (a method with no minimum rate, no deviation tolerance).</summary>
    private const double RelativeFloor = 1e-9;
}

/// <summary>
/// ESP — an electric submersible pump. Adds pressure directly, at high rate,
/// and draws real power.
///
/// <para>SDD-003 §6.2: head-vs-rate is a piecewise-linear catalogue curve at
/// reference density 1000 kg/m³, scaled by ρ_mix/ρ_ref. The scaling is why a
/// pump sized on water underperforms on oil — head is a height, and converting
/// it to pressure needs the density of what is actually being lifted.</para>
///
/// <para>Its weakness is gas: an ESP in a gassy well gas-locks. That is the
/// envelope breach R7 §2.2 is written about.</para>
/// </summary>
public sealed class ElectricSubmersiblePump : LiftMethod
{
    /// <summary>The density the catalogue curve is quoted at (SDD-003 §6.2).</summary>
    private const double ReferenceDensityKgPerM3 = 1000.0;

    private readonly IReadOnlyList<(double RateM3PerS, double HeadM)> _curve;
    private readonly double _efficiency;

    public ElectricSubmersiblePump(
        EntityId<IWellComponent> id,
        ContentId tier,
        LiftEnvelope envelope,
        GameDate installed,
        IReadOnlyList<(double RateM3PerS, double HeadM)> headCurve,
        double efficiency)
        : base(id, tier, envelope, installed)
    {
        ArgumentNullException.ThrowIfNull(headCurve);

        if (headCurve.Count < 2)
            throw new ModelFault("SDD-003 §6.2", null,
                $"ESP tier {tier.Value} declares {headCurve.Count} curve points; a " +
                "piecewise-linear head curve needs at least two");

        for (int i = 1; i < headCurve.Count; i++)
            if (headCurve[i].RateM3PerS <= headCurve[i - 1].RateM3PerS)
                throw new ModelFault("SDD-003 §6.2", null,
                    $"ESP tier {tier.Value}: curve points must ascend in rate; " +
                    $"point {i} is at {Format(headCurve[i].RateM3PerS)} m³/s");

        if (efficiency <= 0.0 || efficiency > 1.0)
            throw new ModelFault("SDD-003 §6.2", null,
                $"ESP tier {tier.Value} declares efficiency {Format(efficiency)}, not in (0, 1]");

        _curve = headCurve;
        _efficiency = efficiency;
    }

    public override LiftEffect EffectAt(ReservoirRate rate, Density mixtureDensity)
    {
        double headM = HeadAt(rate.CubicMetresPerSecond);

        // Head is a HEIGHT of the reference fluid. Converting it to a pressure
        // rise uses the density actually in the tubing, which is the whole of
        // §6.2's ρ_mix/ρ_ref scaling.
        double boostPa = headM * mixtureDensity.KgPerCubicMetre * PhysicalConstants.GravityMPerS2;

        // Hydraulic power over efficiency. This is what R7 §2.4 means by the
        // power draw being real: twenty ESPs need generation, and if the field
        // has not got it, something else goes offline.
        double hydraulicW = boostPa * rate.CubicMetresPerSecond;

        return new LiftEffect(
            new Pressure(boostPa),
            DensityFactor: 1.0,
            DisplacementCap: null,
            new Power(hydraulicW / _efficiency));
    }

    /// <summary>Piecewise-linear interpolation on the catalogue curve, flat
    /// outside its ends — a pump run past its last catalogue point is off its
    /// curve, and extrapolating a fitted line there would invent head the
    /// datasheet never claimed.</summary>
    public double HeadAt(double rateM3PerS)
    {
        if (rateM3PerS <= _curve[0].RateM3PerS) return _curve[0].HeadM;

        for (int i = 1; i < _curve.Count; i++)
        {
            (double r1, double h1) = _curve[i];
            if (rateM3PerS > r1) continue;

            (double r0, double h0) = _curve[i - 1];
            return h0 + (h1 - h0) * (rateM3PerS - r0) / (r1 - r0);
        }

        return _curve[^1].HeadM;
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Gas lift — injects gas into the tubing to lighten the column.
///
/// <para>It adds no pressure at all: it makes the hydrostatic term smaller by
/// reducing ρ_mix above the injection valve. That is why it works on high-GOR
/// and deviated wells where an ESP will not, and why it needs a compressed gas
/// supply that competes with sales gas (R9).</para>
///
/// <para><b>The optimum is real</b> (R7-V4). Too little gas does not lighten the
/// column enough to pay for itself; too much adds friction faster than it
/// removes weight, because the extra volume has to be pushed up the same tubing.
/// The turning point falls out of those two terms rather than being asserted.</para>
/// </summary>
public sealed class GasLift : LiftMethod
{
    private readonly double _injectionRateM3PerS;
    private readonly double _gasDensityKgPerM3;

    public GasLift(
        EntityId<IWellComponent> id,
        ContentId tier,
        LiftEnvelope envelope,
        GameDate installed,
        double injectionRateM3PerS,
        double gasDensityKgPerM3)
        : base(id, tier, envelope, installed)
    {
        if (injectionRateM3PerS < 0.0)
            throw new ModelFault("SDD-003 §6.2", null,
                $"gas-lift injection rate {Format(injectionRateM3PerS)} m³/s is negative");

        if (gasDensityKgPerM3 <= 0.0)
            throw new ModelFault("SDD-003 §6.2", null,
                $"gas-lift gas density {Format(gasDensityKgPerM3)} kg/m³ is not positive");

        _injectionRateM3PerS = injectionRateM3PerS;
        _gasDensityKgPerM3 = gasDensityKgPerM3;
    }

    /// <summary>The injected rate, exposed because R9 replaces the purchased
    /// supply with the field's own compressed gas and needs to know how much.</summary>
    public ReservoirRate InjectionRate => new(_injectionRateM3PerS);

    public override LiftEffect EffectAt(ReservoirRate rate, Density mixtureDensity)
    {
        double liquid = Math.Max(0.0, rate.CubicMetresPerSecond);

        // Nothing injected is genuinely no lift. But ZERO LIQUID is not: the gas
        // goes down the tubing whether or not the well is producing, so at zero
        // rate the column is pure injected gas — the lightest it ever gets.
        //
        // A first version returned None there, and it broke revival outright:
        // §6.3's bracket floor is the VLP at zero rate, so a well whose lift
        // vanished at zero could never enter a search in which the lift would
        // have saved it. The formula below handles the limit correctly on its
        // own, which is why the special case had to go rather than be corrected.
        if (_injectionRateM3PerS <= 0.0) return LiftEffect.None;

        // Volume-weighted mixing above the injection valve. The gas occupies
        // volume it barely adds mass to, so the average density falls.
        double total = liquid + _injectionRateM3PerS;
        double mixed = (liquid * mixtureDensity.KgPerCubicMetre
                      + _injectionRateM3PerS * _gasDensityKgPerM3) / total;

        return new LiftEffect(
            new Pressure(0.0),
            DensityFactor: mixed / mixtureDensity.KgPerCubicMetre,
            DisplacementCap: null,
            new Power(0.0));            // the compressor's power is R9's, not the well's
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Rod pump — mechanical lift by a downhole plunger on a rod string.
///
/// <para>SDD-003 §6.2: it REPLACES the outflow relation with a displacement cap,
/// <c>q ≤ q_pump</c>. That is the whole model and it is the honest one: a
/// positive-displacement pump moves a fixed volume per stroke, so the rate is
/// set by the pump and not by the reservoir. A well capable of ten times the
/// pump's displacement produces the pump's displacement (R7-V6).</para>
///
/// <para>Rate-limited and deviation-limited, which is why it belongs to shallow,
/// low-rate, late-life wells — exactly the ones an ESP would be absurd on.</para>
/// </summary>
public sealed record RodPumpTier(
    ContentId Id, LiftEnvelope Envelope, double DisplacementCubicMetresPerSecond);

public sealed class RodPump : LiftMethod
{
    private readonly double _displacementM3PerS;

    public RodPump(
        EntityId<IWellComponent> id,
        ContentId tier,
        LiftEnvelope envelope,
        GameDate installed,
        double displacementM3PerS)
        : base(id, tier, envelope, installed)
    {
        if (displacementM3PerS <= 0.0)
            throw new ModelFault("SDD-003 §6.2", null,
                $"rod-pump displacement {Format(displacementM3PerS)} m³/s is not positive");

        _displacementM3PerS = displacementM3PerS;
    }

    public override LiftEffect EffectAt(ReservoirRate rate, Density mixtureDensity) =>
        new(new Pressure(0.0),
            DensityFactor: 1.0,
            DisplacementCap: new ReservoirRate(_displacementM3PerS),
            new Power(0.0));

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// PCP — a progressing cavity pump. Also positive-displacement, so also a
/// displacement cap, and distinguished from the rod pump by its ENVELOPE:
/// viscous oil and sand it tolerates, heat its elastomer does not.
///
/// <para>Sharing the rod pump's model is the correct answer, not a shortcut.
/// §6.2 gives both the same relation, the difference between them is entirely
/// in the tier's envelope fields, and inventing a distinct formula to make the
/// two types look different in code would be exactly the fabrication F-1
/// exists to prevent.</para>
/// </summary>
public sealed class ProgressingCavityPump : LiftMethod
{
    private readonly double _displacementM3PerS;

    public ProgressingCavityPump(
        EntityId<IWellComponent> id,
        ContentId tier,
        LiftEnvelope envelope,
        GameDate installed,
        double displacementM3PerS)
        : base(id, tier, envelope, installed)
    {
        if (displacementM3PerS <= 0.0)
            throw new ModelFault("SDD-003 §6.2", null,
                $"PCP displacement {Format(displacementM3PerS)} m³/s is not positive");

        _displacementM3PerS = displacementM3PerS;
    }

    public override LiftEffect EffectAt(ReservoirRate rate, Density mixtureDensity) =>
        new(new Pressure(0.0),
            DensityFactor: 1.0,
            DisplacementCap: new ReservoirRate(_displacementM3PerS),
            new Power(0.0));

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
