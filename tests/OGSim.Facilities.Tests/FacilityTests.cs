// R8's verification suite (R8 §4).

using OGSim.Contracts;
using OGSim.Facilities;
using OGSim.Kernel;

namespace OGSim.Facilities.Tests;

internal static class Fx
{
    public const int MaterialCount = 3;      // 0 oil, 1 gas, 2 water

    public static readonly SegmentContext WholeTick =
        new(DurationDays: 30, Temperature.FromCelsius(15.0), WeatherSeverity: 0.0);

    public static Composition Comp(double oil, double gas, double water) =>
        Composition.Validated([oil, gas, water]);

    public static Allocation One { get; } =
        Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1));

    public static MaterialStream Stream(double oil, double gas, double water) =>
        new(Comp(oil, gas, water), Pressure.FromBar(30.0), Temperature.FromCelsius(60.0), One);

    public static TransformInput In(MaterialStream stream) =>
        new([stream], WholeTick, SolvedRate: null);

    /// <summary>A fluid whose ideal split is by MATERIAL: oil to liquid, gas to
    /// gas, water to aqueous. Not thermodynamics — the point of these tests is
    /// what the VESSEL achieved, and a real flash would obscure it.</summary>
    public sealed class IdealSplitFluid : IFluidPropertyModel
    {
        public ContentId Id { get; } = new("ideal-split-test-fluid");
        public FluidForm Form => FluidForm.BlackOil;
        public Pressure Pb => Pressure.FromBar(50.0);
        public double Rs(Pressure p) => 100.0;
        public double Rv(Pressure p) => 0.0;
        public FormationVolumeFactor Bo(Pressure p) => new(1.2);
        public GasFormationVolumeFactor Bg(Pressure p) => new(0.005);
        public FormationVolumeFactor Bw(Pressure p) => new(1.0);
        public Viscosity MuOil(Pressure p) => new(2e-3);
        public Viscosity MuGas(Pressure p) => new(1.5e-5);
        public double Z(Pressure p, Temperature t) => 0.9;
        public ValidityRange Validity { get; } = new(
            new Pressure(1.0), new Pressure(1e9),
            Temperature.FromCelsius(-50.0), Temperature.FromCelsius(300.0));

        public PhaseSplit SplitAt(Composition composition, Pressure p, Temperature t) =>
            new(
            [
                (new MaterialId(0), 0.0, 1.0, 0.0),      // oil  -> liquid
                (new MaterialId(1), 1.0, 0.0, 0.0),      // gas  -> gas
                (new MaterialId(2), 0.0, 0.0, 1.0),      // water-> aqueous
            ]);
    }

    /// <summary>The same fluid, remembering what pressure it was asked about.
    /// The split is pressure-independent here on purpose: what is under test is
    /// which pressure the VESSEL passes down, not what a flash returns.</summary>
    public sealed class RecordingFluid(List<double> askedAtPascals) : IFluidPropertyModel
    {
        private readonly IdealSplitFluid _inner = new();

        public ContentId Id => _inner.Id;
        public FluidForm Form => _inner.Form;
        public Pressure Pb => _inner.Pb;
        public double Rs(Pressure p) => _inner.Rs(p);
        public double Rv(Pressure p) => _inner.Rv(p);
        public FormationVolumeFactor Bo(Pressure p) => _inner.Bo(p);
        public GasFormationVolumeFactor Bg(Pressure p) => _inner.Bg(p);
        public FormationVolumeFactor Bw(Pressure p) => _inner.Bw(p);
        public Viscosity MuOil(Pressure p) => _inner.MuOil(p);
        public Viscosity MuGas(Pressure p) => _inner.MuGas(p);
        public double Z(Pressure p, Temperature t) => _inner.Z(p, t);
        public ValidityRange Validity => _inner.Validity;

        public PhaseSplit SplitAt(Composition composition, Pressure p, Temperature t)
        {
            askedAtPascals.Add(p.Pascals);
            return _inner.SplitAt(composition, p, t);
        }
    }
}

public class SeparationTests
{
    private static SeparatorTier Tier(
        double gasCapacity = 100.0,
        double liquidCapacity = 100.0,
        double designRate = 1.0,
        double carryOver = 0.0,
        double carryUnder = 0.0,
        double waterKnockout = 0.0,
        double waterIntoOil = 0.0,
        double operatingBar = 15.0) =>
        new(new ContentId("sep-tier-a"),
            new MassRate(gasCapacity), new MassRate(liquidCapacity),
            new ReservoirVolume(10.0),
            new SeparationEfficiency(carryUnder, carryOver, waterKnockout, waterIntoOil),
            new ReservoirRate(designRate),
            Pressure.FromBar(operatingBar));

    private static Separator Vessel(SeparatorTier tier) =>
        new(new EntityId<IFlowElement>(1), tier,
            new FixedEfficiencySeparationModel(), new Fx.IdealSplitFluid(), Fx.MaterialCount);

    [Fact] // R8-V1: a known fluid splits into the expected legs
    public void R8V1_a_perfect_vessel_splits_by_phase()
    {
        TransformResult result = Vessel(Tier()).Transform(Fx.In(Fx.Stream(60.0, 30.0, 10.0)));

        // Gas leg, liquid leg, water leg — in port order.
        Assert.Equal(30.0, result.Outlets[0].MassRates[new MaterialId(1)].KgPerSecond, 9);
        Assert.Equal(60.0, result.Outlets[1].MassRates[new MaterialId(0)].KgPerSecond, 9);
        Assert.Equal(10.0, result.Outlets[2].MassRates[new MaterialId(2)].KgPerSecond, 9);
    }

    /// <summary>
    /// SDD-006 §1, finding 157. A separator IMPOSES its pressure on the network:
    /// every leg leaves at P_sep, whatever pressure arrived.
    ///
    /// <para>This is what the solver reads as the vessel's pressure drop, and
    /// therefore the only way a facility reaches the reservoir at all (FV5). The
    /// vessel used to stamp its inlet's pressure on every leg, which made it
    /// invisible to the network it exists to hold back — and the wells behind it
    /// flowed against the terminal sink boundary.</para>
    /// </summary>
    [Fact]
    public void FV5_every_leg_leaves_at_the_vessels_operating_pressure()
    {
        // The stream arrives at 30 bar — a completion's outlet carries RESERVOIR
        // pressure, which is far above any vessel's.
        TransformResult result = Vessel(Tier(operatingBar: 8.0))
            .Transform(Fx.In(Fx.Stream(60.0, 30.0, 10.0)));

        for (int i = 0; i < result.Outlets.Count; i++)
            Assert.Equal(8.0e5, result.Outlets[i].P.Pascals, precision: 6);
    }

    /// <summary>
    /// The other half of the same field: the flash is taken INSIDE the vessel.
    ///
    /// <para>Splitting at the inlet pressure asks what phases exist two thousand
    /// metres down, where the answer is "one" — so a separator fed straight from
    /// a completion separated nothing. A real fluid model returns different
    /// fractions at 8 bar than at 30, and the vessel must be asking it about
    /// 8.</para>
    /// </summary>
    [Fact]
    public void R8V1_the_flash_is_taken_at_the_vessel_pressure_not_the_inlets()
    {
        var seen = new List<double>();
        var fluid = new Fx.RecordingFluid(seen);

        new Separator(new EntityId<IFlowElement>(1), Tier(operatingBar: 8.0),
                      new FixedEfficiencySeparationModel(), fluid, Fx.MaterialCount)
            .Transform(Fx.In(Fx.Stream(60.0, 30.0, 10.0)));

        Assert.Equal(8.0e5, Assert.Single(seen), precision: 6);
    }

    /// <summary>A vessel held at nothing would flash against a vacuum and impose
    /// nothing — refused at construction rather than reached through content.</summary>
    [Fact]
    public void A_vessel_with_no_operating_pressure_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() => Vessel(Tier(operatingBar: 0.0)));

        Assert.Contains("operating pressure", fault.Fault.Detail);
    }

    [Fact] // R8-V10: every unit conserves; nothing is created or destroyed
    public void R8V10_separation_conserves_mass_exactly()
    {
        MaterialStream inlet = Fx.Stream(60.0, 30.0, 10.0);
        TransformResult result = Vessel(Tier(carryOver: 0.08, carryUnder: 0.05, waterKnockout: 0.9))
            .Transform(Fx.In(inlet));

        for (int m = 0; m < Fx.MaterialCount; m++)
        {
            var id = new MaterialId(m);
            double outbound = 0.0;
            for (int i = 0; i < result.Outlets.Count; i++)
                outbound += result.Outlets[i].MassRates[id].KgPerSecond;

            // EXACTLY: the legs are shares of the inlet with the last taking the
            // remainder, so there is no rounding residue for INV1 to catch.
            Assert.Equal(inlet.MassRates[id].KgPerSecond, outbound, precision: 12);
        }
    }

    [Fact] // R8 §2.6: separation is never 100% — carry-over reaches the gas leg
    public void R8V3_carry_over_puts_liquid_in_the_gas_leg()
    {
        TransformResult perfect = Vessel(Tier()).Transform(Fx.In(Fx.Stream(60.0, 30.0, 0.0)));
        TransformResult leaky = Vessel(Tier(carryOver: 0.10))
            .Transform(Fx.In(Fx.Stream(60.0, 30.0, 0.0)));

        double perfectOilInGas = perfect.Outlets[0].MassRates[new MaterialId(0)].KgPerSecond;
        double leakyOilInGas = leaky.Outlets[0].MassRates[new MaterialId(0)].KgPerSecond;

        Assert.Equal(0.0, perfectOilInGas, precision: 12);
        Assert.True(leakyOilInGas > 0.0, "carry-over must put liquid in the gas leg");
    }

    [Fact] // R8-V3: an UNDERSIZED vessel at high rate separates worse
    public void R8V3_an_overloaded_vessel_separates_worse()
    {
        SeparatorTier tier = Tier(designRate: 0.05, carryOver: 0.05);
        Separator vessel = Vessel(tier);

        // At design rate, the rated efficiency holds.
        SeparationEfficiency atDesign = vessel.EfficiencyAt(Fx.Stream(20.0, 10.0, 0.0));
        // At four times it, residence time has collapsed.
        SeparationEfficiency overloaded = vessel.EfficiencyAt(Fx.Stream(80.0, 40.0, 0.0));

        Assert.True(overloaded.GasFromLiquid > atDesign.GasFromLiquid,
            "an overloaded vessel must carry over more");
        Assert.True(overloaded.WaterFromLiquid <= atDesign.WaterFromLiquid,
            "an overloaded vessel must knock out less water");
    }

    [Fact] // R8-V2: the two legs bind INDEPENDENTLY and are attributed separately
    public void R8V2_gas_and_liquid_capacity_are_reported_separately()
    {
        // Gas-limited: plenty of liquid room, gas over its cap.
        IReadOnlyList<ConstraintEvaluation> gasLimited =
            Vessel(Tier(gasCapacity: 10.0, liquidCapacity: 1000.0))
                .EvaluateConstraints(Fx.In(Fx.Stream(50.0, 40.0, 0.0)));

        ConstraintEvaluation gas = Single(gasLimited, ConstraintKind.GasCapacity);
        ConstraintEvaluation liquid = Single(gasLimited, ConstraintKind.LiquidCapacity);

        Assert.True(gas.Load > gas.Capacity, "the gas leg should be over capacity");
        Assert.True(liquid.Load <= liquid.Capacity, "the liquid leg should not be");

        // Liquid-limited: the same vessel, ten years later.
        IReadOnlyList<ConstraintEvaluation> liquidLimited =
            Vessel(Tier(gasCapacity: 1000.0, liquidCapacity: 10.0))
                .EvaluateConstraints(Fx.In(Fx.Stream(50.0, 5.0, 30.0)));

        Assert.True(Single(liquidLimited, ConstraintKind.LiquidCapacity).Load > 10.0);
        Assert.True(Single(liquidLimited, ConstraintKind.GasCapacity).Load <= 1000.0);

        static ConstraintEvaluation Single(
            IReadOnlyList<ConstraintEvaluation> all, ConstraintKind kind)
        {
            foreach (ConstraintEvaluation c in all) if (c.Kind == kind) return c;
            throw new InvalidOperationException($"no {kind} constraint reported");
        }
    }

    // R8-V4 (staged separation recovers more stock-tank liquid) IS NOT HERE, and
    // the reason is worth recording rather than working around.
    //
    // The gain is not a vessel property at all: staging wins because the gas
    // removed at high pressure is LEANER — mostly methane — so the liquid keeps
    // its intermediates, where one large drop to low pressure vaporises them.
    // That is a statement about COMPONENT composition, and SDD-006 §4 is
    // explicit that components exist in exactly one place: the NGL plant0027s
    // per-component recovery fractions (FD2). With a scalar oil/gas/water
    // composition, every arrangement of stages retains the same mass by
    // construction, and any test claiming otherwise would be asserting an
    // artefact of its own carry-over numbers.
    //
    // A first draft did exactly that and got the sign wrong, which is how the
    // gap was found. R8-V4 is gated at R9, where components arrive.

    [Fact] // Bad efficiency data is refused where the content is still in hand
    public void R8V1_an_efficiency_outside_zero_to_one_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() =>
            new FixedEfficiencySeparationModel().SeparateAt(
                Fx.Stream(1.0, 1.0, 0.0),
                new SeparationEfficiency(1.5, 0.0, 0.0, 0.0),
                new Fx.IdealSplitFluid()));

        Assert.Contains("not a fraction", fault.Fault.Detail);
    }
}

public class TankTests
{
    private static Tank Full(double capacity, double held) =>
        new(new EntityId<IFlowElement>(5),
            new TankTier(new ContentId("tank-a"), new Mass(capacity), VapourLossRatePerTick: 0.0),
            Fx.MaterialCount, MaterialInventory.Of(held, 0.0, 0.0), Fx.One);

    [Fact] // R8-V5: a full tank accepts nothing, and says so as a CAPACITY
    public void R8V5_a_full_tank_reports_zero_ullage_capacity()
    {
        Tank tank = Full(capacity: 1000.0, held: 1000.0);

        Assert.Equal(0.0, tank.Ullage.Kilograms, precision: 9);

        ConstraintEvaluation ullage = Assert.Single(
            tank.EvaluateConstraints(Fx.In(Fx.Stream(5.0, 0.0, 0.0))));

        Assert.Equal(ConstraintKind.Ullage, ullage.Kind);
        Assert.Equal(0.0, ullage.Capacity, precision: 12);
        Assert.True(ullage.Load > 0.0, "arrival with no ullage must be over capacity");
    }

    [Fact] // R8-V5: a PARTLY full tank accepts a rate, not all-or-nothing
    public void R8V5_ullage_is_a_rate_so_throttling_is_proportional()
    {
        Tank tank = Full(capacity: 1000.0, held: 400.0);

        ConstraintEvaluation ullage = Assert.Single(
            tank.EvaluateConstraints(Fx.In(Fx.Stream(1.0, 0.0, 0.0))));

        // 600 kg of room over a 30-day segment.
        Assert.Equal(600.0 / (30.0 * 86_400.0), ullage.Capacity, precision: 12);

        // Expressed as a rate so S3 can throttle PRO-RATA. A boolean "full"
        // would make the solver choose between all and nothing, and a tank with
        // room for half the tick's production would take none of it.
        Assert.True(ullage.Capacity > 0.0);
    }

    [Fact] // Transform is PURE — the solver iterates it many times per segment
    public void R8V5_transform_does_not_change_inventory()
    {
        Tank tank = Full(capacity: 1000.0, held: 100.0);

        for (int i = 0; i < 50; i++) tank.Transform(Fx.In(Fx.Stream(5.0, 0.0, 0.0)));

        // A tank that accumulated inside Transform would have filled up many
        // times over during one solve.
        Assert.Equal(100.0, tank.Held.Total.Kilograms, precision: 9);
    }

    [Fact] // Stage 6 is the only thing that fills it
    public void R8V5_receive_is_what_changes_inventory()
    {
        Tank tank = Full(capacity: 100_000_000.0, held: 0.0);

        tank.Receive(Fx.Comp(1.0, 0.0, 0.0), Fx.One, Duration.FromTicks(1.0));

        Assert.Equal(30.0 * 86_400.0, tank.Held.Total.Kilograms, precision: 6);
    }

    [Fact] // FV10: provenance blends by mass, so a lifting allocates back correctly
    public void R8V5_receipts_blend_provenance_by_mass()
    {
        Tank tank = Full(capacity: 100_000_000.0, held: 0.0);

        Allocation fieldA = Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 10));
        Allocation fieldB = Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 20));

        tank.Receive(Fx.Comp(3.0, 0.0, 0.0), fieldA, Duration.FromTicks(1.0));
        tank.Receive(Fx.Comp(1.0, 0.0, 0.0), fieldB, Duration.FromTicks(1.0));

        Assert.Equal(2, tank.Provenance.Shares.Length);
        Assert.Equal(0.75, tank.Provenance.Shares[0].Fraction, 9);
        Assert.Equal(0.25, tank.Provenance.Shares[1].Fraction, 9);
    }

    [Fact] // A commit beyond ullage is an invariant failure, not a clamp
    public void R8V5_committing_more_than_the_ullage_is_an_invariant_fault()
    {
        Tank tank = Full(capacity: 100.0, held: 99.0);

        // The ullage constraint should have throttled this at S3. Reaching the
        // commit means the solve and the commit disagree.
        var fault = Assert.Throws<InvariantFault>(() =>
            tank.Receive(Fx.Comp(10.0, 0.0, 0.0), Fx.One, Duration.FromTicks(1.0)));

        Assert.Contains("ullage", fault.Fault.Detail);
    }

    [Fact] // A lifting takes a proportional composition — a tank does not fractionate
    public void R8V5_a_draw_is_proportional_to_what_is_held()
    {
        var tank = new Tank(new EntityId<IFlowElement>(5),
            new TankTier(new ContentId("tank-a"), new Mass(1000.0), 0.0),
            Fx.MaterialCount, MaterialInventory.Of(75.0, 0.0, 25.0), Fx.One);

        MaterialInventory drawn = tank.Draw(new Mass(50.0));

        Assert.Equal(37.5, drawn[new MaterialId(0)].Kilograms, 9);
        Assert.Equal(12.5, drawn[new MaterialId(2)].Kilograms, 9);
        Assert.Equal(50.0, tank.Held.Total.Kilograms, 9);
    }

    [Fact] // Boil-off is a CONSERVATION TERM — returned, never vanished
    public void R8V10_vapour_loss_is_returned_rather_than_discarded()
    {
        var tank = new Tank(new EntityId<IFlowElement>(5),
            new TankTier(new ContentId("tank-a"), new Mass(1000.0), VapourLossRatePerTick: 0.01),
            Fx.MaterialCount, MaterialInventory.Of(100.0, 0.0, 0.0), Fx.One);

        MaterialInventory lost = tank.VapourLossOver(Duration.FromTicks(1.0));

        Assert.Equal(1.0, lost.Total.Kilograms, precision: 9);
        Assert.Equal(99.0, tank.Held.Total.Kilograms, precision: 9);

        // The caller routes it to vapour recovery or to the emissions ledger;
        // what matters here is that it LEFT as a number somebody can account for.
        Assert.Equal(100.0, lost.Total.Kilograms + tank.Held.Total.Kilograms, precision: 9);
    }
}

public class SpecificationTests
{
    private static Specification Spec { get; } = new(
    [
        new SpecLimit(SpecProperty.BasicSedimentAndWater, 0.005),
        new SpecLimit(SpecProperty.H2SFraction, 1e-5),
    ]);

    private static StreamProperties Properties(double bsw = 0.0, double h2s = 0.0) =>
        new(bsw, h2s, Co2Fraction: 0.0, WaterInGasFraction: 0.0,
            LightEndsFraction: 0.0, Heating: new HeatingValue(45e6));

    [Fact] // R8-V6: an on-spec stream passes whole
    public void R8V6_an_on_spec_stream_passes()
    {
        var point = new CustodyTransferPoint(
            new EntityId<IFlowElement>(9), Spec, Fx.MaterialCount, _ => Properties());

        TransformResult result = point.Transform(Fx.In(Fx.Stream(50.0, 0.0, 0.0)));

        Assert.Empty(point.LastBreaches);
        Assert.Equal(50.0, result.Outlets[0].MassRates.Total.KgPerSecond, 9);
        Assert.Equal(0.0, result.Outlets[1].MassRates.Total.KgPerSecond, 12);
    }

    [Fact] // R8-V6: off-spec DOES NOT PASS, and the reject equals the feed exactly
    public void R8V6_an_off_spec_stream_is_rejected_whole_and_exactly()
    {
        var point = new CustodyTransferPoint(
            new EntityId<IFlowElement>(9), Spec, Fx.MaterialCount, _ => Properties(bsw: 0.03));

        TransformResult result = point.Transform(Fx.In(Fx.Stream(50.0, 0.0, 0.0)));

        Assert.Equal(0.0, result.Outlets[0].MassRates.Total.KgPerSecond, 12);
        Assert.Equal(50.0, result.Outlets[1].MassRates.Total.KgPerSecond, 9);

        // FV6: the rejected volume equals the feed EXACTLY, which is what lets
        // the flare volume equal it in turn.
        Assert.Equal(50.0,
            result.Outlets[0].MassRates.Total.KgPerSecond
            + result.Outlets[1].MassRates.Total.KgPerSecond, precision: 12);
    }

    [Fact] // R8 §2.4: the rejection carries a REASON — the mechanism, not a prompt
    public void R8V6_a_rejection_names_the_failing_parameter_and_its_margin()
    {
        var point = new CustodyTransferPoint(
            new EntityId<IFlowElement>(9), Spec, Fx.MaterialCount, _ => Properties(bsw: 0.03));

        point.Transform(Fx.In(Fx.Stream(50.0, 0.0, 0.0)));

        SpecBreach breach = Assert.Single(point.LastBreaches);
        Assert.Equal(SpecProperty.BasicSedimentAndWater, breach.Property);
        Assert.Equal(0.005, breach.Limit, 9);
        Assert.Equal(0.03, breach.Measured, 9);
        Assert.Equal(0.025, breach.Margin, 9);
    }

    [Fact] // Every breach at once — one report, not one purchase per round
    public void R8V6_all_failing_limits_are_reported_together()
    {
        var point = new CustodyTransferPoint(
            new EntityId<IFlowElement>(9), Spec, Fx.MaterialCount,
            _ => Properties(bsw: 0.03, h2s: 0.001));

        point.Transform(Fx.In(Fx.Stream(50.0, 0.0, 0.0)));

        Assert.Equal(2, point.LastBreaches.Count);
    }

    [Fact] // A minimum binds from BELOW — a lean gas fails a calorific contract
    public void R8V6_a_minimum_heating_value_binds_downward()
    {
        var lean = new Specification([new SpecLimit(SpecProperty.HeatingValueMin, 40e6)]);

        Assert.Single(SpecificationCheck.Evaluate(
            lean, Properties() with { Heating = new HeatingValue(30e6) }));

        Assert.Empty(SpecificationCheck.Evaluate(
            lean, Properties() with { Heating = new HeatingValue(45e6) }));
    }

    [Fact] // The Reject port is declared, so the network build can require it
    public void R8V6_a_spec_gate_declares_a_reject_port()
    {
        var point = new CustodyTransferPoint(
            new EntityId<IFlowElement>(9), Spec, Fx.MaterialCount, _ => Properties());

        Assert.Contains(point.Ports,
            p => p.Role == PortRole.Reject && p.Direction == PortDirection.Outlet);
    }
}

public class PowerBalanceTests
{
    private sealed record Source(Power MaxSupply, int MeritRank) : IPowerSource;

    private static PowerDemand Demand(ulong id, double watts, PowerPriority priority) =>
        new(new EntityId<IFlowElement>(id), new Power(watts), priority);

    [Fact] // Enough supply: nothing sheds
    public void R8V7_a_balanced_facility_takes_nothing_offline()
    {
        PowerBalanceResult result = PowerBalance.Balance(
            [new Source(new Power(1000.0), 0)],
            [Demand(1, 400.0, PowerPriority.Processing),
             Demand(2, 300.0, PowerPriority.Discretionary)]);

        Assert.False(result.Shortfall);
        Assert.Empty(result.Offline);
        Assert.Equal(700.0, result.DemandAfter.Watts, 9);
    }

    [Fact] // R8-V7: units go offline in the DECLARED priority order
    public void R8V7_a_shortfall_sheds_lowest_priority_first()
    {
        PowerBalanceResult result = PowerBalance.Balance(
            [new Source(new Power(500.0), 0)],
            [
                Demand(1, 300.0, PowerPriority.SafetyCritical),
                Demand(2, 300.0, PowerPriority.Processing),
                Demand(3, 300.0, PowerPriority.Discretionary),
            ]);

        Assert.True(result.Shortfall);

        // Discretionary first, then processing — safety-critical survives.
        Assert.Equal(2, result.Offline.Count);
        Assert.Contains(new EntityId<IFlowElement>(3), result.Offline);
        Assert.Contains(new EntityId<IFlowElement>(2), result.Offline);
        Assert.DoesNotContain(new EntityId<IFlowElement>(1), result.Offline);
    }

    [Fact] // Within a priority, the LARGEST goes first — shedding should end quickly
    public void R8V7_within_a_priority_the_largest_draw_sheds_first()
    {
        PowerBalanceResult result = PowerBalance.Balance(
            [new Source(new Power(100.0), 0)],
            [
                Demand(1, 50.0, PowerPriority.Processing),
                Demand(2, 200.0, PowerPriority.Processing),
                Demand(3, 40.0, PowerPriority.Processing),
            ]);

        // Shedding the 200 W unit clears it in one. Smallest-first would have
        // blacked out both small units and still not fitted.
        Assert.Single(result.Offline);
        Assert.Equal(2UL, result.Offline[0].Value);
    }

    [Fact] // R7 §2.4 / R7-V7: an ESP fleet takes OTHER equipment offline
    public void R7V7_an_esp_fleet_takes_processing_equipment_offline()
    {
        // The coupling R7 promised: solve a lift problem, create a processing one.
        var demands = new List<PowerDemand>
        {
            Demand(1, 400.0, PowerPriority.SafetyCritical),
            Demand(2, 600.0, PowerPriority.Processing),      // the separator train
        };

        // Twenty ESPs at 50 kW each, export-critical.
        for (ulong i = 0; i < 20; i++)
            demands.Add(Demand(100 + i, 50_000.0, PowerPriority.ExportCritical));

        PowerBalanceResult result = PowerBalance.Balance(
            [new Source(new Power(900_000.0), 0)], demands);

        Assert.True(result.Shortfall);
        Assert.Contains(new EntityId<IFlowElement>(2), result.Offline);
    }

    [Fact] // Merit order sums every source, lowest rank first
    public void R8V7_supply_is_the_sum_of_the_sources_in_merit_order()
    {
        PowerBalanceResult result = PowerBalance.Balance(
            [new Source(new Power(300.0), 2), new Source(new Power(700.0), 0)],
            [Demand(1, 100.0, PowerPriority.Processing)]);

        Assert.Equal(1000.0, result.Supply.Watts, 9);
    }

    [Fact] // A facility whose safety load alone exceeds supply is REPORTED, not refused
    public void R8V7_an_unfixable_shortfall_is_reported_rather_than_faulted()
    {
        PowerBalanceResult result = PowerBalance.Balance(
            [new Source(new Power(10.0), 0)],
            [Demand(1, 500.0, PowerPriority.SafetyCritical)]);

        // Refusing to tick would deny the player the chance to fix it.
        Assert.True(result.Shortfall);
        Assert.Single(result.Offline);
    }

    [Fact] // The offline list is stable, whatever order shedding reached them
    public void R8V7_the_offline_list_is_reported_in_id_order()
    {
        PowerBalanceResult result = PowerBalance.Balance(
            [new Source(new Power(10.0), 0)],
            [
                Demand(7, 100.0, PowerPriority.Discretionary),
                Demand(3, 200.0, PowerPriority.Discretionary),
                Demand(5, 150.0, PowerPriority.Discretionary),
            ]);

        Assert.Equal([3UL, 5UL, 7UL], result.Offline.Select(o => o.Value));
    }
}
