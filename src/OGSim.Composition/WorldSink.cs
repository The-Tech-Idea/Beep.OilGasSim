// R20d.8 — the generated world reaches the engine (SDD-010 §4).
//
// THE GENERATOR HAS EXISTED AND NEVER RUN. `BasinWorldGenerator` draws traps,
// charges some and leaves others empty, sizes accumulations log-normally and
// derives depth, pressure, temperature and access from where each one sits — and
// the only thing that ever called it was its own test. `IWorldSink` had exactly
// one implementation in the repository and it was a recording double.
//
// So a game did not begin by finding out what was under it. Compartments were
// hand-built by whoever composed the engine, every run got the same field, and
// the whole exploration half of the design — subtlety classes, regional data,
// probability of success — had nothing to be about.
//
// THIS IS THE DOOR, and it is a narrow one on purpose (SDD-010 §4). The
// generator emits VALUES; this builds truth from them. It never sees a module
// store, which is what keeps the generator a replaceable slot (03 §3.2:
// procedural ↔ handcrafted scenario ↔ replay) without opening the truth wall.
//
// AND BELIEFS COME THROUGH THE SAME DOOR AS EVERYTHING ELSE (R15-V10). Starting
// knowledge is applied as an Observation, not copied from what was generated —
// so a new game's beliefs are as wrong as regional gravity and magnetics
// actually are, and the first survey is worth buying.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// THE WORLD THE ENGINE IS PLAYING IN — composed empty and filled once, by
/// generation, before the first tick.
///
/// <para>It is a separate object from the sink that fills it because two very
/// different things need it and one of them cannot wait for the other: the sink
/// writes truth into module stores as it goes, while the FIELD reads a
/// prospect's position every time a well is tied in. Folding both into one type
/// would make the module that owns the field depend on the module that owns
/// generation and the reverse at the same time.</para>
/// </summary>
public sealed class WorldState : IStateOwner
{
    private readonly List<EntityId<IProspect>> _prospects = [];
    private readonly List<Coordinate> _at = [];

    // The compartment behind a prospect, for the ones charge reached. A prospect
    // missing from this is a DRY STRUCTURE: real, mappable, drillable, and with
    // nothing under it (SDD-010 §4b). Truth — a caller outside composition
    // cannot see it, and drilling is the only thing that asks.
    private readonly Dictionary<EntityId<IProspect>,
                                EntityId<IReservoirCompartmentEntity>> _found = [];
    private readonly List<ClimateRegion> _climate = [];
    private readonly List<Jurisdiction> _jurisdictions = [];

    private IReadOnlyList<Harbour> _harbours = [];
    private GeneratedSurface? _surface;

    /// <summary>
    /// The renderable world, or null before generation has run — the same
    /// answer, for the same reason, as a read model before the first tick: a
    /// game that has not been created has no map, and an empty one would be a
    /// lie about a world that was never drawn.
    /// </summary>
    public WorldView? View => _surface is null ? null : new WorldView(
        _surface.Terrain,
        _surface.Settlements,
        _surface.Transport,
        _surface.Harbours,
        _surface.LandStatus,
        _climate,
        _jurisdictions);

    /// <summary>
    /// The prospects, in the order they were generated. What a player has to
    /// choose between — and, once R20d.7's probability of success has a subject,
    /// what they are choosing on.
    /// </summary>
    public IReadOnlyList<EntityId<IProspect>> Prospects => _prospects;

    /// <summary>How many compartments generation built, so the subject the
    /// generator assumed can be checked rather than trusted.</summary>
    public int Count => _prospects.Count;

    /// <summary>Places a closed structure and hands back its identity. Every
    /// one the generator finds, charged or dry (SDD-010 §4b).</summary>
    internal EntityId<IProspect> Place(Coordinate at, ReservoirVolume capacity)
    {
        var prospect = new EntityId<IProspect>((ulong)(_prospects.Count + 1));

        _prospects.Add(prospect);
        _at.Add(at);
        _capacity.Add(capacity);

        return prospect;
    }

    private readonly List<ReservoirVolume> _capacity = [];

    /// <summary>
    /// What a structure could hold — TRUTH, and what a survey measures. Every
    /// closed high has one whether or not charge reached it, which is exactly
    /// why a survey can be shot at a dry prospect and cannot be used to ask
    /// whether there is oil (SDD-010 §4b).
    /// </summary>
    internal ReservoirVolume CapacityOf(EntityId<IProspect> prospect)
    {
        int index = _prospects.IndexOf(prospect);

        return index < 0
            ? throw new InvariantFault("SDD-010 §4b", null,
                $"prospect {prospect.Value} was never placed, so it has no structure and " +
                "nothing to measure")
            : _capacity[index];
    }

    /// <summary>
    /// A field a SCENARIO declares outright rather than one a world buried
    /// (SDD-010 §4b). It is already known to be there, so it is placed and found
    /// in one step and carries no exploration risk — there is nothing left to be
    /// wrong about, which is exactly what "here is your field, develop it"
    /// means.
    /// </summary>
    public EntityId<IProspect> DeclareKnownField(
        EntityId<IReservoirCompartmentEntity> compartment, ReservoirVolume capacity)
    {
        EntityId<IProspect> prospect = Place(default, capacity);

        Found(prospect, compartment);

        return prospect;
    }

    internal void Found(EntityId<IProspect> prospect, EntityId<IReservoirCompartmentEntity> in_)
    {
        _found.Add(prospect, in_);
        _structureOf.Add(in_, prospect);
    }

    private readonly Dictionary<EntityId<IReservoirCompartmentEntity>,
                                EntityId<IProspect>> _structureOf = [];

    /// <summary>
    /// How far a DISCOVERED field is from market — the same distance as the
    /// structure it was found in, asked the other way round because the surface
    /// chain is laid when a well is tied into a compartment and the compartment
    /// is what that code has in hand.
    /// </summary>
    /// <summary>
    /// Where the company built its header, set when the first field was
    /// developed and never moved (SDD-006 §1c). A manifold is a structure on the
    /// seabed or a pad on the ground; later fields reach it rather than it
    /// reaching them.
    /// </summary>
    internal void HeaderAt(Coordinate site) => _header ??= site;

    private Coordinate? _header;

    // ------------------------------------------------ the save (SDD-010 §4c)
    //
    // THE HEADER IS A DECISION, and the only part of this object that is
    // unambiguously one. Where a company built its manifold is not a function of
    // the world seed — it is where the first field it chose to develop happens
    // to be — and every later well's gathering line is as long as its field is
    // from it. Unsaved, a reloaded campaign re-sited the header at whichever
    // field it next tied in.
    //
    // THE PLACEMENTS ARE NOT HERE YET, and deliberately: §4c's boundary — the
    // count of prospects standing when generation finished — has to exist before
    // a save can tell a generated structure from a declared one, and guessing
    // which entries to replay would restore a world subtly unlike the one saved.
    // A generated world still cannot be reloaded (finding 195); this closes the
    // part that needs no seam.

    public StateKey Key { get; } = new("world.decisions");

    public int SchemaVersion => 1;

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // A FLAG rather than a sentinel coordinate: (0,0) is a legitimate place
        // to build, and picking any coordinate to mean "nowhere" would make that
        // spot unusable the day a basin puts a field there.
        writer.WriteInt64("has-header", _header is null ? 0L : 1L);

        if (_header is not Coordinate header) return;

        writer.WriteDouble("header-x", header.X);
        writer.WriteDouble("header-y", header.Y);
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (reader.ReadInt64("has-header") == 0L)
        {
            _header = null;
            return;
        }

        // Assigned directly rather than through HeaderAt, which is write-once by
        // design (`??=`): a restore is allowed to set what a game has already
        // decided, and going through the one-way door would silently keep
        // whatever a rebuild had put there first.
        _header = new Coordinate(
            reader.ReadDouble("header-x"), reader.ReadDouble("header-y"));
    }

    /// <summary>
    /// How far a field is from the header — the gathering line a well on it
    /// needs. Null until a header exists, and null for a compartment no world
    /// placed.
    /// </summary>
    public Length? DistanceToHeaderOf(EntityId<IReservoirCompartmentEntity> compartment)
    {
        if (_header is not Coordinate header) return null;

        if (!_structureOf.TryGetValue(compartment, out EntityId<IProspect> prospect))
            return null;

        Coordinate at = PositionOf(prospect);

        double dx = header.X - at.X;
        double dy = header.Y - at.Y;

        return new Length(DetMath.Sqrt((dx * dx) + (dy * dy)));
    }

    public Length? DistanceToMarketOf(EntityId<IReservoirCompartmentEntity> compartment) =>
        _structureOf.TryGetValue(compartment, out EntityId<IProspect> prospect)
            ? DistanceToMarket(prospect)
            : null;

    /// <summary>
    /// Which structure a known compartment belongs to. For a scenario that
    /// declared its field outright, this is what a drilling command aims at —
    /// the prospect, because that is what drilling targets whether or not
    /// anybody already knows what is under it.
    /// </summary>
    public EntityId<IProspect> ProspectFor(EntityId<IReservoirCompartmentEntity> compartment) =>
        _structureOf.TryGetValue(compartment, out EntityId<IProspect> prospect)
            ? prospect
            : throw new InvariantFault("SDD-010 §4b", null,
                $"compartment {compartment.Value} belongs to no structure; a field is either " +
                "generated or declared, and neither happened here");

    /// <summary>
    /// What is actually under a prospect, or null for a dry structure — TRUTH,
    /// and the only thing entitled to ask is a bit that has already been
    /// drilled. It is `internal` for that reason: a read model that could call
    /// this would hand the player the answer POS exists to make them guess.
    /// </summary>
    internal EntityId<IReservoirCompartmentEntity>? Beneath(EntityId<IProspect> prospect) =>
        _found.TryGetValue(prospect, out EntityId<IReservoirCompartmentEntity> found)
            ? found
            : null;

    internal void Surface(GeneratedSurface surface)
    {
        _surface = surface;
        _harbours = surface.Harbours;
    }

    internal void Climate(ClimateRegion region) => _climate.Add(region);

    internal void Jurisdiction(Jurisdiction jurisdiction) => _jurisdictions.Add(jurisdiction);

    /// <summary>
    /// HOW FAR A FIELD IS FROM MARKET — the distance from the prospect to the
    /// nearest harbour, which is the flowline a company has to lay to develop it
    /// (SDD-006 §7c.1).
    ///
    /// <para>Null when there is nothing to measure: a basin with no coast, or an
    /// engine playing a hand-built field that no generator ever placed. Both are
    /// legitimately "no distance", and returning a made-up one would let a game
    /// route oil to a sea that is not there or charge a test field for a journey
    /// it never makes.</para>
    /// </summary>
    /// <summary>Where a prospect sits, for a map and for measuring against
    /// anything else on it.</summary>
    public Coordinate PositionOf(EntityId<IProspect> prospect)
    {
        int index = _prospects.IndexOf(prospect);

        return index < 0 ? default : _at[index];
    }

    public Length? DistanceToMarket(EntityId<IProspect> prospect)
    {
        int index = _prospects.IndexOf(prospect);

        if (index < 0 || _harbours.Count == 0) return null;

        Coordinate at = _at[index];
        double nearest = double.PositiveInfinity;

        for (int i = 0; i < _harbours.Count; i++)
        {
            double dx = _harbours[i].Site.X - at.X;
            double dy = _harbours[i].Site.Y - at.Y;

            nearest = Math.Min(nearest, (dx * dx) + (dy * dy));
        }

        return new Length(DetMath.Sqrt(nearest));
    }
}

/// <summary>
/// SDD-010 §4's sink: the generator's only output channel.
/// </summary>
public sealed class WorldSink : IWorldSink
{
    private readonly FieldControl _field;
    private readonly IBeliefStore _beliefs;
    private readonly WorldState _world;
    private readonly OGSim.Information.ProspectRisks _risks;

    public WorldSink(
        FieldControl field,
        IBeliefStore beliefs,
        WorldState world,
        OGSim.Information.ProspectRisks risks)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(beliefs);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(risks);

        _field = field;
        _beliefs = beliefs;
        _world = world;
        _risks = risks;
    }

    private ulong _prospectsPlaced;

    public void AddAccumulation(GeneratedAccumulation accumulation)
    {
        ArgumentNullException.ThrowIfNull(accumulation);

        // EVERY CLOSED STRUCTURE IS A PROSPECT, charged or not (SDD-010 §4b).
        // The id is assigned here and in generation order, which is what lets
        // the generator name its regional observations positionally.
        EntityId<IProspect> prospect =
            _world.Place(accumulation.Closure.Centroid, accumulation.Capacity);

        _prospectsPlaced++;

        // placed below

        // WHAT THE COMPANY THINKS ITS CHANCES ARE (SDD-008 §4). Registered with
        // the PLAY it belongs to, so source, reservoir and seal are one belief
        // shared across every prospect drawing on the same system — and a dry
        // hole on any of them re-prices all the others.
        //
        // The trap factor is this structure's own, weighted by how subtly it is
        // expressed: a four-way dome is nearly certainly there, a stratigraphic
        // pinch-out is an interpretation. That is what a detect class means,
        // said as risk rather than only as visibility.
        _risks.Register(
            new EntityRef(EntityKind.Prospect, prospect.Value),
            accumulation.Play,
            Defaults.TrapConfidenceOf(accumulation.Subtlety));

        for (int i = 0; i < accumulation.Compartments.Count; i++)
        {
            GeneratedCompartment generated = accumulation.Compartments[i];

            // DRAINAGE AREA IS THE CLOSURE'S. The trap's own footprint is what a
            // well in it drains, so where the accumulation is and how big its
            // structure is reach all the way into the inflow equation — which is
            // the difference between a field that is large because a content
            // file says so and one that is large because of its shape.
            _world.Found(prospect, _field.AddCompartment(
                generated,
                permeability: Defaults.Inflow.Permeability,
                netThickness: Defaults.Inflow.PerforatedInterval,
                drainageArea: accumulation.Closure.Area,
                rockCompressibility: RockCompressibility,

                // Contacts bracket the accumulation's own depth. Generated
                // geology does not yet emit them (SDD-010 §2 step 7 draws
                // volume, φ and So and stops), so they are placed around what it
                // DID say rather than at a fixed depth that would put a
                // shallow trap below its own oil-water contact.
                gasOilContact: new Length(generated.Depth.Metres - ContactStandoff),
                oilWaterContact: new Length(generated.Depth.Metres + ContactStandoff),
                Defaults.Wettability,
                Defaults.Drive,
                Defaults.AquiferStrength,
                Defaults.AquiferResponseTime));

        }
    }

    /// <summary>
    /// Pore-volume compressibility of a consolidated sandstone, 1/Pa. The same
    /// number every hand-built compartment in this repository has used; it moves
    /// into content with R20c.9's loader alongside the rest of the rock sheet.
    /// </summary>
    private const double RockCompressibility = 4.5e-10;

    /// <summary>
    /// How far above and below an accumulation its contacts sit. A hundred
    /// metres — enough that the completion is in oil, which is all this can
    /// honestly say until step 7 generates a column height.
    /// </summary>
    private const double ContactStandoff = 100.0;

    public void SetSurface(GeneratedSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _world.Surface(surface);
    }

    public void AddClimateRegion(ClimateRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        _world.Climate(region);
    }

    public void AddJurisdiction(Jurisdiction jurisdiction)
    {
        ArgumentNullException.ThrowIfNull(jurisdiction);
        _world.Jurisdiction(jurisdiction);
    }

    /// <summary>
    /// R15-V10's leak guarantee, and the reason this method is not a setter.
    /// Starting knowledge goes through <see cref="IBeliefStore.Apply"/> — the
    /// same conjugate update a well test uses — so it carries the sigma and the
    /// provenance of the survey that supposedly produced it, and a player can
    /// see that what they were given is regional analogue data rather than fact.
    /// </summary>
    public void DeliverRegionalObservation(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        // THE GENERATOR NAMES ITS SUBJECT BY POSITION — structure i becomes
        // prospect i+1. Checked rather than assumed: a belief pointing at the
        // wrong subject is worse than no belief at all, and it would be
        // invisible — a plausible number attached to the wrong structure.
        if (observation.Subject.Value > _prospectsPlaced)
            throw new InvariantFault("SDD-010 §4", observation.Subject,
                $"regional data names prospect {observation.Subject.Value} and only " +
                $"{_prospectsPlaced} were placed; the generator's positional subject and the " +
                "compartments this sink created have drifted apart");

        _beliefs.Apply(observation);
    }
}
