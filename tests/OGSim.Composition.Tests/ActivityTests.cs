// R12b — the activity catalogue, played (SDD-007 §5, SDD-008 §3).
//
// Five activities on one engine, each its own class. What is asserted here is
// what a player would notice: what they are allowed to order, what it costs them,
// and what they know afterwards that they did not know before.
//
// Nothing in this file touches truth. Every measurement is read back through
// IBeliefStore, which is the only side of the wall a host can see.

using OGSim.Company;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition.Tests;

public sealed class ActivityTests
{
    private const double TruePressurePascals = 30.0e6;

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
                    InitialPressure: new Pressure(TruePressurePascals),
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

    /// <summary>An engine with one well already producing, so the downhole
    /// measurements have something to be run in.</summary>
    private static (Engine Engine, EntityId<IReservoirCompartmentEntity> Target) Drilled()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        // Keep drilling until one lands. Every hole is paid for either way, which
        // is the point of the loop and not an inefficiency in the test.
        while (engine.ReadModel is null || engine.ReadModel.Wells == 0)
        {
            if (engine.ReadModel?.ActivitiesRunning == 0)
                engine.Commands.Submit(new DrillWellCommand(Structure(engine, target), new Length(2000.0)));

            engine.Pipeline.AdvanceTick();
        }

        return (engine, target);
    }

    private static Belief? BeliefAbout(
        Engine engine, EntityId<IReservoirCompartmentEntity> target, string kind) =>
        engine.Provided.Resolve<IBeliefStore>()
            .Get(new EntityRef(EntityKind.Compartment, target.Value), new ContentId(kind));

    /// <summary>
    /// What a survey teaches, which is about the STRUCTURE rather than the
    /// compartment: seismic images a closure, and it does so whether or not
    /// anything is in it (SDD-010 §4b).
    /// </summary>
    private static Belief? BeliefAboutStructure(Engine engine, EntityId<IProspect> prospect) =>
        engine.Provided.Resolve<IBeliefStore>()
            .Get(new EntityRef(EntityKind.Prospect, prospect.Value),
                 new ContentId("structure-capacity"));

    private static Money CapitalSpentBy(Engine engine) =>
        engine.Provided.Resolve<OGSim.Company.CompanyState>().Ledger.BalanceOf(Account.Capex_PPE);

    private static Money ExpensedBy(Engine engine) =>
        engine.Provided.Resolve<OGSim.Company.CompanyState>().Ledger.BalanceOf(Account.Opex);

    private static void RunToQuiet(Engine engine)
    {
        engine.Pipeline.AdvanceTick();

        while (engine.ReadModel!.ActivitiesRunning > 0) engine.Pipeline.AdvanceTick();
    }

    /// <summary>
    /// Orders a measurement until one of them works, and answers with what was
    /// learned.
    ///
    /// <para>A FAILED MEASUREMENT DELIVERS NOTHING — the money is gone and the
    /// company knows no more than before — so the belief this returns is the
    /// product of exactly one successful reading however many were paid for. That
    /// is what makes it fair to compare one source against another.</para>
    /// </summary>
    private static Belief Learn<TCommand>(
        Engine engine,
        EntityId<IReservoirCompartmentEntity> target,
        string kind,
        Func<TCommand> order) where TCommand : Command
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            Assert.IsType<Accepted>(engine.Commands.Submit(order()));
            RunToQuiet(engine);

            if (BeliefAbout(engine, target, kind) is Belief learned) return learned;
        }

        throw new InvalidOperationException(
            $"forty {typeof(TCommand).Name}s and not one of them read {kind}");
    }

    // ------------------------------------------------------- the opening move

    /// <summary>
    /// A SURVEY IS THE ONLY THING A COMPANY WITH NOTHING DRILLED CAN ORDER, and
    /// that is the whole reason the exploration game has a first move. Every
    /// downhole measurement needs a hole; seismic is shot from the surface.
    /// </summary>
    [Fact]
    public void With_nothing_drilled_only_a_survey_can_be_ordered()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Assert.IsType<Accepted>(engine.Commands.Submit(new SeismicSurveyCommand(Structure(engine, target))));

        Assert.IsType<Rejected>(engine.Commands.Submit(new WellTestCommand(target)));
        Assert.IsType<Rejected>(engine.Commands.Submit(new WirelineLogCommand(target)));
        Assert.IsType<Rejected>(engine.Commands.Submit(new CutCoreCommand(target)));
    }

    /// <summary>
    /// And it teaches the company how big the accumulation might be — the one
    /// thing no downhole measurement can reach, because a wellbore sees one point
    /// and says nothing about how far the oil extends.
    /// </summary>
    [Fact]
    public void A_survey_is_how_a_company_learns_the_size_of_an_accumulation()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Assert.Null(BeliefAboutStructure(engine, Structure(engine, target)));

        engine.Commands.Submit(new SeismicSurveyCommand(Structure(engine, target)));
        RunToQuiet(engine);

        Belief? learned = BeliefAboutStructure(engine, Structure(engine, target));

        Assert.NotNull(learned);
        Assert.Equal(Provenance.Seismic, learned.Value.BestSource);
        Assert.Equal(BeliefSpace.Log, learned.Value.Space);

        // A volume, and a positive one. Log space is what guarantees it: sampled
        // additively at this sigma the survey could have returned a negative
        // accumulation, which is a belief nobody could act on.
        Assert.True(OGSim.Information.Quantiles.P50(learned.Value) > 0.0);
    }

    // ------------------------------------------------------------- staleness

    /// <summary>
    /// R14-V15 (SDD-008 §2d). A BELIEF GOES STALE ON WHAT THE FIELD PRODUCED.
    ///
    /// <para><c>BeliefStore.Age</c> implemented §2's staleness from the day R14
    /// landed, had a unit test, and was called by NOTHING — so a pressure
    /// reading from month four was exactly as trustworthy in month four
    /// hundred, and the reason a player re-tests a well was missing
    /// (finding 200). Not an L3 breach, which is why nothing caught it: the
    /// member had behaviour and coverage and lacked only a caller.</para>
    ///
    /// <para>Porosity is asserted alongside because it is the half that must NOT
    /// move: rock does not become less certain by being ignored, and a player
    /// must never have to re-log a well to learn what its core already told
    /// them. A test that only watched sigma grow would pass just as well against
    /// a drift applied to everything.</para>
    /// </summary>
    [Fact]
    public void R14V15_a_producing_fields_pressure_belief_widens_and_its_rock_does_not()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Drilled();

        Belief pressure = Learn(
            engine, target, "reservoir-pressure", () => new WellTestCommand(target));

        Belief porosity = Learn(
            engine, target, "porosity", () => new WirelineLogCommand(target));

        // BACK ON PRODUCTION — a build-up test shuts every well on the
        // compartment in and leaves them shut (SDD-007 §5's R12b.18
        // amendment), which the player reverses through the same door any
        // other shut-in does.
        IReadOnlyList<WellStatusView> tested = engine.ReadModel!.Wellbores;

        for (var i = 0; i < tested.Count; i++)
            Assert.IsType<Accepted>(engine.Commands.Submit(
                new SetWellChokeCommand(new EntityId<ICompletion>(tested[i].Well.Value), Open: true)));

        // A YEAR OF PRODUCTION. The field is drilled and flowing, so every tick
        // withdraws from this compartment and every tick ages what is believed
        // about its pressure.
        Fixture.Run(engine, months: 12);

        Belief aged = Assert.NotNull(BeliefAbout(engine, target, "reservoir-pressure"));

        Assert.True(aged.Sigma > pressure.Sigma,
            $"a year of production left the pressure belief at sigma {aged.Sigma}, no wider " +
            $"than the {pressure.Sigma} it was measured at — nothing is ageing it");

        // AND THE ROCK IS UNTOUCHED, to the last bit.
        Assert.Equal(
            porosity.Sigma,
            Assert.NotNull(BeliefAbout(engine, target, "porosity")).Sigma);
    }

    /// <summary>
    /// R14-V16 (SDD-008 §2d.2, closing S008-2). AND A SHUT-IN FIELD'S BELIEFS
    /// HOLD — drift is charged to PRODUCTION, not to the calendar.
    ///
    /// <para>A pressure belief goes stale because the pressure MOVED, and what
    /// moves it is withdrawal. Charging drift to the clock instead would make
    /// waiting a source of uncertainty, so the optimal play would become
    /// re-testing on a timer rather than when something changed — which is the
    /// opposite of what an information game should reward.</para>
    ///
    /// <para>The same twelve months as V15, with the wells shut. This is the
    /// decision stated as a test rather than as a preference.</para>
    ///
    /// <para>Nothing shuts the wells in explicitly any more (SDD-007 §5's
    /// R12b.18 amendment): a build-up test already leaves every well on the
    /// compartment shut when it completes, which is exactly the state this
    /// test wants to hold for a year — asserted rather than re-created.</para>
    /// </summary>
    [Fact]
    public void R14V16_a_shut_in_fields_pressure_belief_does_not_go_stale()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Drilled();

        Belief measured = Learn(
            engine, target, "reservoir-pressure", () => new WellTestCommand(target));

        // SHUT EVERYTHING — already true. A compartment nothing is drawing
        // from gives up no volume, so it never reaches the withdrawals
        // stage 11 reads.
        IReadOnlyList<WellStatusView> wells = engine.ReadModel!.Wellbores;

        Assert.True(wells.Count > 0, "the fixture drilled no well to hold shut");

        for (var i = 0; i < wells.Count; i++)
            Assert.Equal(WellStatus.ShutIn, wells[i].Status);

        Fixture.Run(engine, months: 12);

        Assert.Equal(
            measured.Sigma,
            Assert.NotNull(BeliefAbout(engine, target, "reservoir-pressure")).Sigma);
    }

    // ---------------------------------------------- a build-up really shuts in

    /// <summary>
    /// SDD-007 §5's R12b.18 amendment. This file's own header claimed "the
    /// well is shut in for the build-up, so the test costs the month's oil"
    /// while nothing in the class touched a choke (finding 245) — proved here
    /// against the read model a player actually sees, not a reconstructed
    /// double.
    /// </summary>
    [Fact]
    public void R12bV18_ordering_a_well_test_shuts_in_every_well_on_the_compartment()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Drilled();

        IReadOnlyList<WellStatusView> before = engine.ReadModel!.Wellbores;
        Assert.True(before.Count > 0, "the fixture drilled no well");

        foreach (WellStatusView well in before)
            Assert.Equal(WellStatus.Producing, well.Status);

        Assert.IsType<Accepted>(engine.Commands.Submit(new WellTestCommand(target)));

        // SHUT THE INSTANT IT IS BOOKED — before any tick has run, which is
        // the only way the shut-in can reach the SAME tick's flow solve
        // (SolveFlow is stage 5; a test with a one-tick duration completes
        // inside stage 3, Operations, and a shut-in applied only there would
        // never be visible to a solve that already ran this tick). Read off
        // FieldControl directly rather than the read model, which is a
        // snapshot rebuilt on tick advance and would not have moved yet.
        FieldControl field = engine.Provided.Resolve<FieldControl>();

        foreach (WellStatusView well in before)
            Assert.True(field.IsShutIn(new EntityId<ICompletion>(well.Well.Value)),
                $"well {well.DisplayId} is still open the instant after a test was booked");
    }

    /// <summary>
    /// AND IT COSTS THE MONTH'S OIL FOR REAL — the economic claim the header
    /// makes, checked against actual production rather than against the
    /// choke setting alone. A shut-in that never reached the solve would
    /// leave this test green while the company's revenue never moved.
    ///
    /// <para>Read off the FIELD'S total, not <see cref="WellStatusView.ProducedThisTick"/>
    /// — that member is documented as always zero: a per-well split of the
    /// solve does not exist, so the honest answer lives on the field beside
    /// it. This fixture has exactly one well, so the field's total IS this
    /// well's production.</para>
    /// </summary>
    [Fact]
    public void R12bV18b_a_shut_in_well_produces_nothing_the_tick_it_is_tested()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Drilled();

        // A YEAR OF NORMAL MONTHS FIRST, so there is a non-zero baseline to
        // lose — the well the drilling loop stops on has not necessarily
        // reached the network yet on the very tick it completes. Two years,
        // not one: an untrained crew (SDD-007 §4.1's finding-265 amendment)
        // runs every scheduled operation 15% longer, including the repairs
        // Fixture.Run orders for whatever equipment fails first, so a year
        // that used to be comfortable margin no longer always is.
        Fixture.Run(engine, months: 24);

        Assert.True(engine.ReadModel!.ProducedThisTick.CubicMetres > 0.0,
            "the fixture field produced nothing even before the test — no baseline to lose");

        Assert.IsType<Accepted>(engine.Commands.Submit(new WellTestCommand(target)));
        engine.Pipeline.AdvanceTick();

        WellStatusView tested = Assert.Single(engine.ReadModel!.Wellbores);
        Assert.Equal(WellStatus.ShutIn, tested.Status);
        Assert.Equal(0.0, engine.ReadModel!.ProducedThisTick.CubicMetres);
    }

    /// <summary>A compartment with nothing to test is refused naming why,
    /// scoped to the TARGET compartment rather than "the company has a well
    /// somewhere" — the looser check this replaced would have let a test be
    /// ordered against a compartment nothing has ever penetrated.</summary>
    [Fact]
    public void R12bV18c_a_well_test_refuses_a_compartment_with_no_well_on_it()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> undrilled) = Undrilled();

        Assert.IsType<Rejected>(engine.Commands.Submit(new WellTestCommand(undrilled)));
    }

    /// <summary>
    /// AND IT REFUSES AGAINST AN ALREADY SHUT-IN WELL — reopening it when the
    /// test ended would override whatever reason the player had for closing
    /// it, and leaving it closed would give "shut in for the test" nothing it
    /// actually changed.
    /// </summary>
    [Fact]
    public void R12bV18d_a_well_test_refuses_when_a_well_on_the_compartment_is_already_shut_in()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Drilled();

        WellStatusView well = Assert.Single(engine.ReadModel!.Wellbores);

        Assert.IsType<Accepted>(engine.Commands.Submit(
            new SetWellChokeCommand(new EntityId<ICompletion>(well.Well.Value), Open: false)));

        Assert.IsType<Rejected>(engine.Commands.Submit(new WellTestCommand(target)));
    }

    /// <summary>
    /// AND IT IS DATED WHEN IT WAS LEARNED — SDD-008 §2.1's update rule ends
    /// "AsOf = now", and until R20d.12.11 the store was built with a literal
    /// epoch, so every belief in every game claimed January 1965 (finding 199).
    ///
    /// <para>The test above stops one field short of this, which is exactly how
    /// it survived: it asserts the provenance and the space of a freshly learned
    /// belief and not its date. A CONSTANT IS PERFECTLY SELF-CONSISTENT — every
    /// belief agreed with every other, the projection was populated and the save
    /// round-tripped it exactly — so nothing short of comparing it to the clock
    /// could tell.</para>
    ///
    /// <para>The survey is ordered after two years have passed, because a check
    /// made in month one would pass against the epoch as well.</para>
    /// </summary>
    [Fact]
    public void A_belief_is_dated_when_it_was_learned_and_not_at_the_epoch()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        for (var month = 0; month < 24; month++) engine.Pipeline.AdvanceTick();

        engine.Commands.Submit(new SeismicSurveyCommand(Structure(engine, target)));
        RunToQuiet(engine);

        Belief learned = BeliefAboutStructure(engine, Structure(engine, target))!.Value;

        Assert.Equal(engine.ReadModel!.Date, learned.AsOf);
        Assert.NotEqual(Fixture.Settings().Epoch, learned.AsOf);
    }

    /// <summary>
    /// The rig is not involved, so a survey runs while a well is being drilled.
    /// A company that had to choose between exploring and developing would only
    /// ever do one of them.
    /// </summary>
    [Fact]
    public void A_survey_needs_no_rig_and_runs_alongside_drilling()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Assert.IsType<Accepted>(
            engine.Commands.Submit(new DrillWellCommand(Structure(engine, target), new Length(2000.0))));

        Assert.IsType<Accepted>(engine.Commands.Submit(new SeismicSurveyCommand(Structure(engine, target))));

        engine.Pipeline.AdvanceTick();

        Assert.Equal(2, engine.ReadModel!.ActivitiesRunning);
    }

    // --------------------------------------------------- downhole measurement

    /// <summary>
    /// A build-up is the only source that can see pressure, and pressure is the
    /// thing that moves — which is what makes the test worth shutting a well in
    /// for rather than a formality.
    /// </summary>
    [Fact]
    public void A_well_test_is_how_a_company_learns_the_pressure()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Drilled();

        Assert.Null(BeliefAbout(engine, target, "reservoir-pressure"));

        Belief learned = Learn(
            engine, target, "reservoir-pressure", () => new WellTestCommand(target));

        Assert.Equal(Provenance.WellTest, learned.BestSource);

        // Near the truth the test planted, and NOT equal to it. A source that
        // returned truth would make measurement free and belief pointless.
        Assert.NotEqual(TruePressurePascals, learned.Mu);
        Assert.True(double.Abs(learned.Mu - TruePressurePascals) < 1.0e6,
            "a build-up that misses by more than a megapascal is not a build-up");
    }

    /// <summary>
    /// A LOG CANNOT SEE THE SIZE OF AN ACCUMULATION AT ALL — it returns nothing
    /// rather than a wide guess. Absence and uncertainty are different states,
    /// and only the first should leave a prospect unbookable (SDD-008 §3).
    /// </summary>
    [Fact]
    public void A_log_reads_the_rock_and_says_nothing_about_how_far_it_extends()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Drilled();

        Learn(engine, target, "porosity", () => new WirelineLogCommand(target));

        Assert.NotNull(BeliefAbout(engine, target, "permeability"));
        Assert.Null(BeliefAboutStructure(engine, Structure(engine, target)));
    }

    /// <summary>
    /// A core is several times the price of a log for the same two properties.
    /// The only thing the player buys is sharpness — so if the core is not
    /// sharper, the decision between them is not a decision.
    /// </summary>
    [Fact]
    public void A_core_is_sharper_than_a_log_on_the_same_two_properties()
    {
        (Engine logged, EntityId<IReservoirCompartmentEntity> loggedTarget) = Drilled();
        (Engine cored, EntityId<IReservoirCompartmentEntity> coredTarget) = Drilled();

        foreach (string kind in new[] { "porosity", "permeability" })
        {
            Belief fromLog = Learn(
                logged, loggedTarget, kind, () => new WirelineLogCommand(loggedTarget));

            Belief fromCore = Learn(
                cored, coredTarget, kind, () => new CutCoreCommand(coredTarget));

            Assert.True(fromCore.Sigma < fromLog.Sigma,
                $"a core must beat a log on {kind}, or nobody would ever cut one");
        }
    }

    /// <summary>
    /// A build-up beats even a core on permeability, because it measures what the
    /// reservoir flows at over days rather than what one plug of rock does. That
    /// ordering is the reason all three measurements exist rather than one.
    /// </summary>
    [Fact]
    public void A_build_up_beats_a_core_on_permeability()
    {
        (Engine tested, EntityId<IReservoirCompartmentEntity> testedTarget) = Drilled();
        (Engine cored, EntityId<IReservoirCompartmentEntity> coredTarget) = Drilled();

        Belief fromBuildUp = Learn(
            tested, testedTarget, "permeability", () => new WellTestCommand(testedTarget));

        Belief fromCore = Learn(
            cored, coredTarget, "permeability", () => new CutCoreCommand(coredTarget));

        Assert.True(fromBuildUp.Sigma < fromCore.Sigma,
            "a build-up measures what the reservoir flows at; a core measures one plug");
    }

    // ------------------------------------------------------------- refusals

    /// <summary>
    /// ONE MEASUREMENT AT A TIME PER TARGET, and it is load-bearing rather than
    /// tidy: sigma combines conjugately, so a player allowed to queue twenty
    /// surveys of one compartment would average the noise away for the price of
    /// twenty cheap surveys and would never need to drill to learn anything.
    /// </summary>
    [Fact]
    public void The_same_measurement_cannot_be_bought_twice_at_once()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Assert.IsType<Accepted>(engine.Commands.Submit(new SeismicSurveyCommand(Structure(engine, target))));

        Rejected second = Assert.IsType<Rejected>(
            engine.Commands.Submit(new SeismicSurveyCommand(Structure(engine, target))));

        Assert.Contains(second.Reasons, r => r.LocId == "$loc:reject.already-under-way");
    }

    /// <summary>
    /// Every refusal names a reason a host can render, whichever activity was
    /// refused (R21-V5). A new activity that returned a bare string would be a
    /// sentence the host had to invent.
    /// </summary>
    [Fact]
    public void Every_activitys_refusals_are_renderable()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Command[] impossible =
        [
            new WellTestCommand(target),
            new WirelineLogCommand(target),
            new CutCoreCommand(target),
            new DrillWellCommand(Structure(engine, target), new Length(-1.0)),
        ];

        foreach (Command command in impossible)
        {
            Rejected rejected = Assert.IsType<Rejected>(engine.Commands.Submit(command));

            Assert.NotEmpty(rejected.Reasons);

            Assert.All(rejected.Reasons, reason =>
            {
                Assert.StartsWith("$loc:", reason.LocId, StringComparison.Ordinal);
                Assert.NotEmpty(reason.Detail);
            });
        }
    }

    // ------------------------------------------------------------- the money

    /// <summary>
    /// KNOWLEDGE IS NOT AN ASSET (SDD-009 §1). A survey is bought and consumed in
    /// the same month, and capitalising it would let a company inflate its
    /// balance sheet by shooting seismic.
    /// </summary>
    [Fact]
    public void A_survey_is_expensed_and_a_well_is_capitalised()
    {
        (Engine surveying, EntityId<IReservoirCompartmentEntity> surveyTarget) = Undrilled();
        (Engine drilling, EntityId<IReservoirCompartmentEntity> drillTarget) = Undrilled();

        surveying.Commands.Submit(new SeismicSurveyCommand(Structure(surveying, surveyTarget)));
        drilling.Commands.Submit(new DrillWellCommand(Structure(drilling, drillTarget), new Length(2000.0)));

        surveying.Pipeline.AdvanceTick();
        drilling.Pipeline.AdvanceTick();

        Assert.Equal(Money.Zero, CapitalSpentBy(surveying));
        Assert.True(ExpensedBy(surveying) > Money.Zero,
            "a survey is bought and consumed in the same month");

        Assert.True(CapitalSpentBy(drilling) > Money.Zero,
            "a well is PP&E — the money buys something the company still owns next month");
    }

    /// <summary>
    /// A measurement costs money whether or not it works. That is what makes
    /// buying information a decision instead of a formality.
    /// </summary>
    [Fact]
    public void A_measurement_is_paid_for_while_it_runs()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        engine.Pipeline.AdvanceTick();
        Money before = engine.ReadModel!.Cash;

        engine.Commands.Submit(new SeismicSurveyCommand(Structure(engine, target)));
        engine.Pipeline.AdvanceTick();

        Assert.True(engine.ReadModel!.Cash < before, "the survey must have been paid for");
    }
}
