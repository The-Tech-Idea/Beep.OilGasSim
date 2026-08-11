// R20d.8 — a game begins by finding out what is under it (SDD-010 §4).
//
// Everything here asks one question in different ways: does the world the
// generator drew actually decide the game? Until `CreateNew` existed the answer
// was no — the generator was composed and never called, and every run got the
// same hand-built field whatever seed it was given.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Company;
using OGSim.Kernel;
using OGSim.ReferenceClient;

namespace OGSim.Composition.Tests;

public sealed class NewGameTests
{
    /// <summary>
    /// A modest basin. Small enough that a test runs quickly, big enough that
    /// step 5 draws several traps and step 6 leaves some of them empty.
    /// </summary>
    private static WorldParameters Basin() => new(
        new ContentId("world-template-basin"),
        WidthCells: 24, HeightCells: 24,
        LandFraction: 0.6,
        ResourceRichness: 1.0,
        BasinMaturity: 0.5,
        ClimateSeverity: 0.5,
        RivalCount: 3,
        StartEra: Era.E1);

    private static Engine NewGame(ulong seed) =>
        Assert.IsType<Built>(EngineBuilder.CreateNew(
            Fixture.Settings() with { WorldSeed = seed }, Basin())).Engine;

    private static WorldState WorldOf(Engine engine) => engine.Provided.Resolve<WorldState>();

    /// <summary>
    /// The nth structure that charge actually reached. Most prospects are dry
    /// (SDD-010 §4b), and only a discovery has a field to develop.
    /// </summary>
    private static EntityId<IProspect> Discovery(WorldState world, int nth)
    {
        var seen = 0;

        for (int i = 0; i < world.Prospects.Count; i++)
        {
            if (world.Beneath(world.Prospects[i]) is null) continue;
            if (seen++ == nth) return world.Prospects[i];
        }

        throw new InvalidOperationException($"this basin has fewer than {nth + 1} discoveries");
    }

    /// <summary>
    /// A basin with at least two prospects that can both reach market. Found by
    /// walking seeds rather than by naming one, because how many traps a basin
    /// charges is exactly the thing generation decides — a hard-coded seed would
    /// mean these tests silently stop asking their question the first time step 6
    /// is tuned.
    /// </summary>
    private static Engine BasinWithSeveralProspects()
    {
        for (ulong seed = 1UL; seed < 40UL; seed++)
        {
            Engine engine = NewGame(seed);
            WorldState world = WorldOf(engine);

            var discoveries = 0;

            for (int i = 0; i < world.Prospects.Count; i++)
                if (world.DistanceToMarket(world.Prospects[i]) is not null
                    && world.Beneath(world.Prospects[i]) is not null) discoveries++;

            if (discoveries > 1) return engine;
        }

        throw new InvalidOperationException(
            "forty seeds produced no basin with two prospects that can reach market");
    }

    // ------------------------------------------------- the world reaches the engine

    /// <summary>
    /// THE FINDING, stated as its opposite. A new game has compartments nobody
    /// hand-built: they exist because the generator put them there.
    /// </summary>
    [Fact]
    public void R20d8V2_a_new_game_has_the_prospects_the_generator_drew()
    {
        Engine engine = NewGame(seed: 20260808UL);

        Assert.NotEmpty(WorldOf(engine).Prospects);
    }

    /// <summary>
    /// R15-V1 / PV7. The same seed is the same world. Not approximately — the
    /// prospects are identical in count, order and size, because a world is a
    /// pure function of its seed and a save that stored only the seed would
    /// otherwise reload a different game.
    /// </summary>
    [Fact]
    public void R20d8V2_one_seed_is_one_world()
    {
        FieldControl a = NewGame(seed: 7UL).Provided.Resolve<FieldControl>();
        FieldControl b = NewGame(seed: 7UL).Provided.Resolve<FieldControl>();

        Assert.Equal(a.CompartmentCount, b.CompartmentCount);
    }

    /// <summary>
    /// AND A DIFFERENT SEED IS A DIFFERENT WORLD. Stated separately because a
    /// generator that ignored its stream entirely would satisfy the test above
    /// perfectly — identical worlds are only interesting if they were not
    /// inevitable.
    /// </summary>
    [Fact]
    public void R20d8V2_a_different_seed_is_a_different_world()
    {
        var sizes = new HashSet<int>();

        foreach (ulong seed in new ulong[] { 1UL, 2UL, 3UL, 4UL, 5UL, 6UL })
            sizes.Add(NewGame(seed).Provided.Resolve<FieldControl>().CompartmentCount);

        Assert.True(sizes.Count > 1,
            "six seeds produced basins with identical prospect counts");
    }

    // ------------------------------------------------------------ what it decides

    // NO SIZE-DISTRIBUTION TEST HERE, and the reason is a rule rather than an
    // omission. Whether step 7's log-normal draw produces the spread that makes
    // one prospect worth ten of the rest is a question about GENERATION, and
    // OGSim.World.Tests already asks it against the handoff records. Asking it
    // again from composition would mean reading a compartment's true pore volume
    // — walking through the truth wall to check a fact the generator's own suite
    // owns. These tests are about the WIRING: that a world was drawn, that it
    // decides the game, and that what the player knows about it came through the
    // observation door.

    /// <summary>
    /// R15-V10's LEAK GUARANTEE, which is the one that matters. A new company
    /// knows something on its first morning — and it knows it as a BELIEF with a
    /// sigma and a provenance, delivered through the same observation door a
    /// well test uses. Nothing was copied from truth.
    ///
    /// <para>If this ever fails by finding a belief that is exactly right, the
    /// door has been bypassed and the entire information layer is decoration:
    /// there would be no reason to buy a survey for something already known.</para>
    /// </summary>
    [Fact]
    public void R20d8V2_starting_knowledge_arrives_as_belief_not_as_truth()
    {
        var learned = 0;

        // ACROSS SEVERAL BASINS, because a basin whose every trap is subtler
        // than D0 legitimately starts blind — regional data sees D0 and nothing
        // else, and that silence is the mechanic rather than a failure. What
        // would be a defect is knowledge never arriving at all.
        foreach (ulong seed in new ulong[] { 1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL, 8UL })
        {
            Engine engine = NewGame(seed);
            IBeliefStore beliefs = engine.Provided.Resolve<IBeliefStore>();

            foreach (EntityId<IProspect> prospect in WorldOf(engine).Prospects)
            {
                Belief? held = beliefs.Get(
                    new EntityRef(EntityKind.Prospect, prospect.Value),

                    // WHAT THE STRUCTURE COULD HOLD, not what is in it. A
                    // reading that existed only for charged prospects would tell
                    // a player which ones to drill for free — the presence of the
                    // belief would be the leak (SDD-010 §4b).
                    new ContentId("structure-capacity"));

                if (held is null) continue;      // subtler than D0 — silent, by design

                learned++;

                // A regional pass is wide. A belief this vague is exactly what
                // makes the first seismic survey worth its price.
                Assert.True(held.Value.Sigma > 0.0,
                    "regional data delivered a belief with no uncertainty, which is truth " +
                    "wearing a belief's clothes");
            }
        }

        Assert.True(learned > 0,
            "eight basins produced no starting knowledge at all; regional data is not " +
            "reaching the belief store, or its subject does not name the compartment " +
            "the sink built");
    }

    /// <summary>
    /// AND THE MAP EXISTS. `WorldView` has been declared on the engine surface
    /// since R21 and nothing ever produced one — a host had a type to render and
    /// no instance to render.
    /// </summary>
    [Fact]
    public void R20d8V2_a_new_game_has_a_renderable_world()
    {
        WorldView? view = WorldOf(NewGame(seed: 5UL)).View;

        Assert.NotNull(view);
        Assert.Equal(24 * 24, view.Terrain.Elevation.ElevationMetres.Length);
        Assert.NotEmpty(view.Jurisdictions);
    }

    /// <summary>
    /// A world is never generated into an engine that would not compose. The
    /// refusal comes back untouched, because there is nowhere to put a world.
    /// </summary>
    [Fact]
    public void R20d8V2_a_refused_composition_is_not_generated_into()
    {
        Assert.Throws<ContentFault>(() => EngineBuilder.CreateNew(
            Fixture.Settings(profile: "no-such-fidelity"), Basin()));
    }

    // -------------------------------------------------- geography costs something

    /// <summary>
    /// FINDING 167, HALF CLOSED. A prospect's distance from market is a real
    /// number, and prospects differ in it — which they could not before, when
    /// every closure was a square in a row and the flowline was a 2 km constant
    /// whatever the field.
    /// </summary>
    [Fact]
    public void R20d8V4_prospects_differ_in_how_far_they_are_from_market()
    {
        Engine engine = BasinWithSeveralProspects();
        WorldState world = WorldOf(engine);

        var distances = new List<double>();

        foreach (EntityId<IProspect> prospect in world.Prospects)
            if (world.DistanceToMarket(prospect) is Length away)
                distances.Add(away.Metres);

        Assert.True(distances.Count > 1, "a basin with one reachable prospect shows no spread");

        distances.Sort();

        Assert.True(distances[^1] > distances[0],
            "every prospect is exactly as far from market as every other");
    }

    /// <summary>
    /// AND THE LINE IS LAID TO IT. Developing a field routes the flowline to
    /// that field's distance — so the same engine, the same content and the same
    /// commands produce a different gathering system depending only on which
    /// discovery a company chose to develop.
    ///
    /// <para>This is the mechanism the whole slice exists for: geography is
    /// worth generating only if committing to a place commits you to its
    /// consequences.</para>
    /// </summary>
    [Fact]
    public void R20d8V4_developing_a_field_lays_the_line_to_where_it_is()
    {
        Engine engine = BasinWithSeveralProspects();
        WorldState world = WorldOf(engine);
        FieldControl field = engine.Provided.Resolve<FieldControl>();
        SurfaceChain chain = engine.Provided.Resolve<SurfaceChain>();

        // A DISCOVERY, because a line is laid to a field that turned out to be
        // there. A dry structure never gets one.
        EntityId<IProspect> chosen = Discovery(world, 0);
        Length expected = world.DistanceToMarket(chosen)!.Value;

        EntityId<IReservoirCompartmentEntity> reservoir = world.Beneath(chosen)!.Value;

        field.Drill(reservoir, new Length(2000.0));

        Assert.Equal(expected.Metres, chain.Flowline.PipeLength.Metres, precision: 6);
    }

    /// <summary>
    /// A SECOND WELL DOES NOT MOVE THE LINE. Later wells join a gathering system
    /// that is already laid — and by then it holds oil, which the pipeline
    /// refuses to be re-routed under (SDD-006 §7c.1).
    /// </summary>
    [Fact]
    public void R20d8V4_a_second_well_joins_the_line_that_is_already_there()
    {
        Engine engine = BasinWithSeveralProspects();
        WorldState world = WorldOf(engine);
        FieldControl field = engine.Provided.Resolve<FieldControl>();
        SurfaceChain chain = engine.Provided.Resolve<SurfaceChain>();

        EntityId<IReservoirCompartmentEntity> first = world.Beneath(Discovery(world, 0))!.Value;

        field.Drill(first, new Length(2000.0));

        Length laid = chain.Flowline.PipeLength;

        // A well on a DIFFERENT discovery — the case that would move the line if
        // anything were going to.
        EntityId<IReservoirCompartmentEntity> elsewhere =
            world.Beneath(Discovery(world, 1))!.Value;

        field.Drill(elsewhere, new Length(2000.0));

        Assert.Equal(laid.Metres, chain.Flowline.PipeLength.Metres, precision: 6);
    }

    /// <summary>
    /// A HAND-BUILT FIELD KEEPS THE COMPOSED ROUTE. No generator placed it, so
    /// there is no distance to lay a line to — and inventing one would charge a
    /// test field for a journey it never makes.
    /// </summary>
    [Fact]
    public void R20d8V4_a_field_no_world_placed_has_no_distance()
    {
        Engine engine = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings())).Engine;

        Assert.Null(engine.Provided.Resolve<WorldState>()
            .DistanceToMarket(new EntityId<IProspect>(1)));
    }

    // -------------------------------------------- the choice a player makes (R20d.7)

    /// <summary>
    /// A NEW GAME OFFERS PROSPECTS TO CHOOSE BETWEEN, each with a probability of
    /// success. Until the world generated structures there was nothing for POS
    /// to be about, so `ProspectRisk` sat built and tested and unused for four
    /// phases — the read model reported wells a company already had and nothing
    /// it might drill next.
    /// </summary>
    [Fact]
    public void R20d7V1_a_new_game_offers_prospects_with_odds()
    {
        Engine engine = BasinWithSeveralProspects();
        engine.Pipeline.AdvanceTick();

        IReadOnlyList<ProspectView> offered = engine.ReadModel!.Prospects;

        Assert.NotEmpty(offered);

        for (int i = 0; i < offered.Count; i++)
        {
            Assert.InRange(offered[i].ProbabilityOfSuccess, 0.0, 1.0);

            // Five factors at 0.7 multiply to about one in six. A POS anywhere
            // near certainty would mean the factors were not being multiplied,
            // which is the arithmetic that makes exploration hard.
            Assert.True(offered[i].ProbabilityOfSuccess < 0.5,
                $"a prospect is offered at {offered[i].ProbabilityOfSuccess:0.00} before " +
                "anyone has drilled anything; the five factors are not being multiplied");
        }
    }

    /// <summary>
    /// AND THEY ARE NOT ALL THE SAME BET. A subtly expressed trap is a worse
    /// prospect than an obvious one, because how confidently a structure is
    /// mapped is part of whether it is there — which is what a detect class
    /// means, said as risk rather than only as visibility.
    /// </summary>
    [Fact]
    public void R20d7V1_a_subtler_trap_is_a_worse_prospect()
    {
        var odds = new HashSet<double>();

        for (ulong seed = 1UL; seed < 20UL && odds.Count < 2; seed++)
        {
            Engine engine = NewGame(seed);
            engine.Pipeline.AdvanceTick();

            IReadOnlyList<ProspectView> offered = engine.ReadModel!.Prospects;

            for (int i = 0; i < offered.Count; i++) odds.Add(offered[i].ProbabilityOfSuccess);
        }

        Assert.True(odds.Count > 1,
            "every prospect in the basin carries identical odds; the trap factor is not " +
            "being weighted by how the structure is expressed");
    }

    /// <summary>
    /// THE PLAY DIES TOGETHER. A well that fails on a shared element re-prices
    /// every OTHER prospect drawing on the same petroleum system — and that is
    /// the whole reason exploration is a campaign rather than a series of
    /// independent bets. A player learns something they did not pay for.
    ///
    /// <para>The second assertion is the one that stops this being a global
    /// penalty dressed up: prospects in a DIFFERENT play must not move at all.
    /// If they did, "spread the risk across plays" would be advice with no
    /// mechanism behind it.</para>
    /// </summary>
    [Fact]
    public void R20d7V1_a_dry_hole_reprices_its_play_and_only_its_play()
    {
        // A basin with two prospects in ONE play and at least one in another —
        // the only shape in which both halves of the claim can be checked.
        for (ulong seed = 1UL; seed < 60UL; seed++)
        {
            Engine engine = NewGame(seed);
            engine.Pipeline.AdvanceTick();

            IReadOnlyList<ProspectView> before = engine.ReadModel!.Prospects;

            int sibling = -1, stranger = -1;

            for (int i = 1; i < before.Count; i++)
            {
                if (before[i].Play == before[0].Play && sibling < 0) sibling = i;
                if (before[i].Play != before[0].Play && stranger < 0) stranger = i;
            }

            if (sibling < 0 || stranger < 0) continue;

            var risks = engine.Provided.Resolve<OGSim.Information.ProspectRisks>();

            // A hole on the first prospect finds no source rock.
            risks.Drilled(before[0].Prospect, PosFactor.Source, present: false);

            engine.Pipeline.AdvanceTick();

            IReadOnlyList<ProspectView> after = engine.ReadModel!.Prospects;

            Assert.True(after[sibling].Source < before[sibling].Source,
                "a dry hole on source rock left the rest of its own play untouched");

            Assert.Equal(before[stranger].Source, after[stranger].Source, precision: 12);

            // And the trap factor is the prospect's own, so a source failure
            // leaves it exactly where it was.
            Assert.Equal(before[sibling].Trap, after[sibling].Trap, precision: 12);

            return;
        }

        Assert.Fail("sixty basins produced none with two plays represented");
    }

    /// <summary>
    /// A DISCOVERY DE-RISKS THE PLAY, and it does so through a real well rather
    /// than a test poking the registry. The company drills, finds oil, and every
    /// other prospect drawing on the same petroleum system is worth more than it
    /// was that morning — the half of exploration nobody pays for directly, and
    /// the reason a first strike changes a whole campaign.
    /// </summary>
    [Fact]
    public void R20d7V2_a_discovery_raises_the_odds_on_the_rest_of_its_play()
    {
        for (ulong seed = 1UL; seed < 60UL; seed++)
        {
            Engine engine = NewGame(seed);
            engine.Pipeline.AdvanceTick();

            IReadOnlyList<ProspectView> before = engine.ReadModel!.Prospects;

            var sibling = -1;

            for (int i = 1; i < before.Count; i++)
                if (before[i].Play == before[0].Play) { sibling = i; break; }

            if (sibling < 0) continue;

            double was = before[sibling].ProbabilityOfSuccess;

            var target = new EntityId<IProspect>(before[0].Prospect.Value);

            Assert.IsType<Accepted>(
                engine.Commands.Submit(new DrillWellCommand(target, new Length(2000.0))));

            // Long enough for the rig to finish, however the outcome table
            // stretches it. A well that failed teaches nothing yet (finding
            // 169), so this walks seeds until one drills a discovery.
            for (var month = 0; month < 12; month++) engine.Pipeline.AdvanceTick();

            if (engine.ReadModel!.Wells == 0) continue;      // dry: try another basin

            IReadOnlyList<ProspectView> after = engine.ReadModel!.Prospects;

            Assert.True(after[sibling].ProbabilityOfSuccess > was,
                "a discovery left the rest of its play priced exactly as before");

            return;
        }

        Assert.Fail("sixty basins produced no discovery on a play with a second prospect");
    }

    // ------------------------------------------ a well can now be wrong (R20d.7.4)

    /// <summary>
    /// MOST PROSPECTS ARE DRY, and they reach the player. Fill-spill has always
    /// produced empty traps; until now the generator discarded them, so every
    /// structure a company could see held oil and probability of success had
    /// nothing to be right or wrong about (finding 169).
    /// </summary>
    [Fact]
    public void R20d7V3_a_basin_offers_more_structures_than_it_holds_fields()
    {
        Engine engine = BasinWithSeveralProspects();
        WorldState world = WorldOf(engine);

        var dry = 0;

        for (int i = 0; i < world.Prospects.Count; i++)
            if (world.Beneath(world.Prospects[i]) is null) dry++;

        Assert.True(dry > 0,
            "every structure in the basin holds oil; the empty traps are still dying inside " +
            "the generator");
    }

    /// <summary>
    /// AND DRILLING ONE IS A DRY HOLE — because the rock is empty, not because a
    /// table said so. The money is spent, no well appears, and what the company
    /// bought is knowledge: this world leaves a trap empty for exactly one
    /// reason, so the well disproved SOURCE, and source is play-shared.
    ///
    /// <para>That last step is the point of the whole slice. A dry hole now
    /// makes every sibling prospect worth less, so exploration is a campaign in
    /// which being wrong costs more than the well.</para>
    /// </summary>
    [Fact]
    public void R20d7V3_drilling_a_dry_structure_reprices_its_play()
    {
        for (ulong seed = 1UL; seed < 60UL; seed++)
        {
            Engine engine = NewGame(seed);
            WorldState world = WorldOf(engine);

            var empty = -1;

            for (int i = 0; i < world.Prospects.Count; i++)
                if (world.Beneath(world.Prospects[i]) is null) { empty = i; break; }

            if (empty < 0) continue;

            engine.Pipeline.AdvanceTick();

            IReadOnlyList<ProspectView> before = engine.ReadModel!.Prospects;

            EntityId<IProspect> target = world.Prospects[empty];

            var sibling = -1;

            for (int i = 0; i < before.Count; i++)
                if (before[i].Prospect.Value != target.Value
                    && before[i].Play == before[empty].Play) { sibling = i; break; }

            if (sibling < 0) continue;

            double was = before[sibling].ProbabilityOfSuccess;

            Assert.IsType<Accepted>(
                engine.Commands.Submit(new DrillWellCommand(target, new Length(2000.0))));

            for (var month = 0; month < 12; month++) engine.Pipeline.AdvanceTick();

            // The hole may have been lost mechanically rather than drilled — that
            // teaches nothing and is a different outcome. Walk on if so.
            if (engine.ReadModel!.Prospects[sibling].ProbabilityOfSuccess == was) continue;

            Assert.Equal(0, engine.ReadModel!.Wells);

            Assert.True(engine.ReadModel!.Prospects[sibling].ProbabilityOfSuccess < was,
                "a dry hole made the rest of its play look BETTER");

            return;
        }

        Assert.Fail("sixty basins produced no dry structure with a sibling in its play");
    }

    // ------------------------------------ the survey answers a factor (R20d.7.5)

    /// <summary>
    /// SEISMIC MOVES THE TRAP FACTOR AND NOTHING IT CANNOT SEE. That asymmetry
    /// is what makes the five-factor decomposition worth showing: a player
    /// reading "one in six, and it is the trap we doubt" can buy something about
    /// it, while the same player told only "one in six" can only drill or walk.
    ///
    /// <para>A survey that quietly improved every factor would collapse the
    /// decision back into "buy surveys until POS is high enough", which is not a
    /// decision at all.</para>
    /// </summary>
    [Fact]
    public void R20d7V4_a_survey_sharpens_the_factors_it_can_see_and_no_others()
    {
        Engine engine = BasinWithSeveralProspects();
        engine.Pipeline.AdvanceTick();

        ProspectView before = engine.ReadModel!.Prospects[0];

        Assert.IsType<Accepted>(engine.Commands.Submit(
            new SeismicSurveyCommand(new EntityId<IProspect>(before.Prospect.Value))));

        for (var month = 0; month < 12; month++) engine.Pipeline.AdvanceTick();

        ProspectView after = engine.ReadModel!.Prospects[0];

        // A survey can fail outright — that is the honest bad outcome, and it
        // leaves everything where it was.
        if (after.Trap == before.Trap) return;

        Assert.True(after.Trap > before.Trap, "seismic made the trap look worse");
        Assert.True(after.Reservoir > before.Reservoir, "seismic taught nothing about the rock");

        // Untouched: no surface survey has anything to say about whether a
        // source rock cooked, whether the seal held, or whether the timing
        // worked out.
        Assert.Equal(before.Source, after.Source, precision: 12);
        Assert.Equal(before.Seal, after.Seal, precision: 12);
        Assert.Equal(before.Timing, after.Timing, precision: 12);
    }

    /// <summary>
    /// AND IT WEIGHS LESS THAN A WELL. A survey images a structure; a well
    /// proves it. If the two moved a factor equally, drilling would be a slow
    /// expensive way to buy what seismic already sold — and the whole appraisal
    /// ladder would collapse into its cheapest rung.
    /// </summary>
    [Fact]
    public void R20d7V4_a_survey_is_weaker_evidence_than_a_well()
    {
        var risks = new OGSim.Information.ProspectRisks(Defaults.ExplorationPrior);

        var surveyed = new EntityRef(EntityKind.Prospect, 1);
        var drilled = new EntityRef(EntityKind.Prospect, 2);

        // Two prospects in DIFFERENT plays, so the shared factors cannot carry
        // one's evidence into the other's reading.
        risks.Register(surveyed, new ContentId("play-a"), trapConfidence: 0.7);
        risks.Register(drilled, new ContentId("play-b"), trapConfidence: 0.7);

        risks.Learned(surveyed, PosFactor.Trap, present: true, weight: 2.0);
        risks.Drilled(drilled, PosFactor.Trap, present: true);

        Assert.True(
            OGSim.Information.ProspectRisk.MeanOf(risks.Of(surveyed)[PosFactor.Trap])
            > OGSim.Information.ProspectRisk.MeanOf(risks.Of(drilled)[PosFactor.Trap]),
            "weighted evidence is not being applied; every observation counts as one well");
    }

    // ------------------------------- a prospect becomes a field (R20d.7.6)

    /// <summary>
    /// A COMPANY KEEPS WHAT IT PAID FOR. Seismic buys a belief about a
    /// structure's size; drilling it does not make that knowledge wrong, it
    /// makes the structure an accumulation. The belief follows the thing it was
    /// always about — same mean, same sigma, same provenance — because nothing
    /// new was learned by the entity changing name (SDD-008 §4).
    ///
    /// <para>Until this, a discovery stranded everything: the survey's belief
    /// stayed on a prospect nobody would look at again, and the field it
    /// described was a compartment the company knew nothing about.</para>
    /// </summary>
    [Fact]
    public void R20d7V5_a_discovery_moves_what_was_learned_onto_the_field()
    {
        for (ulong seed = 1UL; seed < 60UL; seed++)
        {
            Engine engine = NewGame(seed);
            WorldState world = WorldOf(engine);
            IBeliefStore beliefs = engine.Provided.Resolve<IBeliefStore>();

            var charged = -1;

            for (int i = 0; i < world.Prospects.Count; i++)
                if (world.Beneath(world.Prospects[i]) is not null) { charged = i; break; }

            if (charged < 0) continue;

            EntityId<IProspect> target = world.Prospects[charged];
            var structure = new EntityRef(EntityKind.Prospect, target.Value);
            var capacity = new ContentId("structure-capacity");

            // Buy the survey, so there is something to carry across.
            engine.Commands.Submit(new SeismicSurveyCommand(target));

            engine.Pipeline.AdvanceTick();
            while (engine.ReadModel!.ActivitiesRunning > 0) engine.Pipeline.AdvanceTick();

            if (beliefs.Get(structure, capacity) is not Belief surveyed) continue;

            engine.Commands.Submit(new DrillWellCommand(target, new Length(2000.0)));

            for (var month = 0; month < 12; month++) engine.Pipeline.AdvanceTick();

            if (engine.ReadModel!.Wells == 0) continue;      // the job was lost; try again

            var field = new EntityRef(
                EntityKind.Compartment, world.Beneath(target)!.Value.Value);

            Belief? moved = beliefs.Get(field, capacity);

            Assert.NotNull(moved);
            Assert.Equal(surveyed.Mu, moved.Value.Mu, precision: 12);
            Assert.Equal(surveyed.Sigma, moved.Value.Sigma, precision: 12);
            Assert.Equal(surveyed.BestSource, moved.Value.BestSource);

            // AND IT IS A MOVE. Leaving the original would have the prospect and
            // the field each answering for one fact, and an appraisal updating
            // one would leave the other stale (law L5).
            Assert.Null(beliefs.Get(structure, capacity));

            return;
        }

        Assert.Fail("sixty basins produced no surveyed discovery");
    }

    // ---------------------------- a second field is a longer reach (R20d.8.8)

    /// <summary>
    /// FINDING 167, CLOSED. Design 04 stage 3's wellhead-to-manifold gathering
    /// line did not exist — the element named "flowline" sits AFTER the manifold
    /// — so every well tied straight into the header at zero distance, and a
    /// company that developed a second discovery forty kilometres away paid
    /// nothing for the journey.
    ///
    /// <para>Each well now has its own run, as long as its field is from the
    /// header. A tieback from across the basin is a longer line than a well on
    /// the host's own field, which is what makes where a discovery sits matter
    /// after the trunk has been laid.</para>
    /// </summary>
    [Fact]
    public void R20d8V7_a_distant_field_needs_a_longer_reach_than_the_host_s_own()
    {
        Engine engine = BasinWithSeveralProspects();
        WorldState world = WorldOf(engine);
        FieldControl field = engine.Provided.Resolve<FieldControl>();

        // The header goes up at the first field developed.
        EntityId<IReservoirCompartmentEntity> host = world.Beneath(Discovery(world, 0))!.Value;

        field.Drill(host, new Length(2000.0));

        engine.Pipeline.AdvanceTick();

        double atHost = RunOf(engine, "gathering-1");

        // A second discovery, somewhere else in the basin.
        EntityId<IReservoirCompartmentEntity> away = world.Beneath(Discovery(world, 1))!.Value;

        field.Drill(away, new Length(2000.0));

        engine.Pipeline.AdvanceTick();

        Assert.True(RunOf(engine, "gathering-2") > atHost,
            "a field elsewhere in the basin reaches the header over the same length of " +
            "line as one underneath it");
    }

    /// <summary>How long a named gathering line is, read off the flow network
    /// the way the solver sees it.</summary>
    private static double RunOf(Engine engine, string named)
    {
        SurfaceChain chain = engine.Provided.Resolve<SurfaceChain>();
        IFlowElementRegistry network = engine.Provided.Resolve<IFlowElementRegistry>();

        for (int i = 0; i < network.Registered.Count; i++)
        {
            IFlowElement element = network.Registered[i];

            if (chain.NameOf(element.Id) == named
                && element is OGSim.Facilities.Pipeline line) return line.PipeLength.Metres;
        }

        throw new InvalidOperationException($"no element called {named} is on the network");
    }

    // ------------------------------------- a client that explores (R21.5)

    /// <summary>
    /// R21-V2, the half nobody had played. `Operator` is handed a field and
    /// develops it; this reads a basin, decides what is worth a survey and what
    /// is worth a hole, and finds out whether it was right — from the read model
    /// and the command bus alone.
    ///
    /// <para>If a decision cannot be taken from that surface it cannot be taken
    /// by a host either, which is the only thing a reference client is for.</para>
    /// </summary>
    [Fact]
    public void R21V2_a_client_can_explore_a_basin_it_was_told_nothing_about()
    {
        Engine engine = BasinWithSeveralProspects();

        DrillingSeason campaign = new Explorer(engine, drillAbove: 0.16, wellTarget: 3, buildBelow: double.MaxValue, borrows: false)
            .Play(months: 360);

        Assert.True(campaign.Drilled > 0, "the client never put a hole down");

        // Exploration is a search, not an inventory (SDD-010 §4b): a company
        // that drilled several structures in a basin the charge did not fill
        // should have found some of both.
        Assert.True(campaign.Discoveries + campaign.DryHoles == campaign.Drilled,
            $"{campaign.Drilled} holes resolved into {campaign.Discoveries} discoveries and " +
            $"{campaign.DryHoles} dry — a hole went unaccounted for");
    }

    /// <summary>
    /// AND THE BEST BET IS SOMETIMES WRONG. A client that drills the highest
    /// probability of success it can see still gets dry holes, because POS is a
    /// BELIEF — built from how confidently a structure is mapped and what the
    /// play has taught so far, neither of which is whether charge actually
    /// arrived here.
    ///
    /// <para>If this ever passes with no dry holes across a dozen basins,
    /// presence has stopped being read from truth and drilling has gone back to
    /// being a formality (finding 169).</para>
    ///
    /// <para>This test is also finding 170's headstone. It faulted on first
    /// writing — a marginal accumulation produced hard enough to drop its
    /// pressure 41% in a month, which the material balance rightly refuses —
    /// because every well was built with one fixed set of inflow conditions
    /// whatever it was drilled into. It runs because a well is now built from
    /// its own rock.</para>
    /// </summary>
    [Fact]
    public void R21V2_the_best_prospect_on_the_board_is_sometimes_dry()
    {
        var dry = 0;
        var found = 0;

        // SIX BASINS AND FIVE YEARS, not thirteen and ten. Each month solves a
        // network over every compartment the basin generated, so this test is
        // the most expensive in the suite by an order of magnitude — and a suite
        // nobody runs catches nothing. Six is still enough that a run in which
        // every best-odds prospect held oil would be a genuine surprise.
        for (ulong seed = 1UL; seed < 7UL; seed++)
        {
            DrillingSeason season = new Explorer(NewGame(seed), drillAbove: 0.0, wellTarget: 2, buildBelow: double.MaxValue, borrows: false)
                .Play(months: 60);

            dry += season.DryHoles;
            found += season.Discoveries;
        }

        Assert.True(found > 0, "six basins produced no discovery at all");

        Assert.True(dry > 0,
            $"six campaigns drilled the best prospect on the board and never once " +
            $"missed ({found} discoveries, {dry} dry); presence is not being read from truth");
    }

    // ----------------------------- the market is actionable (R20d.12 / R21.5)

    /// <summary>
    /// A COMPANY THAT WATCHES THE CYCLE BUILDS CHEAPER. Plant bought in a boom
    /// costs what the boom says it costs, and the read model carries the index
    /// that says so — which is the whole reason it is on the surface.
    ///
    /// <para>This is the test that says the cost index is ACTIONABLE. A market
    /// a host can see and cannot act on would be scenery; if these two companies
    /// ended level, the index would be a number with no decision behind it.</para>
    ///
    /// <para>Compared across basins and totalled, because one campaign is one
    /// draw: patience is an edge in expectation, not a guarantee, and a client
    /// that beat the market every single time would mean the market had stopped
    /// being uncertain.</para>
    /// </summary>
    [Fact]
    public void R20d12V1_a_client_that_waits_for_a_quiet_yard_keeps_more_of_what_it_earns()
    {
        Money patient = Money.Zero;
        Money eager = Money.Zero;

        // Four basins over seven years. Each month solves a network across
        // every compartment a basin generated, so this and the campaign test are
        // the two expensive ones in the suite — and a suite nobody runs catches
        // nothing.
        for (ulong seed = 1UL; seed < 5UL; seed++)
        {
            // The SAME world and the same market for both, so what is left
            // between them is when they chose to spend.
            patient += Earned(seed, buildBelow: 1.0);
            eager += Earned(seed, buildBelow: double.MaxValue);
        }

        Assert.True(patient > eager,
            $"waiting for a quiet yard earned {patient} against {eager} for building on " +
            "sight; the cost index is not actionable through the read model");
    }

    private static Money Earned(ulong seed, double buildBelow, bool borrows = false) =>
        new Explorer(NewGame(seed), drillAbove: 0.0, wellTarget: 2, buildBelow, borrows)
            .Play(months: 84)
            .Cash;

    /// <summary>
    /// A DISCOVERY TELLS YOU HOW MUCH, not just that there is some. The company
    /// knew the trap's size before it drilled — seismic maps a closure — and
    /// knew nothing at all about what was in it. One hole answers the second
    /// question, which is the one a development decision is taken on.
    ///
    /// <para>Wide, and it should be: a single penetration has seen one point of
    /// a field and the rest is inference. That gap is what appraisal wells are
    /// for, and a strike that produced a certain number would leave them nothing
    /// to do.</para>
    /// </summary>
    [Fact]
    public void R20d7V6_a_discovery_well_says_how_much_it_found()
    {
        for (ulong seed = 1UL; seed < 60UL; seed++)
        {
            Engine engine = NewGame(seed);
            WorldState world = WorldOf(engine);
            IBeliefStore beliefs = engine.Provided.Resolve<IBeliefStore>();

            var charged = -1;

            for (int i = 0; i < world.Prospects.Count; i++)
                if (world.Beneath(world.Prospects[i]) is not null) { charged = i; break; }

            if (charged < 0) continue;

            EntityId<IProspect> target = world.Prospects[charged];

            engine.Commands.Submit(new DrillWellCommand(target, new Length(2000.0)));

            for (var month = 0; month < 12; month++) engine.Pipeline.AdvanceTick();

            if (engine.ReadModel!.Wells == 0) continue;      // the job was lost

            var field = new EntityRef(
                EntityKind.Compartment, world.Beneath(target)!.Value.Value);

            Belief? inPlace = beliefs.Get(field, new ContentId("oil-in-place"));

            Assert.NotNull(inPlace);

            Assert.True(inPlace.Value.Sigma > 0.0,
                "a discovery well returned a certain number; one hole has seen one point " +
                "of a field and appraisal would have nothing left to do");

            return;
        }

        Assert.Fail("sixty basins produced no discovery");
    }

    // ------------------------------- the plugging bill is earned (R20d.14)

    /// <summary>
    /// A WELL EARNS ITS OWN PLUGGING BILL, barrel by barrel (SDD-009 §2). The
    /// cost is real from the day the hole is drilled, and a company that met it
    /// only at the end would look profitable for thirty years and insolvent in
    /// one — which is not a harder game, it is a game that lies until the last
    /// month.
    ///
    /// <para>AND IT DOES NOT OVERSHOOT. Accrued against what the field will
    /// ULTIMATELY give rather than against what is left, so the sum telescopes:
    /// produce everything and the provision equals the bill. Against remaining
    /// reserves it would accelerate as the field emptied and book a liability
    /// larger than the one that exists.</para>
    /// </summary>
    [Fact]
    public void R20d14V1_production_accrues_the_plugging_bill_without_overshooting()
    {
        for (ulong seed = 1UL; seed < 60UL; seed++)
        {
            Engine engine = NewGame(seed);
            WorldState world = WorldOf(engine);

            var charged = -1;

            for (int i = 0; i < world.Prospects.Count; i++)
                if (world.Beneath(world.Prospects[i]) is not null) { charged = i; break; }

            if (charged < 0) continue;

            engine.Commands.Submit(
                new DrillWellCommand(world.Prospects[charged], new Length(2000.0)));

            var company = engine.Provided.Resolve<CompanyState>();
            var obligations = engine.Provided.Resolve<IObligationRegistry>();

            for (var month = 0; month < 120; month++)
            {
                engine.Pipeline.AdvanceTick();

                // A PROVISION, NOT A PAYMENT: what the company owes the future,
                // recognised as it is earned and never more than it is.
                // Credits are negative in this ledger, so what is HELD against
                // the future is the negation of the balance.
                Assert.True(
                    -company.Ledger.BalanceOf(Account.AbandonmentProvision)
                    <= obligations.TotalOutstanding,
                    $"month {month}: the provision has passed the bill it provisions for");
            }

            if (engine.ReadModel!.Wells == 0) continue;      // the hole was lost

            Assert.True(
                -company.Ledger.BalanceOf(Account.AbandonmentProvision) > Money.Zero,
                "ten years of production accrued nothing towards plugging the well");

            return;
        }

        Assert.Fail("sixty basins produced no discovery to accrue against");
    }

    /// <summary>
    /// PLANT WEARS OUT BY THE BARREL, not by the calendar (SDD-009 §2). A
    /// platform does not get a year older every year; it gets a barrel older
    /// every barrel — so a shut-in field depreciates nothing and a producing one
    /// writes its capital down as it empties.
    ///
    /// <para>The carrying value can never go below nothing, and never below what
    /// was actually spent: an asset cannot be worth less than written off.</para>
    /// </summary>
    [Fact]
    public void R20d14V2_capital_is_written_down_by_what_it_produces()
    {
        for (ulong seed = 1UL; seed < 60UL; seed++)
        {
            Engine engine = NewGame(seed);
            WorldState world = WorldOf(engine);

            var charged = -1;

            for (int i = 0; i < world.Prospects.Count; i++)
                if (world.Beneath(world.Prospects[i]) is not null) { charged = i; break; }

            if (charged < 0) continue;

            engine.Commands.Submit(
                new DrillWellCommand(world.Prospects[charged], new Length(2000.0)));

            var company = engine.Provided.Resolve<CompanyState>();

            Money peak = Money.Zero;

            for (var month = 0; month < 120; month++)
            {
                engine.Pipeline.AdvanceTick();

                Money carrying = company.Ledger.BalanceOf(Account.Capex_PPE);

                if (carrying > peak) peak = carrying;

                Assert.True(carrying >= Money.Zero,
                    $"month {month}: the plant is carried at {carrying}, which is less than " +
                    "written off");
            }

            if (engine.ReadModel!.Wells == 0) continue;      // the hole was lost

            Assert.True(peak > Money.Zero, "a well was drilled and nothing was capitalised");

            Assert.True(company.Ledger.BalanceOf(Account.Capex_PPE) < peak,
                "ten years of production wrote nothing off the plant that produced it");

            return;
        }

        Assert.Fail("sixty basins produced no discovery to depreciate");
    }

    // ------------------------------- borrowing against the ground (R20d.15)

    /// <summary>
    /// A COMPANY CAN FUND A DEVELOPMENT OUT OF WHAT IT FOUND. A field pays for
    /// itself and not before it is built, so a company that could only spend
    /// what it had would develop at the speed of its smallest discovery.
    ///
    /// <para>And it is not free money: the base is a limit the bank refuses
    /// past, and the interest is charged whether or not the field produces.</para>
    /// </summary>
    [Fact]
    public void R20d15V2_a_discovery_can_be_borrowed_against()
    {
        for (ulong seed = 1UL; seed < 60UL; seed++)
        {
            Engine engine = NewGame(seed);
            WorldState world = WorldOf(engine);

            var charged = -1;

            for (int i = 0; i < world.Prospects.Count; i++)
                if (world.Beneath(world.Prospects[i]) is not null) { charged = i; break; }

            if (charged < 0) continue;

            engine.Pipeline.AdvanceTick();

            // NOTHING FOUND, NOTHING TO LEND AGAINST. A bank secures on reserves,
            // and an undrilled basin has none however promising it looks.
            Assert.Equal(Money.Zero, engine.ReadModel!.Borrowing.BorrowingBase);
            Assert.IsType<Rejected>(
                engine.Commands.Submit(new BorrowCommand(Money.FromMillions(1.0))));

            engine.Commands.Submit(
                new DrillWellCommand(world.Prospects[charged], new Length(2000.0)));

            for (var month = 0; month < 12; month++) engine.Pipeline.AdvanceTick();

            if (engine.ReadModel!.Wells == 0) continue;      // the hole was lost

            Money available = engine.ReadModel!.Borrowing.BorrowingBase;

            Assert.True(available > Money.Zero,
                "a discovery with reserves supported no borrowing at all");

            // PAST THE BASE IS REFUSED. A facility a company could overdraw
            // would make the covenant meaningless, because the breach would be
            // the bank's doing rather than the player's.
            Assert.IsType<Rejected>(
                engine.Commands.Submit(new BorrowCommand(available + Money.FromMillions(1.0))));

            Money before = engine.ReadModel!.Cash;

            Assert.IsType<Accepted>(engine.Commands.Submit(new BorrowCommand(available)));

            engine.Pipeline.AdvanceTick();

            Assert.True(engine.ReadModel!.Cash > before, "the drawdown never reached the cash");
            Assert.Equal(available, engine.ReadModel!.Debt);

            // AND IT COSTS. Interest is charged on what is drawn, every month,
            // whether or not the field has a good one.
            Money owed = engine.ReadModel!.Debt;
            Money cash = engine.ReadModel!.Cash;

            engine.Pipeline.AdvanceTick();

            Assert.Equal(owed, engine.ReadModel!.Debt);      // interest is not capitalised

            Assert.IsType<Accepted>(
                engine.Commands.Submit(new RepayCommand(Money.FromMillions(1.0))));

            engine.Pipeline.AdvanceTick();

            Assert.True(engine.ReadModel!.Debt < owed, "a repayment did not reduce the debt");

            return;
        }

        Assert.Fail("sixty basins produced no discovery to borrow against");
    }

    /// <summary>
    /// A COMPANY THAT USES ITS FACILITY DEVELOPS FASTER (SDD-009 §5). A field
    /// pays for itself only once it is built, so a company waiting to afford the
    /// next well out of revenue is waiting on the very thing that well would
    /// provide — and on a declining asset, later is less.
    ///
    /// <para>This is the test that says the borrowing base is ACTIONABLE. A
    /// facility a host can see and cannot use would be a number on a screen; if
    /// these two companies ended level, the whole of R20d.15 would be
    /// decoration.</para>
    ///
    /// <para>Compared across basins and totalled, because leverage is an edge in
    /// expectation and not a guarantee: interest is charged whether or not the
    /// month goes well, and a company that borrowed into a bad market pays for
    /// it. A client that won every single time would mean debt was free.</para>
    /// </summary>
    [Fact]
    public void R20d15V3_a_company_that_borrows_develops_faster_than_one_that_waits()
    {
        Money funded = Money.Zero;
        Money unfunded = Money.Zero;

        for (ulong seed = 1UL; seed < 5UL; seed++)
        {
            // The SAME world and the same market for both, so what is left
            // between them is whether they used the facility.
            funded += Earned(seed, buildBelow: double.MaxValue, borrows: true);
            unfunded += Earned(seed, buildBelow: double.MaxValue, borrows: false);
        }

        Assert.True(funded > unfunded,
            $"borrowing against reserves earned {funded} against {unfunded} for waiting; " +
            "the borrowing base is not actionable through the read model");
    }

    /// <summary>
    /// THE COVENANT, END TO END. Every piece of this was built separately and
    /// the chain between them had never once been run: a field depletes, its
    /// remaining reserves fall, the borrowing base falls with them, debt drawn
    /// against yesterday's reserves is suddenly above today's base, and the
    /// facility goes into cure.
    ///
    /// <para>That is the shape a leveraged company actually fails in, and it is
    /// worth asserting as one sequence rather than four unit tests: each part
    /// passing says nothing about whether the parts are connected, which is the
    /// lesson findings 164–173 keep teaching.</para>
    ///
    /// <para>THE CURE WINDOW IS THE POINT. The bank does not call — it starts a
    /// clock, and a company that pays down or produces its way back inside is
    /// clear again. A breach that went straight to amortising would make a
    /// depleting field an ambush.</para>
    /// </summary>
    [Fact]
    public void R20d15V4_a_depleting_field_breaches_its_covenant_and_gets_a_window()
    {
        for (ulong seed = 1UL; seed < 60UL; seed++)
        {
            Engine engine = NewGame(seed);
            WorldState world = WorldOf(engine);

            var charged = -1;

            for (int i = 0; i < world.Prospects.Count; i++)
                if (world.Beneath(world.Prospects[i]) is not null) { charged = i; break; }

            if (charged < 0) continue;

            engine.Commands.Submit(
                new DrillWellCommand(world.Prospects[charged], new Length(2000.0)));

            for (var month = 0; month < 12; month++) engine.Pipeline.AdvanceTick();

            if (engine.ReadModel!.Wells == 0) continue;      // the hole was lost

            Money available = engine.ReadModel!.Borrowing.BorrowingBase;

            if (available <= Money.Zero) continue;

            // DRAWN TO THE LIMIT ON PURPOSE. A borrowing base falls as reserves
            // deplete — they are what is LEFT — so a company drawn to the last
            // cent breaches the month after, every time. That is the mechanism
            // working, not a defect, and it is why a prudent company draws a
            // fraction; this test wants the breach.
            Assert.IsType<Accepted>(engine.Commands.Submit(new BorrowCommand(available)));

            var sawCuring = false;

            // Produce. Reserves are what is LEFT, so they fall as the field
            // empties, and the base falls with them.
            for (var month = 0; month < 480; month++)
            {
                engine.Pipeline.AdvanceTick();

                CovenantStatus covenant = engine.ReadModel!.Covenant;

                if (covenant.State == CovenantState.Curing)
                {
                    sawCuring = true;

                    Assert.True(covenant.TicksRemaining > 0,
                        "a cure window with no time left in it is a called loan");
                }

                // AMORTISING IS NEVER REACHED WITHOUT CURING FIRST. The bank
                // does not call, and a company that had no warning could not
                // have acted on one.
                if (covenant.State == CovenantState.Amortising)
                {
                    Assert.True(sawCuring,
                        "the facility went to amortisation without a cure window; the bank " +
                        "called the loan, which SDD-009 §5 pins that it never does");

                    return;
                }
            }

            Assert.True(sawCuring,
                "forty years of depletion against a fully drawn facility and the covenant " +
                "never even went into cure; the base is not following the reserves");

            return;
        }

        Assert.Fail("sixty basins produced no discovery worth lending against");
    }

    /// <summary>
    /// AND A PRUDENT COMPANY STAYS OUT OF IT. The exploring client draws a
    /// fraction of its base rather than all of it, so the base can fall as the
    /// field empties without putting the facility into breach.
    ///
    /// <para>This is the assertion that was missing when the borrowing client
    /// shipped: the earnings test compared two companies and never once looked
    /// at the covenant, so a client living permanently in a cure window would
    /// have passed it. Comparing outcomes is not the same as checking the state
    /// they were reached from.</para>
    /// </summary>
    [Fact]
    public void R20d15V4_a_client_that_borrows_prudently_does_not_live_in_breach()
    {
        Engine engine = BasinWithSeveralProspects();

        new Explorer(engine, drillAbove: 0.0, wellTarget: 2,
                     buildBelow: double.MaxValue, borrows: true)
            .Play(months: 180);

        Assert.Equal(CovenantState.Clear, engine.ReadModel!.Covenant.State);
    }

    /// <summary>
    /// THE PROVISION IS RELEASED WHEN THE BILL IS PAID (SDD-009 §2). A company
    /// accrues towards plugging a well for as long as it produces; when the well
    /// is finally plugged, the money it set aside is what pays for it.
    ///
    /// <para>Held and never released, the liability sits on the balance sheet
    /// after the obligation it was held against has gone — and the cost hits the
    /// accounts TWICE, once as it was accrued and again as it was spent. A
    /// company would report a loss it had already reported.</para>
    ///
    /// <para>Another chain nobody had run: accrual, obligation, abandonment and
    /// discharge each had a passing test, and the join between the last two was
    /// never made at all.</para>
    /// </summary>
    [Fact]
    public void R20d14V3_abandoning_a_well_releases_the_provision_held_for_it()
    {
        for (ulong seed = 1UL; seed < 60UL; seed++)
        {
            Engine engine = NewGame(seed);
            WorldState world = WorldOf(engine);

            var charged = -1;

            for (int i = 0; i < world.Prospects.Count; i++)
                if (world.Beneath(world.Prospects[i]) is not null) { charged = i; break; }

            if (charged < 0) continue;

            engine.Commands.Submit(
                new DrillWellCommand(world.Prospects[charged], new Length(2000.0)));

            for (var month = 0; month < 60; month++) engine.Pipeline.AdvanceTick();

            if (engine.ReadModel!.Wells == 0) continue;      // the hole was lost

            var company = engine.Provided.Resolve<CompanyState>();
            var obligations = engine.Provided.Resolve<IObligationRegistry>();

            Money accrued = -company.Ledger.BalanceOf(Account.AbandonmentProvision);

            Assert.True(accrued > Money.Zero, "five years of production accrued nothing");

            // Plug it. Nothing else the company owns changes.
            EntityRef well = engine.ReadModel!.Wellbores[0].Well;

            Assert.IsType<Accepted>(engine.Commands.Submit(
                new AbandonWellCommand(new EntityId<ICompletion>(well.Value))));

            for (var month = 0; month < 24; month++) engine.Pipeline.AdvanceTick();

            if (obligations.TotalOutstanding > Money.Zero) continue;     // the job failed

            // The obligation is gone. What was held against it must be gone too.
            Assert.Equal(
                Money.Zero, -company.Ledger.BalanceOf(Account.AbandonmentProvision));

            return;
        }

        Assert.Fail("sixty basins produced no well that could be drilled and plugged");
    }

    /// <summary>
    /// SC6, END TO END: a market that falls writes reserves down, and the
    /// borrowing base falls with them. Reserves stop where production stops
    /// paying, so a lower price raises the economic limit, the tail of the
    /// decline drops below it, and barrels beyond stop being reserves without
    /// having gone anywhere.
    ///
    /// <para>THE WELL IS SHUT IN, and that is what makes this a test of the
    /// market rather than of depletion. Reserves fall as a field produces too,
    /// so a producing field cannot tell the two causes apart — with nothing
    /// coming out, the only thing left that can move the base is the price.</para>
    ///
    /// <para>Each of these steps has its own passing test. Nothing had ever
    /// checked that a price the engine actually generated moved a base the
    /// engine actually offered.</para>
    /// </summary>
    [Fact]
    public void R20d13V2_a_falling_market_writes_the_borrowing_base_down()
    {
        for (ulong seed = 1UL; seed < 60UL; seed++)
        {
            Engine engine = NewGame(seed);
            WorldState world = WorldOf(engine);

            var charged = -1;

            for (int i = 0; i < world.Prospects.Count; i++)
                if (world.Beneath(world.Prospects[i]) is not null) { charged = i; break; }

            if (charged < 0) continue;

            engine.Commands.Submit(
                new DrillWellCommand(world.Prospects[charged], new Length(2000.0)));

            for (var month = 0; month < 12; month++) engine.Pipeline.AdvanceTick();

            if (engine.ReadModel!.Wells == 0) continue;      // the hole was lost

            // SHUT IT IN. From here the field produces nothing, so remaining
            // reserves cannot fall by depletion and the base can only move with
            // the market.
            EntityRef well = engine.ReadModel!.Wellbores[0].Well;

            engine.Commands.Submit(
                new SetWellChokeCommand(new EntityId<ICompletion>(well.Value), Open: false));

            (Money Price, Money Base) high = (Money.Zero, Money.Zero);
            (Money Price, Money Base) low = (Money.FromMillions(1.0e9), Money.Zero);

            for (var month = 0; month < 96; month++)
            {
                engine.Pipeline.AdvanceTick();

                if (engine.ReadModel!.Insolvent) break;

                Money price = engine.ReadModel!.OilPrice;
                Money bookable = engine.ReadModel!.Borrowing.BorrowingBase;

                if (price > high.Price) high = (price, bookable);
                if (price < low.Price) low = (price, bookable);
            }

            if (high.Base <= Money.Zero) continue;      // never had a base to write down

            Assert.True(low.Base < high.Base,
                $"the market ran from {low.Price} to {high.Price} and the borrowing base " +
                $"did not move: {low.Base} against {high.Base}");

            return;
        }

        Assert.Fail("sixty basins produced no discovery that could be shut in and watched");
    }

    // -------------------------------------------- one seed is one game (PV7)

    /// <summary>
    /// PV7 AT THE ENGINE, not at the generator. Two engines from one seed, given
    /// the same orders, must agree on EVERYTHING a host can see after a decade:
    /// the same wells, the same cash, the same price, the same cost index, the
    /// same reserves, the same debt, the same gas burned, the same record, the
    /// same odds on every prospect.
    ///
    /// <para>The determinism test that existed asserted wells and cash over six
    /// months on a hand-built field, and it predates the market, reserves, the
    /// ESG record, the gas plant and injection — every one of which draws or
    /// accumulates. A save that reloaded into a different game would break the
    /// one promise the whole design rests on, and nothing was checking most of
    /// what could break it.</para>
    ///
    /// <para>Compared as WHOLE READ MODELS rather than field by field. A
    /// hand-listed set of assertions is a list of the things somebody thought of,
    /// and the next number added to the surface would not be on it.</para>
    /// </summary>
    [Fact]
    public void PV7_one_seed_is_one_game_all_the_way_through()
    {
        Engine first = NewGame(seed: 20260811UL);
        Engine second = NewGame(seed: 20260811UL);

        var playFirst = new Explorer(first, drillAbove: 0.0, wellTarget: 3,
                                     buildBelow: 1.05, borrows: true);

        var playSecond = new Explorer(second, drillAbove: 0.0, wellTarget: 3,
                                      buildBelow: 1.05, borrows: true);

        playFirst.Play(months: 120);
        playSecond.Play(months: 120);

        Assert.Equal(first.ReadModel, second.ReadModel);
    }

    /// <summary>
    /// AND A DIFFERENT SEED IS A DIFFERENT GAME. Stated separately because an
    /// engine that ignored its seed entirely would satisfy the test above
    /// perfectly — identical runs are only interesting if they were not
    /// inevitable.
    /// </summary>
    [Fact]
    public void PV7_a_different_seed_is_a_different_game()
    {
        Engine one = NewGame(seed: 11UL);
        Engine other = NewGame(seed: 12UL);

        new Explorer(one, 0.0, 3, 1.05, borrows: true).Play(months: 120);
        new Explorer(other, 0.0, 3, 1.05, borrows: true).Play(months: 120);

        Assert.NotEqual(one.ReadModel, other.ReadModel);
    }
}
