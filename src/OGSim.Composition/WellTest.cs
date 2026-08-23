// R12b — flow a well and watch the pressure build back up (SDD-008 §3, design 06 §3).
//
// The sharpest measurement of a compartment there is: it watches the reservoir
// answer for itself over days, which is why it beats a log on permeability and
// is the only source that can see pressure at all. It is also the reason a player
// would ever stop producing on purpose — the well is shut in for the build-up, so
// the test costs the month's oil as well as its own price.
//
// AND THAT SHUT-IN IS REAL (SDD-007 §5's R12b.18 amendment). This file's own
// header made that claim while nothing in the class touched a well's choke —
// finding it was the class documenting an economics mechanic that was pure
// flavour text (finding 245). Every well the compartment has is closed the
// instant the test is booked.
//
// AND IT STAYS CLOSED — Complete does not reopen it. `ActivityStage.Execute`
// calls `Complete` from INSIDE stage 3 (Operations), and SolveFlow is stage 5
// of the SAME tick: a one-tick test that reopened its well in `Complete`
// would have closed and reopened it before the solve that tick ever ran, and
// "costs the month's oil" would still be false with the well never actually
// absent from a single segment. Left shut, the well is closed for the solve
// exactly like any other shut-in, and stays that way until the player
// reopens it through `SetWellChokeCommand` — the same door every other
// shut-in reason already uses, and arguably more honest: a company that
// forgets to put a well back on production after a test loses more than one
// month, which is what would actually happen.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Company;
using OGSim.Wells;

namespace OGSim.Composition;

/// <summary>Shut a well in and measure the compartment behind it.</summary>
public sealed record WellTestCommand(
    EntityId<IReservoirCompartmentEntity> Target) : Command(Subject: null);

internal sealed class WellTestActivity(
    ActivityTerms terms,
    ContentId source,
    ContentId pressureKind,
    ContentId permeabilityKind,
    FieldControl field,
    OGSim.Subsurface.SubsurfaceState subsurface,
    ObservationDoor door) : Activity<WellTestCommand>(terms)
{
    /// <summary>Knowledge is not PP&amp;E (SDD-009 §1).</summary>
    public override bool LeavesAnAsset => false;

    /// <summary>A build-up survey is bought information (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Exploration;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(WellTestCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return (new EntityRef(EntityKind.Compartment, command.Target.Value), NoDepth);
    }

    public override string QuantityUnit => "metre";

    /// <summary>The hole this job runs in (finding 289): work on a deep well
    /// costs and takes more than the same job on a shallow one.</summary>
    public override double Quantity(WellTestCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return field.WellDepthOf(Aim(command).Target).Metres;
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(WellTestCommand command)
    {
        IReadOnlyList<EntityId<ICompletion>> wells = field.WellsOn(command.Target);

        // A test needs a wellbore to be measured in. Letting a company buy a
        // pressure survey of a compartment it has never penetrated would hand
        // over the one measurement that makes drilling worth doing, for the price
        // of not drilling.
        if (wells.Count == 0)
            return
            [
                new RejectionReason(
                    "$loc:reject.no-well-to-test",
                    "a build-up is measured in a wellbore, and the company has none " +
                    "on this compartment"),
            ];

        // ALREADY SHUT IN refuses rather than proceeds, on both sides of the
        // same reason: reopening it when the test completed would override a
        // choice the player made for some other reason, and leaving it closed
        // would give "reopen when done" nothing honest to restore.
        for (int i = 0; i < wells.Count; i++)
            if (field.IsShutIn(wells[i]))
                return
                [
                    new RejectionReason(
                        "$loc:reject.well-already-shut-in",
                        "a build-up shuts producing wells in for the test and reopens them " +
                        "when it ends; one on this compartment is shut in already"),
                ];

        return [];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        var target = new EntityId<IReservoirCompartmentEntity>(done.Target.Value);

        // NOT REOPENED HERE (this file's header explains why) — the well
        // stays shut in for the player to reopen when ready, on both a
        // success and a failure: the shut-in is the cost of ATTEMPTING the
        // test, not a bonus withheld for succeeding at reading the gauge.

        // A failed test is the honest bad outcome: the money is gone and the
        // company knows nothing new, which is what makes buying information a
        // decision rather than a formality.
        if (!done.Succeeded) return;

        door.Deliver(source, pressureKind, done.Target,
                     subsurface.TruePressureOf(target).Pascals, Provenance.WellTest);

        // kh, from the same build-up. A test sees the permeability the RESERVOIR
        // flows at over days rather than the plug a core cut out of it, and that
        // is the number the inflow model actually needs.
        door.Deliver(source, permeabilityKind, done.Target,
                     subsurface.TruePermeabilityOf(target).SquareMetres, Provenance.WellTest);
    }

    /// <summary>
    /// The one activity with its own applier — SHUTS IN every well on the
    /// target compartment the instant the test is booked, wrapping the shared
    /// applier rather than duplicating what it does (law L5).
    /// </summary>
    public override void Register(IModuleComposition composition, ActivityOrders orders)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.HandleCommand(
            new ActivityValidator<WellTestCommand>(this, orders),
            new ShutInApplier(this, orders, field));
    }

    private sealed class ShutInApplier(
        WellTestActivity activity, ActivityOrders orders, FieldControl field)
        : ICommandApplier<WellTestCommand>
    {
        private readonly ActivityApplier<WellTestCommand> _booking = new(activity, orders);

        public Applied Apply(WellTestCommand command, AuditId submission)
        {
            ArgumentNullException.ThrowIfNull(command);

            Applied applied = _booking.Apply(command, submission);

            IReadOnlyList<EntityId<ICompletion>> wells = field.WellsOn(command.Target);
            for (int i = 0; i < wells.Count; i++) field.SetChoke(wells[i], ChokeSetting.Closed);

            return applied;
        }
    }
}
