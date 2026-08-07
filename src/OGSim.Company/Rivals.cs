// R16.3 / R16.4 — rivals and licence rounds (SDD-011 §2–3).
//
// A RIVAL IS A POLICY OVER BELIEFS, NEVER A READER OF TRUTH.
//
// That is the architectural rule and it buys the fairness claim outright. A
// rival holds its own IBeliefStore, buys surveys through the same observation
// door, and bids from what it believes. There is no rival-specific data path,
// so there is nothing to audit for cheating — the architecture test that keeps
// truth internal to OGSim.Information protects the player BY CONSTRUCTION.
//
// It costs almost nothing (rivals are few, their belief sets coarse) and it
// makes `rival.result` events real: when a rival drills a dry hole, they drilled
// it on a belief that was wrong, exactly as the player would have.
//
// Their TECHNOLOGY is deterministic diffusion, not dice: a rival acquires each
// node at (era start + their techLag). The player races real clocks and can
// learn a rival's pace — which is a strategy — rather than guessing at draws,
// which is not.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>Content personality, per rival (SDD-011 §2.1).</summary>
public sealed record RivalPersonality(
    ContentId Id,
    double Aggressiveness,        // a — the bid multiplier on their own EMV
    int TechLagTicks,             // deterministic diffusion delay
    Money BudgetPerRound);

/// <summary>One sealed bid.</summary>
public sealed record Bid(EntityId<ICompany> Company, ContentId Block, Money Amount);

/// <summary>Marker for a company id — the player and every rival share it.</summary>
public interface ICompany { }

/// <summary>
/// SDD-011 §2.1's rival. Everything it decides, it decides from its OWN beliefs.
/// </summary>
public sealed class Rival
{
    private readonly RivalPersonality _personality;
    private readonly IBeliefStore _beliefs;
    private readonly IRandomStream _market;

    public Rival(
        EntityId<ICompany> id,
        RivalPersonality personality,
        IBeliefStore beliefs,
        IRandomStream marketStream)
    {
        ArgumentNullException.ThrowIfNull(personality);
        ArgumentNullException.ThrowIfNull(beliefs);
        ArgumentNullException.ThrowIfNull(marketStream);

        if (personality.Aggressiveness <= 0.0)
            throw new ModelFault("SDD-011 §2.1", null,
                $"rival {personality.Id.Value} has non-positive aggressiveness; it would " +
                "never bid at all, which is a rival that need not exist");

        if (personality.TechLagTicks < 0)
            throw new ModelFault("SDD-011 §2.1", null,
                $"rival {personality.Id.Value} has a negative tech lag; a rival cannot " +
                "acquire a technology before its era begins");

        Id = id;
        _personality = personality;
        _beliefs = beliefs;
        _market = marketStream;
    }

    public EntityId<ICompany> Id { get; }

    public ContentId Personality => _personality.Id;

    /// <summary>
    /// SDD-011 §2.1's TD2 diffusion. Deterministic — no draws.
    ///
    /// <para>Leaders have small lags and laggards large ones, so the player
    /// races real clocks. A dice-based acquisition would make a rival's
    /// capability unknowable rather than merely unknown, and there would be
    /// nothing to learn by watching them.</para>
    /// </summary>
    public bool HasTechnologyAt(Tick eraStart, Tick now) =>
        now.Value >= eraStart.Value + _personality.TechLagTicks;

    /// <summary>
    /// SDD-011 §2.1's bid: <c>EMV · a · U</c>, with <c>U ~ Uniform(0.8, 1.2)</c>
    /// from the `market` stream.
    ///
    /// <para>The EMV comes from THEIR beliefs about the block — which is the
    /// whole architectural point. A rival that read truth would bid correctly
    /// every time, and the player would be competing against an oracle.</para>
    ///
    /// <para>Returns null when they know nothing about the block. A rival does
    /// not bid on acreage it has never looked at, any more than the player
    /// would.</para>
    /// </summary>
    public Bid? BidFor(
        ContentId block,
        EntityRef subject,
        ContentId valueKind,
        Func<double, Money> valueOf)
    {
        ArgumentNullException.ThrowIfNull(valueOf);

        if (_beliefs.Get(subject, valueKind) is not Belief belief) return null;

        // Their P50, not the truth and not their P10 — a rival bidding its
        // upside would systematically overpay, which is a personality rather
        // than a default.
        Money expected = valueOf(Median(belief));

        double jitter = 0.8 + _market.NextUnit() * 0.4;
        Money amount = Money.RoundHalfEven(
            expected.Cents * _personality.Aggressiveness * jitter);

        if (amount.Cents <= 0) return null;
        if (amount.Cents > _personality.BudgetPerRound.Cents)
            amount = _personality.BudgetPerRound;

        return new Bid(Id, block, amount);
    }

    /// <summary>The belief's median, in linear units.</summary>
    private static double Median(Belief belief) =>
        belief.Space == BeliefSpace.Log ? DetMath.Exp(belief.Mu) : belief.Mu;
}

/// <summary>
/// SDD-011 §1's licence round: sealed-bid, highest wins, ties by lowest company
/// id.
///
/// <para>Sealed rather than open-outcry because an open auction would need a
/// bidding AI with a theory of mind about the player, and the design's rival
/// policy is deliberately simple. Ties break on id so the outcome is TOTAL —
/// two identical bids must not depend on enumeration order.</para>
/// </summary>
public static class LicenceRound
{
    public sealed record Award(ContentId Block, EntityId<ICompany> Winner, Money Price);

    /// <summary>
    /// Resolves one block. Returns null when nobody bid — an unbid block stays
    /// open, which is a real and common outcome and not a failure.
    /// </summary>
    public static Award? Resolve(ContentId block, IReadOnlyList<Bid> bids)
    {
        ArgumentNullException.ThrowIfNull(bids);

        Bid? best = null;

        for (int i = 0; i < bids.Count; i++)
        {
            Bid bid = bids[i];
            if (bid.Block != block) continue;

            if (best is null
                || bid.Amount.Cents > best.Amount.Cents
                || (bid.Amount.Cents == best.Amount.Cents
                    && bid.Company.Value < best.Company.Value))
                best = bid;
        }

        return best is null ? null : new Award(block, best.Company, best.Amount);
    }
}

/// <summary>
/// SDD-011 §3. A rival's completed work publishes an observation available to
/// everyone — industry disclosure.
///
/// <para>Same machinery, extra sigma: <b>you read their press release, not their
/// logs</b>. It updates the player's beliefs through the identical conjugate
/// path, so R16-V5 needs no new mechanism at all — which is the strongest
/// evidence that the observation door was the right shape.</para>
/// </summary>
public static class PublicDisclosure
{
    /// <summary>
    /// Widens an observation for public release.
    ///
    /// <para>Precision, not sigma, is what adds — so the widened sigma is
    /// <c>sqrt(σ² + extra²)</c> rather than <c>σ + extra</c>. Adding sigmas
    /// would make two disclosures of the same well worse than one, which is
    /// backwards.</para>
    /// </summary>
    public static Observation Publish(Observation theirs, double extraSigma)
    {
        ArgumentNullException.ThrowIfNull(theirs);

        if (extraSigma < 0.0)
            throw new ModelFault("SDD-011 §3", null,
                "public disclosure cannot SHARPEN an observation; a press release is " +
                "never better than the log it describes");

        double widened = DetMath.Sqrt(theirs.Sigma * theirs.Sigma + extraSigma * extraSigma);

        return theirs with { Sigma = widened };
    }
}
