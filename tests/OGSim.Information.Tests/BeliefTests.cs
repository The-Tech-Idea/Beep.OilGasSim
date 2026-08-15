// R14's verification suite (SDD-008).

using OGSim.Contracts;
using OGSim.Information;
using OGSim.Kernel;

namespace OGSim.Information.Tests;

public static class Fx
{
    public static readonly EntityRef Compartment = new(EntityKind.Compartment, 1);
    public static readonly ContentId Porosity = new("porosity");
    public static readonly ContentId Permeability = new("permeability");

    public static (BeliefStore Store, AuditTrail Trail) New(double sigmaFloor = 0.0)
    {
        var clock = new SimulationClock(new GameDate(1970, 1));
        var trail = new AuditTrail(clock, new AuditRetention(2000));

        return (new BeliefStore(trail, _ => sigmaFloor, () => new GameDate(1970, 1)), trail);
    }

    public static Observation Obs(
        double value, double sigma,
        Provenance source = Provenance.Seismic,
        BeliefSpace space = BeliefSpace.Linear,
        ContentId? kind = null) =>
        new(Compartment, kind ?? Porosity, value, sigma, space, source);
}

public class BeliefUpdateTests
{
    // ------------------------------------------------------------ §2.1

    [Fact] // Nothing known is NULL, not a wide prior
    public void R14V2_an_unobserved_property_is_null_rather_than_a_default()
    {
        (BeliefStore store, _) = Fx.New();

        // "We have never looked" and "we looked and learned little" are
        // different states, and only the first leaves a map region unrendered.
        Assert.Null(store.Get(Fx.Compartment, Fx.Porosity));
    }

    [Fact] // The first observation IS the belief
    public void R14V2_the_first_observation_becomes_the_prior()
    {
        (BeliefStore store, _) = Fx.New();
        store.Apply(Fx.Obs(0.22, 0.05));

        Belief belief = Assert.NotNull(store.Get(Fx.Compartment, Fx.Porosity));
        Assert.Equal(0.22, belief.Mu, 12);
        Assert.Equal(0.05, belief.Sigma, 12);
        Assert.Equal(Provenance.Seismic, belief.BestSource);
    }

    [Fact] // SDD-008 §2.1: PRECISION ADDS — hand-computed from the formula
    public void R14V2_the_conjugate_update_matches_the_formula()
    {
        (BeliefStore store, _) = Fx.New();

        store.Apply(Fx.Obs(0.20, 0.04));
        store.Apply(Fx.Obs(0.26, 0.03));

        // precision = 1/0.04² + 1/0.03² = 625 + 1111.11… = 1736.11…
        // mu = (0.20·625 + 0.26·1111.11…) / 1736.11… = 0.23840
        // sigma = sqrt(1/1736.11…) = 0.024
        double priorPrecision = 1.0 / (0.04 * 0.04);
        double obsPrecision = 1.0 / (0.03 * 0.03);
        double expectedMu = (0.20 * priorPrecision + 0.26 * obsPrecision)
                          / (priorPrecision + obsPrecision);

        Belief belief = Assert.NotNull(store.Get(Fx.Compartment, Fx.Porosity));

        Assert.Equal(expectedMu, belief.Mu, 12);
        Assert.Equal(Math.Sqrt(1.0 / (priorPrecision + obsPrecision)), belief.Sigma, 12);
    }

    [Fact] // A second mediocre survey still helps — information combines by precision
    public void R14V2_every_observation_narrows_the_belief()
    {
        (BeliefStore store, _) = Fx.New();

        store.Apply(Fx.Obs(0.20, 0.05));
        double previous = Assert.NotNull(store.Get(Fx.Compartment, Fx.Porosity)).Sigma;

        for (int i = 0; i < 5; i++)
        {
            store.Apply(Fx.Obs(0.21, 0.05));

            double now = Assert.NotNull(store.Get(Fx.Compartment, Fx.Porosity)).Sigma;
            Assert.True(now < previous, $"observation {i} did not narrow the belief");
            previous = now;
        }
    }

    [Fact] // A sharp observation dominates a vague prior, as precision weighting says
    public void R14V2_a_sharper_observation_carries_more_weight()
    {
        (BeliefStore store, _) = Fx.New();

        store.Apply(Fx.Obs(0.10, 0.20));      // a vague seismic guess
        store.Apply(Fx.Obs(0.25, 0.01));      // a core

        Belief belief = Assert.NotNull(store.Get(Fx.Compartment, Fx.Porosity));

        // The posterior sits very close to the core, not halfway.
        Assert.True(belief.Mu > 0.24, $"the core did not dominate: mu = {belief.Mu}");
    }

    [Fact] // Provenance is the BEST contributor, not the latest
    public void R14V2_provenance_records_the_best_source_not_the_most_recent()
    {
        (BeliefStore store, _) = Fx.New();

        store.Apply(Fx.Obs(0.25, 0.01, Provenance.Core));
        store.Apply(Fx.Obs(0.20, 0.10, Provenance.Seismic));

        // A cheap seismic pass after an expensive core does not make the belief
        // seismic-grade — the player-facing "how do we know this?" would lie.
        Assert.Equal(Provenance.Core,
            Assert.NotNull(store.Get(Fx.Compartment, Fx.Porosity)).BestSource);
    }

    // ------------------------------------------------------------ INV8

    [Fact] // INV8: sigma has a floor, so nobody becomes certain of a reservoir
    public void R14V2_the_sigma_floor_stops_belief_collapsing_to_certainty()
    {
        (BeliefStore store, _) = Fx.New(sigmaFloor: 0.02);

        for (int i = 0; i < 100; i++) store.Apply(Fx.Obs(0.22, 0.05));

        // Without the floor, a hundred surveys would drive sigma to 0.005 and
        // the player would be certain of something nobody can be certain of.
        Assert.Equal(0.02, Assert.NotNull(store.Get(Fx.Compartment, Fx.Porosity)).Sigma, 12);
    }

    [Fact] // Measured is EXEMPT — a meter's reading IS the quantity
    public void R14V2_a_measured_observation_is_not_floored()
    {
        (BeliefStore store, _) = Fx.New(sigmaFloor: 0.02);

        store.Apply(Fx.Obs(0.22, 0.0001, Provenance.Measured));

        // There is no hidden truth left to be uncertain about.
        Assert.Equal(0.0001, Assert.NotNull(store.Get(Fx.Compartment, Fx.Porosity)).Sigma, 12);
    }

    // ------------------------------------------------------------ staleness

    [Fact] // §2: DYNAMIC kinds drift; static rock properties do not
    public void R14V2_staleness_widens_only_the_kind_it_is_applied_to()
    {
        (BeliefStore store, _) = Fx.New();

        store.Apply(Fx.Obs(0.22, 0.05, kind: Fx.Porosity));
        store.Apply(Fx.Obs(300.0, 20.0, kind: Fx.Permeability));

        store.Age(Fx.Compartment, Fx.Porosity, driftPerYear: 0.01, years: 3.0);

        Assert.Equal(0.08, Assert.NotNull(store.Get(Fx.Compartment, Fx.Porosity)).Sigma, 12);

        // Permeability is untouched: a rock does not become less certain by
        // being ignored, and making everything drift would have the player
        // re-log a well to learn what its core already told them.
        Assert.Equal(20.0, Assert.NotNull(store.Get(Fx.Compartment, Fx.Permeability)).Sigma, 12);
    }

    // ------------------------------------------------------------ Held

    [Fact] // A company that has never looked has nothing to walk
    public void R21V7_an_unobserved_store_holds_nothing()
    {
        (BeliefStore store, _) = Fx.New();

        // The no-leak property, stated at the door the projection uses: a pair
        // enters Held only through Apply, so a subject nobody has observed has
        // no entry for a host to find. Enumerability is not a second way in.
        Assert.Empty(store.Held);
    }

    [Fact] // SDD-008 §3: ordered by first learning, so a projection is stable (D-5)
    public void R21V7_held_beliefs_are_in_the_order_they_were_learned()
    {
        (BeliefStore store, _) = Fx.New();

        store.Apply(Fx.Obs(300.0, 20.0, kind: Fx.Permeability));
        store.Apply(Fx.Obs(0.22, 0.05, kind: Fx.Porosity));

        Assert.Equal(
            [Fx.Permeability, Fx.Porosity],
            store.Held.Select(held => held.PropertyKind));
    }

    [Fact] // A second reading SHARPENS an entry; it does not add one
    public void R21V7_observing_a_known_pair_again_updates_it_in_place()
    {
        (BeliefStore store, _) = Fx.New();

        store.Apply(Fx.Obs(0.22, 0.05));
        store.Apply(Fx.Obs(0.24, 0.05));

        // Two entries for one (subject, kind) would let a host render one
        // compartment's porosity twice, at two different numbers, and give the
        // player no way to tell which was current.
        HeldBelief only = Assert.Single(store.Held);

        Assert.Equal(Fx.Compartment, only.Subject);
        Assert.Equal(Fx.Porosity, only.PropertyKind);
        Assert.Equal(Assert.NotNull(store.Get(Fx.Compartment, Fx.Porosity)), only.Belief);
    }

    [Fact] // Held and Get are one fact, not two that can disagree (law L5)
    public void R21V7_every_held_belief_is_the_one_Get_answers()
    {
        (BeliefStore store, _) = Fx.New();

        store.Apply(Fx.Obs(0.22, 0.05, kind: Fx.Porosity));
        store.Apply(Fx.Obs(300.0, 20.0, kind: Fx.Permeability));
        store.Age(Fx.Compartment, Fx.Porosity, driftPerYear: 0.01, years: 3.0);

        // Ageing walks the same list the projection reads. A store that widened
        // one copy and projected the other would show a host a certainty the
        // engine no longer holds.
        foreach (HeldBelief held in store.Held)
            Assert.Equal(
                Assert.NotNull(store.Get(held.Subject, held.PropertyKind)), held.Belief);
    }

    // ------------------------------------------------------------ refusals

    [Fact] // A source claiming zero error is claiming to be truth
    public void R14V2_a_zero_sigma_observation_is_a_model_fault()
    {
        (BeliefStore store, _) = Fx.New();

        var fault = Assert.Throws<ModelFault>(() => store.Apply(Fx.Obs(0.22, 0.0)));
        Assert.Contains("HONEST uncertainty", fault.Fault.Detail);
    }

    [Fact] // Mixing belief spaces would average a log with a linear
    public void R14V2_combining_across_belief_spaces_is_an_invariant_fault()
    {
        (BeliefStore store, _) = Fx.New();

        store.Apply(Fx.Obs(0.22, 0.05, space: BeliefSpace.Linear));

        var fault = Assert.Throws<InvariantFault>(
            () => store.Apply(Fx.Obs(0.22, 0.05, space: BeliefSpace.Log)));

        Assert.Contains("space", fault.Fault.Detail);
    }

    [Fact] // Every update is audited — the fairness record
    public void R14V2_belief_updates_are_audited()
    {
        (BeliefStore store, AuditTrail trail) = Fx.New();

        store.Apply(Fx.Obs(0.22, 0.05, Provenance.Log));

        AuditEntry entry = Assert.Single(
            trail.Query(new AuditQuery(null, AuditCategory.BeliefUpdate, null, null)));

        Assert.Equal("porosity", entry.Data["kind"].Value);
        Assert.Equal("Log", entry.Data["source"].Value);
        Assert.Equal("0.05", entry.Data["sigma"].Value);
    }
}

public class QuantileTests
{
    [Fact] // R14.8: P90 is the LOW case — the petroleum convention, pinned
    public void R14V8_p90_is_the_low_case_and_p10_the_high()
    {
        var belief = new Belief(100.0, 20.0, BeliefSpace.Linear,
                                Provenance.Seismic, new GameDate(1970, 1));

        Assert.Equal(100.0, Quantiles.P50(belief), 12);

        // Reading these the statistical way round would book POSSIBLE reserves
        // as PROVED, which is why the convention is pinned on the contract.
        Assert.True(Quantiles.P90(belief) < Quantiles.P50(belief));
        Assert.True(Quantiles.P10(belief) > Quantiles.P50(belief));

        Assert.Equal(100.0 - 1.281552 * 20.0, Quantiles.P90(belief), 9);
        Assert.Equal(100.0 + 1.281552 * 20.0, Quantiles.P10(belief), 9);
    }

    [Fact] // A Log-space Normal IS the log-normal the design requires
    public void R14V8_log_space_quantiles_are_log_normal()
    {
        // Mu and sigma are in LOG space; the quantiles come back in linear.
        var belief = new Belief(Math.Log(1.0e6), 0.5, BeliefSpace.Log,
                                Provenance.Seismic, new GameDate(1970, 1));

        Assert.Equal(1.0e6, Quantiles.P50(belief), 3);

        // Multiplicative, not additive — which is what makes a volume estimate
        // skew the way real ones do.
        double low = Quantiles.P90(belief);
        double high = Quantiles.P10(belief);

        Assert.True(low > 0.0, "a log-normal quantile is never negative");
        Assert.Equal(1.0e6 / low, high / 1.0e6, precision: 6);
    }

    [Fact] // The skew is the point: a linear belief could go negative, a log cannot
    public void R14V8_a_log_belief_cannot_produce_a_negative_low_case()
    {
        var wide = new Belief(Math.Log(1000.0), 2.0, BeliefSpace.Log,
                              Provenance.Assumed, new GameDate(1970, 1));

        Assert.True(Quantiles.P90(wide) > 0.0);

        // The same width in linear space would.
        var linear = new Belief(1000.0, 2000.0, BeliefSpace.Linear,
                                Provenance.Assumed, new GameDate(1970, 1));

        Assert.True(Quantiles.P90(linear) < 0.0,
            "the linear case should show why multiplicative kinds are declared Log");
    }
}

public class ProspectRiskTests
{
    private static ProspectRisk New(double alpha = 7.0, double beta = 3.0) =>
        new(new FactorBelief(alpha, beta));

    [Fact] // SDD-008 §4: POS is the PRODUCT of five means
    public void R14V7_the_probability_of_success_multiplies_the_five_factors()
    {
        ProspectRisk risk = New(alpha: 7.0, beta: 3.0);

        // Five factors at 0.7 each. A player reasoning factor by factor sees
        // five encouraging numbers; the product is one chance in six.
        Assert.Equal(Math.Pow(0.7, 5), risk.ProbabilityOfSuccess, 12);
        Assert.True(risk.ProbabilityOfSuccess < 0.17);
    }

    [Fact] // Beta-Bernoulli: evidence moves the factor, conjugately
    public void R14V7_a_success_raises_the_factor_and_a_failure_lowers_it()
    {
        ProspectRisk risk = New();
        double before = ProspectRisk.MeanOf(risk[PosFactor.Source]);

        risk.Observe(PosFactor.Source, present: true);
        double afterSuccess = ProspectRisk.MeanOf(risk[PosFactor.Source]);
        Assert.True(afterSuccess > before);

        risk.Observe(PosFactor.Source, present: false);
        risk.Observe(PosFactor.Source, present: false);
        Assert.True(ProspectRisk.MeanOf(risk[PosFactor.Source]) < afterSuccess);
    }

    [Fact] // The prior's MAGNITUDE is how much conviction you started with
    public void R14V7_a_stronger_prior_moves_less_on_one_result()
    {
        ProspectRisk weak = New(alpha: 1.4, beta: 0.6);      // mean 0.7, thin
        ProspectRisk strong = New(alpha: 70.0, beta: 30.0);  // mean 0.7, heavy

        double weakBefore = ProspectRisk.MeanOf(weak[PosFactor.Trap]);
        double strongBefore = ProspectRisk.MeanOf(strong[PosFactor.Trap]);

        weak.Observe(PosFactor.Trap, present: false);
        strong.Observe(PosFactor.Trap, present: false);

        double weakMoved = weakBefore - ProspectRisk.MeanOf(weak[PosFactor.Trap]);
        double strongMoved = strongBefore - ProspectRisk.MeanOf(strong[PosFactor.Trap]);

        // "How many wells' worth of conviction we started with" is a quantity a
        // geologist can argue about, which is the point of using a Beta at all.
        Assert.True(weakMoved > strongMoved * 5.0);
    }

    // ------------------------------------------------------------ R14.10

    [Fact] // R14.10: a shared factor IS the play correlation
    public void R14V10_a_dry_hole_on_source_rock_informs_the_whole_play()
    {
        ProspectRisk play = New();
        ProspectRisk prospectA = New();
        ProspectRisk prospectB = New();

        prospectA.ShareFrom(play, PosFactor.Source);
        prospectB.ShareFrom(play, PosFactor.Source);

        double before = prospectB.ProbabilityOfSuccess;

        // A well fails on source rock. The play's belief moves...
        play.Observe(PosFactor.Source, present: false);

        // ...AND NOBODY RE-SYNCS. This test used to call ShareFrom again here,
        // which meant it was demonstrating the correlation by performing it: the
        // factor was copied, so a prospect kept its old number until a caller
        // remembered to refresh, and no caller ever did. A shared factor is now
        // bound, so the play moving IS the prospect moving.
        //
        // ...and every prospect sharing that factor moves with it, because they
        // were never independent. That is the whole content of "the play died",
        // and correlating OUTCOMES instead would have needed a covariance
        // nobody could state.
        Assert.True(prospectB.ProbabilityOfSuccess < before);
        Assert.Equal(prospectA.ProbabilityOfSuccess, prospectB.ProbabilityOfSuccess, 12);
    }

    [Fact] // A degenerate prior would be immovable
    public void R14V7_a_zero_prior_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() => new ProspectRisk(new FactorBelief(0.0, 1.0)));
        Assert.Contains("positive alpha and beta", fault.Fault.Detail);
    }

    /// <summary>
    /// AND A WELL DRILLED ON A PROSPECT INFORMS THE PLAY, not just itself. The
    /// evidence is recorded where the belief lives — otherwise a dry hole on
    /// source rock would tell the company something about one prospect and
    /// nothing about the twenty others that depend on the same source.
    /// </summary>
    [Fact]
    public void R14V10_evidence_on_a_shared_factor_is_recorded_on_the_play()
    {
        ProspectRisk play = New();
        ProspectRisk drilled = New();
        ProspectRisk untouched = New();

        drilled.ShareFrom(play, PosFactor.Source);
        untouched.ShareFrom(play, PosFactor.Source);

        double before = untouched.ProbabilityOfSuccess;

        // The well is on `drilled`, and it fails on the shared element.
        drilled.Observe(PosFactor.Source, present: false);

        Assert.True(untouched.ProbabilityOfSuccess < before,
            "a dry hole on a shared element left the rest of the play untouched");
    }

    /// <summary>
    /// A PROSPECT-LOCAL FACTOR STAYS LOCAL. Trap geometry is this structure's
    /// own, so proving it says nothing about the next one — and if it did, the
    /// distinction between play-shared and prospect-local would be decoration.
    /// </summary>
    [Fact]
    public void R14V10_a_local_factor_does_not_move_the_play()
    {
        ProspectRisk play = New();
        ProspectRisk drilled = New();
        ProspectRisk untouched = New();

        drilled.ShareFrom(play, PosFactor.Source);
        untouched.ShareFrom(play, PosFactor.Source);

        double before = untouched.ProbabilityOfSuccess;

        drilled.Observe(PosFactor.Trap, present: false);

        Assert.Equal(before, untouched.ProbabilityOfSuccess, precision: 12);
    }

    /// <summary>
    /// A prospect belongs to ONE play. Sharing from a second is refused rather
    /// than silently accepted, because its risk would then depend on which call
    /// came last — a defect that would show up as an unreproducible POS.
    /// </summary>
    [Fact]
    public void R14V10_a_prospect_cannot_belong_to_two_plays()
    {
        ProspectRisk prospect = New();

        prospect.ShareFrom(New(), PosFactor.Source);

        Assert.Throws<ModelFault>(() => prospect.ShareFrom(New(), PosFactor.Reservoir));
    }
}
