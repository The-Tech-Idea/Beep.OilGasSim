// R18's verification suite (SDD-012).

using OGSim.Contracts;
using OGSim.Integrity;
using OGSim.Kernel;

namespace OGSim.Integrity.Tests;

public static class Fx
{
    public static readonly Duration OneYear = Duration.FromTicks(12.0);
    public static readonly Duration OneTick = Duration.FromTicks(1.0);

    public static DegradationCoefficients Coefficients(
        double baseRate = 0.05,
        double waterCut = 1.0,
        double sour = 2.0,
        double duty = 0.5,
        double temperature = 1.5,
        double serviceInterval = 0.2) =>
        new(baseRate, waterCut, sour, duty, temperature, serviceInterval);

    public static SeverityWeightedDegradation Decay(DegradationCoefficients? c = null) =>
        new(new ContentId("pump-tier-b"), c ?? Coefficients());

    public static ExponentialHazardModel Hazard(double baseRate = 0.05, double k = 4.0) =>
        new(new ContentId("pump-tier-b"), baseRate, k);

    public static ServiceSeverity Mild { get; } = new(0.05, 0.0, 0.3, 0.0, 0.0);
    public static ServiceSeverity Harsh { get; } = new(0.90, 0.40, 1.0, 0.60, 24.0);

    public static (IntegrityPass Pass, AuditTrail Trail) NewPass(ulong seed = 1965UL)
    {
        var clock = new SimulationClock(new GameDate(1970, 1));
        var trail = new AuditTrail(clock, new AuditRetention(5000));

        return (new IntegrityPass(
            Decay(), Hazard(),
            new RandomSource(seed).Stream(StreamId.Hazard), trail), trail);
    }

    public static ComponentState Component(ulong id, double condition = 1.0) =>
        new(new EntityId<IWellComponent>(id), new ContentId("pump-tier-b"), condition, false);
}

public class DegradationTests
{
    // ------------------------------------------------------------ R18-V1

    [Fact] // R18-V1: harsh service degrades faster, by the declared factors
    public void R18V1_harsh_service_degrades_faster_than_mild()
    {
        SeverityWeightedDegradation decay = Fx.Decay();

        double mild = decay.NextCondition(1.0, Fx.Mild, Fx.OneYear);
        double harsh = decay.NextCondition(1.0, Fx.Harsh, Fx.OneYear);

        Assert.True(harsh < mild, $"harsh service left {harsh}, not below {mild}");

        // BY THE DECLARED FACTORS, recomputed here from the coefficients:
        // Δc = base · (1 + Σ k·s) · years
        double severitySum = 1.0 * 0.90 + 2.0 * 0.40 + 0.5 * 1.0 + 1.5 * 0.60 + 0.2 * 2.0;
        Assert.Equal(1.0 - 0.05 * (1.0 + severitySum) * 1.0, harsh, 12);
    }

    [Fact] // Mild service still ages — severity ADDS to one rather than scaling it
    public void R18V1_mild_service_still_degrades()
    {
        // Equipment does not stop wearing out because conditions are pleasant,
        // and a multiplicative form with a zero severity would have said it did.
        double none = Fx.Decay().NextCondition(
            1.0, new ServiceSeverity(0.0, 0.0, 0.0, 0.0, 0.0), Fx.OneYear);

        Assert.Equal(0.95, none, 12);
    }

    [Fact] // Condition never goes below zero, and never rises on its own
    public void R18V1_condition_is_clamped_and_never_restored_implicitly()
    {
        SeverityWeightedDegradation decay = Fx.Decay();

        double worn = 0.02;
        for (int i = 0; i < 20; i++) worn = decay.NextCondition(worn, Fx.Harsh, Fx.OneYear);

        Assert.Equal(0.0, worn, 12);

        // And it stays there. Only a completed maintenance operation raises it —
        // a component that healed by being left alone would make maintenance
        // optional in the one way the player would notice.
        Assert.Equal(0.0, decay.NextCondition(worn, Fx.Mild, Fx.OneYear), 12);
    }

    [Fact] // Decay is proportional to elapsed time
    public void R18V1_decay_scales_with_elapsed_time()
    {
        SeverityWeightedDegradation decay = Fx.Decay();

        double afterTick = 1.0 - decay.NextCondition(1.0, Fx.Mild, Fx.OneTick);
        double afterYear = 1.0 - decay.NextCondition(1.0, Fx.Mild, Fx.OneYear);

        Assert.Equal(12.0, afterYear / afterTick, precision: 9);
    }

    [Fact] // Equipment does not improve by being used
    public void R18V1_a_negative_base_rate_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(
            () => Fx.Decay(Fx.Coefficients(baseRate: -0.01)));

        Assert.Contains("does not improve", fault.Fault.Detail);
    }
}

public class HazardTests
{
    // ------------------------------------------------------------ R18-V2

    [Fact] // R18-V2: the rate rises with falling condition, with NO threshold
    public void R18V2_the_hazard_curve_is_smooth_and_has_no_threshold()
    {
        ExponentialHazardModel hazard = Fx.Hazard(baseRate: 0.05, k: 4.0);

        double previous = 0.0;
        var rates = new List<double>();

        for (double c = 1.0; c >= 0.0; c -= 0.05)
        {
            double rate = hazard.RateAt(c);
            Assert.True(rate > previous, $"the rate did not rise at condition {c}");
            previous = rate;
            rates.Add(rate);
        }

        // Exponential in (1 − c), recomputed from the law.
        Assert.Equal(0.05, hazard.RateAt(1.0), 12);
        Assert.Equal(0.05 * Math.Exp(4.0), hazard.RateAt(0.0), 9);

        // NO THRESHOLD: every step is a similar RATIO, so a player sitting just
        // above a line is not rewarded for it. The cost of deferral grows
        // smoothly rather than arriving all at once, which teaches what the
        // number means instead of teaching them to game it.
        for (int i = 2; i < rates.Count; i++)
        {
            double step = rates[i] / rates[i - 1];
            Assert.InRange(step, 1.15, 1.30);
        }
    }

    [Fact] // The probability saturates toward 1 and never exceeds it
    public void R18V2_the_failure_probability_never_exceeds_one()
    {
        ExponentialHazardModel severe = Fx.Hazard(baseRate: 5.0, k: 6.0);

        double p = severe.FailureProbability(0.0, Duration.FromTicks(120.0));

        // A linear λ·Δt would have produced a probability far above 1 for a
        // badly-degraded component over a long interval.
        Assert.InRange(p, 0.99, 1.0);
    }

    [Fact] // 1 − exp(−λΔt), recomputed
    public void R18V2_the_probability_matches_the_pinned_law()
    {
        ExponentialHazardModel hazard = Fx.Hazard(baseRate: 0.2, k: 3.0);

        double lambda = 0.2 * Math.Exp(3.0 * (1.0 - 0.6));
        double expected = 1.0 - Math.Exp(-lambda * 1.0);

        Assert.Equal(expected, hazard.FailureProbability(0.6, Fx.OneYear), 9);
    }

    [Fact] // Zero elapsed time is zero probability
    public void R18V2_no_time_is_no_hazard()
    {
        Assert.Equal(0.0, Fx.Hazard().FailureProbability(0.1, new Duration(0.0)), 12);
    }

    [Fact] // Worse condition making failure LESS likely is content nonsense
    public void R18V2_a_negative_condition_exponent_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(
            () => new ExponentialHazardModel(new ContentId("bad"), 0.05, -1.0));

        Assert.Contains("LESS likely", fault.Fault.Detail);
    }
}

public class IntegrityPassTests
{
    // ------------------------------------------------------------ R18-V7

    [Fact] // R18-V7: the same seed gives identical failure sequences
    public void R18V7_the_same_seed_produces_identical_failures()
    {
        static List<ulong> Run(ulong seed)
        {
            (IntegrityPass pass, _) = Fx.NewPass(seed);

            var components = new List<ComponentState>();
            for (ulong i = 1; i <= 20; i++) components.Add(Fx.Component(i, condition: 0.4));

            var failed = new List<ulong>();
            for (int tick = 0; tick < 24; tick++)
            {
                IReadOnlyList<FailureOutcome> outcomes =
                    pass.Advance(components, Fx.Harsh, Fx.OneTick, out IReadOnlyList<ComponentState> aged);

                components = [.. aged];
                foreach (FailureOutcome outcome in outcomes) failed.Add(outcome.Component.Value);
            }

            return failed;
        }

        Assert.Equal(Run(99UL), Run(99UL));
        Assert.NotEqual(Run(99UL), Run(1234UL));
    }

    [Fact] // Components are processed in ASCENDING ID, whatever order they arrive
    public void R18V7_the_component_order_does_not_depend_on_the_caller()
    {
        static List<ulong> Run(IReadOnlyList<ComponentState> components)
        {
            (IntegrityPass pass, _) = Fx.NewPass(7UL);

            IReadOnlyList<FailureOutcome> outcomes =
                pass.Advance(components, Fx.Harsh, Fx.OneYear, out _);

            return [.. outcomes.Select(o => o.Component.Value)];
        }

        List<ComponentState> ascending =
            [Fx.Component(1, 0.2), Fx.Component(2, 0.2), Fx.Component(3, 0.2)];

        List<ComponentState> shuffled =
            [Fx.Component(3, 0.2), Fx.Component(1, 0.2), Fx.Component(2, 0.2)];

        // A dictionary walk here would make a whole campaign's failure history
        // depend on hash order (D-5).
        Assert.Equal(Run(ascending), Run(shuffled));
    }

    // ------------------------------------------------------------ R18-V8

    [Fact] // R18-V8: every failure records stream, draw and threshold
    public void R18V8_a_failure_is_audited_with_its_draw_and_threshold()
    {
        (IntegrityPass pass, AuditTrail trail) = Fx.NewPass();

        // Badly degraded over a long interval: failure is near-certain.
        IReadOnlyList<FailureOutcome> outcomes = pass.Advance(
            [Fx.Component(1, condition: 0.05)], Fx.Harsh, Duration.FromTicks(60.0), out _);

        FailureOutcome outcome = Assert.Single(outcomes);

        AuditEntry entry = Assert.Single(
            trail.Query(new AuditQuery(null, AuditCategory.StochasticOutcome, null, null)));

        Assert.Equal("hazard", entry.Data["stream"].Value);

        double draw = double.Parse(entry.Data["draw"].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        double threshold = double.Parse(entry.Data["threshold"].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        // The recorded draw ACTUALLY falls under the recorded threshold — the
        // fairness record is checkable, not merely present.
        Assert.Equal(outcome.Draw, draw, precision: 12);
        Assert.True(draw < threshold);
    }

    [Fact] // The failure day is an INTEGER in {0..29}
    public void R18V3_the_failure_day_lands_on_the_thirtieths_grid()
    {
        (IntegrityPass pass, _) = Fx.NewPass();

        var components = new List<ComponentState>();
        for (ulong i = 1; i <= 40; i++) components.Add(Fx.Component(i, condition: 0.02));

        IReadOnlyList<FailureOutcome> outcomes =
            pass.Advance(components, Fx.Harsh, Duration.FromTicks(60.0), out _);

        Assert.NotEmpty(outcomes);

        // A fractional day would put a segment boundary between grid points and
        // every downstream duration would inherit the error.
        foreach (FailureOutcome outcome in outcomes)
            Assert.InRange(outcome.FailureDay, 0, 29);
    }

    [Fact] // A failed component stops degrading
    public void R18V3_a_failed_component_is_not_aged_further()
    {
        (IntegrityPass pass, _) = Fx.NewPass();

        var failed = new ComponentState(
            new EntityId<IWellComponent>(1), new ContentId("pump-tier-b"), 0.3, Failed: true);

        pass.Advance([failed], Fx.Harsh, Fx.OneYear, out IReadOnlyList<ComponentState> aged);

        // It is already out of service; continuing to age it would make the
        // repair arbitrarily worse the longer it was left, which is a different
        // mechanic nobody specified.
        Assert.Equal(0.3, Assert.Single(aged).Condition, 12);
    }

    [Fact] // A healthy fleet consumes exactly one draw per component per tick
    public void R18V7_a_tick_with_no_failures_has_a_predictable_stream_position()
    {
        var trail = new AuditTrail(
            new SimulationClock(new GameDate(1970, 1)), new AuditRetention(100));

        IRandomStream stream = new RandomSource(3UL).Stream(StreamId.Hazard);
        var pass = new IntegrityPass(Fx.Decay(), Fx.Hazard(baseRate: 0.0), stream, trail);

        // Base rate zero: no failure is possible, so no day is drawn.
        IReadOnlyList<FailureOutcome> outcomes = pass.Advance(
            [Fx.Component(1), Fx.Component(2), Fx.Component(3)], Fx.Mild, Fx.OneTick, out _);

        Assert.Empty(outcomes);

        // Three components, three draws — the fourth value is the next one a
        // separate stream would produce.
        IRandomStream reference = new RandomSource(3UL).Stream(StreamId.Hazard);
        for (int i = 0; i < 3; i++) reference.NextUnit();

        Assert.Equal(reference.NextUnit(), stream.NextUnit(), precision: 15);
    }
}

public class MaintenanceStrategyTests
{
    [Fact] // Run-to-failure schedules nothing
    public void R18V5_run_to_failure_never_triggers()
    {
        var policy = new MaintenancePolicy(
            MaintenanceStrategy.RunToFailure, 0, 0.0, HasMonitoring: false);

        Assert.False(policy.IsDue(0.01, 999));
    }

    [Fact] // Scheduled triggers on the clock, whatever the condition
    public void R18V5_scheduled_maintenance_triggers_on_its_interval()
    {
        var policy = new MaintenancePolicy(
            MaintenanceStrategy.Scheduled, IntervalTicks: 12, 0.0, HasMonitoring: false);

        Assert.False(policy.IsDue(condition: 0.99, ticksSinceService: 11));
        Assert.True(policy.IsDue(condition: 0.99, ticksSinceService: 12));

        // Even on a healthy component — which is exactly why scheduled
        // maintenance costs more than it needs to on equipment in mild service.
        Assert.True(policy.IsDue(condition: 1.0, ticksSinceService: 24));
    }

    [Fact] // Condition-based triggers on condition — and NEEDS the monitoring tier
    public void R18V5_condition_based_maintenance_requires_monitoring()
    {
        var monitored = new MaintenancePolicy(
            MaintenanceStrategy.ConditionBased, 0, ConditionTrigger: 0.5, HasMonitoring: true);

        Assert.False(monitored.IsDue(0.6, 0));
        Assert.True(monitored.IsDue(0.4, 0));

        // WITHOUT the instrument it never triggers, and does NOT fall back to
        // scheduled. A fallback would make the monitoring purchase free, and a
        // player would get condition-based behaviour without paying for the
        // thing that makes it possible.
        var blind = monitored with { HasMonitoring = false };

        Assert.False(blind.IsDue(0.01, 999));
    }
}
