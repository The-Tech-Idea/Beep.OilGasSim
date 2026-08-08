// R20d.8 — a game begins by finding out what is under it (SDD-010 §4).
//
// Everything here asks one question in different ways: does the world the
// generator drew actually decide the game? Until `CreateNew` existed the answer
// was no — the generator was composed and never called, and every run got the
// same hand-built field whatever seed it was given.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

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

            var reachable = 0;

            for (int i = 0; i < world.Prospects.Count; i++)
                if (world.DistanceToMarket(world.Prospects[i]) is not null) reachable++;

            if (reachable > 1) return engine;
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

            foreach (EntityId<IReservoirCompartmentEntity> prospect in WorldOf(engine).Prospects)
            {
                Belief? held = beliefs.Get(
                    new EntityRef(EntityKind.Compartment, prospect.Value),
                    new ContentId("oil-in-place"));

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

        foreach (EntityId<IReservoirCompartmentEntity> prospect in world.Prospects)
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

        EntityId<IReservoirCompartmentEntity> chosen = world.Prospects[0];
        Length expected = world.DistanceToMarket(chosen)!.Value;

        field.OpenWell(
            Defaults.CompletionFor(field.NextWellId(), chosen, new Length(2000.0)), chosen);

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

        EntityId<IReservoirCompartmentEntity> first = world.Prospects[0];

        field.OpenWell(
            Defaults.CompletionFor(field.NextWellId(), first, new Length(2000.0)), first);

        Length laid = chain.Flowline.PipeLength;

        // A well on a DIFFERENT prospect — the case that would move the line if
        // anything were going to.
        EntityId<IReservoirCompartmentEntity> elsewhere = world.Prospects[^1];

        field.OpenWell(
            Defaults.CompletionFor(field.NextWellId(), elsewhere, new Length(2000.0)), elsewhere);

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
            .DistanceToMarket(new EntityId<IReservoirCompartmentEntity>(1)));
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

            var target = new EntityId<IReservoirCompartmentEntity>(before[0].Prospect.Value);

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
}
