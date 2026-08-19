// R22.1 — the daily severity process (SDD-016 §1).
//
// x(d+1) = ρ·x(d) + sqrt(1 − ρ²)·ε(d),  ε ~ N(0,1)
//
// The coefficient pair is what makes the process STATIONARY N(0,1): the
// variance of the next state is ρ²·1 + (1 − ρ²)·1 = 1, so the content curves
// written over x mean the same thing in month one and month four hundred. A
// process that merely damped toward zero would quietly narrow its own weather
// as a campaign ran, and nothing in the content would say so.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Environment;

/// <summary>SDD-016 §1's AR(1) advance.</summary>
public sealed class Ar1Weather : IWeatherModel
{
    private readonly double _persistence;
    private readonly double _innovation;

    public Ar1Weather(double persistence)
    {
        // ρ = 1 is a random walk, not weather: it never returns to the seasonal
        // mean and the variance grows without bound, so the curves over x stop
        // describing anything. Excluded rather than clamped.
        if (!double.IsFinite(persistence) || persistence < 0.0 || persistence >= 1.0)
            throw new ContentFault("SDD-016 §1", null,
                "weather persistence is in [0, 1): 0 is a fresh draw every day and " +
                "1 is a random walk that never comes back to the season");

        _persistence = persistence;
        _innovation = DetMath.Sqrt(1.0 - (persistence * persistence));
    }

    public ContentId Id { get; } = new("ar1-weather");

    public double NextState(double x, IRandomStream weather)
    {
        ArgumentNullException.ThrowIfNull(weather);

        return (_persistence * x) + (_innovation * weather.NextNormal());
    }

    /// <summary>
    /// ρ, exposed because the FORECAST is a theorem about this process rather
    /// than a second model (SDD-016 §4): E[x(d+h)] = ρ^h·x(d). A forecaster that
    /// held its own persistence could drift from the generator, which is the
    /// failure §4 exists to make impossible.
    /// </summary>
    public double Persistence => _persistence;
}
