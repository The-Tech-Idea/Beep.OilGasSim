// Plans 25 — the two products are two styles over one engine, and these are the
// tests that say so. OGSim itself is untouched by either.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using Xunit;

namespace OGSim.Composition.Tests;

public sealed class GameStyleTests
{
    /// <summary>
    /// The two shipped modes differ on every axis, which is what makes them two
    /// products rather than one with a slider.
    /// </summary>
    [Fact]
    public void GS1_the_two_styles_differ_on_all_three_axes()
    {
        IGameStyle days = GameStyles.Days;
        IGameStyle engineer = GameStyles.Engineer;

        Assert.NotEqual(days.RealityProfile, engineer.RealityProfile);
        Assert.NotEqual(days.StartingState, engineer.StartingState);
        Assert.NotEqual(days.Rules, engineer.Rules);

        // And each is a combination somebody chose, not a shuffle: Days is the
        // builder's game and Engineer is the operator's.
        Assert.Equal(StartingStates.BareGround, days.StartingState);
        Assert.Equal(RuleSets.Frontier.Id, days.Rules);

        Assert.Equal(StartingStates.OpeningPosition, engineer.StartingState);
        Assert.Equal(RuleSets.Realistic.Id, engineer.Rules);
    }

    /// <summary>
    /// A mode writes all three axes and nothing else — the reason it exists is
    /// that a caller setting two of three was one edit away from a save
    /// reloading under rules it was never played under.
    /// </summary>
    [Fact]
    public void GS2_composing_at_a_style_writes_every_axis()
    {
        EngineSettings before = Fixture.Settings();

        EngineSettings after = GameStyles.Days.Compose(before);

        Assert.Equal(GameStyles.Days.RealityProfile, after.RealityProfile);
        Assert.Equal(GameStyles.Days.StartingState, after.StartingState);
        Assert.Equal(GameStyles.Days.Rules, after.Rules);

        // Everything the mode has no opinion about is untouched.
        Assert.Equal(before.Epoch, after.Epoch);
        Assert.Equal(before.WorldSeed, after.WorldSeed);
        Assert.Same(before.Content, after.Content);
    }

    /// <summary>
    /// An unknown mode is refused by name, the way an unknown reality profile
    /// and an unknown rule set already are. Guessing would compose a game
    /// nobody asked for and call it the one that was named.
    /// </summary>
    [Fact]
    public void GS3_an_unknown_style_is_refused_and_names_what_is_on_offer()
    {
        InvariantFault fault = Assert.Throws<InvariantFault>(
            () => GameStyles.Named(new ContentId("sandbox")));

        Assert.Contains("sandbox", fault.Message, StringComparison.Ordinal);
        Assert.Contains("days", fault.Message, StringComparison.Ordinal);
        Assert.Contains("engineer", fault.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// BOTH MODES COMPOSE FROM THE SAME ENGINE. Not two builds, not two module
    /// sets — one <see cref="EngineBuilder"/>, twice.
    /// </summary>
    [Fact]
    public void GS4_both_products_are_the_same_engine()
    {
        for (int i = 0; i < GameStyles.All.Count; i++)
        {
            IGameStyle mode = GameStyles.All[i];

            Built built = Assert.IsType<Built>(
                EngineBuilder.Build(mode.Compose(Fixture.Settings())));

            // The same published surface either way: a host written against one
            // mode works against the other, which is the whole claim.
            Assert.NotNull(built.Engine.Commands);
            Assert.NotNull(built.Engine.Pipeline);
        }
    }

    /// <summary>
    /// AND THEY DIFFER WHERE THEY SHOULD: the same command, on the same ground,
    /// accepted by one product and refused by the other.
    /// </summary>
    /// <remarks>
    /// <para>This is the test that would have caught GC-4 — the fix that deleted
    /// an operator's rule to make the game playable. The rule is not gone; it is
    /// in the other mode, and a run that wants it asks for it by name.</para>
    ///
    /// <para>Bare ground under each rule set, because that is where the two
    /// disagree: with no manifold there is no tie-in slot, and whether that
    /// refuses a well is exactly what a rule set decides.</para>
    /// </remarks>
    [Fact]
    public void GS5_the_same_hole_is_allowed_in_one_product_and_refused_in_the_other()
    {
        Assert.Empty(new FrontierDrillingRule().Refusals(BareField(RuleSets.Frontier.Id)));

        IReadOnlyList<RejectionReason> refused =
            new OperatingDrillingRule().Refusals(BareField(RuleSets.Realistic.Id));

        Assert.NotEmpty(refused);
    }

    /// <summary>
    /// THE LICENCE MECHANIC IS OFF IN THE BUILDER AND ON IN THE SIMULATOR.
    /// </summary>
    /// <remarks>
    /// <para>Measured, not asserted from taste: at the shipped exploration prior
    /// a company got three or four real attempts before the work commitment fell
    /// due and needed about five, so over half of all Oilfield Days runs lost
    /// the acreage to luck alone and could do nothing for the rest of the decade
    /// (plans 24 §1).</para>
    ///
    /// <para><b>Off means NEUTRAL, not gentler</b> (plans 24 §2). The licence is
    /// still granted, still owned, still projected — it simply cannot be lost.
    /// Nothing reading it knows which game it is in.</para>
    /// </remarks>
    [Fact]
    public void GS6_the_builder_has_no_licence_mechanic_and_the_simulator_does()
    {
        Assert.Empty(GameStyles.Days.Terms.Licence.WorkCommitment);
        Assert.NotEmpty(GameStyles.Engineer.Terms.Licence.WorkCommitment);

        Assert.Empty(Licence(GameStyles.Days).Terms.WorkCommitment);
        Assert.NotEmpty(Licence(GameStyles.Engineer).Terms.WorkCommitment);

        // AND NEITHER HAS LOST ONE ON THE FIRST MORNING, which is what says the
        // neutral licence is a licence and not an absence.
        Assert.True(Licence(GameStyles.Days).IsLive);
        Assert.True(Licence(GameStyles.Engineer).IsLive);
    }

    /// <summary>
    /// A STYLE HANDS OVER TERMS; IT NEVER HANDS OVER GENTLER ONES.
    /// </summary>
    /// <remarks>
    /// Plans 24 §2's law, kept through plans 25's change of mechanism: two
    /// positions, ON and NEUTRAL. Each of these is a mechanic doing nothing at
    /// all rather than doing something smaller.
    /// </remarks>
    [Fact]
    public void GS7_the_builders_neutral_terms_are_neutral_and_not_merely_gentle()
    {
        StyleTerms days = GameStyles.Days.Terms;

        // ABSENT, not gentle. The engine refuses terms describing a collar or a
        // policy that does nothing, so for these two the honest neutral is no
        // terms at all — and the stage slot is not claimed either.
        Assert.Null(days.Hedge);
        Assert.Null(days.Insurance);

        Assert.Equal(0.0, days.DemurrageRate);
        Assert.Equal(Money.Zero, days.Licence.Bond);

        // A THRESHOLD NO RUN REACHES, not a flag. The farm-down keeps its real
        // terms because the engine REFUSES a zero sellable cap, and a tiny one
        // would be a gentler mechanic rather than an absent one — so the
        // takeover is stopped by its clock instead.
        Assert.Equal(int.MaxValue, days.TakeoverAfterAmortisingTicks);

        // ABSENT AS PRESENCE, not as terms (plans 27, W5): the two mechanics
        // whose neutral cannot be expressed as terms are simply not carried —
        // no commitment stage, no tenure refusal, no Borrow or Repay offered.
        Assert.False(days.Tenure);
        Assert.False(days.Banking);
        Assert.False(days.TakeOrPay);
        Assert.True(GameStyles.Engineer.Terms.Tenure);
        Assert.True(GameStyles.Engineer.Terms.Banking);
        Assert.True(GameStyles.Engineer.Terms.TakeOrPay);

        // And the full-fidelity style is the engine's own terms, unchanged.
        Assert.Equal(Defaults.LicenceTerms, GameStyles.Engineer.Terms.Licence);
        Assert.Equal(Defaults.Hedge, GameStyles.Engineer.Terms.Hedge);
    }

    /// <summary>
    /// A PLANT BUILT MID-GAME METERS WHAT IT DELIVERS (finding 285).
    /// </summary>
    /// <remarks>
    /// <para>The ledger probe found every Days run insolvent with the chain
    /// FLOWING: wells lifting, the custody element passing mass in the solve,
    /// and <c>ProducedThisTick</c> zero for seventeen straight months — so
    /// lifting was paid on every barrel and revenue never posted. The loop had
    /// captured <c>chain.MeteredPoints</c> ONCE at composition, when a
    /// bare-ground company's custody point does not exist yet, so the metered
    /// set stayed empty forever: every barrel crossed the meter unrecorded,
    /// unsold and un-stored.</para>
    ///
    /// <para>The opening-position builds could never see it — their custody
    /// point exists at composition, so the captured list was right by luck.
    /// This is the S2 case: build the plant DURING the game, then deliver.</para>
    /// </remarks>
    [Fact]
    public void GS8_a_plant_built_mid_game_meters_what_it_delivers()
    {
        Built built = Assert.IsType<Built>(
            EngineBuilder.Build(GameStyles.Days.Compose(Fixture.Settings())));

        Engine engine = built.Engine;
        FieldControl field = engine.Provided.Resolve<FieldControl>();

        // A reservoir the company already knows is there (SDD-010 §4b), the
        // shared fixtures' own shape — this test is about DELIVERY, not luck.
        EntityId<IReservoirCompartmentEntity> reservoir = field.AddCompartment(
            new GeneratedCompartment(
                PoreVolume: new ReservoirVolume(100.0e6),
                Porosity: 0.22,
                OilSaturation: 0.7,
                InitialPressure: new Pressure(30.0e6),
                Temperature: Temperature.FromCelsius(93.3),
                Depth: new Length(2000.0),
                FluidSystem: new ContentId("medium-crude")),
            permeability: new Permeability(2.0e-13),
            netThickness: new Length(30.0),
            drainageArea: new Area(2.0e5),
            rockCompressibility: 4.5e-10,
            gasOilContact: new Length(1900.0),
            oilWaterContact: new Length(2100.0),
            Defaults.Wettability, Defaults.Drive,
            Defaults.AquiferStrength, Defaults.AquiferResponseTime);

        engine.Provided.Resolve<WorldState>()
            .DeclareKnownField(reservoir, new ReservoirVolume(100.0e6));

        // The well waits shut in (plans 23's suspended well) until the plant
        // it needs is commissioned through the real command.
        field.Drill(reservoir, new Length(2000.0));

        Assert.IsType<Accepted>(
            engine.Commands.Submit(new InstallEarlyProductionFacilityCommand()));

        var month = 0;
        while (month < 12
               && (engine.ReadModel is not FieldReadModel now
                   || now.ProducedThisTick.CubicMetres <= 0.0))
        {
            engine.Pipeline.AdvanceTick();
            month++;
        }

        Assert.True(engine.ReadModel!.ProducedThisTick.CubicMetres > 0.0,
            "a company that built its plant and lifted oil across the meter delivered nothing");

        // AND THE BARRELS WERE SOLD — delivery without revenue would be the
        // same defect one seam later.
        Assert.Contains(
            engine.Audit.Query(new AuditQuery(null, AuditCategory.CustodyTransfer, null, null)),
            entry => entry.Data.ContainsKey("volume-m3"));
    }

    /// <summary>The one licence a build composes.</summary>
    private static OGSim.Company.Licence Licence(IGameStyle mode)
    {
        Built built = Assert.IsType<Built>(
            EngineBuilder.Build(mode.Compose(Fixture.Settings())));

        return built.Engine.Provided.Resolve<OGSim.Company.Licence>();
    }

    /// <summary>A field with a licence, a balance and no plant at all.</summary>
    private static FieldControl BareField(ContentId rules)
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(
            Fixture.Settings(startingState: StartingStates.BareGround, rules: rules)));

        return built.Engine.Provided.Resolve<FieldControl>();
    }

    /// <summary>
    /// <c>Style</c> joined <see cref="EngineSettings"/> in the two-products
    /// update and missed the hand-written <c>Equals</c>/<c>GetHashCode</c> —
    /// the finding-131 defect class, in the same sweep finding 284 caught it
    /// for <c>ScenarioProgress.Deadline</c>. Two settings differing only in
    /// WHICH PRODUCT they compose compared EQUAL, which is exactly the
    /// comparison a host uses to notice a save reloading as the wrong game.
    /// </summary>
    [Fact]
    public void GS9_two_settings_differing_only_in_style_are_not_equal()
    {
        EngineSettings baseline = Fixture.Settings();

        ContentId other = baseline.Style == GameStyles.Days.Id
            ? GameStyles.Engineer.Id
            : GameStyles.Days.Id;

        Assert.NotEqual(baseline, baseline with { Style = other });
        Assert.Equal(baseline, baseline with { });
    }
}
