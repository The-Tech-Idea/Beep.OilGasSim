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
public sealed class BasinWorldGenerator(
    IReadOnlyList<TerrainClassDefinition> terrainClasses,

    /// <summary>SDD-016's R20d.8.10 amendment: composition owns the one climate
    /// this shipped generator can produce and hands over only its NAME, the
    /// same door <paramref name="terrainClasses"/> already came through — the
    /// generator declares which region this basin has, composition decides
    /// what the id resolves to.</summary>
    ContentId climateId,

    /// <summary>Every fluid system this build's content declared (SDD-010 §2's
    /// finding-270 amendment) — Step 7 draws one per compartment from this
    /// list, the same door <paramref name="terrainClasses"/> already came
    /// through.</summary>
    IReadOnlyList<FluidSystemDefinition> fluidSystems,

    /// <summary>Every world template this build's content declared (SDD-010
    /// §1's pass-7 amendment, finding 288). `WorldParameters.Template` names
    /// one; the generator resolves it here because the parameters arrive at
    /// CreateNew, after composition. The template owns which knob values are
    /// legal and the viability band a drawn world must meet.</summary>
    ICatalog<WorldTemplateDefinition> worldTemplates) : IWorldGenerator
{
    /// <summary>How coarse a regional survey is. Deliberately BAD: regional data
    /// is a gravity and magnetics pass over a whole basin, and a player who
    /// could book reserves off it would never buy seismic (R15-V10).</summary>
    private const double RegionalSigmaLog = 1.2;

    /// <summary>
    /// How much oil the kitchen cooked, as a fraction of everything the basin's
    /// traps could hold. Below one, so a basin is never entirely full and there
    /// is always somewhere the charge did not reach — which is what makes
    /// exploration a search rather than an inventory.
    /// </summary>
    private const double ChargeFractionOfCapacity = 0.35;

    private const double MinimumPorosity = 0.12;

    private const double PorositySpread = 0.15;

    public ContentId Id { get; } = new("basin-generator");

    public void Generate(WorldParameters parameters, IWorldSink sink, IRandomStream worldGen)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(worldGen);

        if (fluidSystems.Count == 0)
            throw new ModelFault("SDD-010 §2's finding-270 amendment", null,
                "no fluid system was declared; a generated compartment cannot draw one from " +
                "an empty list");

        // WHICH BASIN ARCHETYPE this world is drawn from (SDD-010 §1's pass-7
        // amendment, finding 288). An unknown id refuses by name through the
        // catalogue's own indexer, the same door an unknown starting state
        // already refuses through.
        WorldTemplateDefinition template = worldTemplates[parameters.Template];

        Validate(parameters, template);

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
        GeneratedSurface surface = GenerateSurface(parameters, streams, terrainClasses);

        IReadOnlyList<GeneratedAccumulation> accumulations =
            ViableGeology(parameters, template, streams, surface.Terrain, fluidSystems);

        for (int i = 0; i < accumulations.Count; i++) sink.AddAccumulation(accumulations[i]);

        sink.SetSurface(surface);

        // ONE region, because this generator produces one basin and SDD-016 §1
        // says a region IS one climate-profile instance's area — "typically a
        // basin" (finding 242's climate half; S016-1 tracks the multi-basin
        // case where more than one would ever matter).
        sink.AddClimateRegion(new ClimateRegion(climateId, WholeMap(parameters)));

        sink.AddJurisdiction(GenerateJurisdiction(parameters, streams));

        DeliverRegionalData(accumulations, sink, streams);
    }

    // ---------------------------------------------------------- geology

    /// <summary>
    /// Geology that meets the template's VIABILITY band (SDD-010 §2 step 8,
    /// finding 288): draw, count what fill-spill charged, and keep the first
    /// world with at least <c>minimumChargedAccumulations</c> — or fault when
    /// the bound is spent, naming the template and the count, because a world
    /// quietly shipped in violation would be a content-tuning error nobody
    /// ever learns about (R15-V8).
    /// </summary>
    /// <remarks>
    /// <para><b>A retry re-draws the whole basin, structure first.</b>
    /// Viability can fail on kitchen maturity — a fact of the step-2 horizon
    /// — so re-drawing only the per-trap noise could never fix it (the §2
    /// step-8 row's own finding-288 correction). Each attempt continues the
    /// step substreams where the last left them, so the retry sequence is a
    /// pure function of seed and parameters and one seed is still one world
    /// (PV7), including which attempt it settled on. Attempt zero consumes
    /// the streams exactly as generation always has, so every template with
    /// a floor of zero — the base game — produces the world it always did,
    /// byte for byte.</para>
    ///
    /// <para><b>Stamping a charged trap instead was considered and refused</b>
    /// (§1's finding-288 amendment): a dry hole proves SOURCE failed for the
    /// whole play (§4b), and a play holding one stamped full trap among
    /// genuinely dry siblings would make that inference false. The world
    /// stays a pure fill-spill outcome; viability only decides which drawn
    /// world is kept.</para>
    /// </remarks>
    private static IReadOnlyList<GeneratedAccumulation> ViableGeology(
        WorldParameters parameters, WorldTemplateDefinition template, StepStreams streams,
        GeneratedTerrain terrain, IReadOnlyList<FluidSystemDefinition> fluidSystems)
    {
        for (var attempt = 0; ; attempt++)
        {
            IReadOnlyList<GeneratedAccumulation> drawn =
                GenerateGeology(parameters, streams, terrain, fluidSystems);

            var charged = 0;

            for (int i = 0; i < drawn.Count; i++)
                if (drawn[i].Compartments.Count > 0) charged++;

            if (charged >= template.MinimumChargedAccumulations) return drawn;

            if (attempt >= template.ResampleBound)
                throw new ModelFault("SDD-010 §2 step 8 (finding 288)", null,
                    $"template '{template.Id.Value}' demands at least " +
                    $"{template.MinimumChargedAccumulations} charged accumulations and the " +
                    $"last of {attempt + 1} draws reached {charged}; the viability band and " +
                    "the basin it asks of cannot both be right — a content-tuning error, " +
                    "reported rather than shipped (R15-V8)");
        }
    }

    private static IReadOnlyList<GeneratedAccumulation> GenerateGeology(
        WorldParameters parameters, StepStreams streams, GeneratedTerrain terrain,
        IReadOnlyList<FluidSystemDefinition> fluidSystems)
    {
        IRandomStream structure = streams.For(WorldStep.Structure);
        IRandomStream traps = streams.For(WorldStep.Traps);
        IRandomStream charge = streams.For(WorldStep.Charge);
        IRandomStream sizing = streams.For(WorldStep.Accumulations);
        IRandomStream plays = streams.For(WorldStep.PlaysAndClasses);

        // Step 4: the surface everything else is read off. A basin's traps are
        // its structural highs, so this is generated FIRST and the traps are
        // FOUND rather than drawn (SDD-010 §2 step 4).
        // STEP 3, and it has to come first: HOW DEEPLY THIS BASIN WAS BURIED.
        //
        // It was a constant, so every basin in every world sat at the same
        // depth — and with the same depth comes the same maturity, the same
        // charge and the same answer to "is there oil here?". Drawn per basin,
        // a shallow one never cooked its source and a deep one cracked it to
        // gas, and neither can be told from the other by looking at the map.
        IRandomStream burial = streams.For(WorldStep.BurialThermal);

        double datum = ShallowestBasinMetres
                     + (burial.NextUnit() * (DeepestBasinMetres - ShallowestBasinMetres));

        var horizon = new StructuralHorizon(
            parameters.WidthCells, parameters.HeightCells,
            unchecked((ulong)structure.NextInt(int.MaxValue)),
            crestDepth: datum,
            relief: HorizonReliefMetres);

        // Step 5: every closed high with a worthwhile column.
        IReadOnlyList<Closure> closures = horizon.Traps(MinimumClosureHeightMetres);

        // Step 6: FILL-SPILL, in migration order (SDD-010 §2.6).
        //
        // Charge is cooked in the deep part of the basin and migrates UP-DIP,
        // so it reaches the deepest traps first. Each fills to its spill point
        // and passes what it cannot hold to the next one up the path; when the
        // charge runs out, every trap above that point is dry.
        //
        // THE EMPTY TRAPS ARE NOW AN OUTCOME rather than a coin flip. Which
        // structures hold oil depends on where they sit relative to the kitchen
        // — so a dry hole says something about its neighbours, exploration has a
        // pattern to learn, and "drill the up-dip prospect first" becomes a
        // choice that can be wrong. A per-trap probability could express none of
        // that: it made every prospect independent, which is exactly the thing
        // real exploration is not.
        IReadOnlyList<Closure> path = MigrationOrder(closures);

        // How much oil the kitchen made, against how much the basin could hold.
        // Expressed as a FRACTION of total capacity rather than as an absolute:
        // a bigger basin has both more source rock and more traps, and an
        // absolute would make grid size decide whether the world had any oil.
        double porosityMidpoint = MinimumPorosity + (PorositySpread * 0.5);

        // STEP 3: HOW MUCH OF THE SOURCE ROCK EVER COOKED (SDD-010 §2 step 3).
        //
        // The source lies below the reservoir, so its depth is the horizon plus
        // the section between them, and how deep it got decides what it made. A
        // basin whose source never reached the oil window has no oil in it at
        // all however many fine structures it has, and one whose source went too
        // deep cracked its oil to gas — which this engine cannot produce, so
        // those basins are oil-barren too.
        //
        // THAT IS WHERE DRY BASINS COME FROM (design 06 §5's "elephants,
        // marginal fields, dry basins"). Until this, every basin had charge in
        // proportion to its traps, so the answer to "is there anything here?"
        // was always yes and only the amount varied. A player could not be
        // wrong about a whole basin, which is the largest way to be wrong there
        // is.
        double mature = MatureFraction(horizon);

        double charged = TotalCapacity(path, porosityMidpoint)
                       * ChargeFractionOfCapacity * parameters.ResourceRichness * mature;

        var accumulations = new List<GeneratedAccumulation>();

        for (int i = 0; i < path.Count; i++)
        {
            Closure closure = path[i];
            // Step 5's subtlety class, drawn per trap — TRUTH, read by the
            // screening gate rather than the other way round. A below-tier
            // survey spawns nothing at all, which is why R15-V10 asks for zero
            // information about above-tier accumulations rather than vague
            // information.
            DetectClass subtlety = (DetectClass)traps.NextInt(4);

            // Step 7: the trap's CAPACITY is its own — the rock between crest and
            // spill, times a porosity draw. Size is a consequence of structure
            // rather than a number: a broad gentle closure holds more than a
            // small sharp one, and the long-tailed distribution real basins show
            // emerges from the surface instead of being imposed on it (R15-V3).
            double porosity = MinimumPorosity + (sizing.NextUnit() * PorositySpread);

            double capacity = CapacityOf(closure, porosity);

            // What actually reached it. The last trap on the path is PARTLY
            // filled — an oil column shorter than its closure, which is a real
            // and common outcome and the reason "how big is the structure" and
            // "how much is in it" are different questions a player has to ask
            // separately.
            double volume = Math.Min(charged, capacity);

            charged -= volume;

            // Depth is where the crest actually is on the horizon, so pressure,
            // temperature and access all follow from the structure rather than
            // from a second unrelated draw.
            var depth = new Length(horizon.DepthAt(closure.Crest));

            int cell = closure.Crest;

            Polygon footprint = FootprintOf(closure, cell, horizon.Width);

            accumulations.Add(new GeneratedAccumulation(
                Play: new ContentId(plays.NextUnit() < 0.5 ? "play-a" : "play-b"),
                Closure: footprint,
                Subtlety: subtlety,
                Access: AccessFor(depth, WaterDepthAt(terrain, cell)),
                // Lighter oil from a source that cooked harder — read off where
                // the SOURCE is, not where the trap ended up. The two are only
                // loosely related, and it was the trap's own depth before, which
                // said a shallow trap must hold heavy oil however hot its
                // kitchen ran.
                Fluid: horizon.DepthAt(closure.Crest) + SourceBelowReservoirMetres
                       > DeepOilWindowMetres
                    ? FluidForm.ModifiedBlackOil
                    : FluidForm.BlackOil,

                // WHAT THE STRUCTURE COULD HOLD (SDD-010 §4b). Every closed high
                // has one; only some of them got any oil. This is what a survey
                // measures, which is why a dry prospect can be surveyed at all.
                Capacity: new ReservoirVolume(capacity),
                // EMPTY WHEN THE CHARGE NEVER ARRIVED (SDD-010 §4b). The
                // structure is still real, still mappable and still drillable —
                // it is a prospect a company can lose money on, which is the
                // whole subject of probability of success. Discarding it here
                // was finding 169.
                Compartments: volume <= 0.0 ? [] :
                [
                    new GeneratedCompartment(
                        PoreVolume: new ReservoirVolume(volume),
                        Porosity: porosity,
                        OilSaturation: 0.60 + sizing.NextUnit() * 0.25,
                        InitialPressure: new Pressure(depth.Metres * 1.0e4),
                        Temperature: new Temperature(288.0 + depth.Metres * 0.03),
                        Depth: depth,

                        // Step 7's crude-quality draw (SDD-010 §2's finding-270
                        // amendment) — same stream, same step, right after the
                        // saturation draw above: an index into this build's
                        // declared fluid systems, not a new stream and not a
                        // new RNG law.
                        FluidSystem: fluidSystems[sizing.NextInt(fluidSystems.Count)].Id),
                ]));
        }

        return accumulations;
    }

    /// <summary>
    /// Traps in the order charge reaches them: DEEPEST FIRST, because oil
    /// migrates up-dip out of the kitchen. Ties broken by cell so two runs of
    /// one seed fill the same traps in the same order (rule D-5).
    /// </summary>
    /// <summary>
    /// The fraction of the basin's source rock sitting in the oil window
    /// (SDD-010 §2 step 3's depth bands).
    ///
    /// <para>A table lookup on generated depth, which is the pinned choice —
    /// legible and tunable, and it means a basin's whole prospectivity follows
    /// from how deeply its section was buried rather than from a draw. Two
    /// basins with identical structures can be worth very different amounts,
    /// and a player cannot tell which by looking at the map.</para>
    /// </summary>
    private static double MatureFraction(StructuralHorizon horizon)
    {
        var mature = 0;

        // Cell order (rule D-5). Every cell of the source horizon is either in
        // the window or it is not; the fraction is the kitchen's size.
        for (int cell = 0; cell < horizon.Count; cell++)
        {
            double source = horizon.DepthAt(cell) + SourceBelowReservoirMetres;

            if (source >= ShallowOilWindowMetres && source <= DeepGasWindowMetres) mature++;
        }

        return mature / (double)horizon.Count;
    }

    /// <summary>How far below the reservoir the source section lies. A kitchen
    /// is not the reservoir it charges.</summary>
    private const double SourceBelowReservoirMetres = 800.0;

    /// <summary>The top of the oil window: above this the source is immature and
    /// has generated nothing at all.</summary>
    private const double ShallowOilWindowMetres = 2300.0;

    /// <summary>Where oil starts giving way to gas — still charge, but lighter
    /// (SDD-010 §2 step 3).</summary>
    private const double DeepOilWindowMetres = 3600.0;

    /// <summary>The base of anything this engine can produce. Below it the oil
    /// has cracked to gas, which no part of this composition can lift — so for
    /// the purposes of an oil company the rock below here made nothing.</summary>
    private const double DeepGasWindowMetres = 4200.0;

    private static IReadOnlyList<Closure> MigrationOrder(IReadOnlyList<Closure> closures)
    {
        var ordered = new List<Closure>(closures);

        ordered.Sort(static (a, b) =>
        {
            int byDepth = b.SpillDepth.CompareTo(a.SpillDepth);
            return byDepth != 0 ? byDepth : a.Crest.CompareTo(b.Crest);
        });

        return ordered;
    }

    private static double CapacityOf(Closure closure, double porosity) =>
        closure.ColumnMetres * CellSizeMetres * CellSizeMetres * porosity;

    private static double TotalCapacity(IReadOnlyList<Closure> closures, double porosity)
    {
        double total = 0.0;

        for (int i = 0; i < closures.Count; i++) total += CapacityOf(closures[i], porosity);

        return total;
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
    /// The trap's footprint on the map — a square of the closure's OWN AREA,
    /// placed at its crest.
    ///
    /// <para>The area is real now: it is the number of cells the closure floods,
    /// so a broad structure has a broad footprint and a well drilled into it
    /// drains accordingly. The SHAPE is not — SDD-010 §2.5 wants the contour of
    /// the horizon at the spill point, and tracing that boundary is the piece
    /// still outstanding. Position and extent are honest; outline is not, and
    /// nothing downstream reads the outline.</para>
    /// </summary>
    private static Polygon FootprintOf(Closure closure, int cell, int width)
    {
        double side = DetMath.Sqrt(closure.Cells * CellSizeMetres * CellSizeMetres);

        double x = ((cell % width) * CellSizeMetres) - (side * 0.5);
        double y = ((cell / width) * CellSizeMetres) - (side * 0.5);

        return new Polygon(
        [
            new Coordinate(x, y), new Coordinate(x + side, y),
            new Coordinate(x + side, y + side), new Coordinate(x, y + side),
        ]);
    }

    /// <summary>The grid's cell size, shared by the horizon and the
    /// heightfield so a trap's cell and the ground above it are the same
    /// place.</summary>
    private const double CellSizeMetres = 1000.0;

    /// <summary>
    /// How shallow a basin's section can be. At this depth the source has never
    /// entered the oil window, so the basin has fine structures and nothing in
    /// them (SDD-010 §2 step 3).
    /// </summary>
    private const double ShallowestBasinMetres = 700.0;

    /// <summary>
    /// And how deep. Down here the source went past the oil window and cracked
    /// what it made to gas — which this composition cannot lift, so for an oil
    /// company the basin is barren for the opposite reason.
    /// </summary>
    private const double DeepestBasinMetres = 4200.0;

    /// <summary>
    /// How much the horizon falls across the basin, dip and structural noise
    /// together. Smaller than the range of burial above, so a basin is
    /// PREDOMINANTLY shallow or deep rather than every basin spanning every
    /// window and averaging out to the same prospectivity.
    /// </summary>
    private const double HorizonReliefMetres = 1600.0;

    /// <summary>
    /// The column a closure needs before it is worth calling a trap. Twenty
    /// metres: below that the structure is a wrinkle, and a basin full of
    /// wrinkles would bury the real prospects in noise.
    /// </summary>
    private const double MinimumClosureHeightMetres = 20.0;

    // ---------------------------------------------------------- surface

    private static GeneratedSurface GenerateSurface(
        WorldParameters parameters, StepStreams streams,
        IReadOnlyList<TerrainClassDefinition> terrainClasses)
    {
        IRandomStream surface = streams.For(WorldStep.Surface);

        int cells = parameters.WidthCells * parameters.HeightCells;
        var elevation = new double[cells];

        for (int i = 0; i < cells; i++)
            elevation[i] = (surface.NextUnit() - parameters.LandFraction) * 400.0;

        var heightfield = new Heightfield(
            new Length(CellSizeMetres), parameters.WidthCells, parameters.HeightCells,
            [.. elevation]);

        // Sea level is elevation zero, so bathymetry is the same field going
        // negative — harbour depth falls out rather than being authored.
        (int[] classByCell, IReadOnlyList<ContentId> classes) = ClassifyTerrain(
            elevation, parameters.WidthCells, parameters.HeightCells, terrainClasses);

        var terrain = new GeneratedTerrain(heightfield, [.. classByCell], classes, [], []);

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
    /// Catalog C16's amendment: height and slope only (no per-cell climate
    /// exists to classify by — finding 242), read straight off the loaded
    /// content rather than a second, hand-duplicated table (law L5).
    ///
    /// <para><b>Bands are PERCENTILE, not absolute metres.</b> The heightfield's
    /// amplitude is a function of <c>WorldParameters.LandFraction</c> — a
    /// mostly-land world compresses toward zero relief — so a fixed metre
    /// threshold would make mountains vanish from some worlds and cover others
    /// entirely. Terciles of THIS world's own land cells guarantee every
    /// generated basin has some flat ground and some steep ground, which is the
    /// property the six-class table is describing.</para>
    ///
    /// <para>No draw: slope is a pure function of the already-generated
    /// heightfield, so this adds nothing to any RNG stream and every existing
    /// seeded test's harbours and settlements are unaffected.</para>
    /// </summary>
    private static (int[] ClassByCell, IReadOnlyList<ContentId> Classes) ClassifyTerrain(
        double[] elevation, int width, int height,
        IReadOnlyList<TerrainClassDefinition> terrainClasses)
    {
        int cells = elevation.Length;
        var slope = new double[cells];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int cell = (y * width) + x;
                double here = elevation[cell];
                double steepest = 0.0;

                if (x > 0) steepest = Math.Max(steepest, Math.Abs(here - elevation[cell - 1]));
                if (x < width - 1)
                    steepest = Math.Max(steepest, Math.Abs(here - elevation[cell + 1]));
                if (y > 0) steepest = Math.Max(steepest, Math.Abs(here - elevation[cell - width]));
                if (y < height - 1)
                    steepest = Math.Max(steepest, Math.Abs(here - elevation[cell + width]));

                slope[cell] = steepest;
            }
        }

        // Only LAND feeds the cuts — however much sea a run happens to draw
        // must not dilute the terrain mix of the land that is actually there.
        var landHeight = new List<double>();
        var landSlope = new List<double>();
        for (int i = 0; i < cells; i++)
        {
            if (elevation[i] < 0.0) continue;
            landHeight.Add(elevation[i]);
            landSlope.Add(slope[i]);
        }

        (double heightLow, double heightHigh) = Terciles(landHeight);
        (double slopeLow, double slopeHigh) = Terciles(landSlope);

        var classByCell = new int[cells];

        for (int i = 0; i < cells; i++)
        {
            if (elevation[i] < 0.0) { classByCell[i] = -1; continue; }   // C16: sea is not a class

            TerrainHeightBand heightBand = elevation[i] < heightLow ? TerrainHeightBand.Low
                : elevation[i] < heightHigh ? TerrainHeightBand.Mid : TerrainHeightBand.High;

            TerrainSlopeBand slopeBand = slope[i] < slopeLow ? TerrainSlopeBand.Flat
                : slope[i] < slopeHigh ? TerrainSlopeBand.Moderate : TerrainSlopeBand.Steep;

            classByCell[i] = MatchingClassIndex(terrainClasses, heightBand, slopeBand);
        }

        var classes = new List<ContentId>(terrainClasses.Count);
        for (int i = 0; i < terrainClasses.Count; i++) classes.Add(terrainClasses[i].Id);

        return (classByCell, classes);
    }

    /// <summary>
    /// The value at the 1/3 and 2/3 rank of a copy of <paramref name="values"/>,
    /// sorted. (0, 0) when there are none — never consulted, since the caller
    /// only reaches a land cell's band lookup when land cells exist.
    /// </summary>
    private static (double Low, double High) Terciles(List<double> values)
    {
        if (values.Count == 0) return (0.0, 0.0);

        var sorted = new double[values.Count];
        values.CopyTo(sorted);
        Array.Sort(sorted);

        int low = sorted.Length / 3;
        int high = (sorted.Length * 2) / 3;

        return (sorted[low], sorted[Math.Min(high, sorted.Length - 1)]);
    }

    /// <summary>
    /// The one class whose declared bands cover (<paramref name="heightBand"/>,
    /// <paramref name="slopeBand"/>) via its wildcard <c>Any</c> climate entry —
    /// there is no per-cell climate to prefer a specific one with (finding 242),
    /// so only a class reachable without one can ever be picked.
    ///
    /// <para>Refuses rather than guesses if the shipped content does not
    /// partition the grid exactly: catalog C16's amendment pins the four
    /// reachable classes' bands as a partition with no gap and no overlap
    /// precisely so this can be an invariant rather than a tie-break rule.</para>
    /// </summary>
    private static int MatchingClassIndex(
        IReadOnlyList<TerrainClassDefinition> terrainClasses,
        TerrainHeightBand heightBand, TerrainSlopeBand slopeBand)
    {
        int found = -1;

        for (int i = 0; i < terrainClasses.Count; i++)
        {
            TerrainFormation formation = terrainClasses[i].Formation;

            if (!Contains(formation.ClimateBands, TerrainClimateBand.Any)) continue;
            if (!Contains(formation.HeightBand, heightBand)) continue;
            if (!Contains(formation.SlopeBand, slopeBand)) continue;

            if (found >= 0)
                throw new ModelFault("catalog C16", null,
                    $"both '{terrainClasses[found].Id.Value}' and '{terrainClasses[i].Id.Value}' " +
                    $"match ({heightBand}, {slopeBand}); the four reachable terrain classes must " +
                    "partition the (height, slope) grid with no overlap");

            found = i;
        }

        if (found < 0)
            throw new ModelFault("catalog C16", null,
                $"no terrain class reachable without climate covers ({heightBand}, {slopeBand}); " +
                "the four reachable classes must partition the (height, slope) grid with no gap");

        return found;
    }

    private static bool Contains<T>(IReadOnlyList<T> values, T target) where T : struct, Enum
    {
        for (int i = 0; i < values.Count; i++)
            if (values[i].Equals(target)) return true;

        return false;
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

            // WHAT THE STRUCTURE COULD HOLD, not what is in it (SDD-010 §4b).
            //
            // Regional gravity and magnetics see a trap. Observing oil-in-place
            // instead would be impossible for a dry structure — there is none,
            // and ln(0) is undefined — and, far worse, a belief that existed
            // only for charged traps would tell a player which prospects hold
            // oil for free. The PRESENCE of a reading would be the leak.
            //
            // Capacity exists for every closed high, so dry and charged
            // prospects are indistinguishable from the surface. That is the
            // position a company is really in, and the reason POS is worth
            // computing at all.
            double truth = DetMath.Ln(accumulation.Capacity.CubicMetres);

            sink.DeliverRegionalObservation(new Observation(
                Subject: new EntityRef(EntityKind.Prospect, (ulong)(i + 1)),
                PropertyKind: new ContentId("structure-capacity"),
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
    ///
    /// <para>Two layers, one refusal (finding 288). The physical guards are
    /// what the quantities MEAN — a maturity of 1.4 is nonsense whatever any
    /// template says — and the TEMPLATE's ranges are what this archetype is
    /// tuned and measured for. Both lists land in the same fault, so a caller
    /// told one of three problems is never fixing one of three.</para>
    /// </summary>
    private static void Validate(WorldParameters parameters, WorldTemplateDefinition template)
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

        if (parameters.WidthCells < template.MinimumWidthCells
            || parameters.WidthCells > template.MaximumWidthCells)
            problems.Add($"width {parameters.WidthCells} cells is outside the " +
                $"[{template.MinimumWidthCells}, {template.MaximumWidthCells}] " +
                $"template '{template.Id.Value}' is tuned for");

        if (parameters.HeightCells < template.MinimumHeightCells
            || parameters.HeightCells > template.MaximumHeightCells)
            problems.Add($"height {parameters.HeightCells} cells is outside the " +
                $"[{template.MinimumHeightCells}, {template.MaximumHeightCells}] " +
                $"template '{template.Id.Value}' is tuned for");

        if (parameters.LandFraction < template.MinimumLandFraction
            || parameters.LandFraction > template.MaximumLandFraction)
            problems.Add($"land fraction {Format(parameters.LandFraction)} is outside the " +
                $"[{Format(template.MinimumLandFraction)}, {Format(template.MaximumLandFraction)}] " +
                $"template '{template.Id.Value}' is tuned for");

        if (parameters.ResourceRichness < template.MinimumResourceRichness
            || parameters.ResourceRichness > template.MaximumResourceRichness)
            problems.Add($"resource richness {Format(parameters.ResourceRichness)} is outside the " +
                $"[{Format(template.MinimumResourceRichness)}, {Format(template.MaximumResourceRichness)}] " +
                $"template '{template.Id.Value}' is tuned for");

        if (parameters.BasinMaturity < template.MinimumBasinMaturity
            || parameters.BasinMaturity > template.MaximumBasinMaturity)
            problems.Add($"basin maturity {Format(parameters.BasinMaturity)} is outside the " +
                $"[{Format(template.MinimumBasinMaturity)}, {Format(template.MaximumBasinMaturity)}] " +
                $"template '{template.Id.Value}' is tuned for");

        if (parameters.ClimateSeverity < template.MinimumClimateSeverity
            || parameters.ClimateSeverity > template.MaximumClimateSeverity)
            problems.Add($"climate severity {Format(parameters.ClimateSeverity)} is outside the " +
                $"[{Format(template.MinimumClimateSeverity)}, {Format(template.MaximumClimateSeverity)}] " +
                $"template '{template.Id.Value}' is tuned for");

        if (parameters.RivalCount < template.MinimumRivalCount
            || parameters.RivalCount > template.MaximumRivalCount)
            problems.Add($"rival count {parameters.RivalCount} is outside the " +
                $"[{template.MinimumRivalCount}, {template.MaximumRivalCount}] " +
                $"template '{template.Id.Value}' is tuned for");

        if (problems.Count > 0)
            throw new ModelFault("SDD-010 §4", null,
                "world parameters are out of range: " + string.Join("; ", problems));
    }

    /// <summary>The generated grid's own extent, counter-clockwise from the
    /// origin — the real basin footprint, unlike <see cref="SquareAround"/>'s
    /// placeholder square.</summary>
    private static Polygon WholeMap(WorldParameters parameters)
    {
        double width = parameters.WidthCells * CellSizeMetres;
        double height = parameters.HeightCells * CellSizeMetres;

        return new Polygon(
        [
            new Coordinate(0.0, 0.0), new Coordinate(width, 0.0),
            new Coordinate(width, height), new Coordinate(0.0, height),
        ]);
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
