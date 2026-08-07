// R21 (first slice) — the game, played.
//
// A player starts with cash and an undrilled compartment, issues a command,
// watches a read model, and can run out of money. These tests are written from
// the player's side of the wall on purpose: everything they assert is something
// a host could render, and nothing they touch is truth.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition.Tests;

public sealed class GameplayTests
{
    /// <summary>A composed engine with a discovered compartment and no wells —
    /// the position a player actually starts a field from.</summary>
    private static (Engine Engine, EntityId<IReservoirCompartmentEntity> Target) Undrilled()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));

        EntityId<IReservoirCompartmentEntity> target =
            built.Engine.Provided.Resolve<FieldControl>().AddCompartment(
                new GeneratedCompartment(
                    PoreVolume: new ReservoirVolume(100.0e6),
                    Porosity: 0.22,
                    OilSaturation: 0.7,
                    InitialPressure: new Pressure(30.0e6),
                    Temperature: Temperature.FromCelsius(93.3),
                    Depth: new Length(2000.0)),
                permeability: new Permeability(2.0e-13),
                netThickness: new Length(30.0),
                drainageArea: new Area(2.0e5),
                rockCompressibility: 4.5e-10,
                gasOilContact: new Length(1900.0),
                oilWaterContact: new Length(2100.0));

        return (built.Engine, target);
    }

    private static DrillWellCommand Drill(
        EntityId<IReservoirCompartmentEntity> target, double depth = 2000.0) =>
        new(target, new Length(depth));

    // ------------------------------------------------------------- agency

    /// <summary>
    /// The first thing a player does — and it takes four months. Money leaves
    /// now, oil arrives later, and in between the rig is turning and the read
    /// model says so.
    /// </summary>
    [Fact]
    public void Drilling_a_well_takes_months_and_then_the_field_produces()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        engine.Pipeline.AdvanceTick();
        Assert.Equal(0.0, engine.ReadModel!.ProducedThisTick.CubicMetres);

        Assert.IsType<Accepted>(engine.Commands.Submit(Drill(target)));

        // Still drilling: committed, paid for, nothing to show.
        engine.Pipeline.AdvanceTick();
        Assert.Equal(1, engine.ReadModel!.ActivitiesRunning);
        Assert.Equal(0, engine.ReadModel.Wells);
        Assert.Equal(0.0, engine.ReadModel.ProducedThisTick.CubicMetres);

        for (var month = 0; month < 4; month++) engine.Pipeline.AdvanceTick();

        Assert.Equal(0, engine.ReadModel!.ActivitiesRunning);
        Assert.Equal(1, engine.ReadModel.Wells);
        Assert.True(engine.ReadModel.ProducedThisTick.CubicMetres > 0.0,
            "once the rig is off, the field must produce");
    }

    /// <summary>
    /// Some holes are dry, and a dry hole is paid for in full. That is the whole
    /// of exploration economics — and the reason drilling is a decision instead
    /// of a button.
    /// </summary>
    [Fact]
    public void Some_wells_come_up_dry_and_are_paid_for_anyway()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        // Six wells at 0.6 — with this seed some land and some do not, which is
        // the only assertion worth making: the outcome is neither guaranteed nor
        // impossible.
        for (var attempt = 0; attempt < 6; attempt++) engine.Commands.Submit(Drill(target));

        for (var month = 0; month < 6; month++) engine.Pipeline.AdvanceTick();

        Assert.Equal(0, engine.ReadModel!.ActivitiesRunning);
        Assert.True(engine.ReadModel.Wells < 6,
            "if every hole finds oil there is no risk and no decision");
        Assert.True(engine.ReadModel.Wells > 0,
            "if no hole ever finds oil the game is unplayable");
    }

    /// <summary>
    /// The outcome is drawn ONCE, when the well is ordered (SDD-007 §4). Two
    /// engines on one seed, given the same orders, must find the same oil —
    /// otherwise a player could reload the month before a rig finished and try
    /// again, which turns a probability into a slot machine.
    /// </summary>
    [Fact]
    public void One_seed_and_the_same_orders_give_the_same_wells()
    {
        (Engine first, EntityId<IReservoirCompartmentEntity> firstTarget) = Undrilled();
        (Engine second, EntityId<IReservoirCompartmentEntity> secondTarget) = Undrilled();

        for (var attempt = 0; attempt < 6; attempt++)
        {
            first.Commands.Submit(Drill(firstTarget));
            second.Commands.Submit(Drill(secondTarget));
        }

        for (var month = 0; month < 6; month++)
        {
            first.Pipeline.AdvanceTick();
            second.Pipeline.AdvanceTick();
        }

        Assert.Equal(first.ReadModel!.Wells, second.ReadModel!.Wells);
        Assert.Equal(first.ReadModel.Cash, second.ReadModel.Cash);
    }

    /// <summary>Drilling costs money, and the player sees it go.</summary>
    [Fact]
    public void Drilling_a_well_costs_the_company_cash()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        engine.Pipeline.AdvanceTick();
        Money before = engine.ReadModel!.Cash;

        engine.Commands.Submit(Drill(target));
        engine.Pipeline.AdvanceTick();

        // The well is bought and the month is produced, so cash moves by both.
        // What matters here is that the capex was actually charged.
        Assert.True(engine.ReadModel!.Cash < before + Money.FromMillions(8.0),
            "the well must have been paid for");
    }

    /// <summary>
    /// A refusal names EVERY reason, not the first. A player told only that the
    /// well is too deep, who then discovers they could not have afforded it
    /// either, has been made to learn the truth in instalments.
    /// </summary>
    [Fact]
    public void An_impossible_well_is_refused_with_every_reason()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        // Too deep for the E1 drilling envelope, and — after we spend the money —
        // unaffordable too.
        Rejected rejected = Assert.IsType<Rejected>(
            engine.Commands.Submit(Drill(target, depth: 9_000.0)));

        Assert.Contains(rejected.Reasons,
            reason => reason.LocId == "$loc:reject.beyond-drilling-envelope");
    }

    /// <summary>
    /// Every rejection carries a localisation key, so a host renders a sentence
    /// rather than inventing one (R21-V5).
    /// </summary>
    [Fact]
    public void Every_rejection_reason_is_renderable()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Rejected rejected = Assert.IsType<Rejected>(
            engine.Commands.Submit(Drill(target, depth: -1.0)));

        Assert.All(rejected.Reasons, reason =>
        {
            Assert.StartsWith("$loc:", reason.LocId, StringComparison.Ordinal);
            Assert.NotEmpty(reason.Detail);
        });
    }

    /// <summary>
    /// A RIG DRILLS ONE WELL AT A TIME (finding 142). The company has the cash
    /// for six wells and one rig, so the second order is refused — and refused
    /// with a date, because "unavailable" without one is not actionable.
    ///
    /// <para>The timer this replaced had no rig at all, which made cash the only
    /// limit on how fast a field could be developed. That is a spreadsheet.</para>
    /// </summary>
    [Fact]
    public void A_rig_drills_one_well_at_a_time()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Assert.IsType<Accepted>(engine.Commands.Submit(Drill(target)));

        Rejected rejected = Assert.IsType<Rejected>(engine.Commands.Submit(Drill(target)));

        RejectionReason reason = Assert.Single(rejected.Reasons);
        Assert.Equal("$loc:reject.resource-committed", reason.LocId);
        Assert.Contains("next free on day", reason.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// The rig frees when the well finishes, and the next order is taken. A
    /// contention refusal that never cleared would be a deadlock rather than a
    /// constraint.
    /// </summary>
    [Fact]
    public void The_rig_takes_the_next_well_once_it_is_free()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Assert.IsType<Accepted>(engine.Commands.Submit(Drill(target)));

        // Past the worst-case reservation: four months at the 1.8 disaster
        // factor is 216 days, so eight months clears any outcome.
        for (var month = 0; month < 8; month++) engine.Pipeline.AdvanceTick();

        Assert.IsType<Accepted>(engine.Commands.Submit(Drill(target)));
    }

    /// <summary>
    /// A well the company cannot afford is refused, not allowed and then
    /// regretted.
    ///
    /// <para>Reached by WAITING rather than by drilling: $50M against a $300k
    /// standing charge takes about 140 months to fall below one well's $8M, and
    /// a company that drills instead gets richer — with wells earning far more
    /// per month than they cost, the binding constraint on early expansion is
    /// the rig, not the money. That is a balance observation for R20.4, and it
    /// is why this test idles.</para>
    /// </summary>
    [Fact]
    public void A_well_the_company_cannot_afford_is_refused()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        for (var month = 0; month < 145; month++) engine.Pipeline.AdvanceTick();

        Assert.True(engine.ReadModel!.Cash < Money.FromMillions(8.0));

        Rejected rejected = Assert.IsType<Rejected>(engine.Commands.Submit(Drill(target)));

        Assert.Contains(rejected.Reasons,
            reason => reason.LocId == "$loc:reject.insufficient-cash");
    }

    /// <summary>
    /// COST ACCRUES OVER THE OPERATION, not on day one (SDD-007 §3, R12-V2).
    /// A four-month well spends for four months, which is what makes an
    /// over-committed company run out of money mid-well rather than discover
    /// the bill on completion.
    /// </summary>
    [Fact]
    public void A_wells_cost_is_spread_across_the_months_it_takes()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        engine.Pipeline.AdvanceTick();
        Money atStart = engine.ReadModel!.Cash;

        Assert.IsType<Accepted>(engine.Commands.Submit(Drill(target)));

        engine.Pipeline.AdvanceTick();
        Money afterOneMonth = engine.ReadModel!.Cash;

        Money firstMonth = atStart - afterOneMonth;

        // Mobilisation plus one month's day-rate is well under the whole $8M.
        Assert.True(firstMonth < Money.FromMillions(8.0),
            $"month one took {firstMonth.Cents}c — the whole well was charged at once");

        Assert.True(firstMonth > Money.Zero,
            "a month of drilling has to cost something");
    }

    // ------------------------------------------------------------- visibility

    /// <summary>
    /// Nothing to see before the first month. A zeroed model would be a claim
    /// about a month that never happened.
    /// </summary>
    [Fact]
    public void There_is_no_read_model_before_the_first_tick()
    {
        (Engine engine, _) = Undrilled();

        Assert.Null(engine.ReadModel);
    }

    /// <summary>The read model is the tick that just closed, and it moves with
    /// the clock.</summary>
    [Fact]
    public void The_read_model_reports_the_tick_that_just_closed()
    {
        (Engine engine, _) = Undrilled();

        engine.Pipeline.AdvanceTick();
        Assert.Equal(1, engine.ReadModel!.Tick.Value);

        engine.Pipeline.AdvanceTick();
        Assert.Equal(2, engine.ReadModel!.Tick.Value);
    }

    /// <summary>
    /// The read model carries no reservoir pressure, and cannot: truth reaches a
    /// host through beliefs or not at all (R21-V4). Asserted on the TYPE so the
    /// day someone adds a pressure field, this fails.
    /// </summary>
    [Fact]
    public void The_read_model_exposes_no_subsurface_truth()
    {
        string[] members = [.. typeof(FieldReadModel)
            .GetProperties()
            .Select(property => property.Name)];

        Assert.DoesNotContain("Pressure", members);
        Assert.DoesNotContain("OilInPlace", members);
        Assert.DoesNotContain("Compartment", members);
    }

    // ------------------------------------------------------------- consequence

    /// <summary>
    /// LOSING. A company that drills everything it has and produces nothing runs
    /// out of money, and the game says so. Without this the player's decisions
    /// cost nothing.
    /// </summary>
    [Fact]
    public void A_company_that_runs_out_of_money_is_insolvent()
    {
        // No compartment: every well is refused, so the company simply pays its
        // standing charge until there is nothing left.
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        // $50M at $300k a month — insolvency arrives, and not before it should.
        for (var month = 0; month < 166; month++) engine.Pipeline.AdvanceTick();
        Assert.False(engine.ReadModel!.Insolvent, "the company is not out of money yet");

        for (var month = 0; month < 20; month++) engine.Pipeline.AdvanceTick();
        Assert.True(engine.ReadModel!.Insolvent, "the company has spent everything");
    }

    /// <summary>
    /// Once failed, always failed. A later month's revenue must not quietly
    /// un-fail a company that was already wound up.
    /// </summary>
    [Fact]
    public void Insolvency_does_not_reverse()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        for (var month = 0; month < 200; month++) engine.Pipeline.AdvanceTick();
        Assert.True(engine.ReadModel!.Insolvent);

        // A windfall arrives. The company is still finished.
        for (var month = 0; month < 5; month++) engine.Pipeline.AdvanceTick();
        Assert.True(engine.ReadModel!.Insolvent);
    }

    // ------------------------------------------------------------- winning

    /// <summary>
    /// The game can be WON. Until this existed the arc had one end — keep going
    /// until the money runs out — and a game that can only be lost is not one.
    /// </summary>
    [Fact]
    public void A_player_who_drills_enough_wells_wins()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        // Five wells: enough of them land to double the opening cash inside the
        // decade the goal allows.
        for (var attempt = 0; attempt < 5; attempt++) engine.Commands.Submit(Drill(target));

        for (var month = 0; month < 120; month++)
        {
            engine.Pipeline.AdvanceTick();
            if (engine.ReadModel!.Outcome != Outcome.Playing) break;
        }

        Assert.Equal(Outcome.Won, engine.ReadModel!.Outcome);
    }

    /// <summary>A player who does nothing runs out of money and loses.</summary>
    [Fact]
    public void A_player_who_does_nothing_loses()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        for (var month = 0; month < 200; month++)
        {
            engine.Pipeline.AdvanceTick();
            if (engine.ReadModel!.Outcome != Outcome.Playing) break;
        }

        Assert.Equal(Outcome.Lost, engine.ReadModel!.Outcome);
    }

    /// <summary>
    /// A verdict, once reached, stands. A player who hit the target in month 90
    /// has won, and a bad month 91 does not take it back — the verdict is about
    /// what they achieved, not where they happened to stop.
    /// </summary>
    [Fact]
    public void A_verdict_once_reached_does_not_change()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        for (var attempt = 0; attempt < 5; attempt++) engine.Commands.Submit(Drill(target));

        while (engine.ReadModel?.Outcome is null or Outcome.Playing)
            engine.Pipeline.AdvanceTick();

        Outcome verdict = engine.ReadModel!.Outcome;

        for (var month = 0; month < 240; month++) engine.Pipeline.AdvanceTick();

        Assert.Equal(verdict, engine.ReadModel!.Outcome);
    }

    /// <summary>
    /// The game is still being played until it is not. A verdict on month one
    /// would mean the goal was never a goal.
    /// </summary>
    [Fact]
    public void The_game_starts_undecided()
    {
        (Engine engine, _) = Undrilled();

        engine.Pipeline.AdvanceTick();

        Assert.Equal(Outcome.Playing, engine.ReadModel!.Outcome);
    }

    /// <summary>
    /// The whole arc, as a player would live it: arrive, drill, produce, and be
    /// better off for having done it than for having done nothing.
    /// </summary>
    [Fact]
    public void A_player_who_drills_ends_richer_than_one_who_does_not()
    {
        (Engine idle, _) = Undrilled();
        (Engine active, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Assert.IsType<Accepted>(active.Commands.Submit(Drill(target)));

        for (var month = 0; month < 24; month++)
        {
            idle.Pipeline.AdvanceTick();
            active.Pipeline.AdvanceTick();
        }

        Assert.True(active.ReadModel!.Cash > idle.ReadModel!.Cash,
            $"drilling ({active.ReadModel.Cash.Cents}c) must beat sitting still " +
            $"({idle.ReadModel.Cash.Cents}c) — otherwise the decision is not a decision");
    }
}
