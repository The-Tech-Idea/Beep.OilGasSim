// R16's verification suite (SDD-011).
//
// The centrepiece is R16-V4: a rival is a policy over BELIEFS. If a rival could
// read truth it would bid correctly every time and the player would be competing
// against an oracle — so the test builds a rival whose beliefs are deliberately
// wrong and shows it bids wrongly, which is the fairness claim made concrete.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company.Tests;

public class LicenceTests
{
    private static readonly ContentId Well = new("exploration-well");
    private static readonly ContentId Seismic = new("seismic-3d");

    private static LicenceTerms Terms(
        int months = 72,
        double wells = 2.0,
        double bondMillions = 25.0,
        int wellsDue = 48) =>
        new(months,
            [new CommitmentItem(Well, wells, new Tick(wellsDue)),
             new CommitmentItem(Seismic, 500.0, new Tick(24))],
            Money.FromMillions(bondMillions),
            [new RelinquishmentStep(0.25, new Tick(36)),
             new RelinquishmentStep(0.25, new Tick(60))],
            new ContentId("psc-generic"),
            new ContentId("hse-strict"));

    private static Licence New(LicenceTerms? terms = null) =>
        new(new EntityId<ILicence>(1), terms ?? Terms(), new Tick(0));

    [Fact] // The term is a whole number of MONTHS on the calendar
    public void R16V2_the_expiry_is_the_grant_plus_the_term_in_months()
    {
        Licence licence = new(new EntityId<ILicence>(1), Terms(months: 72), new Tick(12));

        Assert.Equal(84, licence.Expiry.Value);
        Assert.False(licence.HasExpiredBy(new Tick(83)));
        Assert.True(licence.HasExpiredBy(new Tick(84)));
    }

    [Fact] // Commitment tracking is mechanical
    public void R16V2_qualifying_work_decrements_its_commitment()
    {
        Licence licence = New();

        licence.RecordDelivery(Well, 1.0);
        CommitmentProgress wells = licence.Progress.Single(p => p.Item.Kind == Well);

        Assert.Equal(1.0, wells.Delivered, 12);
        Assert.Equal(1.0, wells.Outstanding, 12);
        Assert.False(wells.Met);

        licence.RecordDelivery(Well, 1.0);
        Assert.True(licence.Progress.Single(p => p.Item.Kind == Well).Met);
    }

    [Fact] // Work against a kind the licence never promised is NOT credited
    public void R16V2_unrelated_work_does_not_discharge_a_commitment()
    {
        Licence licence = New();

        licence.RecordDelivery(new ContentId("gravity-survey"), 10_000.0);

        // A company that drilled the wrong thing has still drilled the wrong
        // thing; quietly counting it would let any activity discharge any
        // promise.
        Assert.All(licence.Progress, p => Assert.Equal(0.0, p.Delivered, 12));
    }

    [Fact] // Nothing is due before its date
    public void R16V2_an_undue_commitment_is_not_assessed()
    {
        Licence licence = New();

        CommitmentAssessment early = licence.AssessAt(new Tick(12));

        Assert.Empty(early.Unmet);
        Assert.False(early.LicenceLost);
        Assert.True(licence.IsLive);
    }

    [Fact] // R16-V2: unmet at the deadline forfeits the bond and loses the licence
    public void R16V2_an_unmet_commitment_forfeits_the_bond_whole()
    {
        Licence licence = New(Terms(bondMillions: 25.0));

        licence.RecordDelivery(Seismic, 500.0);      // the survey was done
        // The wells were not.

        CommitmentAssessment assessment = licence.AssessAt(new Tick(48));

        CommitmentProgress unmet = Assert.Single(assessment.Unmet);
        Assert.Equal(Well, unmet.Item.Kind);
        Assert.Equal(2.0, unmet.Outstanding, 12);

        // WHOLE, not pro-rata. That is what a bond is: it secures the promise,
        // not a fraction of it, and a pro-rata forfeit would make a token well a
        // cheap way to keep most of the money.
        Assert.Equal(Money.FromMillions(25.0), assessment.BondForfeit);
        Assert.True(assessment.LicenceLost);
        Assert.False(licence.IsLive);
    }

    [Fact] // Meeting the programme keeps everything
    public void R16V2_a_met_programme_forfeits_nothing()
    {
        Licence licence = New();

        licence.RecordDelivery(Seismic, 500.0);
        licence.RecordDelivery(Well, 2.0);

        CommitmentAssessment assessment = licence.AssessAt(new Tick(60));

        Assert.Empty(assessment.Unmet);
        Assert.Equal(Money.Zero, assessment.BondForfeit);
        Assert.True(licence.IsLive);
    }

    [Fact] // Relinquishment is CUMULATIVE
    public void R16V2_relinquishment_accumulates_across_steps()
    {
        Licence licence = New();

        Assert.Equal(0.0, licence.RelinquishedFractionDueBy(new Tick(12)), 12);
        Assert.Equal(0.25, licence.RelinquishedFractionDueBy(new Tick(36)), 12);

        // A licence that missed one relinquishment does not get to forget it at
        // the next.
        Assert.Equal(0.50, licence.RelinquishedFractionDueBy(new Tick(60)), 12);
    }

    [Theory] // Content errors are refused where the jurisdiction file is in hand
    [InlineData(0, "term")]
    [InlineData(-12, "term")]
    public void R16V2_an_impossible_term_is_a_model_fault(int months, string expected)
    {
        var fault = Assert.Throws<ModelFault>(() => New(Terms(months: months)));
        Assert.Contains(expected, fault.Fault.Detail);
    }

    [Fact] // A schedule handing back more than the whole licence is content nonsense
    public void R16V2_over_relinquishment_is_a_model_fault()
    {
        var terms = new LicenceTerms(
            72, [new CommitmentItem(Well, 1.0, new Tick(24))], Money.FromMillions(1.0),
            [new RelinquishmentStep(0.7, new Tick(24)),
             new RelinquishmentStep(0.7, new Tick(48))],
            new ContentId("psc"), new ContentId("hse"));

        var fault = Assert.Throws<ModelFault>(
            () => new Licence(new EntityId<ILicence>(1), terms, new Tick(0)));

        Assert.Contains("more than the whole licence", fault.Fault.Detail);
    }

    [Fact] // A promise to do nothing is not a commitment
    public void R16V2_a_zero_quantity_commitment_is_a_model_fault()
    {
        var terms = new LicenceTerms(
            72, [new CommitmentItem(Well, 0.0, new Tick(24))], Money.Zero, [],
            new ContentId("psc"), new ContentId("hse"));

        Assert.Throws<ModelFault>(
            () => new Licence(new EntityId<ILicence>(1), terms, new Tick(0)));
    }
}

public class RivalTests
{
    private static readonly EntityRef Block = new(EntityKind.Compartment, 1);
    private static readonly ContentId InPlace = new("oil-in-place");
    private static readonly ContentId BlockId = new("block-16-2");

    /// <summary>
    /// A rival's own head. Deliberately not the shipped <c>BeliefStore</c>: what
    /// R16-V4 asserts is that a rival bids from beliefs rather than from truth,
    /// and a fake that takes the value it is given makes the number under test
    /// the bid instead of the conjugate update.
    /// </summary>
    private sealed class Beliefs : IBeliefStore
    {
        private readonly List<HeldBelief> _held = [];
        private readonly Dictionary<(EntityRef, ContentId), int> _at = [];

        public IReadOnlyList<HeldBelief> Held => _held;

        /// <summary>
        /// These tests are about licences, and nothing in them discovers a
        /// field — so a re-key here would be a call nobody makes. It faults
        /// rather than doing nothing, because a silent no-op in a double is how
        /// a test comes to pass against behaviour that was never exercised.
        /// </summary>
        public void ReKey(EntityRef from, EntityRef to) =>
            throw new NotSupportedException(
                "these tests never discover a field, so nothing should re-key");

        /// <summary>
        /// Nor does anything here produce, so nothing goes stale. It faults for
        /// the same reason the re-key above does: a double that quietly did
        /// nothing would let a test pass against behaviour it never exercised.
        /// </summary>
        public void Age(EntityRef subject, ContentId kind, double driftPerYear, double years) =>
            throw new NotSupportedException(
                "these tests produce nothing, so no belief should go stale");

        public void Apply(Observation observation)
        {
            var entry = new HeldBelief(
                observation.Subject,
                observation.PropertyKind,
                new Belief(observation.Value, observation.Sigma, observation.Space,
                           observation.Source, new GameDate(1970, 1)));

            if (_at.TryGetValue((observation.Subject, observation.PropertyKind), out int at))
            {
                _held[at] = entry;
                return;
            }

            _at[(observation.Subject, observation.PropertyKind)] = _held.Count;
            _held.Add(entry);
        }

        public Belief? Get(EntityRef subject, ContentId kind) =>
            _at.TryGetValue((subject, kind), out int at) ? _held[at].Belief : null;
    }

    private static Rival New(
        ulong id, double aggressiveness, IBeliefStore beliefs, int techLag = 0) =>
        new(new EntityId<ICompany>(id),
            new RivalPersonality(
                new ContentId($"rival-{id}"), aggressiveness, techLag,
                Money.FromMillions(1000.0)),
            beliefs,
            new RandomSource(id).Stream(StreamId.Market));

    private static Money ValueOf(double barrels) => Money.RoundHalfEven(barrels * 100.0);

    // ------------------------------------------------------------ R16-V4

    [Fact] // R16-V4: a rival bids from ITS OWN BELIEFS, right or wrong
    public void R16V4_a_rival_bids_what_it_believes_not_what_is_true()
    {
        // Two rivals, identical personalities, different beliefs about the same
        // block. Truth does not enter this test at all — and that is the point.
        var optimist = new Beliefs();
        optimist.Apply(new Observation(
            Block, InPlace, DetMath.Ln(50e6), 0.3, BeliefSpace.Log, Provenance.Seismic));

        var pessimist = new Beliefs();
        pessimist.Apply(new Observation(
            Block, InPlace, DetMath.Ln(5e6), 0.3, BeliefSpace.Log, Provenance.Seismic));

        Bid? high = New(1, 1.0, optimist).BidFor(BlockId, Block, InPlace, ValueOf);
        Bid? low = New(2, 1.0, pessimist).BidFor(BlockId, Block, InPlace, ValueOf);

        Assert.NotNull(high);
        Assert.NotNull(low);
        Assert.True(high.Amount.Cents > low.Amount.Cents,
            "the optimist should bid more, because it believes more");
    }

    [Fact] // R16-V4: a rival that has never looked does not bid
    public void R16V4_a_rival_with_no_belief_does_not_bid()
    {
        // No survey, no bid. A rival does not bid on acreage it has never looked
        // at, any more than the player would — and it CANNOT, because there is
        // no truth for it to reach.
        Assert.Null(New(1, 1.0, new Beliefs()).BidFor(BlockId, Block, InPlace, ValueOf));
    }

    [Fact] // Aggressiveness is a personality, and it shows in the bid
    public void R16V4_aggressiveness_scales_the_bid()
    {
        var beliefs = new Beliefs();
        beliefs.Apply(new Observation(
            Block, InPlace, DetMath.Ln(20e6), 0.2, BeliefSpace.Log, Provenance.Seismic));

        // The same rival id, so the same market draw — only the personality
        // differs, which isolates the multiplier.
        Bid? cautious = New(7, 0.5, beliefs).BidFor(BlockId, Block, InPlace, ValueOf);
        Bid? bold = New(7, 2.0, beliefs).BidFor(BlockId, Block, InPlace, ValueOf);

        Assert.NotNull(cautious);
        Assert.NotNull(bold);
        Assert.Equal(4.0, bold.Amount.Cents / (double)cautious.Amount.Cents, precision: 6);
    }

    [Fact] // A bid never exceeds the round budget
    public void R16V4_a_bid_is_capped_by_the_budget()
    {
        var beliefs = new Beliefs();
        beliefs.Apply(new Observation(
            Block, InPlace, DetMath.Ln(1e12), 0.1, BeliefSpace.Log, Provenance.Seismic));

        Bid? bid = New(3, 5.0, beliefs).BidFor(BlockId, Block, InPlace, ValueOf);
        Assert.NotNull(bid);

        Assert.Equal(Money.FromMillions(1000.0), bid.Amount);
    }

    // ------------------------------------------------------------ TD2

    [Fact] // Rival technology is DETERMINISTIC diffusion — no draws
    public void R16V4_rival_technology_arrives_on_a_clock_the_player_can_learn()
    {
        Rival leader = New(1, 1.0, new Beliefs(), techLag: 6);
        Rival laggard = New(2, 1.0, new Beliefs(), techLag: 60);

        var eraStart = new Tick(100);

        Assert.False(leader.HasTechnologyAt(eraStart, new Tick(105)));
        Assert.True(leader.HasTechnologyAt(eraStart, new Tick(106)));

        Assert.False(laggard.HasTechnologyAt(eraStart, new Tick(150)));
        Assert.True(laggard.HasTechnologyAt(eraStart, new Tick(160)));

        // The player races real clocks and can learn a rival's pace, which is a
        // strategy. A dice-based acquisition would make a rival's capability
        // unknowable rather than merely unknown.
    }

    [Fact] // A rival that could never bid is content nonsense
    public void R16V4_a_non_bidding_personality_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() => New(1, 0.0, new Beliefs()));
        Assert.Contains("never bid", fault.Fault.Detail);
    }

    // ------------------------------------------------------------ rounds

    [Fact] // Sealed-bid, highest wins
    public void R16V3_the_highest_bid_takes_the_block()
    {
        List<Bid> bids =
        [
            new(new EntityId<ICompany>(1), BlockId, Money.FromMillions(10.0)),
            new(new EntityId<ICompany>(2), BlockId, Money.FromMillions(30.0)),
            new(new EntityId<ICompany>(3), BlockId, Money.FromMillions(20.0)),
        ];

        LicenceRound.Award? award = LicenceRound.Resolve(BlockId, bids);
        Assert.NotNull(award);

        Assert.Equal(2UL, award.Winner.Value);
        Assert.Equal(Money.FromMillions(30.0), award.Price);
    }

    [Fact] // Ties break on the LOWEST company id, so the outcome is total
    public void R16V3_a_tie_breaks_deterministically()
    {
        List<Bid> bids =
        [
            new(new EntityId<ICompany>(5), BlockId, Money.FromMillions(30.0)),
            new(new EntityId<ICompany>(2), BlockId, Money.FromMillions(30.0)),
        ];

        // Whichever order they arrive in.
        Assert.Equal(2UL, LicenceRound.Resolve(BlockId, bids)!.Winner.Value);

        bids.Reverse();
        Assert.Equal(2UL, LicenceRound.Resolve(BlockId, bids)!.Winner.Value);
    }

    [Fact] // An unbid block stays open — a real and common outcome
    public void R16V3_a_block_nobody_bid_on_is_not_awarded()
    {
        Assert.Null(LicenceRound.Resolve(BlockId, []));

        Assert.Null(LicenceRound.Resolve(BlockId,
            [new Bid(new EntityId<ICompany>(1), new ContentId("other-block"),
                     Money.FromMillions(10.0))]));
    }

    // ------------------------------------------------------------ R16-V5

    [Fact] // R16-V5: public disclosure needs NO new mechanism
    public void R16V5_a_rival_result_publishes_as_a_widened_observation()
    {
        var theirs = new Observation(
            Block, InPlace, DetMath.Ln(30e6), 0.15, BeliefSpace.Log, Provenance.Log);

        Observation published = PublicDisclosure.Publish(theirs, extraSigma: 0.4);

        // You read their press release, not their logs.
        Assert.True(published.Sigma > theirs.Sigma);

        // PRECISION adds, so sigmas add in quadrature. Adding them directly
        // would make two disclosures of one well worse than one, which is
        // backwards.
        Assert.Equal(Math.Sqrt(0.15 * 0.15 + 0.4 * 0.4), published.Sigma, 12);

        // Everything else survives, so it updates the player's beliefs through
        // the identical conjugate path — which is the strongest evidence that
        // the observation door was the right shape.
        Assert.Equal(theirs.Value, published.Value, 12);
        Assert.Equal(theirs.Subject, published.Subject);
        Assert.Equal(theirs.Space, published.Space);
    }

    [Fact] // A press release is never better than the log it describes
    public void R16V5_disclosure_cannot_sharpen_an_observation()
    {
        var theirs = new Observation(
            Block, InPlace, 1.0, 0.5, BeliefSpace.Log, Provenance.Log);

        Assert.Throws<ModelFault>(() => PublicDisclosure.Publish(theirs, extraSigma: -0.2));
    }
}
