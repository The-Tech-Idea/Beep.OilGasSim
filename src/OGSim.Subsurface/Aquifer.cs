// R5.5 — the aquifer (SDD-003 §3.3, Fetkovich form).
//
// An aquifer IS a water compartment, so the influx it reports is a reservoir
// volume like any other withdrawal or injection — it enters §3.1's balance as
// We and nowhere else. That is what keeps the water drive testable independently
// of the aquifer producing the water (R5-V8).

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Subsurface;

/// <summary>
/// Fetkovich: influx is proportional to the pressure difference across the
/// aquifer boundary and to elapsed time, and is bounded by the expansion the
/// aquifer has left.
///
/// <para>The bound is the part that matters for play. An unbounded aquifer holds
/// pressure forever and the field never declines; a real one weakens as it is
/// drawn down, which is why water drive is the best natural drive and still not
/// a free one. The aquifer's own pressure falls by its own material balance —
/// simple compressibility, because a water leg has no gas coming out of
/// solution and no rock term worth separating from the water's.</para>
/// </summary>
internal sealed class FetkovichAquifer : IAquiferModel
{
    public ContentId Id { get; } = new("fetkovich-aquifer");

    private readonly double _productivityIndex;    // J_aq, m³/s/Pa
    private readonly double _initialPressurePa;
    private readonly double _maximumInfluxM3;      // W_ei: total expansion available

    private double _cumulativeInfluxM3;

    public FetkovichAquifer(
        double productivityIndex,
        Pressure initialPressure,
        ReservoirVolume maximumInflux)
    {
        if (productivityIndex <= 0.0 || !double.IsFinite(productivityIndex))
            throw new ModelFault("SDD-003 §3.3", null,
                $"aquifer productivity index {Format(productivityIndex)} m³/s/Pa is not positive " +
                "and finite; an aquifer that cannot deliver is an absent aquifer, which is " +
                "expressed by not attaching one");

        if (maximumInflux.CubicMetres <= 0.0)
            throw new ModelFault("SDD-003 §3.3", null,
                $"aquifer maximum influx {Format(maximumInflux.CubicMetres)} m³ is not positive");

        _productivityIndex = productivityIndex;
        _initialPressurePa = initialPressure.Pascals;
        _maximumInfluxM3 = maximumInflux.CubicMetres;
    }

    /// <summary>Reservoir volume delivered so far. The aquifer's own depletion
    /// is a function of this and nothing else.</summary>
    public ReservoirVolume CumulativeInflux => new(_cumulativeInfluxM3);

    // WHAT A SAVE HAS TO CARRY (R20d.12). An aquifer is three constants and one
    // number that moves, and the one that moves is the whole mechanic: pressure
    // falls with the fraction already delivered, so an aquifer restored at zero
    // comes back at full strength however long the field has been draining it.
    // Its terms are exposed rather than recomputed from the compartment, because
    // this object owns them (L5).

    internal double ProductivityIndex => _productivityIndex;

    internal Pressure InitialPressure => new(_initialPressurePa);

    internal ReservoirVolume MaximumInflux => new(_maximumInfluxM3);

    /// <summary>
    /// Puts a restored aquifer back to what it had already given up.
    ///
    /// <para>Separate from delivering it, exactly as <c>SimulationClock.RestoreTo</c>
    /// is separate from <c>Advance</c>: restoring is allowed to jump, and only
    /// before the engine ticks.</para>
    /// </summary>
    internal void RestoreTo(ReservoirVolume delivered)
    {
        if (delivered.CubicMetres < 0.0 || delivered.CubicMetres > _maximumInfluxM3)
            throw new SaveDataFault("SDD-003 §3.3", null,
                $"a save says this aquifer has delivered {Format(delivered.CubicMetres)} m³ of " +
                $"a possible {Format(_maximumInfluxM3)}; an aquifer cannot have given up more " +
                "than it holds, and a negative influx is water flowing backwards");

        _cumulativeInfluxM3 = delivered.CubicMetres;
    }

    /// <summary>
    /// The aquifer's current pressure: it falls linearly with the fraction of
    /// its total expansion already delivered. Exposed because the aquifer's
    /// weakening is the interesting half of a water drive, and a caller that had
    /// to infer it from influx history would be recomputing a fact this object
    /// owns (law L5).
    /// </summary>
    public Pressure AquiferPressure =>
        new(_initialPressurePa * (1.0 - _cumulativeInfluxM3 / _maximumInfluxM3));

    public ReservoirVolume InfluxDuring(Pressure reservoirPressure, Duration duration)
    {
        double seconds = duration.Seconds;
        if (seconds < 0.0)
            throw new ModelFault("SDD-003 §3.3", null,
                $"negative duration {Format(seconds)} s");

        double drivePa = AquiferPressure.Pascals - reservoirPressure.Pascals;

        // Reservoir above aquifer: no influx. NOT negative influx — water does
        // not flow back into the aquifer at tank fidelity, and letting it would
        // let a compartment refill itself by being shut in.
        if (drivePa <= 0.0) return new ReservoirVolume(0.0);

        double influx = _productivityIndex * drivePa * seconds;

        double remaining = _maximumInfluxM3 - _cumulativeInfluxM3;
        if (influx > remaining) influx = remaining;

        _cumulativeInfluxM3 += influx;
        return new ReservoirVolume(influx);
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
