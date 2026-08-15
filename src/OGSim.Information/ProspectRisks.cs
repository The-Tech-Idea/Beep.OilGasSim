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
public sealed class ProspectRisks : IStateOwner
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

    // ------------------------------------------------ the save (SDD-008 §4b)
    //
    // WHAT A PLAY BELIEVES IS THE CAMPAIGN. Every well drilled, every survey
    // shot, every dry hole that killed a source rock is in these Betas, and none
    // of it is recomputable — a reload that dropped them handed the company back
    // its opening conviction and re-priced every prospect it had spent years
    // learning about (finding 198).
    //
    // Ordinary members rather than explicit ones, unlike `BeliefStore`: there is
    // no wall here. `Observe` is not a door truth could come through, so
    // narrowing the surface would buy nothing and cost the symmetry with every
    // other owner in the engine.

    public StateKey Key { get; } = new("information.prospect-risk");

    public int SchemaVersion => 1;

    /// <summary>Nothing has to be back before this is (SDD-013 §2b).</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        IReadOnlyList<ContentId> plays = PlayOrder();

        writer.WriteInt64("play-count", plays.Count);

        for (int i = 0; i < plays.Count; i++)
        {
            string at = "play." + Index(i);
            ProspectRisk play = _plays[plays[i]];

            writer.WriteString(at + "id", plays[i].Value);

            // All five, because a play object shares nothing and holds its own.
            // Only three are ever read through from a prospect, but which three
            // is §4's business rather than this block's, and writing the two the
            // prospects happen not to use would make this format depend on a rule
            // stated somewhere else.
            for (int f = 0; f < ProspectRisk.Declared.Count; f++)
                Write(writer, at, ProspectRisk.Declared[f], play[ProspectRisk.Declared[f]]);
        }

        writer.WriteInt64("prospect-count", _order.Count);

        for (int i = 0; i < _order.Count; i++)
        {
            string at = "prospect." + Index(i);
            EntityRef prospect = _order[i];

            writer.WriteInt64(at + "subject-kind", (long)prospect.Kind);
            writer.WriteInt64(at + "subject-id", (long)prospect.Value);
            writer.WriteString(at + "play", _playOf[prospect].Value);

            // TRAP AND TIMING ONLY. Source, reservoir and seal are the PLAY's and
            // a prospect reads through to them (§4) — writing a prospect's copy
            // would restore the stale duplicate `ShareFrom` was changed from a
            // copy to a bind to prevent (law L5).
            ProspectRisk risk = _prospects[prospect];

            Write(writer, at, PosFactor.Trap, risk[PosFactor.Trap]);
            Write(writer, at, PosFactor.Timing, risk[PosFactor.Timing]);
        }
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        // REPLACES, for the reason SDD-008 §4b.1 gives about beliefs: a Beta is
        // updated in place, so nothing distinguishes what generation registered
        // from what the drilling changed about it.
        _plays.Clear();
        _prospects.Clear();
        _playOf.Clear();
        _order.Clear();

        long playCount = reader.ReadInt64("play-count");

        for (long i = 0; i < playCount; i++)
        {
            string at = "play." + Index(i);
            var id = new ContentId(reader.ReadString(at + "id"));

            var play = new ProspectRisk(_prior);

            for (int f = 0; f < ProspectRisk.Declared.Count; f++)
                play.RestoreFactor(ProspectRisk.Declared[f], Read(reader, at, ProspectRisk.Declared[f]));

            if (!_plays.TryAdd(id, play))
                throw new SaveDataFault("SDD-008 §4b.3", null,
                    $"the save names play '{id.Value}' twice; the second would silently " +
                    "become what every prospect on it believes");
        }

        long prospectCount = reader.ReadInt64("prospect-count");

        for (long i = 0; i < prospectCount; i++)
        {
            string at = "prospect." + Index(i);

            var prospect = new EntityRef(
                Kind(reader.ReadInt64(at + "subject-kind")),
                (ulong)reader.ReadInt64(at + "subject-id"));

            var playId = new ContentId(reader.ReadString(at + "play"));

            if (!_plays.TryGetValue(playId, out ProspectRisk? shared))
                throw new SaveDataFault("SDD-008 §4b.3", prospect,
                    $"prospect {prospect.Value} draws on play '{playId.Value}', which the save " +
                    "does not carry; restoring it against a fresh prior would give the company " +
                    "back a conviction it had already spent wells revising");

            var risk = new ProspectRisk(_prior);

            // BOUND before the owned factors are written, and in the same three
            // places `Register` binds them — so a restored prospect reads through
            // to the play exactly as a registered one does, rather than holding a
            // snapshot that stops moving when the play does.
            risk.ShareFrom(shared, PosFactor.Source);
            risk.ShareFrom(shared, PosFactor.Reservoir);
            risk.ShareFrom(shared, PosFactor.Seal);

            // No `Weigh` here: the trap confidence a prospect was mapped with is
            // already inside the α and β being restored, and applying it a second
            // time would pull the mean down twice.
            risk.RestoreFactor(PosFactor.Trap, Read(reader, at, PosFactor.Trap));
            risk.RestoreFactor(PosFactor.Timing, Read(reader, at, PosFactor.Timing));

            if (!_prospects.TryAdd(prospect, risk))
                throw new SaveDataFault("SDD-008 §4b.3", prospect,
                    $"the save names prospect {prospect.Value} twice; two risk assessments " +
                    "of one structure have no correct combination, and the second would " +
                    "silently become the one every drilling decision reads");

            _playOf.Add(prospect, playId);
            _order.Add(prospect);
        }
    }

    /// <summary>
    /// The plays, in the order their first prospect registered — derived rather
    /// than kept, because a second list holding the same key set is how two
    /// collections come to disagree about it. The dictionary itself is never
    /// enumerated (rule D-5).
    /// </summary>
    private IReadOnlyList<ContentId> PlayOrder()
    {
        var seen = new HashSet<ContentId>();
        var plays = new List<ContentId>();

        for (int i = 0; i < _order.Count; i++)
        {
            ContentId play = _playOf[_order[i]];

            // Membership asked, never walked.
            if (seen.Add(play)) plays.Add(play);
        }

        return plays;
    }

    private static void Write(IStateWriter writer, string at, PosFactor factor, FactorBelief belief)
    {
        writer.WriteDouble(at + Name(factor) + "-alpha", belief.Alpha);
        writer.WriteDouble(at + Name(factor) + "-beta", belief.Beta);
    }

    private static FactorBelief Read(IStateReader reader, string at, PosFactor factor) =>
        new(reader.ReadDouble(at + Name(factor) + "-alpha"),
            reader.ReadDouble(at + Name(factor) + "-beta"));

    /// <summary>
    /// The factor's NAME rather than its ordinal. A block keyed by ordinal would
    /// reshuffle the moment a factor were inserted into <see cref="PosFactor"/>,
    /// and would do it silently — every key still present, every Beta under the
    /// wrong one.
    /// </summary>
    private static string Name(PosFactor factor) => factor switch
    {
        PosFactor.Source => "source",
        PosFactor.Reservoir => "reservoir",
        PosFactor.Seal => "seal",
        PosFactor.Trap => "trap",
        PosFactor.Timing => "timing",
        _ => throw new InvariantFault("SDD-008 §4b.3", null,
            $"factor {factor} has no name in the save format"),
    };

    private static EntityKind Kind(long value) =>
        Enum.IsDefined((EntityKind)value)
            ? (EntityKind)value
            : throw new SaveDataFault("SDD-008 §4b.3", null,
                $"the save names entity kind {value}, which this build does not declare");

    private static string Index(long index) =>
        index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".";
}
