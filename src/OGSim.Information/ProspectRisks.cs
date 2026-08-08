// R20d.7 — probability of success, per prospect (SDD-008 §4, design 06 §2.1–2.2).
//
// POS HAD NO SUBJECT UNTIL THE WORLD MADE ONE. `ProspectRisk` was built,
// unit-tested and consumed by nothing for four phases, because a probability of
// success is a statement ABOUT A PROSPECT and nothing generated prospects. Now
// that a basin produces dozens, the question a player actually plays — which of
// these do I put the rig on? — is answerable, and this is what answers it.
//
// FIVE FACTORS, THREE OF THEM THE PLAY'S. Source, reservoir and seal belong to
// the play; trap and timing belong to the structure. That split is the whole
// reason exploration is a campaign rather than a series of independent bets: a
// dry hole that failed on source rock moves every prospect drawing on that
// source, and the player learns something they did not pay for directly.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Information;

/// <summary>
/// Who is exposed to which play, and what the company currently believes about
/// each (SDD-008 §4).
/// </summary>
public sealed class ProspectRisks
{
    // Keyed lookups only — never enumerated (rule D-5). The registration order
    // is kept separately so a projection walks prospects identically every run.
    private readonly Dictionary<ContentId, ProspectRisk> _plays = [];
    private readonly Dictionary<EntityRef, ProspectRisk> _prospects = [];
    private readonly Dictionary<EntityRef, ContentId> _playOf = [];
    private readonly List<EntityRef> _order = [];

    private readonly FactorBelief _prior;

    /// <summary>
    /// <paramref name="prior"/> is the company's starting conviction, and its
    /// MAGNITUDE is the interesting half: α+β is how many wells' worth of
    /// evidence the opening view is worth, so a thin prior moves fast on the
    /// first result and a heavy one barely notices it.
    /// </summary>
    public ProspectRisks(FactorBelief prior) => _prior = prior;

    /// <summary>Prospects in registration order.</summary>
    public IReadOnlyList<EntityRef> Known => _order;

    /// <summary>
    /// Puts a prospect on the map with the play it belongs to.
    ///
    /// <para><paramref name="trapConfidence"/> scales the TRAP factor alone,
    /// because how confidently a structure is mapped is a fact about that
    /// structure — a subtle stratigraphic trap and an obvious four-way dome are
    /// not equally likely to be there, and it would be the same play saying so
    /// either way.</para>
    /// </summary>
    public void Register(EntityRef prospect, ContentId play, double trapConfidence)
    {
        if (trapConfidence is <= 0.0 or >= 1.0)
            throw new ModelFault("SDD-008 §4", prospect,
                "trap confidence is strictly inside (0, 1); a structure nobody believes in " +
                "is not a prospect, and one nobody could ever doubt is not a risk");

        if (!_plays.TryGetValue(play, out ProspectRisk? shared))
        {
            shared = new ProspectRisk(_prior);
            _plays.Add(play, shared);
        }

        var risk = new ProspectRisk(_prior);

        risk.ShareFrom(shared, PosFactor.Source);
        risk.ShareFrom(shared, PosFactor.Reservoir);
        risk.ShareFrom(shared, PosFactor.Seal);

        // A less confidently mapped trap: the same mean pulled down by moving
        // belief from α to β, leaving the total evidence unchanged. Drawing a
        // thinner prior instead would say "we are unsure how sure we are",
        // which is a different and much weaker claim.
        risk.Weigh(PosFactor.Trap, trapConfidence);

        _prospects.Add(prospect, risk);
        _playOf.Add(prospect, play);
        _order.Add(prospect);
    }

    public ProspectRisk Of(EntityRef prospect) => _prospects[prospect];

    /// <summary>Which play a prospect draws on — what a company reads to see
    /// that two of its prospects rise and fall together.</summary>
    public ContentId PlayOf(EntityRef prospect) => _playOf[prospect];

    public bool Knows(EntityRef prospect) => _prospects.ContainsKey(prospect);

    /// <summary>
    /// What a well proved or disproved. Shared elements land on the play, so one
    /// hole re-prices every prospect exposed to it (SDD-008 §4).
    /// </summary>
    public void Drilled(EntityRef prospect, PosFactor factor, bool present) =>
        Learned(prospect, factor, present, weight: 1.0);

    /// <summary>
    /// Evidence of a stated strength (SDD-008 §4). A well is hard evidence; a
    /// survey images rather than proves, and says so by weighing less.
    /// </summary>
    public void Learned(EntityRef prospect, PosFactor factor, bool present, double weight)
    {
        if (!_prospects.TryGetValue(prospect, out ProspectRisk? risk))
            throw new InvariantFault("SDD-008 §4", prospect,
                $"a well reported on prospect {prospect.Value}, which carries no risk " +
                "assessment; every prospect is registered when the world places it");

        risk.Observe(factor, present, weight);
    }
}
