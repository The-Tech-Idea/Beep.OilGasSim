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
