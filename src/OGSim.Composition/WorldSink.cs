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
public sealed class WorldState
{
    private readonly List<EntityId<IReservoirCompartmentEntity>> _prospects = [];
    private readonly List<Coordinate> _at = [];
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
    public IReadOnlyList<EntityId<IReservoirCompartmentEntity>> Prospects => _prospects;

    /// <summary>How many compartments generation built, so the subject the
    /// generator assumed can be checked rather than trusted.</summary>
    public int Count => _prospects.Count;

    internal void Add(EntityId<IReservoirCompartmentEntity> prospect, Coordinate at)
    {
        _prospects.Add(prospect);
        _at.Add(at);
    }

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
    public Length? DistanceToMarket(EntityId<IReservoirCompartmentEntity> prospect)
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

    public WorldSink(FieldControl field, IBeliefStore beliefs, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(beliefs);
        ArgumentNullException.ThrowIfNull(world);

        _field = field;
        _beliefs = beliefs;
        _world = world;
    }

    private EntityId<IReservoirCompartmentEntity> _prospectJustBuilt;

    public void AddAccumulation(GeneratedAccumulation accumulation)
    {
        ArgumentNullException.ThrowIfNull(accumulation);

        for (int i = 0; i < accumulation.Compartments.Count; i++)
        {
            GeneratedCompartment generated = accumulation.Compartments[i];

            // DRAINAGE AREA IS THE CLOSURE'S. The trap's own footprint is what a
            // well in it drains, so where the accumulation is and how big its
            // structure is reach all the way into the inflow equation — which is
            // the difference between a field that is large because a content
            // file says so and one that is large because of its shape.
            _prospectJustBuilt = _field.AddCompartment(
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
                Defaults.AquiferResponseTime);

            _world.Add(_prospectJustBuilt, accumulation.Closure.Centroid);
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

        // THE GENERATOR NAMES ITS SUBJECT BY POSITION — accumulation i becomes
        // compartment i+1 — which holds only while every accumulation has
        // exactly one compartment. Checked rather than assumed: a multi-
        // compartment accumulation would silently attach a belief about one
        // structure to a different one, and a belief pointing at the wrong
        // subject is worse than no belief at all.
        if (observation.Subject.Value > (ulong)_world.Count)
            throw new InvariantFault("SDD-010 §4", observation.Subject,
                $"regional data names compartment {observation.Subject.Value} and only " +
                $"{_world.Count} were built; the generator's positional subject and the " +
                "compartments this sink created have drifted apart");

        _beliefs.Apply(observation);
    }
}
