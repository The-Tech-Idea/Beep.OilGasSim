// R20c.9.2 — the six ladders, built from content (SDD-004 §6's R20c.9 amendment).
//
// THE HALF THAT MATTERS. R20c.9.1 wrote the sheets and nothing read them, which
// is the shape findings 164–177 are all versions of: a mechanism built to
// specification and joined to nothing. Until this file existed, design 03's
// eleventh non-negotiable — "rebalancing is a content edit" — was false of the
// only equipment the game shipped, because moving a separator's capacity meant
// editing an engine assembly.
//
// HERE RATHER THAN IN OGSim.Facilities, for the reason FacilitiesState gives:
// a tier is composition's content, and Layer 4 is the only layer entitled to
// name a concrete type (design 03 §2).

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// What a company can buy, in the order it climbs.
///
/// <para>Each list is its kind's definitions sorted by <c>rung</c> — NOT by id,
/// which is what <see cref="ICatalog{TDef}.All"/> gives: <c>gas-plant-e1</c>
/// sorts before <c>gas-plant-none</c>, so a ladder read off catalogue order
/// would let a player install a plant by buying nothing.</para>
///
/// <para>Provided as a contract rather than reached for as a static, because a
/// static would have to load from somewhere and law L2 gives no dependency a
/// default. A module that fits equipment requires this; so does the state owner
/// that restores a tier by name.</para>
/// </summary>
public sealed record FacilityLadders(
    IReadOnlyList<Facilities.SeparatorTier> Separator,
    IReadOnlyList<Facilities.TankTier> Tank,
    IReadOnlyList<Facilities.TreaterTier> Treater,
    IReadOnlyList<Facilities.GasPlantTier> GasPlant,
    IReadOnlyList<Facilities.ExportTier> Export,
    IReadOnlyList<Facilities.ManifoldTier> Manifold,

    /// <summary>
    /// When each rung was invented, by tier id (SDD-005 §2's R20d.10b
    /// amendment).
    ///
    /// <para>Kept BESIDE the ladders rather than on the tiers, because a
    /// <c>SeparatorTier</c> is what the flow model needs to solve a vessel and an
    /// era is not part of that: putting it on the tier would push a purchasing
    /// fact into the physics. The definitions own both and this is the one map
    /// between them.</para>
    ///
    /// <para>Read by key only. Rule D-5 forbids ENUMERATING a dictionary, which
    /// this never does — the ladders carry the order.</para>
    /// </summary>
    IReadOnlyDictionary<ContentId, Era> InventedIn)
{
    // Finding 131: the compiler's record equality is REFERENCE equality for a
    // collection member, so two ladder sets loaded from the same content would
    // compare unequal. Six lists, six comparisons — and the architecture suite
    // caught this record on the run it was written, which is the rule doing
    // exactly what it exists for.
    public bool Equals(FacilityLadders? other) =>
        other is not null
        && Structural.Equal(Separator, other.Separator)
        && Structural.Equal(Tank, other.Tank)
        && Structural.Equal(Treater, other.Treater)
        && Structural.Equal(GasPlant, other.GasPlant)
        && Structural.Equal(Export, other.Export)
        && Structural.Equal(Manifold, other.Manifold)
        && Structural.Equal(InventedIn, other.InventedIn);

    public override int GetHashCode() =>
        HashCode.Combine(
            Structural.HashOf(Separator), Structural.HashOf(Tank),
            Structural.HashOf(Treater), Structural.HashOf(GasPlant),
            Structural.HashOf(Export), Structural.HashOf(Manifold),
            Structural.HashOf(InventedIn));

    /// <summary>
    /// The catalogues as ladders (SDD-004 §6's R20c.9 amendment).
    ///
    /// <para>A missing kind is a REFUSAL and not an empty ladder: every socket on
    /// this chain is occupied from the first tick — the field starts with a
    /// separator, a tank and no gas plant — so a kind with no rung 0 is content
    /// that cannot build the field it describes.</para>
    /// </summary>
    public static FacilityLadders From(ICatalogSet catalogues)
    {
        ArgumentNullException.ThrowIfNull(catalogues);

        var invented = new Dictionary<ContentId, Era>();

        Note<SeparatorDefinition>(catalogues, invented);
        Note<TankDefinition>(catalogues, invented);
        Note<TreaterDefinition>(catalogues, invented);
        Note<GasPlantDefinition>(catalogues, invented);
        Note<ExportLineDefinition>(catalogues, invented);
        Note<ManifoldDefinition>(catalogues, invented);

        return new FacilityLadders(
            Rungs<SeparatorDefinition, Facilities.SeparatorTier>(catalogues, "separator", d =>
                new Facilities.SeparatorTier(
                    d.Id,
                    new MassRate(d.GasCapacityKgPerSecond),
                    new MassRate(d.LiquidCapacityKgPerSecond),
                    new ReservoirVolume(d.VolumeCubicMetres),
                    new SeparationEfficiency(
                        d.LiquidFromGas, d.GasFromLiquid, d.WaterFromLiquid, d.WaterIntoLiquid),
                    new ReservoirRate(d.DesignRateCubicMetresPerSecond),
                    new Pressure(d.OperatingPressurePascals))),

            Rungs<TankDefinition, Facilities.TankTier>(catalogues, "tank", d =>
                new Facilities.TankTier(d.Id, new Mass(d.CapacityKilograms), d.VapourLossRatePerTick)),

            Rungs<TreaterDefinition, Facilities.TreaterTier>(catalogues, "treater", d =>
                new Facilities.TreaterTier(d.Id, d.WaterRemoved)),

            Rungs<GasPlantDefinition, Facilities.GasPlantTier>(catalogues, "gas-plant", d =>
                new Facilities.GasPlantTier(d.Id, new MassRate(d.CapacityKgPerSecond))),

            Rungs<ExportLineDefinition, Facilities.ExportTier>(catalogues, "export-line", d =>
                new Facilities.ExportTier(d.Id, new MassRate(d.OfftakeKgPerSecond))),

            Rungs<ManifoldDefinition, Facilities.ManifoldTier>(catalogues, "manifold", d =>
                new Facilities.ManifoldTier(d.Id, d.Slots)),

            invented);
    }

    /// <summary>One kind's eras into the shared map.</summary>
    private static void Note<TDefinition>(
        ICatalogSet catalogues, Dictionary<ContentId, Era> invented)
        where TDefinition : FacilityUnitDefinition
    {
        IReadOnlyList<TDefinition> all = catalogues.Of<TDefinition>().All;

        for (int i = 0; i < all.Count; i++) invented[all[i].Id] = all[i].AvailableFromEra;
    }

    /// <summary>
    /// Whether a rung can be bought yet (SDD-005 §2's R20d.10b amendment).
    ///
    /// <para>A CALENDAR comparison and not a <c>Requirements</c> check: an era
    /// that has not arrived is not something a company can go and get, so it is
    /// not a missing item the gating validator could name a remedy for.</para>
    /// </summary>
    public bool InventedBy(ContentId tier, Era era) => InventedIn[tier] <= era;

    /// <summary>Why not, in words a player can act on — the era and the year,
    /// per R17 §2.6b.</summary>
    public RejectionReason NotYet(ContentId tier, Era now, Capabilities.EraCalendar calendar) =>
        new("$loc:reject.not-yet-invented",
            $"'{tier.Value}' is {InventedIn[tier]} equipment and the year is {now}; " +
            $"it can be built from {calendar.FirstYearOf(InventedIn[tier])}");

    /// <summary>
    /// One kind's definitions as a ladder: sorted by rung, checked dense.
    ///
    /// <para>DENSE FROM ZERO is checked here and not only in the content's own
    /// consistency pass, because a gap is a statement about a SET and a
    /// per-definition validator sees one definition at a time. Two definitions
    /// claiming rung 1 would otherwise resolve by sort stability, and which one a
    /// player climbed to would depend on the order the files were read.</para>
    /// </summary>
    private static IReadOnlyList<TTier> Rungs<TDefinition, TTier>(
        ICatalogSet catalogues, string kind, Func<TDefinition, TTier> tier)
        where TDefinition : FacilityUnitDefinition
    {
        IReadOnlyList<TDefinition> all = catalogues.Of<TDefinition>().All;

        if (all.Count == 0)
            throw new ContentFault("SDD-004 §6", null,
                $"no '{kind}' is defined; every socket on this chain is occupied " +
                "from the first tick, so a kind with no rungs cannot build the field");

        var ordered = new List<TDefinition>(all);
        ordered.Sort((a, b) => a.Rung.CompareTo(b.Rung));

        var ladder = new List<TTier>(ordered.Count);

        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Rung != i)
                throw new ContentFault("SDD-004 §6", null,
                    $"the '{kind}' ladder jumps to rung {ordered[i].Rung} at position {i} " +
                    $"('{ordered[i].Id.Value}'); rungs are dense from 0, since an install " +
                    "climbs one at a time and a repeat would resolve by file order");

            ladder.Add(tier(ordered[i]));
        }

        return ladder;
    }
}
