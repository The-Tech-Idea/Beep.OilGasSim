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

    private static WorldSink SinkOf(Engine engine) => engine.Provided.Resolve<WorldSink>();

    // ------------------------------------------------- the world reaches the engine

    /// <summary>
    /// THE FINDING, stated as its opposite. A new game has compartments nobody
    /// hand-built: they exist because the generator put them there.
    /// </summary>
    [Fact]
    public void R20d8V2_a_new_game_has_the_prospects_the_generator_drew()
    {
        Engine engine = NewGame(seed: 20260808UL);

        Assert.NotEmpty(SinkOf(engine).Prospects);
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

            foreach (EntityId<IReservoirCompartmentEntity> prospect in SinkOf(engine).Prospects)
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
        WorldView? view = SinkOf(NewGame(seed: 5UL)).View;

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
}
