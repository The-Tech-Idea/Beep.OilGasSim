// SDD-012 — condition, degradation, failure. Both models are replaceable
// (design 03 §3.2) and BLIND to everything but their declared inputs: severity
// in, decay out; condition in, probability out. The hazard draw itself happens
// in the engine at stage 4, consuming ONLY the Hazard stream (D-4).

using OGSim.Kernel;

namespace OGSim.Contracts;

/// <summary>
/// What service does to equipment, per tick (SDD-012 §2): each term is a
/// dimensionless 0..1 severity the datasheet's decay curve responds to.
/// </summary>
public sealed record ServiceSeverity(
    double WaterCut,
    double SourFraction,
    double DutyFraction,
    double OverTemperature,
    double TicksSinceService);

/// <summary>Condition is 0..1, monotonically down between maintenance events (SDD-012 §2).</summary>
public interface IDegradationModel
{
    ContentId Id { get; }
    double NextCondition(double condition, ServiceSeverity severity, Duration dt);
}

/// <summary>
/// Probability the engine tests against the Hazard stream at stage 4
/// (SDD-012 §3). The model never draws — it only maps condition to hazard,
/// which keeps it deterministic and unit-testable without an RNG.
/// </summary>
public interface IHazardModel
{
    ContentId Id { get; }
    double FailureProbability(double condition, Duration dt);
}

/// <summary>
/// SDD-012 §5 — the long-arc hazard.
/// <c>H2S(t) = sourCurve(cumulative IMPORTED water ÷ pore volume)</c>, per
/// waterflooded compartment, with the curve content per rock type.
///
/// <para><b>Imported, not injected</b> (§5's R20d.25 amendment, finding 182).
/// Reinjected produced water has already been through the reservoir: it is
/// anoxic, reduced and stripped of the sulphate the bacteria eat, so it is the
/// fluid that sours a reservoir LEAST. Sea water carries roughly 2,700 ppm of
/// sulphate, and seawater flooding is what actually sours fields — so the ratio
/// counts the water a company BOUGHT and not the water it put back.</para>
///
/// <para><b>This is the tail on every waterflood.</b> A player fights decline by
/// injecting water, the water sours the reservoir over years, and the rising H2S
/// arrives in three places at once: the sales specification (sour crude is
/// discounted or unsellable), the corrosion severity term that ages every wetted
/// component, and the metallurgy envelope that decides whether the equipment is
/// rated for it at all. The decision to install sour-service metallurgy arrives
/// on schedule and years late, which is the design intent — it is a consequence
/// bought years earlier, not an event.</para>
///
/// <para>Truth-side, and it stays that way: what a player knows about souring
/// comes from produced-fluid samples, like everything else.</para>
/// </summary>
public interface ISouringModel
{
    ContentId Id { get; }

    /// <summary>Hydrogen sulphide concentration in ppm at the current
    /// throughput ratio. Monotonic in the ratio — water already injected cannot
    /// un-sour a reservoir.</summary>
    double HydrogenSulphidePpm(ContentId rockType, double importedWaterOverPoreVolume);
}
