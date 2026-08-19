// R15's verification suite (R15 §4, SDD-010).
//
// R15-V10 IS A LEAK TEST. If starting beliefs are too accurate, the exploration
// game is decorative: the player already knows where the oil is and every survey
// they buy is a formality. It is the end-to-end proof of R14's wall.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.World;

namespace OGSim.World.Tests;

/// <summary>Records everything the generator emits, so a test can inspect the
/// whole handoff rather than a projection of it.</summary>
internal sealed class RecordingSink : IWorldSink
{
    public List<GeneratedAccumulation> Accumulations { get; } = [];
    public List<Observation> Observations { get; } = [];
    public List<Jurisdiction> Jurisdictions { get; } = [];
    public GeneratedSurface? Surface { get; private set; }

    public void AddAccumulation(GeneratedAccumulation accumulation) =>
        Accumulations.Add(accumulation);

    public void SetSurface(GeneratedSurface surface) => Surface = surface;

    public void AddClimateRegion(ClimateRegion region) { }

    public void AddJurisdiction(Jurisdiction jurisdiction) => Jurisdictions.Add(jurisdiction);

    public void DeliverRegionalObservation(Observation observation) =>
        Observations.Add(observation);
}

public class WorldGenerationTests
{
    private static WorldParameters Parameters(
        int width = 64, int height = 64,
        double richness = 1.0, double maturity = 0.5, double land = 0.5) =>
        new(new ContentId("north-sea-analogue"), width, height, land,
            richness, maturity, ClimateSeverity: 1.0, RivalCount: 4, StartEra: Era.E2);

    private static RecordingSink Generate(ulong seed, WorldParameters? parameters = null)
    {
        var sink = new RecordingSink();

        new BasinWorldGenerator().Generate(
            parameters ?? Parameters(), sink,
            new RandomSource(seed).Stream(StreamId.WorldGen));

        return sink;
    }

    /// <summary>
    /// The structures charge actually reached. The sink receives EVERY closed
    /// high now (SDD-010 §4b) — a dry one is a prospect a company can drill and
    /// lose on, which is the whole point — so a test about volumes, depths or
    /// how much was charged has to say which it means.
    /// </summary>
    private static IReadOnlyList<GeneratedAccumulation> Charged(RecordingSink sink)
    {
        var found = new List<GeneratedAccumulation>();

        for (int i = 0; i < sink.Accumulations.Count; i++)
            if (sink.Accumulations[i].Compartments.Count > 0) found.Add(sink.Accumulations[i]);

        return found;
    }

    // ------------------------------------------------------------ PV7

    [Fact] // PV7 / R15-V1: the same seed reproduces the world exactly
    public void PV7_the_same_seed_regenerates_an_identical_world()
    {
        RecordingSink first = Generate(20240801UL);
        RecordingSink second = Generate(20240801UL);

        Assert.Equal(first.Accumulations.Count, second.Accumulations.Count);
        Assert.True(first.Accumulations.Count > 0, "the world should contain something");

        for (int i = 0; i < first.Accumulations.Count; i++)
        {
            GeneratedAccumulation a = first.Accumulations[i];
            GeneratedAccumulation b = second.Accumulations[i];

            Assert.Equal(a.Play, b.Play);
            Assert.Equal(a.Subtlety, b.Subtlety);
            Assert.Equal(a.Access, b.Access);
            Assert.Equal(a.Fluid, b.Fluid);

            // COMPARED ELEMENT BY ELEMENT, deliberately. A record's generated
            // equality does NOT recurse into an IReadOnlyList member — it
            // compares the list by reference — so `Assert.Equal(a, b)` on the
            // accumulation would pass for two worlds whose compartments
            // differed in every field. A first draft of this test did exactly
            // that and failed on two IDENTICAL worlds, which is how the trap
            // was found; it would just as happily have passed on two different
            // ones.
            Assert.Equal(a.Compartments.Count, b.Compartments.Count);
            for (int c = 0; c < a.Compartments.Count; c++)
                Assert.Equal(a.Compartments[c], b.Compartments[c]);
        }

        Assert.Equal(first.Observations, second.Observations);
        Assert.Equal(first.Jurisdictions, second.Jurisdictions);
    }

    [Fact] // Different seeds give different worlds — the test above is not vacuous
    public void PV7_a_different_seed_gives_a_different_world()
    {
        RecordingSink a = Generate(1UL);
        RecordingSink b = Generate(2UL);

        Assert.NotEqual(a.Accumulations, b.Accumulations);
    }

    // ------------------------------------------------------------ §1

    [Fact] // SDD-010 §1: step substreams are INDEPENDENT
    public void PV7_each_step_draws_from_its_own_substream()
    {
        var streams = new StepStreams(12345UL);

        IRandomStream charge = streams.For(WorldStep.Charge);
        IRandomStream surface = streams.For(WorldStep.Surface);

        // Drawing from one must not advance the other. This is the property
        // that lets step 7 change without shifting step 9 — the whole reason
        // the substreams exist, and the thing a single shared stream would lose.
        double surfaceFirst = surface.NextUnit();

        var fresh = new StepStreams(12345UL);
        for (int i = 0; i < 100; i++) fresh.For(WorldStep.Charge).NextUnit();

        Assert.Equal(surfaceFirst, fresh.For(WorldStep.Surface).NextUnit(), precision: 15);

        // And the same step name always gives the same stream instance, so a
        // step cannot accidentally restart its own sequence mid-pipeline.
        Assert.Same(charge, streams.For(WorldStep.Charge));
    }

    [Fact] // Substreams differ from each other — names are not colliding
    public void PV7_different_step_names_give_different_sequences()
    {
        var streams = new StepStreams(999UL);

        var seen = new List<double>();
        foreach (string step in WorldStep.InOrder) seen.Add(streams.For(step).NextUnit());

        Assert.Equal(WorldStep.InOrder.Count, seen.Distinct().Count());
    }

    [Fact] // The hash is platform-stable — string.GetHashCode would not be
    public void PV7_the_step_hash_is_stable_across_processes()
    {
        // FNV-1a, computed here independently. string.GetHashCode is randomised
        // per process and would make a world unreproducible between two runs of
        // the SAME binary — the one determinism failure a seed cannot fix.
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong expected = offset;
        foreach (char c in WorldStep.Charge)
        {
            expected ^= c;
            expected *= prime;
        }

        Assert.Equal(expected, StepStreams.Hash(WorldStep.Charge));
    }

    // ------------------------------------------------------------ R15-V10

    [Fact] // R15-V10: starting beliefs are COARSE — the leak test
    public void R15V10_starting_beliefs_do_not_encode_truth()
    {
        // Found by scanning rather than hard-coded: whether a given seed happens
        // to place a D0 trap is exactly the kind of incidental fact a test
        // should not depend on, and a seed that produced none would have made
        // this leak test pass by having nothing to check.
        RecordingSink world = FirstWorldWithObservations();

        Assert.NotEmpty(world.Observations);

        foreach (Observation observation in world.Observations)
        {
            // A regional pass is gravity and magnetics over a whole basin. A
            // sigma this large spans nearly an order of magnitude either way in
            // log space — which is what stops a player booking reserves off it
            // and never buying seismic.
            Assert.True(observation.Sigma >= 1.0,
                $"regional sigma {observation.Sigma} is too sharp to be regional data");

            Assert.Equal(BeliefSpace.Log, observation.Space);
            Assert.Equal(Provenance.Analogue, observation.Source);
        }
    }

    [Fact] // R15-V10: ZERO information about above-tier accumulations
    public void R15V10_subtle_accumulations_get_no_observation_at_all()
    {
        RecordingSink world = Generate(4242UL);

        // Regional data sees D0 only. Everything subtler is SILENT — not vague.
        // A vague reading still says "something is here", and the whole point of
        // a subtlety class is that a subtle trap is invisible until the survey
        // tier catches up with it.
        // EVERY D0 STRUCTURE, charged or not (SDD-010 §4b). Regional data
        // reports what a trap could hold, which a dry one has as much as a full
        // one — and that is deliberate: a reading that appeared only for charged
        // structures would tell a player which prospects hold oil for free, so
        // its very presence would be the leak.
        int visible = world.Accumulations.Count(a => a.Subtlety == DetectClass.D0);

        Assert.Equal(visible, world.Observations.Count);

        Assert.Contains(world.Accumulations, a => a.Subtlety != DetectClass.D0);
    }

    [Fact] // R15-V10: the belief that results is genuinely uninformative
    public void R15V10_the_regional_belief_spans_an_order_of_magnitude()
    {
        RecordingSink world = FirstWorldWithObservations();
        Observation observation = world.Observations[0];

        var belief = new Belief(
            observation.Value, observation.Sigma, observation.Space,
            observation.Source, new GameDate(1965, 1));

        // P10/P90 more than an order of magnitude apart: the company knows
        // there is a basin, and essentially nothing about how big the prize is.
        double low = OGSim.Information.Quantiles.P90(belief);
        double high = OGSim.Information.Quantiles.P10(belief);

        Assert.True(high / low > 10.0,
            $"the regional belief spans only {high / low:F1}x — too informative for step zero");
    }

    /// <summary>The first world carrying regional data. Scanned, because whether
    /// a seed places a D0 trap is incidental — and a leak test that silently had
    /// nothing to inspect would be the worst possible outcome.</summary>
    private static RecordingSink FirstWorldWithObservations()
    {
        for (ulong seed = 1; seed <= 500; seed++)
        {
            RecordingSink world = Generate(seed);
            if (world.Observations.Count > 0) return world;
        }

        throw new InvalidOperationException(
            "no seed in 500 produced regional data; the D0 gate is refusing everything");
    }

    // ------------------------------------------------------------ R15-V7

    [Fact] // R15-V7: a meaningful fraction of valid traps are UNCHARGED
    public void R15V7_fill_spill_leaves_traps_empty()
    {
        // Across many seeds, the charged fraction must be well below one. Empty
        // traps fall out of fill-spill rather than being made by a rule, and a
        // world where every trap is charged would make exploration a formality
        // in the other direction.
        int worlds = 0, withAccumulations = 0;
        int totalAccumulations = 0;

        for (ulong seed = 1; seed <= 200; seed++)
        {
            RecordingSink world = Generate(seed);
            worlds++;

            if (Charged(world).Count > 0) withAccumulations++;
            totalAccumulations += Charged(world).Count;
        }

        Assert.True(worlds > 0);
        Assert.True(totalAccumulations > 0, "no world produced any accumulation");

        // CHARGED TRAPS EXIST AND SO DO DRY ONES, asserted by comparing two
        // charge settings on the same basins rather than against an absolute
        // count.
        //
        // The absolute number used to be a fair proxy — traps were a draw of
        // four to six, so "fewer than ten a world" meant something. Since
        // R20d.8.6 they are the closed highs of a generated horizon, so how many
        // a basin HAS is a property of its structure (around sixty-five at this
        // grid size) and no fixed number can distinguish a spilling algorithm
        // from a generous one. What still distinguishes them is that raising the
        // charge admits traps a lower setting turned away: if nothing were being
        // rejected, both settings would return every closure and these would be
        // equal.
        //
        // R20d.8.7 made the algorithm underneath this the real one — charge
        // migrating up-dip and running out — so R20d8V6 now asks the sharper
        // question this one only approaches: WHERE the newly filled traps are.
        var generous = 0;

        for (ulong seed = 1; seed <= 200; seed++)
            generous += Charged(Generate(seed, Parameters(richness: 2.0))).Count;

        Assert.True(generous > totalAccumulations,
            $"a basin charged twice as hard yielded no more accumulations " +
            $"({generous} against {totalAccumulations}); fill-spill is not spilling");
    }

    // ------------------------------------------------------------ parameters

    [Fact] // A richer basin yields more, without inventing content
    public void R15V1_resource_richness_scales_what_is_generated()
    {
        long lean = 0, rich = 0;

        for (ulong seed = 1; seed <= 60; seed++)
        {
            lean += Charged(Generate(seed, Parameters(richness: 0.5))).Count;
            rich += Generate(seed, Parameters(richness: 1.0)).Accumulations.Count;
        }

        // A parameter SELECTS and SCALES; it never invents a table.
        Assert.True(rich > lean, $"richness did not scale charge: {rich} against {lean}");
    }

    [Fact] // SDD-010 §4: out-of-range names EVERY violation and never clamps
    public void R15V1_out_of_range_parameters_are_refused_with_all_reasons()
    {
        var fault = Assert.Throws<ModelFault>(() => Generate(1UL, new WorldParameters(
            new ContentId("bad"), WidthCells: 0, HeightCells: 0,
            LandFraction: 1.5, ResourceRichness: 0.0, BasinMaturity: 2.0,
            ClimateSeverity: -1.0, RivalCount: -3, StartEra: Era.E1)));

        // A clamped world would start playable and wrong, and the player would
        // never learn which knob they had set impossibly.
        Assert.Contains("grid dimensions", fault.Fault.Detail);
        Assert.Contains("land fraction", fault.Fault.Detail);
        Assert.Contains("resource richness", fault.Fault.Detail);
        Assert.Contains("basin maturity", fault.Fault.Detail);
        Assert.Contains("climate severity", fault.Fault.Detail);
        Assert.Contains("rival count", fault.Fault.Detail);
    }

    // ------------------------------------------------------------ derivation

    [Fact] // SDD-010 §2.7: access requirements are DERIVED, never authored
    public void R15V1_access_requirements_follow_from_generated_depth()
    {
        RecordingSink world = Generate(555UL);

        foreach (GeneratedAccumulation accumulation in Charged(world))
        {
            Length depth = accumulation.Compartments[0].Depth;

            DepthClass expected = depth.Metres switch
            {
                < 1500.0 => DepthClass.Shallow,
                < 3000.0 => DepthClass.Standard,
                < 4500.0 => DepthClass.Deep,
                _ => DepthClass.UltraDeep,
            };

            // A deep discovery is hard BECAUSE it is deep — the gate reads a
            // generated fact rather than a flag somebody set alongside it.
            Assert.Equal(expected, accumulation.Access.Depth);
            Assert.Equal(depth.Metres > 4000.0, accumulation.Access.Hpht);
        }
    }

    [Fact] // The surface's bathymetry IS the heightfield below zero
    public void R15V7a_bathymetry_is_the_same_field_going_negative()
    {
        RecordingSink world = Generate(88UL);
        Assert.NotNull(world.Surface);
        GeneratedSurface surface = world.Surface;

        Heightfield field = surface.Terrain.Elevation;
        Assert.Equal(64 * 64, field.ElevationMetres.Length);

        // Sea is class 0, land class 1 — and the split is the sign of the
        // elevation, so harbour depth falls out of the same field rather than
        // being authored beside it.
        for (int i = 0; i < field.ElevationMetres.Length; i++)
        {
            int expected = field.ElevationMetres[i] < 0.0 ? 0 : 1;
            Assert.Equal(expected, surface.Terrain.ClassByCell[i]);
        }
    }

    // ------------------------------------------------- where everything is (R20d.8)
    //
    // Until this the surface was a heightfield and four empty lists, and every
    // accumulation was a 5×5 square in a row at x = index·10 — so "where" was a
    // slot number, not a place. Nothing offshore existed, because nothing ever
    // asked the ground what was above a trap.

    /// <summary>
    /// A basin has somewhere to load a cargo, and the harbour's depth is the
    /// water it touches — the same heightfield going negative rather than a
    /// second map (SDD-010 §3).
    /// </summary>
    [Fact]
    public void R20d8V3_a_coastline_produces_harbours_with_depth()
    {
        IReadOnlyList<Harbour> harbours = Generate(11UL).Surface!.Harbours;

        Assert.NotEmpty(harbours);

        for (int i = 0; i < harbours.Count; i++)
            Assert.True(harbours[i].Depth.Metres > 0.0,
                "a harbour on dry land is not a harbour");
    }

    /// <summary>
    /// A LAND-LOCKED BASIN HAS NONE, and gets no settlements either. The
    /// mechanic is that the world decides, not that every world is the same
    /// shape — a basin with no coast is a real basin, and it has to reach market
    /// some other way.
    /// </summary>
    [Fact]
    public void R20d8V3_a_basin_with_no_sea_has_no_harbours()
    {
        // Land fraction 0 puts the sea-level percentile below every cell.
        RecordingSink dry = Generate(11UL, Parameters(land: 0.0));

        Assert.Empty(dry.Surface!.Harbours);
        Assert.Empty(dry.Surface!.Settlements);
    }

    /// <summary>
    /// Towns are ranked, not uniform: a basin gets one real port and a scatter
    /// of smaller places. The distribution is what decides where labour and
    /// complaints come from, so a flat one would make every location equivalent.
    /// </summary>
    [Fact]
    public void R20d8V3_settlements_are_ranked_by_population()
    {
        IReadOnlyList<Settlement> towns = Generate(3UL).Surface!.Settlements;

        Assert.True(towns.Count > 1, "a coastline produced fewer than two settlements");

        long biggest = 0, smallest = long.MaxValue;

        for (int i = 0; i < towns.Count; i++)
        {
            biggest = Math.Max(biggest, towns[i].Population);
            smallest = Math.Min(smallest, towns[i].Population);
        }

        Assert.True(biggest > smallest * 2,
            $"the largest settlement ({biggest}) is not materially bigger than the " +
            $"smallest ({smallest})");
    }

    /// <summary>
    /// AN ACCUMULATION IS SOMEWHERE. Traps land on distinct cells of the
    /// generated grid rather than in a row, so two prospects are genuinely in
    /// different places and the distance between them is a real number.
    /// </summary>
    [Fact]
    public void R20d8V3_accumulations_sit_at_distinct_places_on_the_map()
    {
        IReadOnlyList<GeneratedAccumulation> found = Charged(Generate(21UL));

        Assert.True(found.Count > 1, "one accumulation cannot show a spatial spread");

        var places = new HashSet<Coordinate>();
        for (int i = 0; i < found.Count; i++) places.Add(found[i].Closure.Centroid);

        Assert.True(places.Count > 1, "every accumulation was generated at the same place");
    }

    /// <summary>
    /// AND ITS FOOTPRINT IS ITS SIZE. Closure area grows with the volume drawn,
    /// which is what a well drilled into it drains — so a big field is big in
    /// the ground AND on the map, and the two agree.
    /// </summary>
    [Fact]
    public void R20d8V3_a_bigger_accumulation_has_a_bigger_closure()
    {
        IReadOnlyList<GeneratedAccumulation> found = Charged(Generate(21UL));

        GeneratedAccumulation biggest = found[0], smallest = found[0];

        for (int i = 1; i < found.Count; i++)
        {
            if (Volume(found[i]) > Volume(biggest)) biggest = found[i];
            if (Volume(found[i]) < Volume(smallest)) smallest = found[i];
        }

        Assert.True(biggest.Closure.Area.SquareMetres > smallest.Closure.Area.SquareMetres,
            "the largest accumulation does not have the largest footprint");
    }

    private static double Volume(GeneratedAccumulation accumulation) =>
        accumulation.Compartments[0].PoreVolume.CubicMetres;

    /// <summary>
    /// WATER DEPTH IS READ FROM THE GROUND ABOVE THE TRAP, not declared. Every
    /// accumulation was `Onshore` before this regardless of what it sat under,
    /// which made the offshore half of the access gate unreachable — a
    /// development class the game could describe and never present.
    /// </summary>
    [Fact]
    public void R20d8V3_an_accumulation_under_water_is_offshore()
    {
        var offshore = 0;

        // Across several basins: a single one can legitimately draw all its
        // traps onto dry land, and that is the world being a world.
        foreach (ulong seed in new ulong[] { 1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL, 8UL })
        {
            IReadOnlyList<GeneratedAccumulation> found = Charged(Generate(seed));

            for (int i = 0; i < found.Count; i++)
                if (found[i].Access.WaterDepth != WaterDepthClass.Onshore) offshore++;
        }

        Assert.True(offshore > 0,
            "eight basins generated with half their cells under water produced no " +
            "offshore accumulation at all; water depth is not being read from the terrain");
    }

    // ------------------------------------- the structure decides (R20d.8.6)
    //
    // A trap used to be a cell picked at random with a log-normal volume drawn
    // beside it — two numbers that had nothing to do with each other. It is now a
    // closed high on a generated horizon, and its size is the rock between its
    // crest and its spill point. Everything below asks whether that is really
    // true, because a horizon that produced the same answers as the old draw
    // would be an expensive way to keep them.

    /// <summary>
    /// R15-V3 / MB5. The size distribution is LONG-TAILED, and nothing imposes
    /// it: the biggest closure in a basin holds many times what the median one
    /// does because structures vary, which is why one field in a play is worth
    /// ten of the rest.
    /// </summary>
    [Fact]
    public void R20d8V5_field_sizes_are_long_tailed_without_being_drawn_that_way()
    {
        var volumes = new List<double>();

        for (ulong seed = 1; seed <= 20; seed++)
        {
            IReadOnlyList<GeneratedAccumulation> found = Charged(Generate(seed));

            for (int i = 0; i < found.Count; i++)
                volumes.Add(found[i].Compartments[0].PoreVolume.CubicMetres);
        }

        volumes.Sort();

        double median = volumes[volumes.Count / 2];
        double biggest = volumes[^1];

        Assert.True(biggest > median * 10.0,
            $"the largest accumulation ({biggest:0}) is only {biggest / median:0.0}x the " +
            "median; the structure is producing one size of field");
    }

    /// <summary>
    /// A TRAP'S SIZE IS ITS OWN STRUCTURE. Volume and footprint move together,
    /// because both are read off the same closure — which is what makes a big
    /// field big on the map as well as in the ground, and what lets a seismic
    /// survey of the surface say anything about the volume beneath it.
    /// </summary>
    [Fact]
    public void R20d8V5_volume_and_footprint_come_from_the_same_closure()
    {
        IReadOnlyList<GeneratedAccumulation> found = Charged(Generate(4UL));

        Assert.True(found.Count > 2, "too few accumulations to compare");

        GeneratedAccumulation biggest = found[0], smallest = found[0];

        for (int i = 1; i < found.Count; i++)
        {
            if (Volume(found[i]) > Volume(biggest)) biggest = found[i];
            if (Volume(found[i]) < Volume(smallest)) smallest = found[i];
        }

        Assert.True(biggest.Closure.Area.SquareMetres > smallest.Closure.Area.SquareMetres,
            "the accumulation holding the most oil does not cover the most ground");
    }

    /// <summary>
    /// DEPTH IS WHERE THE CREST IS. It used to be an independent uniform draw,
    /// so a basin's deep traps and its big ones were unrelated — and every
    /// consequence of depth (pressure, temperature, access class, whether the
    /// well is HPHT) was therefore unrelated to the structure it came from.
    ///
    /// <para>Read off the horizon, the basin has a shallow side and a deep one,
    /// so depths spread across the relief instead of clustering.</para>
    /// </summary>
    [Fact]
    public void R20d8V5_depth_is_read_from_the_horizon()
    {
        var depths = new List<double>();

        for (ulong seed = 1; seed <= 10; seed++)
        {
            IReadOnlyList<GeneratedAccumulation> found = Charged(Generate(seed));

            for (int i = 0; i < found.Count; i++)
                depths.Add(found[i].Compartments[0].Depth.Metres);
        }

        depths.Sort();

        Assert.True(depths[0] >= 1000.0, "a trap surfaced above the horizon's crest");
        Assert.True(depths[^1] - depths[0] > 500.0,
            $"every trap is at much the same depth ({depths[0]:0} to {depths[^1]:0}); " +
            "the regional dip is not reaching the traps");
    }

    /// <summary>
    /// AND PRESSURE FOLLOWS DEPTH, which is the chain the whole slice exists to
    /// make real: where a structure sits decides what it is worth and what it
    /// takes to produce it. A deeper crest is a higher initial pressure, every
    /// time, because both are read from the same number.
    /// </summary>
    [Fact]
    public void R20d8V5_a_deeper_trap_starts_at_a_higher_pressure()
    {
        IReadOnlyList<GeneratedAccumulation> found = Charged(Generate(4UL));

        GeneratedCompartment deepest = found[0].Compartments[0];
        GeneratedCompartment shallowest = found[0].Compartments[0];

        for (int i = 1; i < found.Count; i++)
        {
            GeneratedCompartment at = found[i].Compartments[0];

            if (at.Depth.Metres > deepest.Depth.Metres) deepest = at;
            if (at.Depth.Metres < shallowest.Depth.Metres) shallowest = at;
        }

        Assert.True(deepest.InitialPressure.Pascals > shallowest.InitialPressure.Pascals,
            "the deepest trap does not start at the highest pressure");
    }

    // ------------------------------------------- fill-spill migrates (R20d.8.7)

    /// <summary>
    /// R15-V7, asked properly at last. Charge is cooked deep and migrates
    /// UP-DIP, so a richer basin does not simply fill the same traps fuller — it
    /// reaches FURTHER, and the traps it newly reaches are the shallower ones.
    ///
    /// <para>This is the whole difference between fill-spill and the per-trap
    /// coin flip it replaced. A probability would charge deep and shallow traps
    /// alike, so raising it would push the shallowest charged trap nowhere in
    /// particular. Because charge has a direction, more of it means oil arrives
    /// somewhere it previously never got to — which is what makes a dry hole
    /// informative about its neighbours and "drill the up-dip prospect" a choice
    /// that can be wrong.</para>
    /// </summary>
    [Fact]
    public void R20d8V6_more_charge_reaches_further_up_dip()
    {
        var lean = double.MaxValue;
        var rich = double.MaxValue;

        for (ulong seed = 1; seed <= 30; seed++)
        {
            lean = Math.Min(lean, ShallowestCharged(seed, richness: 0.5));
            rich = Math.Min(rich, ShallowestCharged(seed, richness: 2.0));
        }

        Assert.True(rich < lean,
            $"a basin charged four times as hard reached no further up-dip " +
            $"({rich:0} m against {lean:0} m); migration has no direction");
    }

    /// <summary>The shallowest trap that received any oil — how far up-dip the
    /// charge got. Infinity for a basin that charged nothing at all.</summary>
    private static double ShallowestCharged(ulong seed, double richness)
    {
        IReadOnlyList<GeneratedAccumulation> found =
            Charged(Generate(seed, Parameters(richness: richness)));

        var shallowest = double.MaxValue;

        for (int i = 0; i < found.Count; i++)
            shallowest = Math.Min(shallowest, found[i].Compartments[0].Depth.Metres);

        return shallowest;
    }

    /// <summary>
    /// AND A BASIN IS NEVER FULL. The kitchen cooks less than the traps could
    /// hold, so there is always somewhere the charge did not reach — which is
    /// what makes exploration a search rather than an inventory.
    /// </summary>
    [Fact]
    public void R20d8V6_some_structures_are_always_dry()
    {
        // Compared against the same basins charged far harder: whatever the
        // absolute counts, a lean basin must leave traps the rich one fills.
        int lean = 0, rich = 0;

        for (ulong seed = 1; seed <= 30; seed++)
        {
            lean += Charged(Generate(seed, Parameters(richness: 0.5))).Count;
            rich += Charged(Generate(seed, Parameters(richness: 2.0))).Count;
        }

        Assert.True(rich > lean,
            $"charging four times as hard filled no more traps ({rich} against {lean})");
    }

    // ------------------------------------ the source has to have cooked (R20d.10)

    /// <summary>
    /// R15-V5. A BASIN CAN BE DRY, and that is design 06 §5's promise of
    /// "elephants, marginal fields, dry basins" made real. Charge is what a
    /// source rock generated, and a source rock that never reached the oil
    /// window generated nothing — however many fine structures sit above it.
    ///
    /// <para>Until this every basin had charge in proportion to its traps, so
    /// the answer to "is there anything here at all?" was always yes and only
    /// the amount varied. A player could not be wrong about a whole basin, which
    /// is the largest way to be wrong there is.</para>
    /// </summary>
    [Fact]
    public void R15V5_a_basin_whose_source_never_cooked_holds_nothing()
    {
        var barren = 0;
        var productive = 0;

        for (ulong seed = 1; seed <= 40; seed++)
        {
            RecordingSink world = Generate(seed);

            // Structures either way — this is about charge, not about geometry.
            Assert.NotEmpty(world.Accumulations);

            if (Charged(world).Count == 0) barren++; else productive++;
        }

        Assert.True(productive > 0, "forty basins and none of them held any oil");

        Assert.True(barren > 0,
            "forty basins and every one of them held oil; maturity is not reaching charge, " +
            "so 'is there anything here?' is a question with one answer");
    }

    /// <summary>
    /// AND A DRY BASIN STILL HAS PROSPECTS. The structures are real, they are
    /// mappable, and regional data reports their size exactly as it would in a
    /// basin full of oil — because gravity sees a trap and not what is in it
    /// (SDD-010 §4b).
    ///
    /// <para>That is what makes a barren basin a way of LOSING rather than a
    /// screen that says "nothing here": the company has to drill to find
    /// out.</para>
    /// </summary>
    [Fact]
    public void R15V5_a_dry_basin_looks_exactly_like_a_full_one_from_the_surface()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            RecordingSink world = Generate(seed);

            if (Charged(world).Count > 0) continue;

            // D0 structures in a barren basin still deliver regional readings.
            var visible = world.Accumulations.Count(a => a.Subtlety == DetectClass.D0);

            Assert.Equal(visible, world.Observations.Count);

            if (visible > 0) return;
        }

        Assert.Fail("no barren basin with a D0 structure in forty seeds");
    }
}
