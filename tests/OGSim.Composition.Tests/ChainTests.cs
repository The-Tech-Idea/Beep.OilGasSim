// R20d.1 — the chain, wired (SDD-002 §7/§9, SDD-006 §1/§1b, SDD-009 §1).
//
// Every barrel now crosses four elements between the reservoir and the bank:
// completion → header → separator → meter. What is asserted here is that the
// chain is LOAD-BEARING rather than present — that the money comes off the
// meter, that the separator's pressure reaches the reservoir, and that a second
// well goes somewhere.
//
// The previous loop could not have failed any of these tests, because it did not
// have anything to fail them with: it solved each well against a hard-coded
// number and sold whatever came out.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Company;
using OGSim.Kernel;

namespace OGSim.Composition.Tests;

public sealed class ChainTests
{
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

    /// <summary>Opens a well directly, the way world generation will — no rig, no
    /// four months, no dice. What is under test is the chain, not drilling.</summary>
    private static void Produce(Engine engine, EntityId<IReservoirCompartmentEntity> target)
    {
        FieldControl field = engine.Provided.Resolve<FieldControl>();

        field.Drill(target, new Length(2000.0));
    }

    // --------------------------------------------------- the money comes off the meter

    /// <summary>
    /// SDD-009 §1. Revenue exists because something crossed a metered point, and
    /// the audit trail can be asked which entry it was.
    ///
    /// <para>The ledger has always refused a revenue credit whose cause is not a
    /// custody transfer — but until now the "custody transfer" was an entry the
    /// pricing stage wrote about itself, immediately before pricing. It is now a
    /// stage-7 record of what a `CustodyTransferPoint` actually passed.</para>
    /// </summary>
    [Fact]
    public void R20dV5_revenue_is_caused_by_a_metered_delivery()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        Assert.True(engine.ReadModel!.ProducedThisTick.CubicMetres > 0.0,
            "a well tied into the chain must deliver something");

        IReadOnlyList<AuditEntry> transfers = engine.Audit.Query(
            new AuditQuery(Subject: null, AuditCategory.CustodyTransfer, Range: null,
                           CauseChainLeaf: null));

        AuditEntry delivery = Assert.Single(transfers);

        // Metered in MASS. The barrels a player reads are that mass through one
        // density, applied once, where it becomes a volume.
        Assert.True(delivery.Data.ContainsKey("mass-kg"),
            "a custody transfer records what the meter measured, which is mass");
    }

    /// <summary>
    /// A month that delivers nothing records no custody transfer — an entry for a
    /// zero delivery would be a transfer that did not happen, and the ledger's
    /// rule that revenue cites one would be satisfied by a fiction.
    /// </summary>
    [Fact]
    public void R20dV5_a_month_with_no_delivery_records_no_transfer()
    {
        (Engine engine, _) = Undrilled();

        engine.Pipeline.AdvanceTick();

        Assert.Empty(engine.Audit.Query(
            new AuditQuery(Subject: null, AuditCategory.CustodyTransfer, Range: null,
                           CauseChainLeaf: null)));
    }

    // ------------------------------------------------------------- the chain binds

    /// <summary>
    /// FV5, end to end through the composed engine. The separator's set point is
    /// what the well flows against — so the rate the reservoir gives up is the
    /// rate the SURFACE allowed, not a number a stage held.
    ///
    /// <para>Asserted against the well's own IPR: at 15 bar wellhead the
    /// completion's operating point is a specific rate, and the whole chain must
    /// deliver the stock-tank equivalent of exactly that. A chain that quietly
    /// dropped the vessel would produce measurably more, because the terminal
    /// sink discharges to atmosphere.</para>
    /// </summary>
    [Fact]
    public void FV5_the_separators_set_point_is_what_the_well_flows_against()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        double delivered = engine.ReadModel!.ProducedThisTick.CubicMetres;

        // What the completion would give at the vessel's pressure, solved
        // independently of the network, then shrunk and taken over the month.
        // The same rock the field would build the well from, so this
        // independent solve and the engine's are answering one question
        // (SDD-008 §2c).
        OGSim.Wells.Completion well = Defaults.CompletionFor(
            1, target, new Length(2000.0), Defaults.Inflow);
        well.SetReservoirConditions(
            new Pressure(30.0e6), Defaults.ReservoirTemperature,
            engine.Provided.Resolve<IFluidPropertyModel>().Rs(new Pressure(30.0e6)),

            // Dry: this asserts the SURFACE's effect on the wellhead, and a
            // watered-out well would be measuring the reservoir instead.
            waterCut: 0.0);

        var flowing = Assert.IsType<Flowing>(
            well.SolveOperatingPoint(Defaults.SeparatorTier.OperatingPressure));

        // The COMPLETION'S OWN Bo, which is the one the conversion actually uses.
        //
        // It is a hard-coded 1.2 in `Defaults.CompletionFor` while the engine
        // also composes a `BlackOilModel` that computes Bo from pressure, and the
        // two disagree by about 9% — one physical fact with two owners (law L5),
        // which the chain surfaced by making the difference show up as barrels.
        // It is content shape rather than chain behaviour: a completion design is
        // a catalogue entry (R20c.9), and its fluid block should be read from the
        // fluid system rather than stated beside it. Finding 160.
        double expected =
            Defaults.CompletionBo
                    .Shrink(new ReservoirVolume(
                        flowing.Rate.CubicMetresPerSecond * Duration.FromTicks(1.0).Seconds))
                    .CubicMetres;

        // Within a percent: the solver damps toward the operating point over its
        // iterations rather than jumping to it, and the compartment depletes a
        // little across the month it is producing.
        Assert.True(Math.Abs(delivered - expected) / expected < 0.01,
            $"the chain delivered {delivered} m³ where the well's operating point at the " +
            $"vessel's {Defaults.SeparatorTier.OperatingPressure.Pascals / 1e5} bar is {expected} m³");
    }

    // ------------------------------------------------- the chain is watchable

    /// <summary>
    /// A production-chain game is played by watching goods move. The read model
    /// now carries the chain element by element, in the order material crosses
    /// it — so a host can draw the line from wellhead to meter.
    ///
    /// <para>Before this, a player could see cash and barrels and could tell
    /// that a field was underperforming without being able to tell WHY. Nothing
    /// between the reservoir and the bank was visible at all.</para>
    /// </summary>
    [Fact]
    public void R20dV1_the_read_model_carries_the_chain_in_flow_order()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        Assert.Equal(
            // The gathering line is design 04 stage 3's wellhead-to-manifold run
            // (SDD-006 §1c) — one per well, as long as that well's field is from
            // the header. It was missing entirely: every well tied straight into
            // the header at zero distance.
            ["well-1", "gathering-1", "manifold", "flowline", "separator",
             "custody-meter", "flare", "water-disposal", "tank"],
            engine.ReadModel!.Chain.Select(element => element.DisplayId));
    }

    /// <summary>
    /// Every element on the FLOWING legs reports what crossed it, so a host can
    /// draw the line rather than only its two ends.
    ///
    /// <para>The water leg is deliberately excluded: a field at connate
    /// saturation produces no water, so the disposal well is dry and reads zero.
    /// That is SDD-003 §3.1c's breakthrough — an idle leg on a young field is a
    /// true statement about it, and a chain that showed water moving before
    /// breakthrough would be the wrong one.</para>
    /// </summary>
    [Fact]
    public void R20dV1_every_element_on_a_flowing_leg_reports_what_crossed_it()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        foreach (ChainElementView element in engine.ReadModel!.Chain)
        {
            if (element.DisplayId == "water-disposal") continue;

            Assert.True(element.Throughput.Kilograms > 0.0,
                $"{element.DisplayId} shows no throughput, so a host cannot draw the flow");
        }
    }

    /// <summary>
    /// The water leg is DRY before breakthrough, and the chain says so rather
    /// than omitting it. A player can see the disposal well is there, is
    /// connected, and is doing nothing yet — which is what makes it obvious what
    /// changes when the field starts making water.
    /// </summary>
    [Fact]
    public void R20dV4_the_water_leg_is_present_and_dry_before_breakthrough()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        ChainElementView disposal = Assert.Single(
            engine.ReadModel!.Chain, element => element.DisplayId == "water-disposal");

        Assert.Equal(0.0, disposal.Throughput.Kilograms, precision: 9);
        Assert.False(disposal.IsBottleneck);
    }

    /// <summary>
    /// A chain that is flowing freely has NO bottleneck, and that is a state a
    /// player must be able to read as clearly as a jammed one.
    ///
    /// <para>An empty list rather than an element reported at "0% blocked":
    /// "nothing is refusing" and "something is refusing nothing" are different
    /// statements, and only one of them asks a player to act.</para>
    /// </summary>
    [Fact]
    public void R20dV1_an_unjammed_chain_reports_no_bottleneck()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        Assert.Empty(engine.ReadModel!.Bottlenecks);

        foreach (ChainElementView element in engine.ReadModel.Chain)
            Assert.False(element.IsBottleneck);
    }

    // --------------------------------------------------- more than one material

    /// <summary>
    /// SDD-003 §6.1b. A well produces solution gas as well as oil, the separator
    /// sends each down its own leg, and the gas is flared — which is what an E1
    /// field with no gas infrastructure does with it.
    ///
    /// <para>Before this a completion carried a single material ordinal: a well
    /// that could only ever produce one substance, so the separator's gas leg had
    /// nothing to carry and the vessel was a pass-through with a capacity.</para>
    /// </summary>
    [Fact]
    public void R20dV3_a_well_produces_gas_and_the_separator_sends_it_to_the_flare()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        ChainElementView flare = Assert.Single(
            engine.ReadModel!.Chain, element => element.DisplayId == "flare");

        Assert.True(flare.Throughput.Kilograms > 0.0,
            "the gas leg reaches the flare, or a field's associated gas vanishes at " +
            "an unconnected port");
    }

    /// <summary>
    /// Gas is produced and NOT sold: only the liquid leg reaches the meter, so
    /// revenue is oil revenue. Flared gas is a cost with no income, which is
    /// exactly the pressure the ESG mechanics are built to apply later.
    /// </summary>
    [Fact]
    public void R20dV3_flared_gas_earns_nothing()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        ChainElementView flare = Assert.Single(
            engine.ReadModel!.Chain, element => element.DisplayId == "flare");
        ChainElementView meter = Assert.Single(
            engine.ReadModel.Chain, element => element.DisplayId == "custody-meter");

        // The meter carries oil and the flare carries gas: two legs of one
        // stream, and only one of them is worth money.
        Assert.True(flare.Throughput.Kilograms > 0.0);
        Assert.True(meter.Throughput.Kilograms > 0.0);

        // What was SOLD is the metered stock-tank oil, not the whole stream.
        Assert.True(
            engine.ReadModel.ProducedThisTick.CubicMetres
                < (meter.Throughput.Kilograms + flare.Throughput.Kilograms)
                  / Defaults.SurfaceOilDensity.KgPerCubicMetre,
            "the barrels a player is paid for must be the metered leg alone");
    }

    // ------------------------------------------- see the jam, build past it

    /// <summary>
    /// THE LOOP AN OPERATIONS GAME IS PLAYED ON, end to end: the vessel refuses
    /// production, the read model names it and says how much it is costing, the
    /// player pays for a bigger one, waits, and the field flows again.
    ///
    /// <para>Every step of that was unreachable a session ago. The chain was
    /// bypassed, so nothing could bind; nothing was projected, so a jam was
    /// invisible; and a unit's tier was fixed at construction, so the catalogue's
    /// ladder could not be climbed.</para>
    /// </summary>
    [Fact]
    public void R12bV8_a_player_sees_the_jam_pays_for_a_bigger_vessel_and_the_field_flows()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        // TWO wells: one fits inside the first vessel comfortably, and the
        // second is what puts the field over it. That is the moment the game
        // is about — the well that pays for itself only if the surface can
        // carry what it makes.
        Produce(engine, target);
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        // The jam, named and priced.
        ChainElementView jammed = Assert.Single(engine.ReadModel!.Bottlenecks);
        Assert.Equal("separator", jammed.DisplayId);

        double capped = engine.ReadModel.ProducedThisTick.CubicMetres;

        // Buy the next rung. No rig — construction is not the drilling crew.
        Assert.IsType<Accepted>(engine.Commands.Submit(new InstallSeparatorCommand()));

        engine.Pipeline.AdvanceTick();
        while (engine.ReadModel!.ActivitiesRunning > 0) engine.Pipeline.AdvanceTick();

        // The field flows again, and the separator has stopped refusing.
        Assert.True(engine.ReadModel!.ProducedThisTick.CubicMetres > capped,
            $"after the refit the field delivered {engine.ReadModel.ProducedThisTick.CubicMetres} m³, " +
            $"no more than the {capped} m³ the old vessel allowed");

        Assert.DoesNotContain(engine.ReadModel.Bottlenecks,
            element => element.DisplayId == "separator");
    }

    /// <summary>
    /// A refit is CAPEX: the money buys something the company still owns next
    /// month (SDD-009 §1), unlike a survey which is bought and consumed.
    /// </summary>
    [Fact]
    public void R12bV8_a_vessel_is_capitalised_and_a_survey_is_not()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        OGSim.Company.CostLedger ledger = engine.Provided.Resolve<OGSim.Company.CompanyState>().Ledger;
        Money before = ledger.BalanceOf(Account.Capex_PPE);

        Assert.IsType<Accepted>(engine.Commands.Submit(new InstallSeparatorCommand()));

        engine.Pipeline.AdvanceTick();
        while (engine.ReadModel!.ActivitiesRunning > 0) engine.Pipeline.AdvanceTick();

        Assert.True(ledger.BalanceOf(Account.Capex_PPE) > before,
            "a vessel the company still owns next month is capital, not an expense");
    }

    /// <summary>
    /// At the top of the ladder the answer is a REFUSAL with a reason, not a
    /// silent no-op: a player who has bought the biggest vessel in the catalogue
    /// must be told that debottlenecking here is finished, rather than paying
    /// three months to have nothing change.
    /// </summary>
    [Fact]
    public void R12bV8_the_top_of_the_ladder_is_refused_with_a_reason()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        // Climb to the top.
        for (var rung = 1; rung < Defaults.SeparatorLadder.Count; rung++)
        {
            Assert.IsType<Accepted>(engine.Commands.Submit(new InstallSeparatorCommand()));

            engine.Pipeline.AdvanceTick();
            while (engine.ReadModel!.ActivitiesRunning > 0) engine.Pipeline.AdvanceTick();
        }

        var rejected = Assert.IsType<Rejected>(
            engine.Commands.Submit(new InstallSeparatorCommand()));

        Assert.Contains(rejected.Reasons,
            reason => reason.LocId == "$loc:reject.top-of-the-ladder");
    }

    // --------------------------------------------------- stopping a losing well

    /// <summary>
    /// THE LEVER THE ECONOMICS DEMAND. Operating cost scales with the liquid a
    /// field lifts, so a well eventually costs more to produce than it earns —
    /// and until this a player could watch that happen and do nothing.
    /// </summary>
    [Fact]
    public void R20V4_a_well_can_be_shut_in_and_stops_producing()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();
        Assert.True(engine.ReadModel!.ProducedThisTick.CubicMetres > 0.0);

        Assert.IsType<Accepted>(engine.Commands.Submit(
            new SetWellChokeCommand(new EntityId<ICompletion>(1), Open: false)));

        engine.Pipeline.AdvanceTick();

        Assert.Equal(0.0, engine.ReadModel!.ProducedThisTick.CubicMetres, precision: 9);
    }

    /// <summary>
    /// And it is REVERSIBLE, which is what distinguishes shutting a well in from
    /// abandoning it. A shut well is choosing not to flow; a dead one cannot.
    /// </summary>
    [Fact]
    public void R20V4_a_shut_well_can_be_opened_again()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        var well = new EntityId<ICompletion>(1);

        engine.Commands.Submit(new SetWellChokeCommand(well, Open: false));
        engine.Pipeline.AdvanceTick();
        Assert.Equal(0.0, engine.ReadModel!.ProducedThisTick.CubicMetres, precision: 9);

        Assert.IsType<Accepted>(engine.Commands.Submit(new SetWellChokeCommand(well, Open: true)));
        engine.Pipeline.AdvanceTick();

        Assert.True(engine.ReadModel!.ProducedThisTick.CubicMetres > 0.0,
            "a shut-in well is choosing not to flow, not unable to");
    }

    /// <summary>
    /// Shutting a well in stops the lifting cost it was incurring. That is the
    /// point of the lever: a watered-out well is stopped because keeping it open
    /// costs money, and the saving has to be real or the decision is not.
    /// </summary>
    [Fact]
    public void R20V4_shutting_a_well_in_stops_what_it_was_costing_to_lift()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        OGSim.Company.CostLedger ledger =
            engine.Provided.Resolve<OGSim.Company.CompanyState>().Ledger;

        engine.Pipeline.AdvanceTick();
        long before = Math.Abs(ledger.BalanceOf(Account.Opex).Cents);

        engine.Pipeline.AdvanceTick();
        long producing = Math.Abs(ledger.BalanceOf(Account.Opex).Cents) - before;

        engine.Commands.Submit(
            new SetWellChokeCommand(new EntityId<ICompletion>(1), Open: false));

        before = Math.Abs(ledger.BalanceOf(Account.Opex).Cents);
        engine.Pipeline.AdvanceTick();
        long shut = Math.Abs(ledger.BalanceOf(Account.Opex).Cents) - before;

        Assert.True(shut < producing,
            $"a shut-in month cost {shut}c against a producing month's {producing}c — if " +
            "stopping a well saves nothing, stopping it is not a decision");
    }

    /// <summary>Setting a valve to where it already is is refused, because "shut
    /// it in" and "it is already shut in" are different answers.</summary>
    [Fact]
    public void R20V4_a_choke_change_that_changes_nothing_is_refused()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        var rejected = Assert.IsType<Rejected>(engine.Commands.Submit(
            new SetWellChokeCommand(new EntityId<ICompletion>(1), Open: true)));

        Assert.Contains(rejected.Reasons, reason => reason.LocId == "$loc:reject.choke-unchanged");
    }

    // ------------------------------------------------------------- the ending

    /// <summary>
    /// R12b.10. A field's arc ends: the wells are plugged, the obligation is
    /// discharged and the standing charge stops.
    ///
    /// <para>Until this the tail ran for thirty years — a field producing almost
    /// nothing while the standing charge ate the cash it had made — and a player
    /// could watch it and had no way to stop paying. That is what the
    /// measurement in R20.4 found and this is the answer to it.</para>
    /// </summary>
    [Fact]
    public void R12bV10_abandoning_the_last_well_closes_the_field()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        OGSim.Company.CostLedger ledger =
            engine.Provided.Resolve<OGSim.Company.CompanyState>().Ledger;

        engine.Pipeline.AdvanceTick();

        Assert.IsType<Accepted>(engine.Commands.Submit(
            new AbandonWellCommand(new EntityId<ICompletion>(1))));

        engine.Pipeline.AdvanceTick();
        while (engine.ReadModel!.ActivitiesRunning > 0) engine.Pipeline.AdvanceTick();

        // The well is plugged: nothing is produced...
        Assert.Equal(0.0, engine.ReadModel!.ProducedThisTick.CubicMetres, precision: 9);

        // ...and the field stops costing anything to keep.
        long before = Math.Abs(ledger.BalanceOf(Account.Opex).Cents);
        engine.Pipeline.AdvanceTick();

        Assert.Equal(before, Math.Abs(ledger.BalanceOf(Account.Opex).Cents));
    }

    /// <summary>
    /// The obligation is registered when the well is DRILLED, not when someone
    /// remembers it (SDD-007 §6, design 02 §3.4) — so a company always knows what
    /// it owes the future, and cannot escape the cost by never recording it.
    /// </summary>
    [Fact]
    public void R12bV10_a_well_carries_its_abandonment_obligation_from_the_day_it_opens()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        var obligations = engine.Provided.Resolve<IObligationRegistry>();
        var well = new EntityRef(EntityKind.Completion, 1);

        Assert.Equal(Money.Zero, obligations.EstimatedCost(well));

        Produce(engine, target);

        Assert.Equal(Defaults.AbandonWellTerms.Cost, obligations.EstimatedCost(well));
    }

    /// <summary>
    /// Only a COMPLETED abandonment discharges it (SDD-007 §6). Shutting a well
    /// in stops what it costs to lift and leaves the liability exactly where it
    /// was — pausing is not leaving.
    /// </summary>
    [Fact]
    public void R12bV10_shutting_a_well_in_does_not_discharge_its_obligation()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Commands.Submit(
            new SetWellChokeCommand(new EntityId<ICompletion>(1), Open: false));
        engine.Pipeline.AdvanceTick();

        Assert.Equal(
            Defaults.AbandonWellTerms.Cost,
            engine.Provided.Resolve<IObligationRegistry>()
                  .EstimatedCost(new EntityRef(EntityKind.Completion, 1)));
    }

    /// <summary>A well already plugged is refused rather than plugged twice —
    /// months and money spent discharging nothing.</summary>
    [Fact]
    public void R12bV10_abandoning_a_plugged_well_is_refused()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        var well = new EntityId<ICompletion>(1);

        engine.Commands.Submit(new AbandonWellCommand(well));
        engine.Pipeline.AdvanceTick();
        while (engine.ReadModel!.ActivitiesRunning > 0) engine.Pipeline.AdvanceTick();

        var rejected = Assert.IsType<Rejected>(
            engine.Commands.Submit(new AbandonWellCommand(well)));

        Assert.Contains(rejected.Reasons,
            reason => reason.LocId == "$loc:reject.already-abandoned");
    }

    // ------------------------------------------------------------- economics

    /// <summary>
    /// SDD-009 §3. The state takes its share, and the company keeps what is
    /// left.
    ///
    /// <para>`IFiscalRegime` was composed at R16 and called by nobody, so a
    /// company kept every barrel's full price and the fiscal terms of a licence
    /// meant nothing — which made every capital decision trivially affordable
    /// and therefore not a decision.</para>
    /// </summary>
    [Fact]
    public void R20V4_royalty_and_tax_are_taken_out_of_what_the_field_earns()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        OGSim.Company.CostLedger ledger =
            engine.Provided.Resolve<OGSim.Company.CompanyState>().Ledger;

        engine.Pipeline.AdvanceTick();

        // Revenue is a CREDIT and royalty and tax are DEBITS, so the sale
        // carries the opposite sign to what the state takes out of it. Compared
        // by magnitude, because what is under test is the share rather than the
        // bookkeeping direction.
        long gross = Math.Abs(ledger.BalanceOf(Account.Revenue).Cents);
        long royalty = Math.Abs(ledger.BalanceOf(Account.Royalty).Cents);
        long tax = Math.Abs(ledger.BalanceOf(Account.Tax).Cents);

        Assert.True(gross > 0, "the field sold something");
        Assert.True(royalty > 0, "a concession pays a royalty on gross");
        Assert.True(tax > 0, "and tax on what is left after costs");

        // Not a rounding: the state's share is a material fraction of the sale.
        Assert.True(royalty + tax > gross / 5,
            $"the state took {royalty + tax}c of a {gross}c sale — fiscal terms that take " +
            "almost nothing are fiscal terms nobody plays around");
    }

    /// <summary>
    /// The variable half of opex, and the one that ends a field's life: lifting
    /// is charged on every tonne of LIQUID, oil and water alike, because the
    /// pumps and the power do not care which.
    ///
    /// <para>A flat operating cost cannot express that, so watering out would be
    /// something a player watches rather than something they answer.</para>
    /// </summary>
    [Fact]
    public void R20V4_opex_scales_with_what_the_field_lifted()
    {
        static Money OpexWith(int wells)
        {
            (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
            for (var well = 0; well < wells; well++) Produce(engine, target);

            engine.Pipeline.AdvanceTick();

            return engine.Provided.Resolve<OGSim.Company.CompanyState>()
                         .Ledger.BalanceOf(Account.Opex);
        }

        long idle = Math.Abs(OpexWith(0).Cents);
        long producing = Math.Abs(OpexWith(1).Cents);

        // A field with no wells still pays its standing charge...
        Assert.True(idle > 0, "the road and the people are paid for either way");

        // ...and a producing one pays more, because something was lifted.
        Assert.True(producing > idle,
            $"a producing field cost {producing}c against an idle one's {idle}c — an " +
            "operating cost that does not move with production cannot make a watered-out " +
            "field uneconomic");
    }

    // ------------------------------------------------ one bottleneck, then the next

    /// <summary>
    /// R8-V5, and the progression that makes an operations game an operations
    /// game: solving one constraint is meeting the next.
    ///
    /// <para>An E1 field is VESSEL-limited, so the tank never fills and export is
    /// invisible. Fit the bigger separator and the field can make more than the
    /// pipeline will take — the tank starts filling, and when it is full the
    /// ullage constraint reaches back down the chain and throttles the wells.
    /// The player has traded a separator problem for an export problem, which is
    /// the whole shape of debottlenecking.</para>
    /// </summary>
    [Fact]
    public void R8V5_fixing_the_vessel_meets_the_export_limit_and_the_tank_backs_up()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        for (var well = 0; well < 6; well++) Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        // The vessel binds; the tank is empty and silent.
        Assert.Equal("separator", Assert.Single(engine.ReadModel!.Bottlenecks).DisplayId);

        Assert.IsType<Accepted>(engine.Commands.Submit(new InstallSeparatorCommand()));
        engine.Pipeline.AdvanceTick();
        while (engine.ReadModel!.ActivitiesRunning > 0) engine.Pipeline.AdvanceTick();

        // Now the field can make more than the line will take. Run until the
        // tank fills and starts refusing.
        var jammed = false;
        for (var month = 0; month < 60 && !jammed; month++)
        {
            engine.Pipeline.AdvanceTick();

            foreach (ChainElementView element in engine.ReadModel!.Bottlenecks)
                if (element.DisplayId == "tank") jammed = true;
        }

        Assert.True(jammed,
            "a field producing above its export rate must eventually fill the tank and be " +
            "throttled by it (R8-V5), or storage is a decoration on the chain");

        ChainElementView tank = Assert.Single(
            engine.ReadModel!.Chain, element => element.DisplayId == "tank");

        Assert.Equal(ConstraintKind.Ullage, Assert.Single(tank.Deferred).Kind);
    }

    // ------------------------------------------------------------- the header

    /// <summary>
    /// Two wells on one header. `FlowNetwork` refuses two edges into one inlet,
    /// so before the manifold existed the second well could not be connected at
    /// all — this is the test that would not compile a session ago.
    /// </summary>
    [Fact]
    public void R20dV1_a_second_well_commingles_instead_of_being_refused()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Produce(engine, target);
        engine.Pipeline.AdvanceTick();
        double one = engine.ReadModel!.ProducedThisTick.CubicMetres;

        Produce(engine, target);
        engine.Pipeline.AdvanceTick();
        double two = engine.ReadModel!.ProducedThisTick.CubicMetres;

        Assert.True(two > one,
            $"a second well on the header must add production: {two} is not above {one}");
    }

    /// <summary>
    /// R6-V14, THE COMMINGLING TRAP, end to end. A second well on a shared line
    /// costs the first one something: more throughput means more pressure lost
    /// down the flowline, which means a higher header pressure, which every well
    /// on that header flows against.
    ///
    /// <para>Nobody codes this. It is backpressure arithmetic — and until the
    /// flowline was in the network it had no term, so two wells on one header did
    /// not feel each other at all. Catalogue C06 calls it "cheap steel with
    /// expensive consequences", and this is the consequence.</para>
    /// </summary>
    [Fact]
    public void R6V14_a_second_well_on_a_shared_line_costs_the_first_one_rate()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Produce(engine, target);
        engine.Pipeline.AdvanceTick();
        double alone = engine.ReadModel!.ProducedThisTick.CubicMetres;

        Produce(engine, target);
        engine.Pipeline.AdvanceTick();
        double shared = engine.ReadModel!.ProducedThisTick.CubicMetres;

        // Two wells still beat one — the line is not that tight.
        Assert.True(shared > alone);

        // But NOT by double: the second well raised the pressure both of them
        // flow against, so each gives up rate to the other.
        Assert.True(shared < 2.0 * alone,
            $"two wells delivered {shared} m³ against one well's {alone} — a shared line " +
            "that costs nothing is a line with no pressure drop in it");
    }

    /// <summary>
    /// Slots are a real limit (SDD-006 §1b, catalogue C06), and the refusal
    /// arrives when the well is ORDERED.
    ///
    /// <para>Checked in the drilling command's own refusals rather than at the
    /// tie-in, because by the time a hole is drilled the player has paid for four
    /// months of rig time — a well that could never have flowed must be refused
    /// before the money moves, not after.</para>
    /// </summary>
    [Fact]
    public void R20dV1_a_well_with_no_slot_on_the_header_is_refused_before_it_is_paid_for()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        FieldControl field = engine.Provided.Resolve<FieldControl>();

        for (var well = 0; well < Defaults.ManifoldTier.Slots; well++) Produce(engine, target);

        Assert.False(field.HasFreeSlot);
        Assert.Equal(0, field.FreeSlots);

        var rejected = Assert.IsType<Rejected>(
            engine.Commands.Submit(new DrillWellCommand(Structure(engine, target), new Length(2000.0))));

        Assert.Contains(rejected.Reasons,
            reason => reason.LocId == "$loc:reject.no-manifold-slot");
    }
}
