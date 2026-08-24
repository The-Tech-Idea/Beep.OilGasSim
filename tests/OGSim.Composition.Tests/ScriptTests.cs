// R24.8 — the scenario's script, executed (SDD-014 §5, finding 291). GM12's
// claim: a mission's scripted beats actually happen — a scripted command goes
// through the player's own bus, a scripted parameter through technology's own
// effect door, and a scripted order the rules refuse lands on the trail
// rather than vanishing or crashing the campaign.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition.Tests;

public sealed class ScriptTests
{
    /// <summary>
    /// GM12: THE SCRIPT'S BEATS HAPPEN. At its tick — and only at its tick — a
    /// scripted command executes through the same bus a player's does: the
    /// fixture's well is open before the beat and shut in after it.
    /// </summary>
    [Fact]
    public void GM12_a_scripted_beat_executes_at_its_tick_and_no_other()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        // A field to work on, or the operator rule refuses every order —
        // "there is nothing here to work on" — before the bus can prove
        // anything (plans 23).
        EntityId<IReservoirCompartmentEntity> target =
            engine.Provided.Resolve<FieldControl>().AddCompartment(
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

        engine.Provided.Resolve<WorldState>().DeclareKnownField(
            target, new ReservoirVolume(100.0e6));

        // A separator upgrade: the opening chain holds an e1 vessel, so the
        // order is accepted on sight — and booking it is observable on the
        // activity register the moment the bus takes it.
        ScenarioScriptStage stage = Stage(engine,
        [
            new ScriptedCommand(new Tick(3), new InstallSeparatorCommand()),
        ]);

        // The tick BEFORE the beat: nothing has happened.
        stage.Execute(Context(2));
        engine.Pipeline.AdvanceTick();
        Assert.Equal(0, engine.ReadModel!.ActivitiesRunning);

        // The beat's own tick: the order goes through the player's bus and the
        // crew is booked — visible on the same surface a player watches.
        stage.Execute(Context(3));
        engine.Pipeline.AdvanceTick();

        string refusals = string.Join(" | ",
            engine.Audit.Query(new AuditQuery(null, AuditCategory.Rejection, null, null))
                .Select(entry => string.Join(",", entry.Data.Select(d => d.Key + "=" + d.Value.Value))));

        Assert.True(engine.ReadModel!.ActivitiesRunning == 1,
            $"expected the scripted install booked; rejections on the trail: [{refusals}]");
    }

    /// <summary>
    /// GM12: A SCRIPTED PARAMETER IS STILL REFUSED AT COMPOSITION — correctly.
    /// The execution arm exists (the same effect door technology applies
    /// through), but `IEffectState.Parameter` has no reader in any composed
    /// model yet, so an override would land in a dictionary nothing consumes
    /// and the scenario would LOOK scripted while changing nothing. The
    /// runner's refusal names that honestly; it lifts when R20d.10 gives a
    /// model a parameter to read.
    /// </summary>
    [Fact]
    public void GM12_a_scripted_parameter_is_refused_until_a_model_consumes_one()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));

        ContentFault fault = Assert.Throws<ContentFault>(() => Stage(built.Engine,
        [
            new ScriptedParameter(
                new Tick(3), new ModelSlot("market"), new ParameterKey("oil-price-shock"),
                Value: 2.0),
        ]));

        Assert.Contains("no model exposes", fault.Fault.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// GM12: A SCRIPTED ORDER THE RULES REFUSE IS RECORDED, NEVER SWALLOWED
    /// (law L4) and never a crash — the mission visibly skips a beat, and the
    /// trail says which beat and why.
    /// </summary>
    [Fact]
    public void GM12_a_refused_scripted_command_lands_on_the_trail()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        // A well that does not exist: refused by the choke command's own
        // validator, whatever else is true of the fixture.
        ScenarioScriptStage stage = Stage(engine,
        [
            new ScriptedCommand(new Tick(2),
                new SetWellChokeCommand(new EntityId<ICompletion>(999), Open: false)),
        ]);

        stage.Execute(Context(2));

        Assert.Contains(
            engine.Audit.Query(new AuditQuery(null, AuditCategory.Rejection, null, null)),
            entry => entry.Data.TryGetValue("kind", out AuditValue kind)
                && kind.Value == "scenario.script-refused");
    }

    /// <summary>
    /// AND THE COMPOSED ENGINE CARRIES THE STAGE, BOUND. The builder attaches
    /// the bus in its last step; an engine whose script stage could not act
    /// would be a mission system in name only.
    /// </summary>
    [Fact]
    public void GM12_the_composed_engine_binds_its_script_stage()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));

        ScenarioScriptStage stage = built.Engine.Provided.Resolve<ScenarioScriptStage>();

        // Bound already — a second bind is the write-once refusal.
        InvariantFault fault = Assert.Throws<InvariantFault>(
            () => stage.BindTo(built.Engine.Commands));

        Assert.Contains("already bound", fault.Fault.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// THE MODIFIER IS A REQUIREMENT (R24.8, finding 291): a scenario
    /// calibrated for one fidelity refuses a build composed at another —
    /// running it laxer would award its scores for a different game — and a
    /// scenario naming none runs anywhere, which is what the shipped default
    /// does at both products' fidelities every day.
    /// </summary>
    [Fact]
    public void A_scenario_calibrated_for_one_fidelity_refuses_another()
    {
        var paths = new ReadModelPaths(Defaults.ProjectedPaths);

        ContentFault fault = Assert.Throws<ContentFault>(() => new ScenarioRunner(
            Defaults.FirstField with { RealityProfile = new ContentId("simulation") },
            paths.Schema,
            composedProfile: new ContentId("arcade")));

        Assert.Contains("simulation", fault.Fault.Detail, StringComparison.Ordinal);
        Assert.Contains("arcade", fault.Fault.Detail, StringComparison.Ordinal);

        // Named and MATCHED composes; null composes anywhere.
        _ = new ScenarioRunner(
            Defaults.FirstField with { RealityProfile = new ContentId("arcade") },
            paths.Schema, composedProfile: new ContentId("arcade"));
        _ = new ScenarioRunner(
            Defaults.FirstField, paths.Schema, composedProfile: new ContentId("arcade"));
    }

    /// <summary>A stage over a scripted variant of the shipped scenario, bound
    /// to the real engine's own bus and trail.</summary>
    private static ScenarioScriptStage Stage(Engine engine, IReadOnlyList<ScriptedEntry> script)
    {
        var paths = new ReadModelPaths(Defaults.ProjectedPaths);
        var runner = new ScenarioRunner(
            Defaults.FirstField with { Script = script }, paths.Schema,
            new ContentId("simulation"));

        var stage = new ScenarioScriptStage(
            runner,
            engine.Provided.Resolve<OGSim.Capabilities.EffectState>(),
            engine.Audit);

        stage.BindTo(engine.Commands);

        return stage;
    }

    private static TickContext Context(int tick) => new()
    {
        Tick = new Tick(tick),
        Date = new GameDate(1965, 1 + tick),
    };
}
