// R1.9 / R1.10 — commands and composition (SDD-001 §7, §9; design 03 §3.1, §5).
// R1-V11 composition succeeds, R1-V12 every failure mode is detected and
// reported COMPLETELY, R1-V13 command atomicity.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class CompositionTests
{
    // ------------------------------------------------------------- fixtures

    private interface IPumpModel;
    private interface ITankModel;
    private interface IMissingModel;

    private sealed class PumpModel : IPumpModel;
    private sealed class TankModel : ITankModel;

    /// <summary>Work for a declared slot. Does nothing on purpose — these tests
    /// are about composition, not about what a stage computes.</summary>
    private sealed class NoOpStage(StageId id) : ITickStage
    {
        public StageId Id => id;

        public void Execute(TickContext context) { }
    }

    private sealed class TestModule(
        string name,
        Type[] provides,
        Type[] requires,
        StateKey[] ownsState,
        StageParticipation[] stages) : IModule
    {
        public ModuleManifest Manifest { get; } = new(
            new ModuleName(name), provides, requires, ownsState, stages, []);

        public Func<IModuleComposition, bool>? OnCompose { get; init; }

        /// <summary>Resolved during Compose — the thing that only works if the
        /// provider was composed first.</summary>
        public Type? RequiresDuringCompose { get; init; }

        public void Compose(IModuleComposition composition)
        {
            if (OnCompose is not null && OnCompose(composition)) return;

            if (RequiresDuringCompose == typeof(IPumpModel)) composition.Require<IPumpModel>();
            if (RequiresDuringCompose == typeof(ITankModel)) composition.Require<ITankModel>();

            IReadOnlyList<Type> declared = Manifest.Provides;
            for (int i = 0; i < declared.Count; i++)
            {
                if (declared[i] == typeof(IPumpModel)) composition.Provide<IPumpModel>(new PumpModel());
                if (declared[i] == typeof(ITankModel)) composition.Provide<ITankModel>(new TankModel());
            }

            // Every declared slot is filled, because a claimed slot left empty is
            // now its own refusal (SDD-001 §9, finding 125).
            IReadOnlyList<StageParticipation> stages = Manifest.Stages;
            for (int i = 0; i < stages.Count; i++)
                composition.Contribute(stages[i].Order, new NoOpStage(stages[i].Stage));
        }
    }

    private static TestModule Module(
        string name,
        Type[]? provides = null,
        Type[]? requires = null,
        StateKey[]? ownsState = null,
        StageParticipation[]? stages = null,
        Type? requiresDuringCompose = null) =>
        new(name, provides ?? [], requires ?? [], ownsState ?? [], stages ?? [])
        {
            RequiresDuringCompose = requiresDuringCompose,
        };

    // ------------------------------------------------------------- R1-V11

    [Fact] // R1-V11: a valid module set composes
    public void R1V11_a_valid_module_set_composes()
    {
        var result = new ModuleComposer().Compose(
        [
            Module("facilities", provides: [typeof(IPumpModel)],
                   ownsState: [new StateKey("facilities")],
                   stages: [new StageParticipation(StageId.SolveFlow, 0)]),
            Module("transport", provides: [typeof(ITankModel)], requires: [typeof(IPumpModel)],
                   ownsState: [new StateKey("transport")],
                   stages: [new StageParticipation(StageId.Custody, 0)]),
        ]);

        var composed = Assert.IsType<Composed>(result);
        Assert.Equal(2, composed.OrderedModules.Count);

        // Ordered by the stage they first run in, not by declaration order.
        Assert.Equal("facilities", composed.OrderedModules[0].Manifest.Name.Value);
        Assert.Equal("transport", composed.OrderedModules[1].Manifest.Name.Value);
    }

    [Fact] // The pipeline needs modules in run order, and the order must be total
    public void R1V11_modules_are_ordered_by_stage_then_order_then_name()
    {
        var result = new ModuleComposer().Compose(
        [
            Module("zeta", stages: [new StageParticipation(StageId.Economics, 0)]),
            Module("alpha", stages: [new StageParticipation(StageId.Open, 1)]),
            Module("beta", stages: [new StageParticipation(StageId.Open, 0)]),
            Module("stateless"),   // no stage participation sorts last
        ]);

        var composed = Assert.IsType<Composed>(result);
        Assert.Equal(["beta", "alpha", "zeta", "stateless"],
                     composed.OrderedModules.Select(m => m.Manifest.Name.Value).ToArray());
    }

    [Fact] // R1-V11: resolution follows the dependency graph, not the argument list
    public void R1V11_a_provider_declared_after_its_consumer_still_resolves_first()
    {
        // "transport" requires IPumpModel and is listed BEFORE the module that
        // provides it. Composition must still succeed: the cycle check proves a
        // construction order exists, and finding 126 is that the composer has to
        // use it rather than compose in the order it was handed (SDD-001 §9).
        var result = new ModuleComposer().Compose(
        [
            Module("transport", provides: [typeof(ITankModel)], requires: [typeof(IPumpModel)],
                   stages: [new StageParticipation(StageId.Custody, 0)],
                   requiresDuringCompose: typeof(IPumpModel)),
            Module("facilities", provides: [typeof(IPumpModel)],
                   stages: [new StageParticipation(StageId.SolveFlow, 0)]),
        ]);

        Assert.IsType<Composed>(result);
    }

    [Fact] // R1-V11: a composed set hands the pipeline exactly the stages it validated
    public void R1V11_contributed_stages_come_back_in_stage_order()
    {
        var result = new ModuleComposer().Compose(
        [
            Module("late", stages: [new StageParticipation(StageId.Company, 0)]),
            Module("early", stages: [new StageParticipation(StageId.Environment, 0)]),
            Module("split", stages:
            [
                new StageParticipation(StageId.Environment, 1),
                new StageParticipation(StageId.Close, 0),
            ]),
        ]);

        var composed = Assert.IsType<Composed>(result);

        Assert.Equal(
            [StageId.Environment, StageId.Environment, StageId.Company, StageId.Close],
            composed.Stages.Select(s => s.Id).ToArray());
    }

    // ------------------------------------------------------------- R1-V12

    [Fact] // Failure mode 6: a slot declared and left empty (law L3, finding 125)
    public void R1V12_a_declared_stage_slot_with_no_work_is_named()
    {
        var idle = new TestModule(
            "idle", [], [], [], [new StageParticipation(StageId.Economics, 0)])
        {
            // Returns true, so the fixture's Compose stops before contributing.
            OnCompose = _ => true,
        };

        var refused = Assert.IsType<CompositionRefused>(new ModuleComposer().Compose([idle]));

        CompositionProblem problem = Assert.Single(refused.Problems);
        Assert.Equal(CompositionProblemKind.UnmetRequirement, problem.Kind);
        Assert.Contains("no work was contributed", problem.Detail);
    }

    [Fact] // Failure mode 7: acting in a stage the manifest never declared
    public void R1V12_contributing_to_an_undeclared_slot_throws()
    {
        var sneak = new TestModule("sneak", [], [], [], [])
        {
            OnCompose = composition =>
            {
                composition.Contribute(0, new NoOpStage(StageId.Economics));
                return true;
            },
        };

        InvariantFault fault = Assert.Throws<InvariantFault>(
            () => new ModuleComposer().Compose([sneak]));

        Assert.Contains("never declared", fault.Message);
    }

    [Fact] // Failure mode 1: a requirement nobody provides
    public void R1V12_an_unmet_requirement_is_named()
    {
        var result = new ModuleComposer().Compose(
            [Module("wells", requires: [typeof(IMissingModel)])]);

        var refused = Assert.IsType<CompositionRefused>(result);
        CompositionProblem problem = Assert.Single(refused.Problems);
        Assert.Equal(CompositionProblemKind.UnmetRequirement, problem.Kind);
        Assert.Contains(nameof(IMissingModel), problem.Detail);
    }

    [Fact] // Failure mode 2: two modules claiming one contract
    public void R1V12_a_duplicate_provider_is_detected()
    {
        var result = new ModuleComposer().Compose(
        [
            Module("facilities", provides: [typeof(IPumpModel)]),
            Module("transport", provides: [typeof(IPumpModel)]),
        ]);

        var refused = Assert.IsType<CompositionRefused>(result);
        Assert.Contains(refused.Problems, p => p.Kind == CompositionProblemKind.DuplicateProvider);
    }

    [Fact] // Failure mode 3: law L5 — two modules owning one fact
    public void R1V12_a_duplicate_state_key_is_detected()
    {
        var result = new ModuleComposer().Compose(
        [
            Module("wells", ownsState: [new StateKey("pressure")]),
            Module("subsurface", ownsState: [new StateKey("pressure")]),
        ]);

        var refused = Assert.IsType<CompositionRefused>(result);
        CompositionProblem problem = Assert.Single(refused.Problems);
        Assert.Equal(CompositionProblemKind.DuplicateStateKey, problem.Kind);
        Assert.Contains("cannot own one fact", problem.Detail);
    }

    [Fact] // Failure mode 4: no construction order exists
    public void R1V12_a_dependency_cycle_is_detected()
    {
        var result = new ModuleComposer().Compose(
        [
            Module("facilities", provides: [typeof(IPumpModel)], requires: [typeof(ITankModel)]),
            Module("transport", provides: [typeof(ITankModel)], requires: [typeof(IPumpModel)]),
        ]);

        var refused = Assert.IsType<CompositionRefused>(result);
        Assert.Contains(refused.Problems, p => p.Kind == CompositionProblemKind.DependencyCycle);
    }

    [Fact] // Failure mode 5: an ambiguous within-stage order is non-deterministic
    public void R1V12_a_stage_slot_conflict_is_detected()
    {
        var result = new ModuleComposer().Compose(
        [
            Module("economics", stages: [new StageParticipation(StageId.Economics, 0)]),
            Module("company", stages: [new StageParticipation(StageId.Economics, 0)]),
        ]);

        var refused = Assert.IsType<CompositionRefused>(result);
        CompositionProblem problem = Assert.Single(refused.Problems);
        Assert.Equal(CompositionProblemKind.StageConflict, problem.Kind);

        // The same stage at DIFFERENT orders is normal and must compose.
        var fine = new ModuleComposer().Compose(
        [
            Module("economics", stages: [new StageParticipation(StageId.Economics, 0)]),
            Module("company", stages: [new StageParticipation(StageId.Economics, 1)]),
        ]);
        Assert.IsType<Composed>(fine);
    }

    [Fact] // R1-V12: EVERY problem, not the first — one fix-run per error is the failure mode
    public void R1V12_every_problem_is_reported_not_just_the_first()
    {
        var result = new ModuleComposer().Compose(
        [
            Module("a", provides: [typeof(IPumpModel)], requires: [typeof(IMissingModel)],
                   ownsState: [new StateKey("shared")],
                   stages: [new StageParticipation(StageId.Open, 0)]),
            Module("b", provides: [typeof(IPumpModel)],
                   ownsState: [new StateKey("shared")],
                   stages: [new StageParticipation(StageId.Open, 0)]),
        ]);

        var refused = Assert.IsType<CompositionRefused>(result);
        Assert.Contains(refused.Problems, p => p.Kind == CompositionProblemKind.UnmetRequirement);
        Assert.Contains(refused.Problems, p => p.Kind == CompositionProblemKind.DuplicateProvider);
        Assert.Contains(refused.Problems, p => p.Kind == CompositionProblemKind.DuplicateStateKey);
        Assert.Contains(refused.Problems, p => p.Kind == CompositionProblemKind.StageConflict);
        Assert.True(refused.Problems.Count >= 4, "the whole list, not the first failure");
    }

    [Fact] // Declaring a contract and then not providing one is only visible at compose time
    public void R1V12_a_declared_but_undelivered_contract_is_caught()
    {
        var liar = new TestModule("liar", [typeof(IPumpModel)], [], [], [])
        {
            OnCompose = _ => true,   // declares IPumpModel, provides nothing
        };

        var refused = Assert.IsType<CompositionRefused>(new ModuleComposer().Compose([liar]));
        Assert.Contains(refused.Problems, p => p.Detail.Contains("never provided"));
    }

    [Fact] // Resolution happens after validation, so Require only sees proven contracts
    public void R1V11_require_resolves_what_another_module_provided()
    {
        IPumpModel? resolved = null;
        var consumer = new TestModule("consumer", [], [typeof(IPumpModel)], [], [])
        {
            OnCompose = composition => { resolved = composition.Require<IPumpModel>(); return true; },
        };

        var result = new ModuleComposer().Compose(
            [Module("facilities", provides: [typeof(IPumpModel)]), consumer]);

        Assert.IsType<Composed>(result);
        Assert.NotNull(resolved);
    }

    // ------------------------------------------------------------- R1.9 commands

    private sealed record SetChokeCommand(EntityRef? Subject, double Opening) : Command(Subject);

    private sealed class ChokeValidator(bool valid) : ICommandValidator<SetChokeCommand>
    {
        public bool WasCalled { get; private set; }

        public IReadOnlyList<RejectionReason> Validate(SetChokeCommand command)
        {
            WasCalled = true;
            return valid
                ? []
                : [new RejectionReason("choke.out-of-range", "opening must be 0-1"),
                   new RejectionReason("choke.no-well", "no such well")];
        }
    }

    private sealed class ChokeApplier : ICommandApplier<SetChokeCommand>
    {
        public int Applications { get; private set; }
        public AuditId SeenSubmission { get; private set; }

        public Applied Apply(SetChokeCommand command, AuditId submission)
        {
            Applications++;
            SeenSubmission = submission;
            return new Applied(submission, []);
        }
    }

    private static (CommandBus Bus, AuditTrail Trail, SimulationClock Clock) NewCommandBus()
    {
        var clock = new SimulationClock(new GameDate(1965, 1));
        var trail = new AuditTrail(clock, new AuditRetention(500));
        return (new CommandBus(trail, new EventBus(clock)), trail, clock);
    }

    [Fact] // Design 03 §5: validate, audit, apply, publish
    public void R1V13_an_accepted_command_is_audited_then_applied()
    {
        (CommandBus bus, AuditTrail trail, _) = NewCommandBus();
        var applier = new ChokeApplier();
        bus.Register(new ChokeValidator(valid: true), applier);

        CommandResult result = bus.Submit(new SetChokeCommand(new EntityRef(EntityKind.Well, 1), 0.5));

        Assert.IsType<Accepted>(result);
        Assert.Equal(1, applier.Applications);

        // The applier receives the submission audit id, which is what lets it
        // stamp Cause on anything it raises (INV12).
        Assert.NotEqual(default, applier.SeenSubmission);
        Assert.Single(trail.Query(new AuditQuery(null, AuditCategory.Command, null, null)));
    }

    [Fact] // R1-V13: a rejected command leaves everything but the rejection entry alone
    public void R1V13_a_rejected_command_never_reaches_the_applier()
    {
        (CommandBus bus, AuditTrail trail, _) = NewCommandBus();
        var applier = new ChokeApplier();
        bus.Register(new ChokeValidator(valid: false), applier);

        CommandResult result = bus.Submit(new SetChokeCommand(null, 5.0));

        var rejected = Assert.IsType<Rejected>(result);
        Assert.Equal(0, applier.Applications);         // nothing mutated

        // ALL reasons, not the first: one round-trip per problem is the failure
        // mode the plural carries.
        Assert.Equal(2, rejected.Reasons.Count);

        // Design 09 §5.1 C3 — audited, but as a Rejection, not a Command.
        Assert.Single(trail.Query(new AuditQuery(null, AuditCategory.Rejection, null, null)));
        Assert.Empty(trail.Query(new AuditQuery(null, AuditCategory.Command, null, null)));
    }

    [Fact] // Law L5 at the command level: two modules cannot own one command
    public void L5_a_command_type_cannot_be_registered_twice()
    {
        (CommandBus bus, _, _) = NewCommandBus();
        bus.Register(new ChokeValidator(valid: true), new ChokeApplier());

        Assert.Throws<InvariantFault>(
            () => bus.Register(new ChokeValidator(valid: true), new ChokeApplier()));
    }

    [Fact] // An unhandled command means composition let something through
    public void R1V13_an_unregistered_command_is_a_fault_not_a_silent_no_op()
    {
        (CommandBus bus, _, _) = NewCommandBus();
        Assert.Throws<InvariantFault>(() => bus.Submit(new SetChokeCommand(null, 0.5)));
    }
}
