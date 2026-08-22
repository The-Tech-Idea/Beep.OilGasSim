// R16.4 — rivals explore, and their results become public (SDD-011 §2/§2.1/
// §3's finding-277 amendment).
//
// EPHEMERAL, LIKE THE HEDGE OR INSURANCE, NOT LIKE THE FIELD. A rival's own
// beliefs are never owned or persisted — `BeliefStore`'s own `IStateOwner`
// key is a hardcoded literal, so a second instance cannot be `Own`'d
// without a shipped SDD-008 class changing its own key shape, which this
// finding does not attempt. Rivals are rebuilt deterministically from truth
// every time the world is (re)generated — a brand-new game and every reload
// alike, since `SaveGame.Load` re-runs the SAME `IWorldGenerator.Generate`
// call `EngineBuilder.CreateNew` does, with the same saved parameters.
//
// NEVER THE PLAYER'S OWN FIELD. A rival explores OTHER prospects in the same
// basin; the player's one licence stays exactly as unconditionally granted
// as it always has been (SDD-011 §1's own R20d.9 amendment).

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// What the roster currently is, and which prospects a rival has already
/// resolved. COMPOSED EMPTY, SEALED after world generation — the same
/// "empty then filled" shape `WorldState`/`WeatherState`'s own
/// `SealGeneration` already has, because a rival needs prospects to explore
/// and nothing has generated any until that instant (SDD-010 §4). A
/// hand-built scenario that never generates (every `EngineBuilder.Build`-only
/// fixture) stays sealed empty forever — zero rivals, a safe default.
/// </summary>
internal sealed class RivalRoster : IStateOwner
{
    public IReadOnlyList<Rival> Rivals { get; private set; } = [];

    // A LIST, not only a HashSet (D-5): capture walks this in the order
    // prospects were resolved, which must be the same order on every run of
    // the same save for PV1's own byte-for-byte guarantee. Membership is a
    // linear scan — the prospect count in this composition is small, and a
    // second HashSet kept in step for one lookup would be a second owner of
    // the same fact.
    private readonly List<EntityId<IProspect>> _explored = [];

    public bool HasExplored(EntityId<IProspect> prospect) => _explored.Contains(prospect);

    public void MarkExplored(EntityId<IProspect> prospect) => _explored.Add(prospect);

    /// <summary>Replaces the roster wholesale — safe because nothing has
    /// ticked, been captured or been restored between composition and this
    /// call, the same guarantee `WeatherState.SealGeneration` relies on.
    /// </summary>
    public void SealRoster(IReadOnlyList<Rival> rivals)
    {
        ArgumentNullException.ThrowIfNull(rivals);

        Rivals = rivals;
    }

    // ------------------------------------------------------- SDD-013 §4

    public StateKey Key { get; } = new("company.rivals");

    public int SchemaVersion => 1;

    /// <summary>Nothing this owner restores depends on another owner's
    /// facts — the resolved-prospect list is this owner's own.</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("explored-count", _explored.Count);

        for (int i = 0; i < _explored.Count; i++)
            writer.WriteInt64(
                "explored." + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture),
                (long)_explored[i].Value);
    }

    /// <summary>The roster itself is NOT restored here — `Rivals` is rebuilt
    /// fresh by <see cref="EngineBuilder.SealRivals"/>, called the same
    /// instant world generation itself is re-run for a reload. Only which
    /// prospects already carry a result is this owner's own history.
    /// </summary>
    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _explored.Clear();

        long count = reader.ReadInt64("explored-count");

        for (long i = 0; i < count; i++)
            _explored.Add(new EntityId<IProspect>((ulong)reader.ReadInt64(
                "explored." + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture))));
    }
}

/// <summary>
/// SDD-011 §2.1/§3's finding-277 amendment. On a fixed cadence, each rival
/// ranks not-yet-explored prospects by `Rival.BidFor`'s own shipped formula
/// (used exactly as it ships — not extended to §2.1's fuller POS-weighted
/// EMV) and attempts its top choice: reads TRUE oil-in-place for that
/// prospect, resolves INSTANTLY (no rig-scheduled multi-month activity — a
/// stated simplification matching R16's own "not full AI operators, at a
/// fraction of the cost" framing), and the result becomes public data,
/// widened through `PublicDisclosure` and applied to the PLAYER's real
/// beliefs through the same door every other observation already uses.
/// </summary>
internal sealed class RivalExplorationStage(
    RivalRoster roster,
    WorldState world,
    OGSim.Subsurface.SubsurfaceState subsurface,
    IBeliefStore playerBeliefs,
    IAuditTrail audit) : ITickStage
{
    public StageId Id => StageId.Company;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (roster.Rivals.Count == 0) return;
        if (context.Tick.Value % Defaults.RivalExplorationCadenceTicks != 0) return;

        for (int i = 0; i < roster.Rivals.Count; i++)
            Explore(roster.Rivals[i], context.Tick);
    }

    private void Explore(Rival rival, Tick tick)
    {
        IReadOnlyList<EntityId<IProspect>> prospects = world.Prospects;

        EntityId<IProspect>? best = null;
        Money bestAmount = Money.Zero;

        for (int i = 0; i < prospects.Count; i++)
        {
            EntityId<IProspect> prospect = prospects[i];
            if (roster.HasExplored(prospect)) continue;

            Bid? bid = rival.BidFor(
                new ContentId("prospect-" + prospect.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
                new EntityRef(EntityKind.Prospect, prospect.Value),
                Defaults.StructureCapacityKind,
                capacityM3 => Money.RoundHalfEven(
                    capacityM3 * Defaults.RivalValueRecoveryFactor * Defaults.Netback.Cents));

            if (bid is null) continue;

            if (best is null || bid.Amount.Cents > bestAmount.Cents)
            {
                best = prospect;
                bestAmount = bid.Amount;
            }
        }

        if (best is EntityId<IProspect> chosen) Resolve(rival, chosen, tick);
    }

    private void Resolve(Rival rival, EntityId<IProspect> prospect, Tick tick)
    {
        EntityId<IReservoirCompartmentEntity>? compartment = world.Beneath(prospect);

        // A DRY hole is real, informative news — design 06 §6.2's own "a
        // rival's dry hole in your play is free information" — floored
        // rather than skipped so it still publishes.
        double trueValue = compartment is EntityId<IReservoirCompartmentEntity> c
            ? System.Math.Max(
                subsurface.TrueOilInPlaceOf(c).CubicMetres, Defaults.RivalMinimumCapacityCubicMetres)
            : Defaults.RivalMinimumCapacityCubicMetres;

        // ITS OWN KIND, keyed to the compartment DIRECTLY for a real
        // discovery (bypassing the prospect, and so bypassing `ReKey`
        // entirely — the compartment already exists in truth the instant a
        // rival's own attempt confirms it), or to the prospect itself for a
        // dry result (no compartment exists, and a dry outcome never
        // triggers `DrillWell.cs`'s own re-key either, so nothing ever
        // migrates this belief onto anything else). Neither oil-in-place
        // nor structure-capacity is safe here: `DrillWell.cs`'s own
        // `IBeliefStore.ReKey` migrates BOTH from a prospect onto its
        // compartment the moment the PLAYER drills it, and ReKey refuses to
        // MERGE two beliefs about one kind (SDD-008 §4) — a real collision
        // this amendment's own first draft found the hard way, against a
        // full slow-suite run.
        EntityRef subject = compartment is EntityId<IReservoirCompartmentEntity> found
            ? new EntityRef(EntityKind.Compartment, found.Value)
            : new EntityRef(EntityKind.Prospect, prospect.Value);

        var raw = new Observation(
            subject,
            Defaults.RivalDisclosureKind,
            DetMath.Ln(trueValue),
            Defaults.RivalDiscoverySigma,
            BeliefSpace.Log,
            Provenance.WellTest);

        Observation widened = PublicDisclosure.Publish(raw, Defaults.RivalDisclosureExtraSigma);

        playerBeliefs.Apply(widened);
        roster.MarkExplored(prospect);

        audit.Record(
            AuditCategory.StateTransition, subject: new EntityRef(EntityKind.Prospect, prospect.Value),
            cause: null,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["kind"] = new("rival.result"),
                ["rival"] = new(rival.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ["discovery"] = new((compartment is not null).ToString()),
                ["tick"] = new(tick.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            });
    }
}
