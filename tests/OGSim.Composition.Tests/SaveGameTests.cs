// R20d.12 — the round trip (SDD-013 §1–§2, §6, finding 188).
//
// THIS IS THE TEST THE NINE STATE OWNERS HAVE NEVER HAD. Each has a unit test
// that captures it and restores it and asserts the values came back; not one of
// them said anything about whether a SAVE exists, whether the owners agree on a
// container, or whether a game continues the same after a reload. That gap is
// what finding 188 was about, and the only way to close it is a test that plays,
// saves, loads and plays on.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Company;
using OGSim.Kernel;
using OGSim.Persistence;

namespace OGSim.Composition.Tests;

public sealed class SaveGameTests
{
    /// <summary>
    /// A developed field, run far enough that every owner has something to lose:
    /// wells drilled, cash moved, equipment worn and instrumented, water bought,
    /// activities in flight.
    /// </summary>
    private static (Engine Engine, EntityId<IReservoirCompartmentEntity> Target) Played(
        int months)
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        FieldControl field = engine.Provided.Resolve<FieldControl>();

        EntityId<IReservoirCompartmentEntity> target = field.AddCompartment(
            new GeneratedCompartment(
                PoreVolume: new ReservoirVolume(100.0e6),
                Porosity: 0.22,
                OilSaturation: 0.7,
                InitialPressure: new Pressure(30.0e6),
                Temperature: Temperature.FromCelsius(93.3),
                Depth: new Length(2000.0)),
            permeability: new Permeability(1.0e-13),
            netThickness: new Length(20.0),
            drainageArea: new Area(2.0e5),
            rockCompressibility: 4.5e-10,
            gasOilContact: new Length(1900.0),
            oilWaterContact: new Length(2100.0),
            Defaults.Wettability, Defaults.Drive,
            Defaults.AquiferStrength, Defaults.AquiferResponseTime);

        EntityId<IProspect> prospect = engine.Provided.Resolve<WorldState>()
            .DeclareKnownField(target, new ReservoirVolume(100.0e6));

        for (var well = 0; well < 3; well++) field.Drill(target, new Length(2000.0));

        // AND LEARN SOMETHING. This fixture drilled, flooded, shut wells in and
        // bought equipment, and it never SURVEYED — so the belief store was empty
        // on both sides of every reload and `Beliefs` compared equal at nothing.
        // Two years of month-by-month comparison said the company remembered what
        // it had paid to learn, and the company had paid to learn nothing
        // (finding 198). A survey is the one activity a company with nothing
        // drilled can order, and it puts a belief in the store this month.
        // AND PUT IT ON A PLAY, so the survey has a risk to move as well as a
        // belief (SDD-008 §4). Registered by hand because `DeclareKnownField` is
        // the scenario door and carries no exploration risk of its own; in a
        // GENERATED world the sink registers every prospect it places, and this
        // stands in for that until a generated world can be reloaded at all
        // (finding 195). What follows is the real path either way: the survey
        // moves Trap on the prospect and Reservoir on the play, so both halves of
        // the block hold something other than the opening prior.
        var risks = engine.Provided.Resolve<OGSim.Information.ProspectRisks>();
        var play = new ContentId("test-play");

        risks.Register(
            new EntityRef(EntityKind.Prospect, prospect.Value), play, trapConfidence: 0.6);

        // AND A SECOND STRUCTURE ON THE SAME PLAY, which is the only way a save
        // can be asked whether the play CORRELATION survived. One prospect cannot
        // tell a bound Beta from a copied one — evidence against its own seal
        // moves either — and a restore that rebuilt the share as a snapshot would
        // pass every value comparison while quietly making the campaign a series
        // of independent bets (SDD-008 §4). Registered rather than placed: a basin
        // puts dozens of structures on one play, and what the risk set is keyed by
        // is the reference, not whether this hand-built fixture drilled it.
        risks.Register(
            new EntityRef(EntityKind.Prospect, prospect.Value + 1UL), play,
            trapConfidence: 0.4);

        engine.Commands.Submit(new SeismicSurveyCommand(prospect));

        engine.Pipeline.AdvanceTick();

        // Instrument the chain so the monitoring kits — the newest saved fact
        // there is (R20d.26.4) — are something the save has to carry.
        IReadOnlyList<ChainElementView> chain = engine.ReadModel!.Chain;
        for (var i = 0; i < chain.Count; i++)
            engine.Commands.Submit(new InstallMonitoringCommand(chain[i].Element));

        // And order a flood, so the imported-water history souring depends on is
        // non-zero by the time this is written out (R20d.25).
        engine.Commands.Submit(new SetVoidageReplacementCommand(1.0));

        // AND BUY SOMETHING. Every save defect this test found was found because
        // the fixture DID the thing — drilled, flooded, shut a well in. It never
        // installed, so six fitted tiers went unsaved and two years of month-by-
        // month comparison saw nothing at all (finding 197). A separator rung is
        // the cheapest purchase in the catalogue and puts a tier on the chain
        // that is not the one composition started with.
        engine.Commands.Submit(new InstallSeparatorCommand());

        // AND THE EXPORT LINE, which is owned by a different module and so a
        // different block. Buying only the separator would have left that one
        // exactly as unsaved as all six were, and just as invisible.
        engine.Commands.Submit(new ExpandExportCommand());

        Fixture.Run(engine, months);

        return (engine, target);
    }

    /// <summary>
    /// Which read-model fields two engines disagree about.
    ///
    /// <para>Here because "the records are not equal" is true and useless: this
    /// suite's whole history is defects that took minutes to find once something
    /// printed the differing NAME and a long time when nothing did. A failure
    /// that says <c>Flared</c> is a fix; one that says the projection differs is
    /// an investigation.</para>
    /// </summary>
    private static string Fields(FieldReadModel a, FieldReadModel b)
    {
        var apart = new List<string>();

        void Check(string name, bool same)
        {
            if (!same) apart.Add(name);
        }

        Check("Tick", a.Tick == b.Tick);
        Check("Date", a.Date == b.Date);
        Check("Cash", a.Cash == b.Cash);
        Check("Wells", a.Wells == b.Wells);
        Check("ActivitiesRunning", a.ActivitiesRunning == b.ActivitiesRunning);
        Check("ProducedThisTick", a.ProducedThisTick == b.ProducedThisTick);
        Check("Insolvent", a.Insolvent == b.Insolvent);
        Check("Progress", a.Progress == b.Progress);
        Check("Beliefs", Structural.Equal(a.Beliefs, b.Beliefs));
        Check("Chain", Structural.Equal(a.Chain, b.Chain));
        Check("Wellbores", Structural.Equal(a.Wellbores, b.Wellbores));
        Check("Prospects", Structural.Equal(a.Prospects, b.Prospects));
        Check("OilPrice", a.OilPrice == b.OilPrice);
        Check("CostIndex", a.CostIndex.Equals(b.CostIndex));
        Check("Reserves", a.Reserves == b.Reserves);
        Check("Borrowing", a.Borrowing == b.Borrowing);
        Check("Covenant", a.Covenant == b.Covenant);
        Check("Debt", a.Debt == b.Debt);
        Check("Flared", a.Flared == b.Flared);
        Check("EsgStanding", a.EsgStanding.Equals(b.EsgStanding));
        Check("Flood", a.Flood == b.Flood);

        // Added with the projection rather than after it. A read-model field PV2
        // does not compare is a field a reload may silently lose — which is how
        // `Beliefs` agreed for two years while both sides were empty
        // (finding 198).
        Check("Storage", a.Storage == b.Storage);
        Check("CashByCause", Structural.Equal(a.CashByCause, b.CashByCause));
        Check("Operations", Structural.Equal(a.Operations, b.Operations));

        return apart.Count == 0
            ? "no named field differs"
            : "fields apart: " + string.Join(", ", apart);
    }

    /// <summary>Which chain ROWS differ, and in which of their parts — the same
    /// reasoning as <see cref="Fields"/>, one level down.</summary>
    private static string Rows(FieldReadModel a, FieldReadModel b)
    {
        if (a.Chain.Count != b.Chain.Count)
            return $"row count {a.Chain.Count} vs {b.Chain.Count}";

        var apart = new List<string>();

        for (var i = 0; i < a.Chain.Count; i++)
        {
            ChainElementView x = a.Chain[i];
            ChainElementView y = b.Chain[i];

            if (x == y) continue;

            var parts = new List<string>();

            if (x.Element != y.Element) parts.Add($"element {x.Element.Value}/{y.Element.Value}");
            if (!string.Equals(x.DisplayId, y.DisplayId, StringComparison.Ordinal))
                parts.Add($"id {x.DisplayId}/{y.DisplayId}");
            if (x.Throughput != y.Throughput)
                parts.Add($"throughput {x.Throughput.Kilograms:F3}/{y.Throughput.Kilograms:F3}");
            if (x.Condition != y.Condition) parts.Add($"condition {x.Condition}/{y.Condition}");
            if (x.Failed != y.Failed) parts.Add($"failed {x.Failed}/{y.Failed}");
            if (!Structural.Equal(x.Deferred, y.Deferred))
                parts.Add($"deferred {x.Deferred.Count}/{y.Deferred.Count}");

            apart.Add($"[{i} {x.DisplayId}] " + string.Join(" ", parts));
        }

        return apart.Count == 0 ? "every chain row agrees" : string.Join("; ", apart);
    }

    /// <summary>Which accounts two engines disagree about — what a divergence
    /// message needs to be actionable rather than merely true.</summary>
    private static string Accounts(Engine a, Engine b)
    {
        CostLedger left = a.Provided.Resolve<CompanyState>().Ledger;
        CostLedger right = b.Provided.Resolve<CompanyState>().Ledger;

        var differences = new List<string>();

        foreach (Account account in Enum.GetValues<Account>())
        {
            Money one = left.BalanceOf(account);
            Money two = right.BalanceOf(account);

            if (one != two) differences.Add($"{account} {one.Cents} vs {two.Cents}");
        }

        return differences.Count == 0
            ? "every account balance agrees"
            : "accounts differing: " + string.Join(", ", differences);
    }

    private static MemoryStream Saved(Engine engine)
    {
        var container = new MemoryStream();
        SaveGame.Write(engine, Fixture.Settings().WorldSeed, container);
        container.Position = 0;

        return container;
    }

    /// <summary>
    /// A COMPANY IN BREACH IS STILL IN BREACH AFTER A RELOAD (finding 210).
    ///
    /// <para>`Bank.Covenant` is assessed as <c>Assess(Terms, Drawn, Covenant)</c>
    /// — it takes its own previous value, so it is a clock rather than a
    /// quantity. No block carried it, so a reload returned a company that was
    /// <c>Clear</c> with zero months however deep in breach it was: **a breach
    /// curable by saving and loading**, which is the same class as the
    /// abandonment obligation SDD-013 §2b is careful not to hand back.</para>
    ///
    /// <para>THE FIXTURE IS THE HALF THAT MATTERS. PV2 already compared
    /// `Covenant` and passed, because it never submits a `BorrowCommand`: with
    /// nothing drawn the covenant is Clear on both sides forever and the check
    /// confirms nothing. That is `Beliefs` before finding 198 exactly, and it is
    /// why this test BORROWS and then produces until the base crosses under the
    /// debt rather than asserting on a company that owes nothing.</para>
    ///
    /// <para>No command can force a breach: the validator will not lend past the
    /// base. So the breach is earned the way a player would earn one — draw the
    /// whole facility while the field is young, then let depletion take the
    /// proved reserves the base is redetermined from (SDD-009 §5).</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void A_company_in_breach_is_still_in_breach_after_a_reload()
    {
        (Engine engine, _) = Played(months: 12);

        // A BANK SECURES ON RESERVES, AND RESERVES COME FROM BELIEFS. The shared
        // fixture drills through `FieldControl` directly, which puts steel in the
        // ground without the company learning anything: the oil-in-place belief
        // `ReservesBook` reads is delivered by the DRILL COMMAND's discovery
        // branch (SDD-008 §3), so a field drilled the short way supports no
        // borrowing at all. Drilled here through the real door for that reason.
        WorldState world = engine.Provided.Resolve<WorldState>();

        // AND THE HOLE IS RETRIED, because most of them are lost. The outcome
        // table decides whether a well was drilled at all, and a lost job teaches
        // the company nothing — three went down before one landed here. Bounded
        // rather than looped forever: if a discovery cannot be made, the failure
        // below says so instead of the suite hanging.
        var attempts = 0;
        while (attempts < DiscoveryAttempts
            && engine.ReadModel!.Borrowing.BorrowingBase == Money.Zero)
        {
            engine.Commands.Submit(
                new DrillWellCommand(world.Prospects[0], new Length(2000.0)));

            Fixture.Run(engine, months: 12);
            attempts++;
        }

        Money cap = engine.ReadModel!.Borrowing.BorrowingBase;
        Assert.True(cap > Money.Zero,
            "the fixture has no borrowing base to draw against, so nothing below " +
            "can breach and this test would be as vacuous as the check it replaces");

        engine.Commands.Submit(new BorrowCommand(cap));
        engine.Pipeline.AdvanceTick();

        Assert.True(engine.ReadModel!.Debt > Money.Zero,
            $"the draw was refused: base {cap.Cents}, debt {engine.ReadModel!.Debt.Cents}");

        // Produce until depletion pulls the base under the debt. The distance is
        // not calculable from the content — it is a race between the decline
        // curve and a base redetermined from what is left — so the bound is
        // generous and the failure reports where it got to.
        var month = 0;
        while (month < BreachSearchMonths
            && engine.ReadModel!.Covenant.State == CovenantState.Clear)
        {
            Fixture.Repair(engine);
            engine.Pipeline.AdvanceTick();
            month++;
        }

        Assert.True(engine.ReadModel!.Covenant.State != CovenantState.Clear,
            $"after {month} months the company is still Clear: debt " +
            $"{engine.ReadModel!.Debt.Cents} against a base of " +
            $"{engine.ReadModel!.Borrowing.BorrowingBase.Cents}. A test that cannot " +
            $"reach a breach cannot tell whether the breach survives a reload");

        CovenantStatus breached = engine.ReadModel!.Covenant;

        using MemoryStream container = Saved(engine);

        Engine reloaded = Assert.IsType<Built>(
            SaveGame.Load(container, Fixture.Settings())).Engine;

        reloaded.Pipeline.AdvanceTick();
        engine.Pipeline.AdvanceTick();

        // Both halves, and the clock is why. A company handed back Curing with a
        // fresh window has had its cure period silently extended, which the state
        // alone cannot show.
        Assert.Equal(engine.ReadModel!.Covenant.State, reloaded.ReadModel!.Covenant.State);
        Assert.Equal(
            engine.ReadModel!.Covenant.TicksRemaining,
            reloaded.ReadModel!.Covenant.TicksRemaining);

        Assert.NotEqual(CovenantState.Clear, breached.State);
    }

    /// <summary>How long the search above will run before giving up. Not a
    /// model constant — a bound on a measurement.</summary>
    private const int BreachSearchMonths = 300;

    /// <summary>How many wells the test will spend before concluding that no
    /// discovery is reachable. Measured: the fourth one landed.</summary>
    private const int DiscoveryAttempts = 8;

    /// <summary>
    /// PV2, WHICH DESIGN 11 §4 CALLS THE ONE THAT MATTERS MOST: "save at tick N,
    /// load, run to N+100 — identical to running straight through". Round-trip
    /// equality proves the bytes match; only continuation equality proves the
    /// BEHAVIOUR does, and it is the check that catches "restored as a value,
    /// not as a live dependency".
    ///
    /// <para>Both engines are run on together after the split, because a reload
    /// that restored every block correctly and left one RNG stream at zero would
    /// pass any check made at the moment of loading and diverge on the first
    /// draw. The comparison is the whole read model — production, cash, the
    /// chain, every condition — which has structural equality (finding 131), so
    /// it names the month it first differs.</para>
    ///
    /// <para>A WELL IS SHUT IN BEFORE THE SAVE, deliberately. The choke lives on
    /// the completion object rather than in any block, so until R20d.12 it was
    /// not written at all and a reload re-opened wells a player had closed. A
    /// fixture that only ever drilled would not have asked.</para>
    ///
    /// <para>IT TOOK SIX DEFECTS TO GET HERE, none of them findable by an
    /// owner's own round-trip test: a reservoir restored onto the wrong drive, a
    /// ledger that re-asked history to justify itself, an aquifer with no water
    /// behind it, a market whose price was never saved, a voidage set point a
    /// reload forgot, and a flood-share list whose absence left the injector
    /// reporting no room for exactly one month (findings 192–196).</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void PV2_a_saved_game_reloaded_continues_identically()
    {
        (Engine original, _) = Played(months: 60);

        // Close one, so the save has something to be wrong about.
        original.Commands.Submit(new SetWellChokeCommand(
            new EntityId<ICompletion>(original.ReadModel!.Wellbores[0].Well.Value),
            Open: false));

        Fixture.Run(original, months: 2);

        using MemoryStream container = Saved(original);

        Engine reloaded = Assert.IsType<Built>(
            SaveGame.Load(container, Fixture.Settings())).Engine;

        Assert.Equal(original.Pipeline.CurrentTick, reloaded.Pipeline.CurrentTick);

        // THE FIXTURE DID THE THING, asserted rather than assumed. `Beliefs` was
        // in the comparison below from the first version of this test and it
        // agreed every month for two years because both sides were EMPTY — the
        // check was true and vacuous, which is the worst state for a check to be
        // in (finding 198). This is what makes the one below mean something.
        Assert.NotEmpty(original.ReadModel!.Beliefs);

        // A LOADED ENGINE HAS NO READ MODEL UNTIL IT TICKS, by design: the
        // projection is built at the close of a month, and a game that has not
        // run one has nothing to show. So both are compared after each tick,
        // never before the first.
        for (var month = 0; month < 24; month++)
        {
            Fixture.Repair(original);
            Fixture.Repair(reloaded);

            original.Pipeline.AdvanceTick();
            reloaded.Pipeline.AdvanceTick();

            FieldReadModel a = original.ReadModel!;
            FieldReadModel b = reloaded.ReadModel!;

            // THE WHOLE PROJECTION, month after month for two years. Every
            // field by name and every chain row by part, so a regression says
            // WHICH rather than "the records differ" — production, cash, the
            // chain and every condition on it, the wells, the beliefs, the
            // reserves, the borrowing terms, the covenant, the flaring record,
            // the ESG standing and the flood.
            // EVERY FIELD BUT THE CHAIN, and the chain agrees in the FIRST
            // month now that last tick's delivered rates are carried (S013-9):
            // the reloaded field no longer ages its plant as though it were dry.
            // It still parts in a later month, so the assertion admits `Chain`
            // and nothing else — a regression anywhere else says which field,
            // and the day the chain closes this becomes one comparison.
            // EVERY FIELD, WITH NO EXCEPTION ADMITTED (R20d.12.18). `Chain` was
            // allowed to differ here for as long as this test existed, and the
            // reason turned out not to be the save at all: connate water
            // saturation had two owners that disagreed in the last bit, and the
            // container faithfully recorded one of them (finding 206). With one
            // owner the chain agrees, so the admission is withdrawn rather than
            // left standing as a licence for the next divergence.
            string apart = Fields(a, b);

            Assert.Equal("no named field differs", apart);

            Assert.Equal("every account balance agrees", Accounts(original, reloaded));
        }
    }

    /// <summary>
    /// PV2-B (SDD-008 §4b.4). A COMPANY THAT SURVEYS, RELOADS, AND STILL KNOWS
    /// IT — belief for belief, in the order it learned them.
    ///
    /// <para>Everything the store holds was bought: a survey, a well test, a log,
    /// a core, a dry hole that re-priced a play. None of it is a function of the
    /// seed, none of it can be recomputed, and none of it was in a save until
    /// R20d.12.10 — so a reloaded company was solvent, drilled, producing, and
    /// had forgotten every survey it had ever paid for.</para>
    ///
    /// <para>Against <c>Held</c> rather than the projection, deliberately: the
    /// read model publishes P10/P50/P90 and NOT Mu and Sigma (SDD-008 §8), so two
    /// stores could project identically while carrying different parameters —
    /// and it is the parameters the next observation combines against. This
    /// compares what the next month will actually use.</para>
    ///
    /// <para>ORDER IS PART OF THE FACT. §3's <c>Held</c> is ordered by first
    /// learning and the stage-13 projection walks it, so a restore that rebuilt
    /// the same SET in a different order would give a host a belief list that
    /// reshuffled itself across a reload.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void PV2B_a_company_that_surveys_still_knows_it_after_a_reload()
    {
        (Engine original, _) = Played(months: 24);

        using MemoryStream container = Saved(original);

        Engine reloaded = Assert.IsType<Built>(
            SaveGame.Load(container, Fixture.Settings())).Engine;

        IReadOnlyList<HeldBelief> before = original.Provided.Resolve<IBeliefStore>().Held;
        IReadOnlyList<HeldBelief> after = reloaded.Provided.Resolve<IBeliefStore>().Held;

        // A survey ran, so there is something to lose. Without this the whole
        // test passes on two empty lists, which is how the gap survived a suite
        // that already compared beliefs every month for two years.
        Assert.NotEmpty(before);

        Assert.Equal(before.Count, after.Count);

        for (var i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].Subject, after[i].Subject);
            Assert.Equal(before[i].PropertyKind, after[i].PropertyKind);

            // EXACT, not approximate. A belief is a pair of doubles written and
            // read back through the canonical form's round-trip representation,
            // and a tolerance here would hide precisely the loss it is there to
            // catch — the next conjugate update weights by 1/sigma squared.
            Assert.Equal(before[i].Belief, after[i].Belief);
        }

        // AND THE RISK SET, which is the other half of what exploration buys
        // (SDD-008 §4b). The survey moved Trap on the prospect and Reservoir on
        // the PLAY, so this fails in two distinguishable ways: a prospect's own
        // Beta lost, or the shared one lost — and the second would also mean a
        // restored prospect had stopped reading through to its play.
        var risksBefore = original.Provided.Resolve<OGSim.Information.ProspectRisks>();
        var risksAfter = reloaded.Provided.Resolve<OGSim.Information.ProspectRisks>();

        Assert.NotEmpty(risksBefore.Known);
        Assert.Equal(risksBefore.Known.Count, risksAfter.Known.Count);

        for (var i = 0; i < risksBefore.Known.Count; i++)
        {
            EntityRef prospect = risksBefore.Known[i];

            Assert.Equal(prospect, risksAfter.Known[i]);
            Assert.Equal(risksBefore.PlayOf(prospect), risksAfter.PlayOf(prospect));

            foreach (PosFactor factor in Enum.GetValues<PosFactor>())
                Assert.Equal(risksBefore.Of(prospect)[factor], risksAfter.Of(prospect)[factor]);

            Assert.Equal(
                risksBefore.Of(prospect).ProbabilityOfSuccess,
                risksAfter.Of(prospect).ProbabilityOfSuccess);
        }

        // THE PLAY IS STILL SHARED, not copied back as five loose numbers — and
        // the check needs TWO prospects, because one cannot tell a bound Beta
        // from a copied one. Eight wells' worth of evidence against the seal is
        // put in through the FIRST and read back off the SECOND: only a restore
        // that rebuilt the binding rather than a snapshot can carry it across.
        // That is the whole content of "the play died", and a restore that lost
        // it would pass every value comparison above while quietly turning a
        // campaign into a series of independent bets (§4).
        EntityRef one = risksAfter.Known[0];
        EntityRef two = risksAfter.Known[1];

        Assert.Equal(risksAfter.PlayOf(one), risksAfter.PlayOf(two));

        double sealBefore = OGSim.Information.ProspectRisk.MeanOf(
            risksAfter.Of(two)[PosFactor.Seal]);

        risksAfter.Learned(one, PosFactor.Seal, present: false, weight: 8.0);

        Assert.True(
            OGSim.Information.ProspectRisk.MeanOf(risksAfter.Of(two)[PosFactor.Seal]) < sealBefore,
            "a dry seal on one prospect did not move the other on the same play, so the " +
            "restored prospects are holding snapshots rather than reading a shared Beta");
    }

    /// <summary>
    /// S013-6. A SAVE KNOWS WHAT MONTH IT IS, whatever the host says.
    ///
    /// <para>A container records a tick COUNT, and a count is only a date against
    /// an epoch. That epoch lived in `EngineSettings` alone, so a save opened
    /// with different settings had its whole history relabelled — every audit
    /// entry, every belief's as-of, every objective deadline shifted — with the
    /// simulation itself identical and nothing to indicate anything had moved.
    /// Every number right and every date wrong is the worst shape a defect can
    /// take, because nothing looks broken.</para>
    ///
    /// <para>Loaded here with a DELIBERATELY WRONG epoch, which is what a second
    /// scenario would have supplied. The date must come back as the saved game's
    /// and not the caller's — the same rule the world seed has followed since
    /// R20d.12.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void A_reloaded_game_keeps_its_own_calendar()
    {
        (Engine original, _) = Played(months: 24);

        GameDate expected = original.ReadModel!.Date;

        using MemoryStream container = Saved(original);

        Engine reloaded = Assert.IsType<Built>(SaveGame.Load(
            container,
            Fixture.Settings() with { Epoch = new GameDate(1990, 7) })).Engine;

        reloaded.Pipeline.AdvanceTick();
        original.Pipeline.AdvanceTick();

        Assert.Equal(original.ReadModel!.Date, reloaded.ReadModel!.Date);

        // And it is the SAVED game's calendar rather than a coincidence: the
        // month after a 24-month run is not July 1990.
        Assert.NotEqual(new GameDate(1990, 7), reloaded.ReadModel!.Date);
        Assert.Equal(expected.AddMonths(1), reloaded.ReadModel!.Date);
    }

    /// <summary>
    /// S013-4. THE TRAIL SURVIVES A RELOAD, ids and cause chains intact.
    ///
    /// <para>Design 09 §4.3 promises a player can ask "why?" of the current
    /// state, and §4.4 promises that nothing explaining the current state is
    /// discarded — guarantees about the STATE, not the session. A trail that
    /// began again at every load could not answer why a well is shut in today if
    /// the failure that shut it predates the save, which is finding 198's shape:
    /// a company that reloads and has forgotten (SDD-013 §1b).</para>
    ///
    /// <para>IDS RESTORE VERBATIM, which is the whole of it. A `Cause` in a saved
    /// entry points at the id the save carried, so renumbering on load would aim
    /// every chain in the file at the wrong entry — and the counter resumes above
    /// the highest, so an entry written after the load cannot collide with one
    /// written before it.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void A_reloaded_game_remembers_why()
    {
        (Engine original, _) = Played(months: 24);

        IReadOnlyList<AuditEntry> before =
            original.Audit.Query(new AuditQuery(null, null, null, null));

        Assert.NotEmpty(before);

        using MemoryStream container = Saved(original);

        Engine reloaded = Assert.IsType<Built>(
            SaveGame.Load(container, Fixture.Settings())).Engine;

        IReadOnlyList<AuditEntry> after =
            reloaded.Audit.Query(new AuditQuery(null, null, null, null));

        Assert.Equal(before.Count, after.Count);

        for (var i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].Id, after[i].Id);
            Assert.Equal(before[i].Tick, after[i].Tick);
            Assert.Equal(before[i].Category, after[i].Category);
            Assert.Equal(before[i].Subject, after[i].Subject);
            Assert.Equal(before[i].Cause, after[i].Cause);
            Assert.True(Structural.Equal(before[i].Data, after[i].Data));
        }

        // AND THE COUNTER RESUMED, so the next entry cannot collide with one the
        // save carried. Ticking on is what writes it.
        reloaded.Pipeline.AdvanceTick();

        IReadOnlyList<AuditEntry> grown =
            reloaded.Audit.Query(new AuditQuery(null, null, null, null));

        Assert.All(grown, entry => Assert.True(
            entry.Cause is not AuditId cause || grown.Any(e => e.Id == cause),
            $"entry {entry.Id.Value} cites a cause the trail no longer holds"));
    }

    /// <summary>
    /// EVERY OWNER CAPTURES TOGETHER, which had never been true before: one block
    /// each, stamped with its own schema version, digested as a set.
    ///
    /// <para>Asserted against the owner LIST rather than a count, so the day a
    /// module adds state the container misses, this fails naming it — which is
    /// design 11's PV4.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R19V9_every_state_owner_is_captured_into_the_container()
    {
        (Engine engine, _) = Played(months: 60);

        using MemoryStream container = Saved(engine);

        var loaded = Assert.IsType<Loaded>(SaveGame.Read(container));

        Assert.Equal(engine.State.Owners.Count, loaded.Blocks.Count);

        for (var i = 0; i < engine.State.Owners.Count; i++)
        {
            string key = engine.State.Owners[i].Key.Value;

            Assert.Contains(loaded.Blocks,
                block => string.Equals(block.Module, key, StringComparison.Ordinal));

            Assert.True(loaded.Header.ModuleDigests.ContainsKey(key),
                $"'{key}' has a state block that the header does not digest, so a corrupted " +
                "copy of it would load unnoticed");
        }

        Assert.Equal(engine.Pipeline.CurrentTick, loaded.Header.Tick);

        // The stream a save most needs and the one whose absence would be
        // invisible: equipment failure draws from it every tick.
        Assert.True(loaded.Header.RngPositions[StreamId.Hazard.ToString()] > 0UL,
            "sixty months produced no hazard draws, so the position proves nothing");
    }

    /// <summary>
    /// PV1. THE SAME GAME WRITES THE SAME BYTES. Two saves of one engine agree
    /// digest for digest — which is what makes the per-module digests worth
    /// carrying, and what a cross-platform check compares.
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R19V9_two_saves_of_one_game_are_identical()
    {
        (Engine engine, _) = Played(months: 24);

        using MemoryStream first = Saved(engine);
        using MemoryStream second = Saved(engine);

        var a = Assert.IsType<Loaded>(SaveGame.Read(first));
        var b = Assert.IsType<Loaded>(SaveGame.Read(second));

        Assert.Equal(a.Header.StateDigest, b.Header.StateDigest);
        Assert.Equal(a.Header, b.Header);
    }

    /// <summary>
    /// SDD-013 §6. A TAMPERED BLOCK IS REFUSED, and the refusal names the module
    /// rather than the file — which is the entire reason §2 carries a digest per
    /// module instead of one over the whole container.
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R19V9_a_state_block_that_does_not_match_its_digest_is_refused()
    {
        (Engine engine, _) = Played(months: 12);

        using MemoryStream container = Saved(engine);

        var loaded = Assert.IsType<Loaded>(SaveGame.Read(container));

        // Re-digest the same blocks with ONE of them altered, and validate
        // against the header the save actually carries.
        var tampered = new List<ModuleBlock>(loaded.Blocks);

        tampered[0] = tampered[0] with
        {
            State = new JsonObject(new Dictionary<string, JsonValue>(StringComparer.Ordinal)
            {
                ["$schema-version"] = new JsonInteger(1),
                ["tampered"] = new JsonInteger(1),
            }),
        };

        var refused = Assert.IsType<Refused>(
            SaveFile.Validate(loaded.Header, tampered, SaveGame.SchemaVersion, []));

        Assert.Contains(refused.Reasons,
            reason => reason.Contains(tampered[0].Module, StringComparison.Ordinal));
    }

    /// <summary>
    /// AND A CONTAINER WITH NO MANIFEST IS REFUSED RATHER THAN THROWN AT. An
    /// empty or unrelated zip is a file a player picked wrongly, which is a
    /// refusal to report and not a fault to halt on.
    /// </summary>
    [Fact]
    public void R19V9_a_container_without_a_manifest_is_refused()
    {
        var empty = new MemoryStream();

        using (var archive = new System.IO.Compression.ZipArchive(
                   empty, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("something-else.txt");
        }

        empty.Position = 0;

        var refused = Assert.IsType<Refused>(SaveGame.Read(empty));

        Assert.Contains(refused.Reasons,
            reason => reason.Contains("manifest", StringComparison.Ordinal));
    }


}
