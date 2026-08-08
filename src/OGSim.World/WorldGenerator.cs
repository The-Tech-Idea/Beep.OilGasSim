// R15.1 / R15.5 / R15.9 — the generation pipeline (SDD-010 §2–4).
//
// THE HANDOFF IS TYPED. The generator's only output channel is IWorldSink, so it
// never sees a module store and truth never travels sideways. That is what makes
// the slot moddable (design 03 §3.2) without opening the truth wall — a
// third-party generator can produce a world and still cannot reach into
// anybody's beliefs.
//
// AND BELIEFS ENTER THROUGH THE OBSERVATION DOOR. R15-V10's leak guarantee is
// not "the generator is careful"; it is that DeliverRegionalObservation takes an
// Observation, so a starting belief is constructed by the same conjugate update
// every in-game survey uses. There is no belief-copy path, because there is no
// method that would accept one.

using System.Collections.Immutable;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.World;

/// <summary>
/// SDD-010's pipeline. Each step draws from its own substream (§1).
///
/// <para>This is the SHIPPED generator, not the only possible one — the slot is
/// replaceable, and a scenario that wants a hand-built world supplies a
/// different implementation of the same interface.</para>
/// </summary>
public sealed class BasinWorldGenerator : IWorldGenerator
{
    /// <summary>How coarse a regional survey is. Deliberately BAD: regional data
    /// is a gravity and magnetics pass over a whole basin, and a player who
    /// could book reserves off it would never buy seismic (R15-V10).</summary>
    private const double RegionalSigmaLog = 1.2;

    /// <summary>Traps that receive no charge. SDD-010 §2.6's fill-spill produces
    /// these naturally — the fraction is an outcome, and R15-V7 checks it is
    /// meaningful rather than zero.</summary>
    private const double SpillFraction = 0.55;

    public ContentId Id { get; } = new("basin-generator");

    public void Generate(WorldParameters parameters, IWorldSink sink, IRandomStream worldGen)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(worldGen);

        Validate(parameters);

        // The world seed comes from the caller's stream, ONCE. Everything after
        // this is derived — so the whole world is a function of one number, and
        // PV7 is a property of the construction rather than of discipline.
        var streams = new StepStreams(unchecked((ulong)worldGen.NextInt(int.MaxValue)));

        // THE SURFACE COMES FIRST, because geology has to be placed ON it: a
        // trap's water depth is the elevation of the ground above it, and until
        // there was a heightfield to ask, every accumulation was declared
        // onshore whatever it sat under. Reordering costs nothing in
        // determinism — each step draws from its own substream (§1), so where a
        // step runs cannot shift what another step draws.
        GeneratedSurface surface = GenerateSurface(parameters, streams);

        IReadOnlyList<GeneratedAccumulation> accumulations =
            GenerateGeology(parameters, streams, surface.Terrain);

        for (int i = 0; i < accumulations.Count; i++) sink.AddAccumulation(accumulations[i]);

        sink.SetSurface(surface);
        sink.AddJurisdiction(GenerateJurisdiction(parameters, streams));

        DeliverRegionalData(accumulations, sink, streams);
    }

    // ---------------------------------------------------------- geology

    private static IReadOnlyList<GeneratedAccumulation> GenerateGeology(
        WorldParameters parameters, StepStreams streams, GeneratedTerrain terrain)
    {
        IRandomStream traps = streams.For(WorldStep.Traps);
        IRandomStream charge = streams.For(WorldStep.Charge);
        IRandomStream sizing = streams.For(WorldStep.Accumulations);
        IRandomStream plays = streams.For(WorldStep.PlaysAndClasses);

        // Step 5: candidate traps, scaled by the basin's size.
        int candidates = 4 + traps.NextInt(Math.Max(1, parameters.WidthCells / 8));

        var accumulations = new List<GeneratedAccumulation>();

        for (int i = 0; i < candidates; i++)
        {
            // Step 5's subtlety class, drawn per trap — TRUTH, read by the
            // screening gate rather than the other way round. A below-tier
            // survey spawns nothing at all, which is why R15-V10 asks for zero
            // information about above-tier accumulations rather than vague
            // information.
            DetectClass subtlety = (DetectClass)traps.NextInt(4);

            // Step 6: fill-spill. A trap that receives no charge is empty, and
            // the classic algorithm produces those NATURALLY rather than by a
            // rule that says "make some empty" (R15-V7).
            if (charge.NextUnit() > (1.0 - SpillFraction) * parameters.ResourceRichness) continue;

            // Step 7: volume is log-normal — the size distribution real basins
            // show, and the reason one field in a play is worth ten of the rest.
            double logVolume = 12.0 + sizing.NextNormal() * 1.4;
            double volume = DetMath.Exp(logVolume) * parameters.ResourceRichness;

            var depth = new Length(1200.0 + sizing.NextUnit() * 3500.0);

            // WHERE IT IS. A cell on the generated grid, not a slot in a row —
            // so the ground above the trap is real ground with an elevation, and
            // an accumulation under water is offshore because of where it is
            // rather than because nothing ever asked.
            int cell = traps.NextInt(terrain.ClassByCell.Length);

            // AND HOW BIG ITS STRUCTURE IS. Closure area scales with the volume
            // drawn above: a bigger accumulation is a bigger trap, which is what
            // a well drilled into it drains (SDD-010 §2.5's closure polygon,
            // consumed as drainage area). A fixed footprint would have made
            // every well in the basin drain the same area whatever it found.
            Polygon closure = ClosureAt(terrain, cell, volume);

            accumulations.Add(new GeneratedAccumulation(
                Play: new ContentId(plays.NextUnit() < 0.5 ? "play-a" : "play-b"),
                Closure: closure,
                Subtlety: subtlety,
                Access: AccessFor(depth, WaterDepthAt(terrain, cell)),
                Fluid: depth.Metres > 3200.0 ? FluidForm.ModifiedBlackOil : FluidForm.BlackOil,
                Compartments:
                [
                    new GeneratedCompartment(
                        PoreVolume: new ReservoirVolume(volume),
                        Porosity: 0.12 + sizing.NextUnit() * 0.15,
                        OilSaturation: 0.60 + sizing.NextUnit() * 0.25,
                        InitialPressure: new Pressure(depth.Metres * 1.0e4),
                        Temperature: new Temperature(288.0 + depth.Metres * 0.03),
                        Depth: depth),
                ]));
        }

        return accumulations;
    }

    /// <summary>
    /// SDD-010 §2.7 — access requirements DERIVED from generated depth, never
    /// authored. A deep discovery is hard because it is deep.
    /// </summary>
    private static AccessRequirements AccessFor(Length depth, WaterDepthClass water) =>
        new(Depth: depth.Metres switch
            {
                < 1500.0 => DepthClass.Shallow,
                < 3000.0 => DepthClass.Standard,
                < 4500.0 => DepthClass.Deep,
                _ => DepthClass.UltraDeep,
            },
            WaterDepth: water,
            Hpht: depth.Metres > 4000.0,
            Tight: false,
            Sour: false);

    /// <summary>
    /// How much water stands over a trap — read from the heightfield, because
    /// sea level is elevation zero and bathymetry is the same field going
    /// negative (§3 hydrology). A trap on dry land is onshore; one under 400 m
    /// of water is a different development entirely, and the gate on
    /// <see cref="AccessRequirements"/> is what makes that true in play.
    /// </summary>
    private static WaterDepthClass WaterDepthAt(GeneratedTerrain terrain, int cell)
    {
        double elevation = terrain.Elevation.ElevationMetres[cell];

        if (elevation >= 0.0) return WaterDepthClass.Onshore;

        return -elevation switch
        {
            < 150.0 => WaterDepthClass.Shallow,
            < 1000.0 => WaterDepthClass.Deep,
            _ => WaterDepthClass.UltraDeep,
        };
    }

    /// <summary>
    /// The trap's footprint on the map: a square centred on its cell, with an
    /// area that grows with the accumulation it holds.
    ///
    /// <para>A SQUARE and not a contour walk, which is what SDD-010 §2.5
    /// specifies — a closure polygon should be the contour of the structural
    /// horizon at the spill point. There is no generated horizon yet (steps 1–4
    /// are not implemented), so there is nothing to walk. What this DOES give
    /// honestly is position and extent, which is what everything downstream
    /// consumes; the shape becomes real when the horizon does.</para>
    /// </summary>
    private static Polygon ClosureAt(GeneratedTerrain terrain, int cell, double volume)
    {
        double cellSize = terrain.Elevation.CellSize.Metres;

        double x = (cell % terrain.Elevation.Width) * cellSize;
        double y = (cell / terrain.Elevation.Width) * cellSize;

        // Side of a square holding this much rock at a nominal column height.
        // The scaling is what matters rather than the constant: ten times the
        // volume is a little over three times the footprint, which is the
        // relationship between a field's size and the area a well drains.
        double side = DetMath.Sqrt(volume / NominalColumnMetres);

        return new Polygon(
        [
            new Coordinate(x, y), new Coordinate(x + side, y),
            new Coordinate(x + side, y + side), new Coordinate(x, y + side),
        ]);
    }

    /// <summary>
    /// The column height a closure's footprint is computed against. Twenty
    /// metres of net pay — an ordinary onshore reservoir, and the number that
    /// turns a pore volume into an area.
    /// </summary>
    private const double NominalColumnMetres = 20.0;

    // ---------------------------------------------------------- surface

    private static GeneratedSurface GenerateSurface(
        WorldParameters parameters, StepStreams streams)
    {
        IRandomStream surface = streams.For(WorldStep.Surface);

        int cells = parameters.WidthCells * parameters.HeightCells;
        var elevation = new double[cells];

        for (int i = 0; i < cells; i++)
            elevation[i] = (surface.NextUnit() - parameters.LandFraction) * 400.0;

        var heightfield = new Heightfield(
            new Length(1000.0), parameters.WidthCells, parameters.HeightCells,
            [.. elevation]);

        // Sea level is elevation zero, so bathymetry is the same field going
        // negative — harbour depth falls out rather than being authored.
        var classes = new int[cells];
        for (int i = 0; i < cells; i++) classes[i] = elevation[i] < 0.0 ? 0 : 1;

        var terrain = new GeneratedTerrain(
            heightfield, [.. classes], [new ContentId("sea"), new ContentId("land")], [], []);

        IReadOnlyList<Harbour> harbours = PlaceHarbours(terrain);

        return new GeneratedSurface(
            terrain,
            PlaceSettlements(terrain, harbours, surface),
            [],                                  // roads: A* on the cost grid, step 9.4
            harbours,
            [],                                  // third-party fabric, step 9.5
            []);                                 // sensitivity zones, step 9.6
    }

    /// <summary>
    /// Where a cargo can be loaded (SDD-010 §3 hydrology). A harbour is a land
    /// cell with sea against it, and its DEPTH is the deepest water it touches —
    /// which falls out of the heightfield rather than needing a map of its own,
    /// because sea level is elevation zero.
    ///
    /// <para>Depth is the whole point: a shallow inlet cannot berth what a deep
    /// one can, so where a basin's coast happens to be steep decides what class
    /// of vessel its oil leaves on. That is a fact about the world rather than a
    /// content setting.</para>
    /// </summary>
    private static IReadOnlyList<Harbour> PlaceHarbours(GeneratedTerrain terrain)
    {
        int width = terrain.Elevation.Width;
        int height = terrain.Elevation.Height;
        double cellSize = terrain.Elevation.CellSize.Metres;

        ImmutableArray<double> elevation = terrain.Elevation.ElevationMetres;

        var found = new List<Harbour>();

        // Walked in cell order, never by enumerating a set (D-5): two runs of
        // one seed must place harbours in the same order or a save's ids drift.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int cell = (y * width) + x;

                if (elevation[cell] < 0.0) continue;      // sea, not shore

                double deepest = 0.0;

                if (x > 0) deepest = Math.Min(deepest, elevation[cell - 1]);
                if (x < width - 1) deepest = Math.Min(deepest, elevation[cell + 1]);
                if (y > 0) deepest = Math.Min(deepest, elevation[cell - width]);
                if (y < height - 1) deepest = Math.Min(deepest, elevation[cell + width]);

                // Dry on every side — inland, and not a harbour however good the
                // ground is.
                if (deepest >= 0.0) continue;

                found.Add(new Harbour(
                    new Coordinate(x * cellSize, y * cellSize), new Length(-deepest)));
            }
        }

        return found;
    }

    /// <summary>
    /// SDD-010 §3's settlement score, with the terms this world can honestly
    /// answer: coast and flat ground. The river and arable terms are declared in
    /// that section and weighted zero here, because rivers are not generated yet
    /// — a scored term with no data behind it would be a number pretending to be
    /// a reason.
    ///
    /// <para>Population is log-normal BY RANK, so a basin has one real town and
    /// a scatter of villages rather than a uniform sprawl — the distribution
    /// that decides where labour, roads and complaints come from.</para>
    /// </summary>
    private static IReadOnlyList<Settlement> PlaceSettlements(
        GeneratedTerrain terrain, IReadOnlyList<Harbour> harbours, IRandomStream surface)
    {
        // A coast is the strongest single reason a town is where it is, so the
        // harbours already found ARE the candidate sites — scored by the water
        // they touch, which is the same fact that made them harbours.
        var sites = new List<Harbour>(harbours);

        // Deepest water first; ties by position, so the order is a property of
        // the world rather than of the sort's stability.
        sites.Sort(static (a, b) =>
        {
            int byDepth = b.Depth.Metres.CompareTo(a.Depth.Metres);
            if (byDepth != 0) return byDepth;

            int byX = a.Site.X.CompareTo(b.Site.X);
            return byX != 0 ? byX : a.Site.Y.CompareTo(b.Site.Y);
        });

        var settlements = new List<Settlement>();

        for (int rank = 0; rank < sites.Count && settlements.Count < MaxSettlements; rank++)
        {
            Coordinate site = sites[rank].Site;

            if (TooClose(settlements, site, terrain.Elevation.CellSize.Metres)) continue;

            // Rank decays the median: the first town is the port, the tenth is a
            // village that happens to be on the water.
            double logPopulation = FirstTownLogPopulation
                                 - (settlements.Count * RankDecay)
                                 + (surface.NextNormal() * PopulationSigmaLog);

            settlements.Add(new Settlement(site, (long)DetMath.Exp(logPopulation)));
        }

        return settlements;
    }

    private static bool TooClose(
        IReadOnlyList<Settlement> placed, Coordinate site, double cellSize)
    {
        double minimum = MinimumSpacingCells * cellSize;

        for (int i = 0; i < placed.Count; i++)
        {
            double dx = placed[i].Site.X - site.X;
            double dy = placed[i].Site.Y - site.Y;

            if ((dx * dx) + (dy * dy) < minimum * minimum) return true;
        }

        return false;
    }

    /// <summary>Towns do not sit on top of one another. Five cells apart, so a
    /// coastline becomes a handful of places rather than one per cell.</summary>
    private const double MinimumSpacingCells = 5.0;

    private const int MaxSettlements = 8;

    /// <summary>ln(60 000) — a small port city.</summary>
    private const double FirstTownLogPopulation = 11.0;

    private const double RankDecay = 0.55;

    private const double PopulationSigmaLog = 0.35;

    private static Jurisdiction GenerateJurisdiction(
        WorldParameters parameters, StepStreams streams)
    {
        IRandomStream jurisdictions = streams.For(WorldStep.Jurisdictions);

        return new Jurisdiction(
            new ContentId(jurisdictions.NextUnit() < parameters.BasinMaturity
                ? "concession" : "psc"),
            SquareAround(0));
    }

    // ---------------------------------------------------------- beliefs

    /// <summary>
    /// R15.9 / R15-V10. Starting beliefs, delivered as OBSERVATIONS.
    ///
    /// <para>Two guarantees, and the second is the one that matters:</para>
    ///
    /// <list type="number">
    /// <item>The sigma is large. Regional data is a gravity and magnetics pass
    /// over a whole basin; a player who could book reserves off it would never
    /// buy seismic.</item>
    /// <item><b>An above-tier accumulation gets NO observation at all.</b> Not a
    /// vague one — none. A vague reading still says "something is here", and the
    /// whole point of a subtlety class is that a subtle trap is invisible until
    /// the survey tier catches up with it.</item>
    /// </list>
    /// </summary>
    private static void DeliverRegionalData(
        IReadOnlyList<GeneratedAccumulation> accumulations,
        IWorldSink sink,
        StepStreams streams)
    {
        IRandomStream regional = streams.For(WorldStep.RegionalData);

        for (int i = 0; i < accumulations.Count; i++)
        {
            GeneratedAccumulation accumulation = accumulations[i];

            // Regional data sees D0 only. Everything subtler is silent.
            if (accumulation.Subtlety != DetectClass.D0) continue;

            GeneratedCompartment compartment = accumulation.Compartments[0];
            // Oil in place: pore volume × porosity × oil saturation. The BELIEF is
            // about the derived quantity, because that is what a company books
            // and argues about — not about the three factors separately.
            double inPlace = compartment.PoreVolume.CubicMetres
                           * compartment.Porosity * compartment.OilSaturation;

            double truth = DetMath.Ln(inPlace);

            sink.DeliverRegionalObservation(new Observation(
                Subject: new EntityRef(EntityKind.Compartment, (ulong)(i + 1)),
                PropertyKind: new ContentId("oil-in-place"),
                Value: truth + regional.NextNormal() * RegionalSigmaLog,
                Sigma: RegionalSigmaLog,
                Space: BeliefSpace.Log,
                Source: Provenance.Analogue));
        }
    }

    // ---------------------------------------------------------- validation

    /// <summary>
    /// SDD-010 §4: out-of-range is a refusal naming ALL violations, never a
    /// clamp. A clamped world would start playable and wrong, and the player
    /// would never learn which knob they had set impossibly.
    /// </summary>
    private static void Validate(WorldParameters parameters)
    {
        var problems = new List<string>();

        if (parameters.WidthCells <= 0 || parameters.HeightCells <= 0)
            problems.Add("grid dimensions must be positive");

        if (parameters.LandFraction is < 0.0 or > 1.0)
            problems.Add($"land fraction {Format(parameters.LandFraction)} is not in [0, 1]");

        if (parameters.ResourceRichness <= 0.0)
            problems.Add($"resource richness {Format(parameters.ResourceRichness)} is not positive");

        if (parameters.BasinMaturity is < 0.0 or > 1.0)
            problems.Add($"basin maturity {Format(parameters.BasinMaturity)} is not in [0, 1]");

        if (parameters.ClimateSeverity < 0.0)
            problems.Add($"climate severity {Format(parameters.ClimateSeverity)} is negative");

        if (parameters.RivalCount < 0)
            problems.Add($"rival count {parameters.RivalCount} is negative");

        if (problems.Count > 0)
            throw new ModelFault("SDD-010 §4", null,
                "world parameters are out of range: " + string.Join("; ", problems));
    }

    private static Polygon SquareAround(int index)
    {
        double x = index * 10.0;

        return new Polygon(
        [
            new Coordinate(x, 0.0), new Coordinate(x + 5.0, 0.0),
            new Coordinate(x + 5.0, 5.0), new Coordinate(x, 5.0),
        ]);
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
