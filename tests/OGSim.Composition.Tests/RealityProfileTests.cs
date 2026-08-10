// R25.1 — fidelity is a mode (design 18 §5b, SDD-005 §7b).
//
// "Fun" and "real" as two ways to play the same game, not two games. What is
// asserted here is that the axis is real — the two profiles genuinely compute
// differently — and that it sits UNDER the game rather than in it: the same
// chain, the same decisions, the same loop, at either setting.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition.Tests;

public sealed class RealityProfileTests
{
    private static Engine At(string profile)
    {
        Built built = Assert.IsType<Built>(
            EngineBuilder.Build(Fixture.Settings(profile: profile)));

        FieldControl field = built.Engine.Provided.Resolve<FieldControl>();

        EntityId<IReservoirCompartmentEntity> target = field.AddCompartment(
            new GeneratedCompartment(
                PoreVolume: new ReservoirVolume(100.0e6),
                Porosity: 0.22,
                OilSaturation: 0.7,
                InitialPressure: new Pressure(30.0e6),
                Temperature: Temperature.FromCelsius(93.3),
                Depth: new Length(2000.0)),
                // Rock the shipped plant is sized for. It said 2e-13 and 30 m
                // while every well was built from Defaults.Inflow's 1e-13 and
                // 20 m — a compartment stating rock nobody read (finding 170).
                // Now that a well is built from the rock it is in, the two have
                // to agree or these fixtures would be testing a field three
                // times more productive than the one the chain was designed
                // against.
                permeability: new Permeability(1.0e-13),
                netThickness: new Length(20.0),
            drainageArea: new Area(2.0e5),
            rockCompressibility: 4.5e-10,
            gasOilContact: new Length(1900.0),
            oilWaterContact: new Length(2100.0),
            Defaults.Wettability, Defaults.Drive,
            Defaults.AquiferStrength, Defaults.AquiferResponseTime);

        // A SCENARIO DECLARING A KNOWN FIELD (SDD-010 §4b). These fixtures place
        // their reservoir directly rather than generating a basin, so it is
        // already known to be there — placed and found in one step, carrying no
        // exploration risk because there is nothing left to be wrong about.
        built.Engine.Provided.Resolve<WorldState>().DeclareKnownField(target, new ReservoirVolume(100.0e6));

        field.Drill(target, new Length(2000.0));

        return built.Engine;
    }

    // ------------------------------------------------------- the axis is real

    /// <summary>
    /// Both profiles compose and both play. A fidelity setting that only one
    /// half of the engine honoured would be worse than none.
    /// </summary>
    [Fact]
    public void R25V1_both_profiles_compose_and_run()
    {
        foreach (string profile in new[] { "simulation", "arcade" })
        {
            Engine engine = At(profile);

            engine.Pipeline.AdvanceTick();

            Assert.True(engine.ReadModel!.ProducedThisTick.CubicMetres > 0.0,
                $"a field played at {profile} fidelity must produce");
        }
    }

    /// <summary>
    /// The two genuinely compute differently — the arcade model holds one
    /// formation volume factor where the full one applies a correlation, so the
    /// same reservoir shrinks to a different number of barrels.
    ///
    /// <para>If this ever passes with equal values the axis has become
    /// decoration: two names selecting one implementation.</para>
    /// </summary>
    [Fact]
    public void R25V1_fidelity_changes_what_the_world_computes()
    {
        IFluidPropertyModel real = At("simulation").Provided.Resolve<IFluidPropertyModel>();
        IFluidPropertyModel fun = At("arcade").Provided.Resolve<IFluidPropertyModel>();

        Assert.Equal(new ContentId("black-oil-correlations"), real.Id);
        Assert.Equal(new ContentId("arcade-fluid"), fun.Id);

        var deep = new Pressure(30.0e6);
        var shallow = new Pressure(10.0e6);

        // The full model's Bo moves with pressure; the arcade model's does not.
        Assert.NotEqual(real.Bo(deep).RbPerStb, real.Bo(shallow).RbPerStb, precision: 6);
        Assert.Equal(fun.Bo(deep).RbPerStb, fun.Bo(shallow).RbPerStb, precision: 12);
    }

    /// <summary>
    /// Finding 160, closed in this mode. The arcade model's one factor IS the
    /// factor the shipped completion converts at, so a well's conversion and the
    /// engine's cannot disagree — there is only one number.
    /// </summary>
    [Fact]
    public void R25V1_at_arcade_fidelity_the_well_and_the_engine_agree_on_shrinkage()
    {
        IFluidPropertyModel fun = At("arcade").Provided.Resolve<IFluidPropertyModel>();

        Assert.Equal(
            Defaults.CompletionBo.RbPerStb,
            fun.Bo(new Pressure(30.0e6)).RbPerStb,
            precision: 12);
    }

    // -------------------------------------------- the axis is under the game

    /// <summary>
    /// THE SAME GAME AT EITHER SETTING. The chain is the same shape, the
    /// separator still binds, and the bottleneck is still named — because
    /// fidelity is a dial under the physics and never under the decisions.
    ///
    /// <para>An arcade mode that removed the separator would not be a simpler
    /// game, it would be a different one.</para>
    /// </summary>
    [Fact]
    public void R25V1_the_chain_and_its_decisions_are_the_same_at_either_fidelity()
    {
        foreach (string profile in new[] { "simulation", "arcade" })
        {
            Engine engine = At(profile);
            engine.Pipeline.AdvanceTick();

            Assert.Equal(
                ["well-1", "gathering-1", "manifold", "flowline", "separator",
                 "custody-meter", "flare", "water-disposal", "tank"],
                engine.ReadModel!.Chain.Select(element => element.DisplayId));

            // And the verb that answers a bottleneck is on offer either way.
            Assert.IsType<Accepted>(engine.Commands.Submit(new InstallSeparatorCommand()));
        }
    }

    // ------------------------------------------------------------- refusals

    /// <summary>
    /// A run cannot be played at a fidelity nobody defined. Refused when the
    /// engine composes, with the name in the reason — silently falling back to
    /// the shipped models would give a player a different game from the one they
    /// asked for and never say so.
    /// </summary>
    [Fact]
    public void R25V1_an_unknown_profile_is_refused_rather_than_defaulted()
    {
        var fault = Assert.Throws<ContentFault>(
            () => EngineBuilder.Build(Fixture.Settings(profile: "ultra-realism")));

        Assert.Contains("ultra-realism", fault.Fault.Detail);
    }

    /// <summary>
    /// The simulation profile is EMPTY, and that is correct rather than
    /// unfinished: a profile names departures from the shipped set, and
    /// simulation is the shipped set (SDD-005 §7b).
    /// </summary>
    [Fact]
    public void R25V1_the_simulation_profile_names_no_departures()
    {
        Assert.Empty(Defaults.Simulation.Fidelity);
        Assert.Null(Defaults.Simulation.Selected(Defaults.FluidSlot));

        Assert.Equal(
            new ContentId("arcade-fluid"),
            Defaults.Arcade.Selected(Defaults.FluidSlot));
    }
}
