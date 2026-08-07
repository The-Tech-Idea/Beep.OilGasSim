// R7.6 — artificial lift (SDD-003 §6.2, R7 §4).
//
// R6-V6 established the well that dies. R7-V2 is the answer to it, and it is the
// one test that makes lift worth having: a well with no intersection at all
// acquires one, at a rate the two curves agree on.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Wells;

namespace OGSim.Wells.Tests;

public class LiftTests
{
    private static readonly GameDate Installed = new(1970, 6);

    /// <summary>A permissive envelope, so envelope effects do not contaminate the
    /// tests that are about the lift PHYSICS. R7-V5 uses a tight one on purpose.</summary>
    private static LiftEnvelope Wide { get; } = new(
        MinRate: new ReservoirRate(0.0),
        MaxRate: new ReservoirRate(1.0),
        MaxDepth: new Length(10_000.0),
        MaxDeviationDegrees: 90.0,
        MaxGasFraction: 1.0,
        MaxTemperature: Temperature.FromCelsius(250.0),
        MaxSolidsFraction: 1.0);

    private static ElectricSubmersiblePump Esp(LiftEnvelope? envelope = null) =>
        new(new EntityId<IWellComponent>(10), new ContentId("esp-tier-b"),
            envelope ?? Wide, Installed,
            // Head falls as rate rises — every real pump curve does.
            headCurve: [(0.0, 1400.0), (0.005, 1100.0), (0.010, 700.0), (0.020, 0.0)],
            efficiency: 0.55);

    private static GasLift Gas(double injectionM3PerS) =>
        new(new EntityId<IWellComponent>(11), new ContentId("gas-lift-tier-a"),
            Wide, Installed, injectionM3PerS, gasDensityKgPerM3: 80.0);

    private static RodPump Rod(double displacementM3PerS) =>
        new(new EntityId<IWellComponent>(12), new ContentId("rod-pump-tier-a"),
            Wide, Installed, displacementM3PerS);

    private static Completion Well(double reservoirBarA, ILiftMethod? lift, double permeabilityM2 = 1.0e-13)
    {
        var outflow = new HydrostaticFrictionOutflowModel(
            Fixtures.Tubing(), Density.FromSpecificGravity(0.85), lift);

        return new Completion(
            new EntityId<ICompletion>(1), new EntityId<IWellbore>(1),
            [Fixtures.Perf()],
            new CompositeInflowModel(
                Fixtures.Conditions(permeabilityM2: permeabilityM2, bubblePointPa: 5.0e6)),
            outflow,
            new CompletionFluid(
                Density.FromSpecificGravity(0.85), new FormationVolumeFactor(1.2),
                Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)),
                new Pressure(reservoirBarA * 1e5), Temperature.FromCelsius(80.0)),
            ChokeSetting.Open,
            materialOrdinal: 0, materialCount: 1, lift: lift);
    }

    private static double Rate(OperatingPoint point) =>
        point is Flowing flowing ? flowing.Rate.CubicMetresPerSecond : 0.0;

    // ------------------------------------------------------------ R7-V2

    [Fact] // R7-V2: the well R6-V6 killed, revived — the point of the phase
    public void R7V2_lift_revives_a_well_that_cannot_flow_naturally()
    {
        // 120 bar against a ~167 bar column: dead, exactly as R6-V6 showed.
        Assert.IsType<Dead>(Well(120.0, lift: null).SolveOperatingPoint(new Pressure(10.0e5)));

        // The same well with a pump in it finds an intersection.
        var flowing = Assert.IsType<Flowing>(
            Well(120.0, Esp()).SolveOperatingPoint(new Pressure(10.0e5)));

        Assert.True(flowing.Rate.CubicMetresPerSecond > 0.0);
    }

    [Fact] // R7-V2: and gas lift revives it too, by a completely different route
    public void R7V2_gas_lift_revives_by_lightening_rather_than_pushing()
    {
        Assert.IsType<Dead>(Well(140.0, lift: null).SolveOperatingPoint(new Pressure(10.0e5)));

        var flowing = Assert.IsType<Flowing>(
            Well(140.0, Gas(0.02)).SolveOperatingPoint(new Pressure(10.0e5)));

        Assert.True(flowing.Rate.CubicMetresPerSecond > 0.0);
    }

    // ------------------------------------------------------------ R7-V1

    [Fact] // R7-V1: every method lowers the outflow curve
    public void R7V1_each_method_lowers_the_required_bottomhole_pressure()
    {
        var rate = new ReservoirRate(0.004);
        var wellhead = new Pressure(10.0e5);

        double natural = Required(null);

        Assert.True(Required(Esp()) < natural, "the ESP did not lower the VLP");
        Assert.True(Required(Gas(0.01)) < natural, "gas lift did not lower the VLP");

        static double RequiredWith(ILiftMethod? lift, ReservoirRate r, Pressure wh) =>
            new HydrostaticFrictionOutflowModel(
                Fixtures.Tubing(), Density.FromSpecificGravity(0.85), lift)
                .RequiredBottomhole(r, wh).Pascals;

        double Required(ILiftMethod? lift) => RequiredWith(lift, rate, wellhead);
    }

    [Fact] // R7-V1: the ESP's shift is exactly its head, converted at ρ_mix
    public void R7V1_the_esp_shift_equals_its_head_at_the_mixture_density()
    {
        var rate = new ReservoirRate(0.005);
        Density density = Density.FromSpecificGravity(0.85);
        var wellhead = new Pressure(10.0e5);

        double natural = new HydrostaticFrictionOutflowModel(Fixtures.Tubing(), density, lift: null)
            .RequiredBottomhole(rate, wellhead).Pascals;

        ElectricSubmersiblePump esp = Esp();
        double lifted = new HydrostaticFrictionOutflowModel(Fixtures.Tubing(), density, esp)
            .RequiredBottomhole(rate, wellhead).Pascals;

        // Recomputed independently from the catalogue curve: head × ρ × g.
        double expected = esp.HeadAt(0.005) * density.KgPerCubicMetre
                        * PhysicalConstants.GravityMPerS2;

        Assert.Equal(expected, natural - lifted, precision: 6);
    }

    // ------------------------------------------------------------ R7-V3

    [Fact] // R7-V3: head versus rate matches the catalogue curve
    public void R7V3_the_pump_curve_interpolates_the_catalogue_points()
    {
        ElectricSubmersiblePump esp = Esp();

        // At the declared points, exactly.
        Assert.Equal(1400.0, esp.HeadAt(0.0), precision: 9);
        Assert.Equal(1100.0, esp.HeadAt(0.005), precision: 9);
        Assert.Equal(700.0, esp.HeadAt(0.010), precision: 9);

        // Between them, linearly.
        Assert.Equal(1250.0, esp.HeadAt(0.0025), precision: 9);
        Assert.Equal(900.0, esp.HeadAt(0.0075), precision: 9);

        // Head falls monotonically with rate, as every real pump does.
        double previous = double.MaxValue;
        for (double q = 0.0; q <= 0.020; q += 0.001)
        {
            double head = esp.HeadAt(q);
            Assert.True(head <= previous, $"head rose at {q} m³/s");
            previous = head;
        }
    }

    [Fact] // Off the end of the curve, head is FLAT — not extrapolated
    public void R7V3_beyond_the_catalogue_the_curve_does_not_extrapolate()
    {
        ElectricSubmersiblePump esp = Esp();

        // Extrapolating the last segment would go negative and invent a pump
        // that sucks. The datasheet simply does not claim anything out here.
        Assert.Equal(0.0, esp.HeadAt(0.030), precision: 9);
        Assert.Equal(1400.0, esp.HeadAt(-1.0), precision: 9);
    }

    [Fact] // R7 §2.4: the power draw is real, and it is what R8's balance consumes
    public void R7V7_the_esp_draws_power_proportional_to_the_work_it_does()
    {
        ElectricSubmersiblePump esp = Esp();
        Density density = Density.FromSpecificGravity(0.85);

        LiftEffect light = esp.EffectAt(new ReservoirRate(0.002), density);
        LiftEffect heavy = esp.EffectAt(new ReservoirRate(0.008), density);

        Assert.True(light.PowerDraw.Watts > 0.0);
        Assert.True(heavy.PowerDraw.Watts > light.PowerDraw.Watts,
            "more throughput must cost more power");

        // Hydraulic power over efficiency, recomputed from the curve.
        double expected = esp.HeadAt(0.008) * density.KgPerCubicMetre
                        * PhysicalConstants.GravityMPerS2 * 0.008 / 0.55;

        Assert.Equal(expected, heavy.PowerDraw.Watts, precision: 6);
    }

    // ------------------------------------------------------------ R7-V4

    [Fact] // R7-V4: gas-lift injection has an OPTIMUM — the phase's best test
    public void R7V4_gas_lift_injection_has_an_optimum_rate()
    {
        // Too little gas does not lighten the column enough to pay for itself;
        // too much adds volume that has to be pushed up the same tubing, and
        // friction goes as v². The turning point is not asserted anywhere — it
        // falls out of those two terms fighting.
        double best = -1.0;
        double bestInjection = 0.0;

        var rates = new List<(double Injection, double Rate)>();
        for (double injection = 0.0; injection <= 0.30; injection += 0.005)
        {
            double rate = Rate(Well(150.0, Gas(injection)).SolveOperatingPoint(new Pressure(10.0e5)));
            rates.Add((injection, rate));

            if (rate > best) { best = rate; bestInjection = injection; }
        }

        Assert.True(best > 0.0, "no injection rate made the well flow");

        // The optimum is INTERIOR: not at zero (gas lift helps) and not at the
        // end of the sweep (more gas eventually hurts). Either extreme would
        // mean one of the two competing terms was missing.
        Assert.True(bestInjection > 0.0, "gas lift never helped at all");
        Assert.True(bestInjection < 0.30, $"more gas always helped, up to {bestInjection}");

        // And past the optimum it genuinely declines, rather than plateauing.
        double atEnd = rates[^1].Rate;
        Assert.True(atEnd < best, $"the curve did not turn over: {atEnd} vs best {best}");
    }

    // ------------------------------------------------------------ R7-V6

    [Fact] // R7-V6: a rod pump caps rate by displacement, whatever the reservoir
    public void R7V6_a_rod_pump_caps_the_rate_at_its_displacement()
    {
        const double displacement = 1.0e-4;

        // A strong well — capable of far more than the pump can move.
        double capped = Rate(Well(350.0, Rod(displacement), permeabilityM2: 1.0e-12)
            .SolveOperatingPoint(new Pressure(10.0e5)));

        Assert.Equal(displacement, capped, precision: 12);
    }

    [Fact] // R7-V6: and it does NOT invent rate a weak well cannot supply
    public void R7V6_a_displacement_cap_is_a_ceiling_not_a_floor()
    {
        // A weak well under an absurdly generous pump produces what the
        // RESERVOIR gives, not what the pump could move. A cap that also raised
        // the rate would be a pump manufacturing oil.
        const double generousDisplacement = 1.0;

        double weak = Rate(Well(250.0, Rod(generousDisplacement), permeabilityM2: 1.0e-15)
            .SolveOperatingPoint(new Pressure(10.0e5)));

        Assert.True(weak > 0.0 && weak < generousDisplacement,
            $"expected reservoir-limited flow, got {weak}");
    }

    // ------------------------------------------------------------ R7-V5

    [Fact] // R7-V5: outside the envelope degrades and raises hazard — and REPORTS
    public void R7V5_envelope_violation_degrades_rather_than_refusing()
    {
        var tight = new LiftEnvelope(
            MinRate: new ReservoirRate(0.001),
            MaxRate: new ReservoirRate(0.010),
            MaxDepth: new Length(2500.0),
            MaxDeviationDegrees: 30.0,
            MaxGasFraction: 0.10,
            MaxTemperature: Temperature.FromCelsius(120.0),
            MaxSolidsFraction: 0.01);

        ElectricSubmersiblePump esp = Esp(tight);

        // An ESP in a gassy well: the eight-months-then-lose-the-pump story.
        EnvelopeAssessment gassy = esp.Assess(new LiftConditions(
            Rate: new ReservoirRate(0.005), Depth: new Length(2000.0),
            DeviationDegrees: 10.0, GasFraction: 0.40,
            Temperature: Temperature.FromCelsius(80.0), SolidsFraction: 0.0));

        Assert.False(gassy.Within);
        Assert.Contains(new ContentId("too-gassy"), gassy.Exceeded);
        Assert.True(gassy.PerformanceFactor < 1.0, "out of envelope must degrade");
        Assert.True(gassy.HazardMultiplier > 1.0, "out of envelope must raise the hazard");

        // INSTALLATION IS NOT REFUSED. Nothing above threw; the assessment is a
        // report the player could have read beforehand (R7 §2.2).
    }

    [Fact] // Inside the envelope, nothing is degraded and nothing is named
    public void R7V5_inside_the_envelope_is_unremarkable()
    {
        EnvelopeAssessment fine = Esp().Assess(new LiftConditions(
            Rate: new ReservoirRate(0.005), Depth: new Length(2000.0),
            DeviationDegrees: 10.0, GasFraction: 0.05,
            Temperature: Temperature.FromCelsius(80.0), SolidsFraction: 0.0));

        Assert.True(fine.Within);
        Assert.Equal(1.0, fine.PerformanceFactor, precision: 12);
        Assert.Equal(1.0, fine.HazardMultiplier, precision: 12);
        Assert.Empty(fine.Exceeded);
    }

    [Fact] // Every breach is named, so the diagnosis is possible
    public void R7V5_every_exceeded_limit_is_named()
    {
        var tight = new LiftEnvelope(
            new ReservoirRate(0.001), new ReservoirRate(0.010), new Length(2500.0),
            30.0, 0.10, Temperature.FromCelsius(120.0), 0.01);

        EnvelopeAssessment bad = Esp(tight).Assess(new LiftConditions(
            Rate: new ReservoirRate(0.050), Depth: new Length(4000.0),
            DeviationDegrees: 70.0, GasFraction: 0.40,
            Temperature: Temperature.FromCelsius(180.0), SolidsFraction: 0.20));

        Assert.Contains(new ContentId("rate-above-maximum"), bad.Exceeded);
        Assert.Contains(new ContentId("too-deep"), bad.Exceeded);
        Assert.Contains(new ContentId("too-deviated"), bad.Exceeded);
        Assert.Contains(new ContentId("too-gassy"), bad.Exceeded);
        Assert.Contains(new ContentId("too-hot"), bad.Exceeded);
        Assert.Contains(new ContentId("too-much-solids"), bad.Exceeded);
    }

    // ------------------------------------------------------- the methods

    [Fact] // Each method fills only the hooks it uses — no method-type branch anywhere
    public void R7V1_each_method_uses_only_the_hooks_it_needs()
    {
        Density density = Density.FromSpecificGravity(0.85);
        var rate = new ReservoirRate(0.005);

        LiftEffect esp = Esp().EffectAt(rate, density);
        Assert.True(esp.PressureBoost.Pascals > 0.0);
        Assert.Equal(1.0, esp.DensityFactor, precision: 12);
        Assert.Null(esp.DisplacementCap);

        LiftEffect gas = Gas(0.01).EffectAt(rate, density);
        Assert.Equal(0.0, gas.PressureBoost.Pascals, precision: 12);
        Assert.True(gas.DensityFactor < 1.0, "gas lift must lighten the column");
        Assert.Null(gas.DisplacementCap);

        LiftEffect rod = Rod(1.0e-4).EffectAt(rate, density);
        Assert.Equal(0.0, rod.PressureBoost.Pascals, precision: 12);
        Assert.Equal(1.0, rod.DensityFactor, precision: 12);
        Assert.NotNull(rod.DisplacementCap);

        // The PCP shares the rod pump's relation — §6.2 gives both the same one,
        // and the difference between them is entirely the envelope.
        var pcp = new ProgressingCavityPump(
            new EntityId<IWellComponent>(13), new ContentId("pcp-tier-a"),
            Wide, Installed, 1.0e-4);

        Assert.Equal(rod.DisplacementCap!.Value.CubicMetresPerSecond,
                     pcp.EffectAt(rate, density).DisplacementCap!.Value.CubicMetresPerSecond,
                     precision: 12);
    }

    [Fact] // Bad catalogue data is refused where the content is still in hand
    public void R7V3_an_unusable_pump_curve_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() => new ElectricSubmersiblePump(
            new EntityId<IWellComponent>(10), new ContentId("esp-broken"),
            Wide, Installed, headCurve: [(0.0, 100.0)], efficiency: 0.5));

        Assert.Contains("at least two", fault.Fault.Detail);

        var unordered = Assert.Throws<ModelFault>(() => new ElectricSubmersiblePump(
            new EntityId<IWellComponent>(10), new ContentId("esp-backwards"),
            Wide, Installed, headCurve: [(0.01, 100.0), (0.0, 200.0)], efficiency: 0.5));

        Assert.Contains("ascend in rate", unordered.Fault.Detail);
    }
}
