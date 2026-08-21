// R9's verification suite (R9 §4).
//
// R9-V8 is the phase's headline: with flaring capped and no gas outlet, OIL is
// throttled. If it does not hold, the environmental system is a fine rather than
// a constraint and the design intent is lost.

using OGSim.Contracts;
using OGSim.Facilities;
using OGSim.Kernel;

namespace OGSim.Facilities.Tests;

public class CompressionTests
{
    private static CompressorTier Tier(
        double capacity = 50.0,
        double maxStageRatio = 3.5,
        double n = 1.25,
        double efficiency = 0.75,
        double derate = 0.004,
        double dischargeBar = 70.0) =>
        new(new ContentId("comp-tier-b"), new MassRate(capacity), maxStageRatio, n, efficiency,
            MolarMassKgPerMol: 0.019, DerateFractionPerKelvin: derate,
            DerateReference: Temperature.FromCelsius(15.0),
            Discharge: Pressure.FromBar(dischargeBar));

    private static Compressor Unit(
        double suctionBar = 10.0, double dischargeBar = 70.0, CompressorTier? tier = null) =>
        new(new EntityId<IFlowElement>(1),
            (tier ?? Tier()) with { Discharge = Pressure.FromBar(dischargeBar) },
            Pressure.FromBar(suctionBar),
            averageCompressibility: 0.9, Fx.MaterialCount);

    // ------------------------------------------------------------ R9-V1 / MX6

    [Fact] // MX6: stage work matches the polytropic formula, recomputed independently
    public void MX6_stage_work_matches_the_polytropic_formula()
    {
        Compressor unit = Unit(suctionBar: 10.0, dischargeBar: 30.0);
        var suction = Temperature.FromCelsius(30.0);

        // 30/10 = 3.0 is inside r_max 3.5, so one stage at ratio 3.
        Assert.Equal(1, unit.Stages);
        Assert.Equal(3.0, unit.StageRatio, 9);

        const double n = 1.25, z = 0.9, mw = 0.019;
        double exponent = (n - 1.0) / n;
        double expected = z * PhysicalConstants.GasConstantJPerMolK * suction.Kelvin / mw
                        * (n / (n - 1.0))
                        * (Math.Pow(3.0, exponent) - 1.0);

        Assert.Equal(expected, unit.StageWorkJoulesPerKg(suction), precision: 6);
    }

    [Fact] // MX6: shaft power is stages × work × rate / efficiency
    public void MX6_shaft_power_divides_by_the_polytropic_efficiency()
    {
        Compressor unit = Unit(suctionBar: 10.0, dischargeBar: 30.0);
        var suction = Temperature.FromCelsius(30.0);

        double expected = 20.0 * 1 * unit.StageWorkJoulesPerKg(suction) / 0.75;

        Assert.Equal(expected, unit.ShaftPowerFor(new MassRate(20.0), suction).Watts, precision: 3);
    }

    [Fact] // R9-V1: staging matches the pressure-ratio limit
    public void R9V1_the_stage_count_follows_the_ratio_limit()
    {
        // Inside one stage.
        Assert.Equal(1, Unit(10.0, 30.0).Stages);
        // 7.0 needs two (3.5² = 12.25 covers it).
        Assert.Equal(2, Unit(10.0, 70.0).Stages);
        // 100 needs four? 3.5³ = 42.9, 3.5⁴ = 150 — so four.
        Assert.Equal(4, Unit(1.0, 100.0).Stages);
    }

    [Fact] // Equal ratio per stage — the balanced train, which minimises power
    public void R9V1_stages_take_the_equal_ratio()
    {
        Compressor unit = Unit(10.0, 70.0);

        Assert.Equal(2, unit.Stages);
        Assert.Equal(Math.Sqrt(7.0), unit.StageRatio, 9);

        // And the equal split beats an unbalanced one. Compressing 7.0 as a
        // single stage of 7 would cost more than two of √7 — which is why real
        // trains are balanced rather than front-loaded.
        var suction = Temperature.FromCelsius(30.0);
        Compressor single = Unit(10.0, 70.0, Tier(maxStageRatio: 10.0));

        Assert.Equal(1, single.Stages);
        Assert.True(unit.Stages * unit.StageWorkJoulesPerKg(suction)
                  < single.Stages * single.StageWorkJoulesPerKg(suction),
            "the balanced two-stage train should cost less than one big stage");
    }

    [Fact] // R9-V2: falling field pressure forces additional stages
    public void R9V2_a_declining_suction_pressure_forces_more_stages()
    {
        // Nothing schedules this. The suction falls with the field, the overall
        // ratio climbs, and at some point the arithmetic buys another stage.
        int previous = 0;
        bool grew = false;

        foreach (double suctionBar in new[] { 40.0, 20.0, 10.0, 5.0, 2.0, 1.0 })
        {
            int stages = Unit(suctionBar, dischargeBar: 70.0).Stages;
            Assert.True(stages >= previous, "stages must not fall as suction falls");
            if (stages > previous && previous > 0) grew = true;
            previous = stages;
        }

        Assert.True(grew, "the train never needed another stage, so nothing was proved");
    }

    [Fact] // A stage's discharge temperature is what r_max exists to bound
    public void R9V1_stage_discharge_temperature_follows_the_ratio()
    {
        Compressor unit = Unit(10.0, 30.0);
        var suction = Temperature.FromCelsius(30.0);

        double expected = suction.Kelvin * Math.Pow(3.0, 0.2);
        Assert.Equal(expected, unit.StageDischargeTemperature(suction).Kelvin, precision: 9);
        Assert.True(unit.StageDischargeTemperature(suction).Kelvin > suction.Kelvin);
    }

    // ------------------------------------------------------------ R9 §2.6

    [Fact] // Heat derating: a desert field loses gas capacity in the hottest months
    public void R9V1_capacity_derates_with_ambient_temperature()
    {
        Compressor unit = Unit();

        double cool = unit.CapacityAt(Temperature.FromCelsius(10.0)).KgPerSecond;
        double mild = unit.CapacityAt(Temperature.FromCelsius(15.0)).KgPerSecond;
        double hot = unit.CapacityAt(Temperature.FromCelsius(45.0)).KgPerSecond;

        // At or below the reference, rated. Above it, less — 0.4% per K.
        Assert.Equal(50.0, cool, 9);
        Assert.Equal(50.0, mild, 9);
        Assert.Equal(50.0 * (1.0 - 0.004 * 30.0), hot, 9);

        Assert.True(hot < mild, "a hot day must cost capacity");
    }

    [Fact] // The compressor reports its derated capacity as its constraint
    public void R9V1_the_reported_capacity_is_the_derated_one()
    {
        Compressor unit = Unit();

        var hot = new SegmentContext(30, Temperature.FromCelsius(45.0), 0.0);
        ConstraintEvaluation constraint = Assert.Single(
            unit.EvaluateConstraints(new TransformInput([Fx.Stream(0.0, 40.0, 0.0)], hot, null)));

        Assert.Equal(ConstraintKind.TotalCapacity, constraint.Kind);
        Assert.Equal(50.0 * (1.0 - 0.004 * 30.0), constraint.Capacity, 9);
    }

    [Fact] // R9-V11: a compressor conserves — it raises pressure, it does not eat gas
    public void R9V11_compression_conserves_mass()
    {
        MaterialStream inlet = Fx.Stream(0.0, 40.0, 0.0);
        TransformResult result = Unit().Transform(Fx.In(inlet));

        Assert.Equal(40.0, result.Outlets[0].MassRates.Total.KgPerSecond, 12);
        Assert.Equal(0.0, result.FuelConsumed.Total.KgPerSecond, 12);

        // And it did raise the pressure.
        Assert.Equal(Pressure.FromBar(70.0).Pascals, result.Outlets[0].P.Pascals, 6);
    }

    /// <summary>
    /// R9.1's own join (finding 257): a bigger train is fitted the way every
    /// other socket in this engine is — suction stays where the unit SITS,
    /// only what is fitted into it changes.
    /// </summary>
    [Fact]
    public void R9V1_a_bigger_train_is_fitted_without_moving_its_suction()
    {
        Compressor unit = Unit(suctionBar: 10.0, dischargeBar: 30.0);

        Assert.Equal(1, unit.Stages);
        Assert.Equal(Tier(dischargeBar: 30.0), unit.Tier);

        unit.Fit(Tier(dischargeBar: 70.0));

        Assert.Equal(2, unit.Stages);
        Assert.Equal(Pressure.FromBar(70.0), unit.Tier.Discharge);

        TransformResult result = unit.Transform(Fx.In(Fx.Stream(0.0, 40.0, 0.0)));
        Assert.Equal(Pressure.FromBar(70.0).Pascals, result.Outlets[0].P.Pascals, 6);
    }

    [Theory] // Content errors are refused where the datasheet is still in hand
    [InlineData(1.0, 1.25, 0.75, "max stage ratio")]
    [InlineData(3.5, 1.0, 0.75, "polytropic exponent")]
    [InlineData(3.5, 1.25, 1.5, "polytropic efficiency")]
    public void R9V1_an_unusable_tier_is_a_model_fault(
        double ratio, double n, double efficiency, string expected)
    {
        var fault = Assert.Throws<ModelFault>(() => Unit(
            tier: Tier(maxStageRatio: ratio, n: n, efficiency: efficiency)));

        Assert.Contains(expected, fault.Fault.Detail);
    }
}

public class GasTreatingTests
{
    // ------------------------------------------------------------ R9-V3 / V4

    [Fact] // R9-V3: dehydration removes water to spec, and the water is accounted for
    public void R9V3_dehydration_removes_water_and_accounts_for_it()
    {
        var dehydrator = new RemovalUnit(
            new EntityId<IFlowElement>(2),
            new RemovalUnitTier(new ContentId("teg-contactor"), RemovalEfficiency: 0.98, ByProductYield: 0.0),
            targetOrdinal: 2, byProductOrdinal: 2, Fx.MaterialCount);

        TransformResult result = dehydrator.Transform(Fx.In(Fx.Stream(0.0, 90.0, 10.0)));

        Assert.Equal(0.2, result.Outlets[0].MassRates[new MaterialId(2)].KgPerSecond, 9);
        Assert.Equal(9.8, result.Outlets[1].MassRates[new MaterialId(2)].KgPerSecond, 9);

        // R9-V11: nothing vanished.
        Assert.Equal(10.0,
            result.Outlets[0].MassRates[new MaterialId(2)].KgPerSecond
            + result.Outlets[1].MassRates[new MaterialId(2)].KgPerSecond, precision: 12);
    }

    [Fact] // R9-V4: sweetening produces sulphur in proportion to the acid gas removed
    public void R9V4_sweetening_produces_sulphur_from_what_it_removed()
    {
        // Ordinal 1 stands for the acid-gas-bearing stream, ordinal 0 for the
        // sulphur product. A third of the removed mass becomes sulphur.
        var amine = new RemovalUnit(
            new EntityId<IFlowElement>(3),
            new RemovalUnitTier(new ContentId("amine-unit"), RemovalEfficiency: 0.90, ByProductYield: 1.0 / 3.0),
            targetOrdinal: 1, byProductOrdinal: 0, Fx.MaterialCount);

        TransformResult result = amine.Transform(Fx.In(Fx.Stream(0.0, 30.0, 0.0)));

        double removed = 30.0 * 0.90;
        double sulphur = removed / 3.0;

        Assert.Equal(3.0, result.Outlets[0].MassRates[new MaterialId(1)].KgPerSecond, 9);
        Assert.Equal(sulphur, result.Outlets[1].MassRates[new MaterialId(0)].KgPerSecond, 9);

        // R9-V11: the sulphur is a CONVERSION within the reject, not new mass.
        double total = 0.0;
        foreach (MaterialStream outlet in result.Outlets)
            total += outlet.MassRates.Total.KgPerSecond;

        Assert.Equal(30.0, total, precision: 12);
    }

    [Fact] // A yield above 1 would make sulphur from nothing — refused
    public void R9V4_a_by_product_yield_above_one_is_a_model_fault()
    {
        var bad = new RemovalUnit(
            new EntityId<IFlowElement>(3),
            new RemovalUnitTier(new ContentId("amine-broken"), RemovalEfficiency: 0.9, ByProductYield: 1.5),
            targetOrdinal: 1, byProductOrdinal: 0, Fx.MaterialCount);

        var fault = Assert.Throws<ModelFault>(() => bad.Transform(Fx.In(Fx.Stream(0.0, 30.0, 0.0))));
        Assert.Contains("more sulphur than there was acid gas", fault.Fault.Detail);
    }

    // Not a declared verification id: RemovalUnit is not composed (see
    // SDD-006 §4's finding 260) — this exercises Fit() at the unit level only.
    [Fact]
    public void A_removal_unit_fits_a_bigger_tier_without_changing_what_it_removes()
    {
        var dehydrator = new RemovalUnit(
            new EntityId<IFlowElement>(2),
            new RemovalUnitTier(new ContentId("teg-none"), RemovalEfficiency: 0.0, ByProductYield: 0.0),
            targetOrdinal: 2, byProductOrdinal: 2, Fx.MaterialCount);

        dehydrator.Fit(new RemovalUnitTier(
            new ContentId("teg-contactor"), RemovalEfficiency: 0.98, ByProductYield: 0.0));

        Assert.Equal(new ContentId("teg-contactor"), dehydrator.Tier.Id);

        TransformResult result = dehydrator.Transform(Fx.In(Fx.Stream(0.0, 90.0, 10.0)));

        Assert.Equal(9.8, result.Outlets[1].MassRates[new MaterialId(2)].KgPerSecond, 9);
    }

    // ------------------------------------------------------------ R9-V5

    [Fact] // R9-V5: component recovery matches the declared efficiencies
    public void R9V5_ngl_recovery_matches_the_declared_component_efficiencies()
    {
        // A typical rich gas and a typical turbo-expander plant.
        ComponentSplit feed = ComponentSplit.Validated(0.75, 0.12, 0.07, 0.04, 0.02);
        NglRecovery recovery = NglRecovery.Validated(0.02, 0.60, 0.95, 0.98, 0.99);

        var plant = new NglExtractionPlant(
            new EntityId<IFlowElement>(4), feed, recovery,
            gasOrdinal: 1, liquidOrdinal: 0, Fx.MaterialCount);

        // Recomputed here from the two declarations, not read back.
        double expected = 0.75 * 0.02 + 0.12 * 0.60 + 0.07 * 0.95 + 0.04 * 0.98 + 0.02 * 0.99;
        Assert.Equal(expected, plant.RecoveredFraction, 12);

        TransformResult result = plant.Transform(Fx.In(Fx.Stream(0.0, 100.0, 0.0)));

        Assert.Equal(100.0 * expected, result.Outlets[1].MassRates[new MaterialId(0)].KgPerSecond, 9);
        Assert.Equal(100.0 * (1.0 - expected),
                     result.Outlets[0].MassRates[new MaterialId(1)].KgPerSecond, 9);
    }

    [Fact] // R9-V5 / R9-V11: TOTAL mass is conserved across the split, exactly
    public void R9V5_the_component_split_conserves_total_mass_exactly()
    {
        var plant = new NglExtractionPlant(
            new EntityId<IFlowElement>(4),
            ComponentSplit.Validated(0.75, 0.12, 0.07, 0.04, 0.02),
            NglRecovery.Validated(0.02, 0.60, 0.95, 0.98, 0.99),
            gasOrdinal: 1, liquidOrdinal: 0, Fx.MaterialCount);

        MaterialStream inlet = Fx.Stream(5.0, 100.0, 2.0);
        TransformResult result = plant.Transform(Fx.In(inlet));

        // TOTAL, not per material — and the distinction is the point (SDD-002 §5,
        // finding 118). The NGL plant is the engine's first CONVERTING element:
        // it takes propane dissolved in gas and produces liquid propane, so gas
        // mass falls and liquid mass rises by exactly the same amount. A
        // per-material assertion here is false by construction, and a first draft
        // of this test made it and found the SDD's wording wrong.
        double outbound = result.Outlets[0].MassRates.Total.KgPerSecond
                        + result.Outlets[1].MassRates.Total.KgPerSecond;

        Assert.Equal(inlet.MassRates.Total.KgPerSecond, outbound, precision: 12);

        // Materials the plant does not touch DO close per material.
        var water = new MaterialId(2);
        Assert.Equal(inlet.MassRates[water].KgPerSecond,
                     result.Outlets[0].MassRates[water].KgPerSecond
                     + result.Outlets[1].MassRates[water].KgPerSecond, precision: 12);

        // And the conversion is exactly what left the gas.
        double gasLost = inlet.MassRates[new MaterialId(1)].KgPerSecond
                       - result.Outlets[0].MassRates[new MaterialId(1)].KgPerSecond;
        double liquidGained = result.Outlets[1].MassRates[new MaterialId(0)].KgPerSecond;

        Assert.Equal(gasLost, liquidGained, precision: 12);
    }

    [Fact] // FD2's boundary: a split that does not sum to 1 is refused at the door
    public void R9V5_a_component_split_must_sum_to_one()
    {
        var fault = Assert.Throws<InvariantFault>(
            () => ComponentSplit.Validated(0.5, 0.2, 0.1, 0.1, 0.05));

        Assert.Contains("not 1", fault.Fault.Detail);
    }

    [Fact] // And a recovery outside [0,1] is content, not physics
    public void R9V5_a_recovery_outside_zero_to_one_is_refused()
    {
        var fault = Assert.Throws<InvariantFault>(
            () => NglRecovery.Validated(0.0, 0.6, 1.4, 0.9, 0.9));

        Assert.Contains("not a fraction", fault.Fault.Detail);
    }
}

public class FlareTests
{
    private static Flare Unit(double capacity = 1000.0, double efficiency = 0.98) =>
        new(new EntityId<IFlowElement>(6), new MassRate(capacity), efficiency, Fx.MaterialCount);

    [Fact] // R9-V7: flared mass equals rejected mass EXACTLY
    public void R9V7_flared_mass_equals_what_arrived_exactly()
    {
        TransformResult result = Unit().Transform(Fx.In(Fx.Stream(0.0, 37.5, 0.0)));

        double accounted = result.Disposed.Flared.Total.KgPerSecond
                         + result.Disposed.Vented.Total.KgPerSecond;

        Assert.Equal(37.5, accounted, precision: 12);
        Assert.Empty(result.Outlets);
    }

    [Fact] // The unburnt fraction is VENTED, not flared — R16 prices them differently
    public void R9V7_the_unburnt_fraction_is_reported_as_vented()
    {
        TransformResult result = Unit(efficiency: 0.98).Transform(Fx.In(Fx.Stream(0.0, 100.0, 0.0)));

        Assert.Equal(98.0, result.Disposed.Flared.Total.KgPerSecond, 9);
        Assert.Equal(2.0, result.Disposed.Vented.Total.KgPerSecond, 9);

        // Reporting it all as Flared would understate a poor flare's emissions
        // by an order of magnitude in warming terms — unburnt methane is not CO2.
        Assert.True(result.Disposed.Vented.Total.KgPerSecond > 0.0);
    }

    [Fact] // The flaring cap is an ordinary capacity constraint
    public void R9V8_the_flaring_cap_is_reported_as_a_capacity()
    {
        ConstraintEvaluation constraint = Assert.Single(
            Unit(capacity: 10.0).EvaluateConstraints(Fx.In(Fx.Stream(0.0, 40.0, 0.0))));

        Assert.Equal(ConstraintKind.TotalCapacity, constraint.Kind);
        Assert.Equal(10.0, constraint.Capacity, 9);
        Assert.Equal(40.0, constraint.Load, 9);

        // Over capacity — which is what S3 throttles against, and R9-V8's whole
        // mechanism. No special handling: the solver already knew what to do
        // with an element that reports a capacity.
        Assert.True(constraint.Load > constraint.Capacity);
    }
}
