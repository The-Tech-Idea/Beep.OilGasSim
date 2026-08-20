// R11-V6 (SDD-006 §3d): boosting pressure restores capacity, and power
// matches the model.

using OGSim.Contracts;
using OGSim.Facilities;
using OGSim.Kernel;

namespace OGSim.Facilities.Tests;

public class PumpStationTests
{
    private static PumpTier Tier(double capacity = 50.0, double efficiency = 0.75) =>
        new(new ContentId("pump-tier-b"), new MassRate(capacity), efficiency);

    private static LiquidPumpStation Unit(
        double suctionBar = 10.0, double dischargeBar = 70.0,
        double densityKgPerM3 = 850.0, PumpTier? tier = null) =>
        new(new EntityId<IFlowElement>(1), tier ?? Tier(),
            Pressure.FromBar(suctionBar), Pressure.FromBar(dischargeBar),
            new Density(densityKgPerM3), Fx.MaterialCount);

    [Fact] // SDD-006 §3d: specific work is the pressure rise over density, exactly
    public void R11V6_specific_work_matches_the_incompressible_formula()
    {
        LiquidPumpStation unit = Unit(suctionBar: 10.0, dischargeBar: 70.0, densityKgPerM3: 850.0);

        double expected = (Pressure.FromBar(70.0).Pascals - Pressure.FromBar(10.0).Pascals) / 850.0;

        Assert.Equal(expected, unit.SpecificWorkJoulesPerKg, precision: 6);
    }

    [Fact] // SDD-006 §3d: shaft power is work × rate / efficiency, recomputed independently
    public void R11V6_shaft_power_matches_the_model()
    {
        LiquidPumpStation unit = Unit(suctionBar: 10.0, dischargeBar: 70.0, densityKgPerM3: 850.0);

        double expected = 20.0 * unit.SpecificWorkJoulesPerKg / 0.75;

        Assert.Equal(expected, unit.ShaftPowerFor(new MassRate(20.0)).Watts, precision: 3);
    }

    [Fact] // R11-V6: boosting pressure conserves — a pump raises pressure, it does not eat liquid
    public void R11V6_pumping_conserves_mass_and_raises_pressure()
    {
        MaterialStream inlet = Fx.Stream(40.0, 0.0, 0.0);
        TransformResult result = Unit().Transform(Fx.In(inlet));

        Assert.Equal(40.0, result.Outlets[0].MassRates.Total.KgPerSecond, 12);
        Assert.Equal(0.0, result.FuelConsumed.Total.KgPerSecond, 12);

        Assert.Equal(Pressure.FromBar(70.0).Pascals, result.Outlets[0].P.Pascals, 6);
    }

    [Fact] // R11-V6: the station reports its rated capacity as its constraint — undegraded (SDD-006 §3d, no derate curve)
    public void R11V6_the_reported_capacity_is_the_rated_one()
    {
        LiquidPumpStation unit = Unit(tier: Tier(capacity: 50.0));

        var hot = new SegmentContext(30, Temperature.FromCelsius(45.0), 0.0);
        ConstraintEvaluation constraint = Assert.Single(
            unit.EvaluateConstraints(new TransformInput([Fx.Stream(40.0, 0.0, 0.0)], hot, null)));

        Assert.Equal(ConstraintKind.TotalCapacity, constraint.Kind);
        Assert.Equal(50.0, constraint.Capacity, 9);
    }

    [Theory] // Content errors are refused where the datasheet is still in hand
    [InlineData(10.0, 5.0, 850.0, 0.75, "a pump raises pressure")]
    [InlineData(10.0, 70.0, 850.0, 0.0, "pump efficiency")]
    [InlineData(10.0, 70.0, 850.0, 1.5, "pump efficiency")]
    public void R11V6_an_unusable_configuration_is_a_model_fault(
        double suctionBar, double dischargeBar, double density, double efficiency, string expected)
    {
        var fault = Assert.Throws<ModelFault>(() => Unit(
            suctionBar: suctionBar, dischargeBar: dischargeBar, densityKgPerM3: density,
            tier: Tier(efficiency: efficiency)));

        Assert.Contains(expected, fault.Fault.Detail);
    }
}
