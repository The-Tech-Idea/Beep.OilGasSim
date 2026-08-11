// R23's verification suite (SDD-012 §4b, design 14).
//
// HS3/HS4's design intent, stated so it is testable: a player should be able to
// make their injury rate look excellent while process-safety barriers degrade,
// and the leading indicators should say so loudly to anyone who looks. The near
// miss is that indicator, and it comes free.

using OGSim.Contracts;
using OGSim.Integrity;
using OGSim.Kernel;

namespace OGSim.Integrity.Tests;

public class BarrierTests
{
    private static Barrier Preventive(params ulong[] elements) =>
        new(new ContentId("well-control"),
            [.. elements.Select(e => new EntityId<IFlowElement>(e))],
            IsPreventive: true);

    [Fact] // R23.1: strength is WEAKEST-LINK, not an average
    public void HS1_barrier_strength_is_the_weakest_element()
    {
        Barrier barrier = Preventive(1, 2, 3);

        var conditions = new Dictionary<ulong, double> { [1] = 0.9, [2] = 0.3, [3] = 0.8 };

        double strength = barrier.StrengthGiven(
            e => conditions[e.Value], crewCompetency: 1.0, procedureCompliance: 1.0);

        // A barrier is only as good as its worst element. Averaging would let a
        // well-maintained valve hide an untrained crew — which is exactly the
        // failure the doctrine exists to prevent.
        Assert.Equal(0.3, strength, 12);
    }

    [Fact] // Competency and compliance are part of the min, not multipliers
    public void HS1_crew_and_procedure_enter_the_same_minimum()
    {
        Barrier barrier = Preventive(1);

        Assert.Equal(0.4, barrier.StrengthGiven(_ => 0.95, 0.4, 0.9), 12);
        Assert.Equal(0.2, barrier.StrengthGiven(_ => 0.95, 0.9, 0.2), 12);

        // Perfect equipment operated by an untrained crew under lapsed
        // procedures is not a strong barrier, and multiplying would have made
        // three mediocre factors look worse than one terrible one.
        Assert.Equal(0.95, barrier.StrengthGiven(_ => 0.95, 1.0, 1.0), 12);
    }

    [Fact] // R23 §2.2: barrier condition IS equipment condition — no safety stat
    public void HS1_degrading_equipment_degrades_the_barrier()
    {
        Barrier barrier = Preventive(1, 2);

        double fresh = barrier.StrengthGiven(_ => 1.0, 1.0, 1.0);
        double worn = barrier.StrengthGiven(_ => 0.35, 1.0, 1.0);

        // A separate safety number would be a second representation of one fact
        // (law L5) and would drift from the plant.
        Assert.True(worn < fresh);
        Assert.Equal(0.35, worn, 12);
    }

    [Fact] // A strength outside [0,1] is content nonsense
    public void HS1_an_out_of_range_input_is_a_model_fault()
    {
        Barrier barrier = Preventive(1);

        Assert.Throws<ModelFault>(() => barrier.StrengthGiven(_ => 1.5, 1.0, 1.0));
        Assert.Throws<ModelFault>(() => barrier.StrengthGiven(_ => 1.0, -0.1, 1.0));
    }
}

public class BowTieTests
{
    private static readonly ContentId Threat = new("overpressure");

    private static (BowTie Tie, AuditTrail Trail) New(ulong seed = 42UL)
    {
        var clock = new SimulationClock(new GameDate(1980, 1));
        var trail = new AuditTrail(clock, new AuditRetention(5000));

        return (new BowTie(new RandomSource(seed).Stream(StreamId.Hazard), trail), trail);
    }

    private static IReadOnlyList<Barrier> Barriers(int preventive = 3, int mitigating = 2)
    {
        var barriers = new List<Barrier>();

        for (int i = 0; i < preventive; i++)
            barriers.Add(new Barrier(new ContentId($"prevent-{i}"), [], IsPreventive: true));

        for (int i = 0; i < mitigating; i++)
            barriers.Add(new Barrier(new ContentId($"mitigate-{i}"), [], IsPreventive: false));

        return barriers;
    }

    // ------------------------------------------------------------ HS2

    [Fact] // Perfect barriers suppress everything
    public void HS2_a_threat_against_perfect_barriers_is_suppressed()
    {
        (BowTie tie, _) = New();

        ThreatResolution result = tie.Resolve(Threat, Barriers(), _ => 1.0);

        Assert.Equal(ThreatOutcome.Suppressed, result.Outcome);
        Assert.Empty(result.FailedBarriers);
    }

    [Fact] // Utterly failed barriers give the top event
    public void HS2_a_threat_against_failed_barriers_reaches_the_top_event()
    {
        (BowTie tie, _) = New();

        ThreatResolution result = tie.Resolve(Threat, Barriers(), _ => 0.0);

        Assert.Equal(ThreatOutcome.TopEvent, result.Outcome);
        Assert.Equal(3, result.FailedBarriers.Count);
    }

    // ------------------------------------------------------------ HS3/HS4

    [Fact] // R23.4: a NEAR MISS names exactly the barriers that failed
    public void HS4_a_near_miss_names_the_failed_barriers()
    {
        (BowTie tie, _) = New();

        // One barrier is weak, two are sound: some fail, some hold.
        double Strength(Barrier b) => b.Id.Value == "prevent-1" ? 0.0 : 1.0;

        ThreatResolution result = tie.Resolve(Threat, Barriers(), Strength);

        Assert.Equal(ThreatOutcome.NearMiss, result.Outcome);

        // BY NAME — which is what makes it point at something the player can
        // act on rather than merely worry about.
        Assert.Equal("prevent-1", Assert.Single(result.FailedBarriers).Value);
    }

    [Fact] // Every preventive barrier is sampled, EVEN AFTER ONE HAS FAILED
    public void HS4_the_near_miss_report_is_complete_not_short_circuited()
    {
        (BowTie tie, _) = New();

        // Barriers 0 and 2 are dead, 1 is sound. A short-circuit on the first
        // failure would report one barrier and miss the other — and knowing
        // WHICH barriers failed is the whole value of the mechanic.
        double Strength(Barrier b) => b.Id.Value == "prevent-1" ? 1.0 : 0.0;

        ThreatResolution result = tie.Resolve(Threat, Barriers(), Strength);

        Assert.Equal(ThreatOutcome.NearMiss, result.Outcome);
        Assert.Equal(2, result.FailedBarriers.Count);
        Assert.Contains(new ContentId("prevent-0"), result.FailedBarriers);
        Assert.Contains(new ContentId("prevent-2"), result.FailedBarriers);
    }

    [Fact] // HS3: degrading process-safety barriers show up in near misses
    public void HS3_weakening_barriers_raise_the_near_miss_rate_before_the_incident()
    {
        static int NearMisses(double strength)
        {
            (BowTie tie, _) = New(seed: 777UL);
            IReadOnlyList<Barrier> barriers = Barriers();

            int count = 0;
            for (int i = 0; i < 2000; i++)
                if (tie.Resolve(Threat, barriers, _ => strength).Outcome
                    == ThreatOutcome.NearMiss)
                    count++;

            return count;
        }

        // The design intent, made testable: a player CAN let process-safety
        // barriers degrade, and the leading indicator says so loudly before the
        // top event does. Near misses rise long before losses of containment
        // become common.
        int sound = NearMisses(0.98);
        int slipping = NearMisses(0.75);

        Assert.True(slipping > sound * 3,
            $"degradation from 0.98 to 0.75 gave {slipping} near misses against {sound}");
    }

    [Fact] // Mitigating barriers are sampled ONLY on a top event
    public void HS2_mitigating_barriers_are_not_drawn_on_a_quiet_day()
    {
        // The emergency shutdown is not tested on a day nothing happened.
        // Drawing for it anyway would consume stream values on every suppressed
        // threat and make the sequence depend on how many quiet days preceded a
        // bad one.
        var trail = new AuditTrail(
            new SimulationClock(new GameDate(1980, 1)), new AuditRetention(100));

        IRandomStream stream = new RandomSource(5UL).Stream(StreamId.Hazard);
        var tie = new BowTie(stream, trail);

        tie.Resolve(Threat, Barriers(preventive: 2, mitigating: 4), _ => 1.0);

        // Two preventive draws, and nothing else.
        IRandomStream reference = new RandomSource(5UL).Stream(StreamId.Hazard);
        reference.NextUnit();
        reference.NextUnit();

        Assert.Equal(reference.NextUnit(), stream.NextUnit(), precision: 15);
    }

    [Fact] // On a top event, the mitigating barriers select the consequence tier
    public void HS2_mitigating_barriers_are_sampled_on_a_top_event()
    {
        (BowTie tie, _) = New();

        // Preventives dead, mitigations perfect: containment is lost but the
        // consequence is contained.
        double Strength(Barrier b) => b.IsPreventive ? 0.0 : 1.0;

        ThreatResolution result = tie.Resolve(
            Threat, Barriers(preventive: 2, mitigating: 4), Strength);

        Assert.Equal(ThreatOutcome.TopEvent, result.Outcome);
        Assert.Equal(4, result.MitigatingBarriersHeld);
    }

    [Fact] // A threat nothing stands against is not a bow-tie
    public void HS2_a_threat_with_no_preventive_barriers_is_a_model_fault()
    {
        (BowTie tie, _) = New();

        var fault = Assert.Throws<ModelFault>(
            () => tie.Resolve(Threat, Barriers(preventive: 0, mitigating: 2), _ => 1.0));

        Assert.Contains("no preventive barriers", fault.Fault.Detail);
    }

    [Fact] // Every resolution is audited, with the failed barriers named
    public void HS2_a_near_miss_is_audited_with_its_failed_barriers()
    {
        (BowTie tie, AuditTrail trail) = New();

        tie.Resolve(Threat, Barriers(), b => b.Id.Value == "prevent-1" ? 0.0 : 1.0);

        AuditEntry entry = Assert.Single(
            trail.Query(new AuditQuery(null, AuditCategory.StochasticOutcome, null, null)));

        Assert.Equal("NearMiss", entry.Data["outcome"].Value);
        Assert.Equal("prevent-1", entry.Data["failed0"].Value);
    }
}

public class EsgStandingTests
{
    private static IReadOnlyList<(double, double)> Clean =>
        [(1.0, 0.0), (1.0, 0.0), (1.0, 0.0)];

    private static IReadOnlyList<(double, double)> Dirty =>
        [(2.0, 10.0), (1.5, 8.0), (1.0, 6.0)];

    [Fact] // A clean operator with no incidents scores 100
    public void HS12_a_clean_record_scores_full_marks()
    {
        Assert.Equal(100.0, new EsgStanding(halfLifeTicks: 36.0).Standing(Clean), 12);
    }

    [Fact] // Intensities and incidents both cost
    public void HS12_intensities_and_incidents_both_reduce_the_standing()
    {
        var standing = new EsgStanding(36.0);

        // 2·10 + 1.5·8 + 1·6 = 38
        Assert.Equal(62.0, standing.Standing(Dirty), 12);

        standing.RecordIncident(20.0);
        Assert.Equal(42.0, standing.Standing(Dirty), 12);
    }

    [Fact] // CI4: A CLEAN DECADE GENUINELY REHABILITATES
    public void HS12_incident_points_decay_on_the_declared_half_life()
    {
        var standing = new EsgStanding(halfLifeTicks: 36.0);
        standing.RecordIncident(40.0);

        standing.Age(Duration.FromTicks(36.0));
        Assert.Equal(20.0, standing.IncidentPoints, 9);

        standing.Age(Duration.FromTicks(36.0));
        Assert.Equal(10.0, standing.IncidentPoints, 9);

        // Without the decay, one bad year would price a company's debt forever
        // and the loop would have no exit — which design rule CI4 forbids.
        standing.Age(Duration.FromTicks(360.0));
        Assert.True(standing.IncidentPoints < 0.01);
    }

    [Fact] // TWO EXITS, per CI4: clean up, or wait out the record
    public void HS12_the_loop_has_two_exits()
    {
        var byCleaning = new EsgStanding(36.0);
        byCleaning.RecordIncident(20.0);

        // Exit one: reduce the intensities.
        Assert.True(byCleaning.Standing(Clean) > byCleaning.Standing(Dirty));

        var byWaiting = new EsgStanding(36.0);
        byWaiting.RecordIncident(20.0);
        double before = byWaiting.Standing(Dirty);

        // Exit two: let the record decay.
        byWaiting.Age(Duration.FromTicks(72.0));
        Assert.True(byWaiting.Standing(Dirty) > before);

        // A loop with one exit is a trap, and a loop with none is a punishment.
    }

    [Fact] // The standing is bounded, so a catastrophe cannot go below zero
    public void HS12_the_standing_is_clamped_to_its_range()
    {
        var standing = new EsgStanding(36.0);
        standing.RecordIncident(1000.0);

        Assert.Equal(0.0, standing.Standing(Dirty), 12);
    }

    [Fact] // An incident cannot improve the record, and neither can a bad half-life
    public void HS12_impossible_inputs_are_model_faults()
    {
        Assert.Throws<ModelFault>(() => new EsgStanding(halfLifeTicks: 0.0));
        Assert.Throws<ModelFault>(() => new EsgStanding(36.0).RecordIncident(-5.0));
    }
}
