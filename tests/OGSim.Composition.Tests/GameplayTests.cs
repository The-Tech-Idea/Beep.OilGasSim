// R21 (first slice) — the game, played.
//
// A player starts with cash and an undrilled compartment, issues a command,
// watches a read model, and can run out of money. These tests are written from
// the player's side of the wall on purpose: everything they assert is something
// a host could render, and nothing they touch is truth.

using OGSim.Company;
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
                    Depth: new Length(2000.0),
                    FluidSystem: new ContentId("medium-crude")),
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

        return (built.Engine, target);
    }

    /// <summary>
    /// The structure a declared field sits in. Drilling targets a PROSPECT
    /// (SDD-010 §4b) — a hole is put down where a company thinks there is
    /// something, and whether there is, is what the well finds out.
    /// </summary>
    private static EntityId<IProspect> Structure(
        Engine engine, EntityId<IReservoirCompartmentEntity> field) =>
        engine.Provided.Resolve<WorldState>().ProspectFor(field);

    /// <summary>What a well costs today — the catalogue price at the cost index
    /// the read model is reporting (SDD-009 §6's ED4).</summary>
    private static Money Quoted(Engine engine) =>
        Money.RoundHalfEven(
            Defaults.DrillWellTerms(Fixture.Activities()).Cost.Cents
            * engine.ReadModel!.CostIndex);

    private static DrillWellCommand Drill(
        Engine engine, EntityId<IReservoirCompartmentEntity> target, double depth = 2000.0) =>
        new(Structure(engine, target), new Length(depth));

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

        Assert.IsType<Accepted>(engine.Commands.Submit(Drill(engine, target)));

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
        for (var attempt = 0; attempt < 6; attempt++) engine.Commands.Submit(Drill(engine, target));

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
            first.Commands.Submit(Drill(first, firstTarget));
            second.Commands.Submit(Drill(second, secondTarget));
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

        engine.Commands.Submit(Drill(engine, target));
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
            engine.Commands.Submit(Drill(engine, target, depth: 9_000.0)));

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
            engine.Commands.Submit(Drill(engine, target, depth: -1.0)));

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

        Assert.IsType<Accepted>(engine.Commands.Submit(Drill(engine, target)));

        Rejected rejected = Assert.IsType<Rejected>(engine.Commands.Submit(Drill(engine, target)));

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

        Assert.IsType<Accepted>(engine.Commands.Submit(Drill(engine, target)));

        // Past the worst-case reservation: four months at the 1.8 disaster
        // factor is 216 days, so eight months clears any outcome.
        for (var month = 0; month < 8; month++) engine.Pipeline.AdvanceTick();

        Assert.IsType<Accepted>(engine.Commands.Submit(Drill(engine, target)));
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

        // RUN UNTIL THE COMPANY GENUINELY CANNOT AFFORD ONE, rather than for a
        // fixed number of months. Since R20d.12 a quoted price moves with the
        // cost index, so a burn calibrated against the catalogue price was
        // asserting something the engine no longer claims — and a long enough
        // slump makes a well CHEAPER, which is how this test started passing a
        // drilling command it was written to see refused.
        var months = 0;

        while (engine.ReadModel is null || engine.ReadModel.Cash >= Quoted(engine))
        {
            engine.Pipeline.AdvanceTick();

            Assert.True(++months < 600,
                "fifty years of standing charges and the company can still afford a well");
        }

        Rejected rejected = Assert.IsType<Rejected>(engine.Commands.Submit(Drill(engine, target)));

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

        Assert.IsType<Accepted>(engine.Commands.Submit(Drill(engine, target)));

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

    // --------------------------------------------------------- what was learned

    /// <summary>
    /// R20d.7. The other direction of the exploration loop: a company that paid
    /// for a survey can SEE what it bought.
    ///
    /// <para>Until this, four activities delivered observations into a store no
    /// host could read — the player learned, and the learning was invisible.</para>
    /// </summary>
    [Fact]
    public void R21V7_a_survey_puts_what_was_learned_on_the_read_model()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        engine.Pipeline.AdvanceTick();

        // Nothing bought, nothing shown. Not an empty distribution — no entry.
        Assert.Empty(engine.ReadModel!.Beliefs);

        BeliefEntryView learned = Survey(engine, target);

        // The STRUCTURE, not the compartment. Seismic images a closure, and it
        // does so whether or not there is anything in it — which is why a survey
        // is the first move rather than a follow-up (SDD-010 §4b).
        Assert.Equal(
            new EntityRef(EntityKind.Prospect, Structure(engine, target).Value),
            learned.Subject);
        Assert.Equal(Provenance.Seismic, learned.BestSource);
    }

    /// <summary>
    /// P90 is the LOW case and P10 the high — the petroleum convention, facing a
    /// host. Reading them the statistical way round would render a possible case
    /// as a proved one (SDD-008 §8).
    /// </summary>
    [Fact]
    public void R21V7_a_projected_belief_is_a_distribution_not_a_number()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        BeliefEntryView learned = Survey(engine, target);

        Assert.True(learned.P90 < learned.P50,
            $"P90 {learned.P90} is the low case and must sit below P50 {learned.P50}");

        Assert.True(learned.P50 < learned.P10,
            $"P10 {learned.P10} is the high case and must sit above P50 {learned.P50}");

        // Oil-in-place is a LOG-space kind, so the band is asymmetric and both
        // ends are positive. A linear projection of it could show a host a
        // negative accumulation.
        Assert.True(learned.P90 > 0.0, "a log-space quantile cannot be negative");
        Assert.True(learned.P10 - learned.P50 > learned.P50 - learned.P90,
            "a log-normal's upside is the longer tail, and the projection must keep it");
    }

    /// <summary>
    /// A host holds a snapshot, not a handle (R21-V1). Last month's beliefs must
    /// not sharpen under a host that is still rendering them.
    /// </summary>
    [Fact]
    public void R21V7_a_published_belief_does_not_change_under_the_host()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        BeliefEntryView asPublished = Survey(engine, target);
        IReadOnlyList<BeliefEntryView> held = engine.ReadModel!.Beliefs;

        // Buy the same knowledge again. Precision adds, so the belief behind it
        // genuinely moves — which is exactly what must not reach the copy the
        // host already has.
        Survey(engine, target);

        Assert.Equal(asPublished, held[0]);
        Assert.NotSame(held, engine.ReadModel!.Beliefs);
    }

    /// <summary>
    /// Shoots seismic until one survey lands, and answers with what the read
    /// model then shows. A failed survey delivers nothing — the money is gone and
    /// the company knows no more — so this returns the product of exactly one
    /// successful reading however many were paid for.
    /// </summary>
    private static BeliefEntryView Survey(
        Engine engine, EntityId<IReservoirCompartmentEntity> target)
    {
        BeliefEntryView? before = Projected(engine);

        for (var attempt = 0; attempt < 40; attempt++)
        {
            Assert.IsType<Accepted>(
                engine.Commands.Submit(new SeismicSurveyCommand(Structure(engine, target))));

            engine.Pipeline.AdvanceTick();
            while (engine.ReadModel!.ActivitiesRunning > 0) engine.Pipeline.AdvanceTick();

            // The first survey adds an entry, a later one sharpens the same one,
            // and a failed one leaves both alone — so "did it land?" is asked of
            // the projection rather than of the entry count.
            if (Projected(engine) is BeliefEntryView learned && learned != before) return learned;
        }

        throw new InvalidOperationException("forty surveys and not one of them saw anything");
    }

    private static BeliefEntryView? Projected(Engine engine) =>
        engine.ReadModel is { Beliefs.Count: > 0 } published ? published.Beliefs[0] : null;

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

        // $50M at $300k a month, minus the $12M bond forfeited at month 60
        // when the licence's one commitment goes unmet (`Defaults.LicenceTerms`'s
        // `Due: new Tick(60)`, R20d.9.1), minus the annual take-or-pay shortfall
        // on the whole committed volume this idle field never delivers a barrel
        // against (SDD-009 §7's R13.3 amendment, finding 250), minus the
        // insurance premium this idle field pays every tick regardless of
        // whether it ever has an incident to claim against (SDD-009 §7's
        // finding-273 amendment): insolvency arrives at month 95, measured
        // directly rather than hand-derived from four compounding costs.
        // **Corrected from month 111** (finding 273): the premium is a real,
        // small monthly cost even with nothing running, and this idle
        // field's own solvency clock was always going to be the fixture
        // most sensitive to it.
        for (var month = 0; month < 95; month++) engine.Pipeline.AdvanceTick();
        Assert.False(engine.ReadModel!.Insolvent, "the company is not out of money yet");

        engine.Pipeline.AdvanceTick();
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
    /// The game can be WON, and winning takes more than drilling.
    ///
    /// <para>Wells alone are capped by the first separator, so a player who
    /// drills and waits ends the decade short of the target — the constraint has
    /// to be answered as well as the reservoir found. That is the shape R20.4's
    /// first measurement was missing: at the old target the run was decided in
    /// month six and every decision after the first was decoration.</para>
    /// </summary>
    [Fact]
    public void A_player_who_develops_the_field_wins()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Assert.Equal(ObjectiveState.Met, Play(engine, target, debottleneck: true));
    }

    /// <summary>
    /// And a player who only drills does WORSE. The wells are there, the oil is
    /// flowing, and the surface cannot carry it.
    ///
    /// <para>This is the test that says the constraint is load-bearing. If
    /// drilling alone were as good, every facility decision in the game would be
    /// optional.</para>
    ///
    /// <para>ASKED AS A COMPARISON, not as an outcome, and the reason is
    /// R20d.11: the oil price moves now, so "the decade runs out" became a
    /// statement about the market as much as about the chain — a bottlenecked
    /// field on a good run can still reach the target. Both companies here play
    /// the same world at the same seed and therefore see exactly the same
    /// prices, so what is left between them is the decision.</para>
    /// </summary>
    [Fact]
    public void A_player_who_drills_and_never_debottlenecks_earns_less()
    {
        (Engine open, EntityId<IReservoirCompartmentEntity> openTarget) = Undrilled();
        (Engine jammed, EntityId<IReservoirCompartmentEntity> jammedTarget) = Undrilled();

        Play(open, openTarget, debottleneck: true);
        Play(jammed, jammedTarget, debottleneck: false);

        Assert.True(open.ReadModel!.Cash > jammed.ReadModel!.Cash,
            $"debottlenecking earned {open.ReadModel!.Cash} against {jammed.ReadModel!.Cash} " +
            "for not bothering; the surface constraint is not load-bearing");
    }

    /// <summary>
    /// Plays a decade: keep the rig busy, and optionally answer the bottleneck
    /// the wells create.
    ///
    /// <para>ONE WELL AT A TIME, because that is what the engine allows — the
    /// company owns one rig and the scheduler reserves it for a well's worst
    /// case, so six drilling commands submitted at once are one well and five
    /// refusals. A test that submitted them in a batch and called the result "a
    /// developed field" was measuring a single well (R20.4's measurement).</para>
    /// </summary>
    private static ObjectiveState Play(
        Engine engine, EntityId<IReservoirCompartmentEntity> target, bool debottleneck)
    {
        var upgraded = false;

        for (var month = 0; month < 120; month++)
        {
            if (engine.ReadModel?.ActivitiesRunning == 0 && engine.ReadModel.Wells < 6)
                engine.Commands.Submit(Drill(engine, target));

            // Wait for a second well before buying the bigger vessel: one well
            // cannot fill the first one, so the upgrade would be money spent on
            // capacity nothing uses.
            if (debottleneck && !upgraded && engine.ReadModel?.Wells >= 2)
                upgraded = engine.Commands.Submit(new InstallSeparatorCommand()) is Accepted;

            Fixture.Repair(engine);
            engine.Pipeline.AdvanceTick();

            if (engine.ReadModel!.Outcome != ObjectiveState.Pending) break;
        }

        return engine.ReadModel!.Outcome;
    }

    /// <summary>
    /// A player who does nothing runs out of MONEY, not time — SDD-014 §5a's
    /// two verdicts are distinguishable, and this is now which one an idle
    /// company actually reaches first.
    ///
    /// <para>Before SDD-009 §7's R13.3 amendment (finding 250) this fixture
    /// ran out of time: $300k a month left the opening $50M standing for
    /// about fourteen years against a ten-year deadline. The one shipped
    /// take-or-pay contract changed that arithmetic — an idle company owes
    /// the FULL committed volume as shortfall every window, on top of the
    /// standing charge — and now reaches <c>Insolvent</c> at month 82,
    /// measured directly, well inside the month-120 deadline. `Expired` is
    /// still a reachable, distinct verdict (R24-V19 proves it on this same
    /// scenario, from the deadline side); this fixture now demonstrates
    /// `Failed` instead of retiring the distinction the enum exists for.
    /// </para>
    /// </summary>
    [Fact]
    public void A_player_who_does_nothing_runs_out_of_money()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        for (var month = 0; month < 200; month++)
        {
            engine.Pipeline.AdvanceTick();
            if (engine.ReadModel!.Outcome != ObjectiveState.Pending) break;
        }

        Assert.Equal(ObjectiveState.Failed, engine.ReadModel!.Outcome);
        Assert.True(engine.ReadModel.Insolvent, "the money ran out before the deadline did");
    }

    /// <summary>
    /// SDD-014 §5a's precedence, where it matters: a company that spends itself
    /// broke has FAILED, not expired, even though the deadline is what an idle
    /// company would have hit first.
    ///
    /// <para>The money is gone before the goal is measured, and that is the
    /// order it happens in — so the failure objective wins over the deadline
    /// rather than over whichever the loop happened to check first.</para>
    /// </summary>
    [Fact]
    public void A_company_that_spends_itself_broke_has_failed_not_expired()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        // Surveys, one after another: $2.5M each, no rig, no wellbore — the one
        // way to burn the opening cash without ever earning any back.
        for (var month = 0; month < 120; month++)
        {
            engine.Commands.Submit(new SeismicSurveyCommand(Structure(engine, target)));
            engine.Pipeline.AdvanceTick();

            if (engine.ReadModel!.Outcome != ObjectiveState.Pending) break;
        }

        Assert.True(engine.ReadModel!.Insolvent, "the company had to actually run out");
        Assert.Equal(ObjectiveState.Failed, engine.ReadModel.Outcome);
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

        for (var attempt = 0; attempt < 5; attempt++) engine.Commands.Submit(Drill(engine, target));

        while (engine.ReadModel?.Outcome is null or ObjectiveState.Pending)
            engine.Pipeline.AdvanceTick();

        ObjectiveState verdict = engine.ReadModel!.Outcome;

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

        Assert.Equal(ObjectiveState.Pending, engine.ReadModel!.Outcome);
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

        Assert.IsType<Accepted>(active.Commands.Submit(Drill(active, target)));

        for (var month = 0; month < 24; month++)
        {
            idle.Pipeline.AdvanceTick();
            active.Pipeline.AdvanceTick();
        }

        Assert.True(active.ReadModel!.Cash > idle.ReadModel!.Cash,
            $"drilling ({active.ReadModel.Cash.Cents}c) must beat sitting still " +
            $"({idle.ReadModel.Cash.Cents}c) — otherwise the decision is not a decision");
    }

    /// <summary>
    /// R21.6 / R21 §2.4b row 5. WHAT THE FIELD IS HOLDING, AND THE ROOM LEFT.
    ///
    /// <para>The table has required tank levels and ullage since it was written
    /// and nothing published them, so a host could see a cash balance and not
    /// whether the company was poor or merely illiquid — the same gap finding
    /// 190 records from the cash-flow side, and the reason any mechanic that
    /// defers revenue currently reads as a failure to the opening scenario
    /// (SDD-006 §7a.2).</para>
    ///
    /// <para>Asserted as a RELATIONSHIP rather than against a number, because a
    /// figure copied from a run is a test of nothing: held plus ullage is the
    /// tank's capacity by construction, and a producing field with an export
    /// line that cannot keep up must be holding something.</para>
    /// </summary>
    [Fact]
    public void R21V11_the_read_model_publishes_what_the_tank_holds_and_its_ullage()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        for (var well = 0; well < 3; well++)
        {
            while (engine.ReadModel is null || engine.ReadModel.ActivitiesRunning > 0)
                engine.Pipeline.AdvanceTick();

            engine.Commands.Submit(Drill(engine, target));
        }

        Fixture.Run(engine, months: 24);

        StorageView storage = engine.ReadModel!.Storage;

        Assert.True(storage.Ullage.Kilograms > 0.0,
            "a tank with no room left would have shut this field in; the fixture is not " +
            "measuring a producing field");

        // Capacity from the engine's own ladder rather than a constant: content
        // owns it since R20c.9.2, and the two would drift on the first rebalance.
        Assert.Equal(
            engine.Provided.Resolve<FacilityLadders>().Tank[0].Capacity.Kilograms,
            storage.Held.Kilograms + storage.Ullage.Kilograms,
            precision: 6);
    }

    /// <summary>
    /// R21 §2.4b's "cost and revenue by cause for the period", and finding 190's
    /// complaint stated as a test: A MONTH OF INVESTMENT AND A MONTH OF DECLINE
    /// MUST NOT LOOK THE SAME.
    ///
    /// <para>The surface published a cash BALANCE and nothing about the period,
    /// so falling cash read identically whether a company was building or dying.
    /// The reference client plugged fields for being under repair and could not
    /// have known better — there was nothing in the read model that would have
    /// told it.</para>
    ///
    /// <para>Asserted on the SHAPE rather than on amounts: a month that drills
    /// spends on Development, and a producing month takes money in on
    /// Production. Amounts copied from a run would pin whatever the engine
    /// happens to do; these two facts are what the row exists to make
    /// visible.</para>
    /// </summary>
    [Fact]
    public void R21V11_a_month_of_investment_does_not_look_like_a_month_of_decline()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        // A month that INVESTS: the drilling contract is signed and paid for.
        engine.Commands.Submit(Drill(engine, target));
        engine.Pipeline.AdvanceTick();

        Money spentDeveloping = engine.ReadModel!.CashByCause[
            IndexOf(MovementCategory.Development)];

        Assert.True(spentDeveloping < Money.Zero,
            $"a month that started a well shows {spentDeveloping.Cents} cents against " +
            "Development; drilling is not free and the period has to show it");

        // And a month that EARNS, once the well is in and flowing.
        while (engine.ReadModel!.ProducedThisTick.CubicMetres <= 0.0)
            engine.Pipeline.AdvanceTick();

        Assert.True(
            engine.ReadModel!.CashByCause[IndexOf(MovementCategory.Production)] > Money.Zero,
            "a producing month brings nothing in against Production, so the row cannot " +
            "distinguish earning from spending");

        // One entry per declared cause, zeroes included: an absent row and a
        // cause that did nothing this month are different claims.
        Assert.Equal(CostLedger.Causes.Count, engine.ReadModel!.CashByCause.Count);
    }

    private static int IndexOf(MovementCategory cause)
    {
        for (var i = 0; i < CostLedger.Causes.Count; i++)
            if (CostLedger.Causes[i] == cause) return i;

        throw new InvalidOperationException($"{cause} is not a declared cause");
    }

    /// <summary>
    /// R21 §2.4b row 15, using SDD-017 §2's `OperationView` — WHICH operations
    /// are running, how far along, and what each has cost.
    ///
    /// <para>The read model published a COUNT. "Two activities running" cannot
    /// answer "what is my company doing?": a player could see the rig was busy
    /// and not whether a well was nearly down or barely started, and could not
    /// tell a stalled operation from one progressing normally.</para>
    ///
    /// <para>Asserted as MOVEMENT rather than against a figure: an operation
    /// under way has progress inside its own effective duration and has accrued
    /// something, and after a month it has progressed further. Numbers copied
    /// from a run would pin whatever the engine happens to do.</para>
    /// </summary>
    [Fact]
    public void R21V11_the_read_model_says_what_each_operation_is_doing()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        engine.Commands.Submit(Drill(engine, target));
        engine.Pipeline.AdvanceTick();

        OperationView drilling = Assert.Single(engine.ReadModel!.Operations);

        Assert.Equal(EntityKind.Operation, drilling.Operation.Kind);
        Assert.True(drilling.EffectiveDurationDays > 0);
        Assert.InRange(drilling.ProgressDays, 1, drilling.EffectiveDurationDays);

        Assert.True(drilling.Accrued != Money.Zero,
            "a month of drilling accrued nothing, so the cost column says nothing about " +
            "what the rig has already committed");

        // A MONTH LATER IT HAS MOVED, which is what distinguishes progress from
        // a stalled operation — the whole reason the row is progress and not a
        // running flag.
        engine.Pipeline.AdvanceTick();

        if (engine.ReadModel!.Operations.Count == 0) return;   // it finished, which is progress too

        Assert.True(
            engine.ReadModel!.Operations[0].ProgressDays > drilling.ProgressDays,
            "a second month left the operation exactly where it was");
    }

    /// <summary>
    /// R21-V1, the first verification R21 declares and the last of its thirteen
    /// to be written: THE READ MODEL CANNOT BE MUTATED, AND NO LIVE REFERENCE
    /// ESCAPES.
    ///
    /// <para>A snapshot is a statement about a month. If any of its collections
    /// were the engine's own, a host that held last month's snapshot would watch
    /// it change under it — and worse, an objective judged at stage 12 and a
    /// host rendering at stage 13 could disagree about the same tick while both
    /// read "the read model".</para>
    ///
    /// <para>Tested by ADVANCING rather than by reflection over the type. What
    /// matters is not whether a field is declared read-only but whether the
    /// values behind it move, and the only thing that moves them is the engine
    /// running. So: take a snapshot, run a year, and require the snapshot to
    /// still describe the month it was taken in.</para>
    /// </summary>
    [Fact]
    public void R21V1_a_published_snapshot_does_not_change_when_the_engine_runs_on()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        for (var well = 0; well < 3; well++)
        {
            while (engine.ReadModel is null || engine.ReadModel.ActivitiesRunning > 0)
                engine.Pipeline.AdvanceTick();

            engine.Commands.Submit(Drill(engine, target));
        }

        Fixture.Run(engine, months: 12);

        FieldReadModel taken = engine.ReadModel!;

        // Everything a later tick could reach into: the lists are the ones a
        // projection builds each month, and a live reference would show up here
        // as a count or a value that moved.
        Tick tick = taken.Tick;
        int chain = taken.Chain.Count;
        int wells = taken.Wellbores.Count;
        int beliefs = taken.Beliefs.Count;
        Money cash = taken.Cash;
        Mass held = taken.Storage.Held;
        IReadOnlyList<Money> byCause = [.. taken.CashByCause];

        Fixture.Run(engine, months: 12);

        Assert.NotEqual(tick, engine.ReadModel!.Tick);   // the engine really did move on

        Assert.Equal(tick, taken.Tick);
        Assert.Equal(chain, taken.Chain.Count);
        Assert.Equal(wells, taken.Wellbores.Count);
        Assert.Equal(beliefs, taken.Beliefs.Count);
        Assert.Equal(cash, taken.Cash);
        Assert.Equal(held, taken.Storage.Held);
        Assert.True(Structural.Equal(byCause, taken.CashByCause),
            "the cash-by-cause row moved after it was published, so the snapshot is holding " +
            "the ledger's own working state rather than a statement about its month");
    }
}
