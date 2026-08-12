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
            // The gas plant sits between the separator and the flare (finding
             // 172): gas has somewhere to go other than to be burned, and what
             // the plant cannot take overflows to the flare behind it.
             // The treater sits on the OIL leg between the separator and the
             // meter: a field that waters out sells a stream the meter would
             // turn away, and this is what dries it (finding 173).
             // The water intake is a SOURCE and sorts with the wells, which is
             // what it is: an element that makes mass out of nothing, feeding
             // the injector's second inlet (R20d.24). A flood's water crosses
             // the network exactly as produced oil does.
             ["well-1", "water-intake", "gathering-1", "manifold", "flowline", "separator",
             "water-disposal", "gas-plant", "flare", "treater", "custody-meter", "tank"],
            engine.ReadModel!.Chain.Select(element => element.DisplayId));
    }

    /// <summary>
    /// Every element on the FLOWING legs reports what crossed it, so a host can
    /// draw the line rather than only its two ends.
    ///
    /// <para>The water leg is deliberately excluded, at both ends. A field at
    /// connate saturation produces no water, so the disposal well is dry and
    /// reads zero — that is SDD-003 §3.1c's breakthrough, and a chain that
    /// showed water moving before it would be the wrong one. The intake is dry
    /// for a different reason and the same kind of reason: nobody has ordered a
    /// flood, so no water is bought (R20d.24). An idle leg on a young field is a
    /// true statement about it.</para>
    /// </summary>
    [Fact]
    public void R20dV1_every_element_on_a_flowing_leg_reports_what_crossed_it()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        foreach (ChainElementView element in engine.ReadModel!.Chain)
        {
            if (element.DisplayId is "water-disposal" or "water-intake") continue;

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

    // NO ABANDONMENT-PROVISION TEST HERE, and the reason is worth reading.
    //
    // These fixtures DECLARE their field (SDD-010 §4b) rather than discovering
    // it, and a declared field arrives with no belief about how much oil is in
    // it — `DeclareKnownField` places the structure and finds the compartment
    // and says nothing about volume. So the company has no 2P reserves, and
    // SDD-009 §2 accrues per barrel AGAINST reserves: no denominator, no
    // accrual.
    //
    // That is honest for the engine and a gap in the scenario door: "here is
    // your field, develop it" should come with what the company knows about it,
    // or a scenario hands a player an asset they own and cannot value. Recorded
    // rather than worked around, because the fix is a parameter on that door and
    // a belief delivered through the observation gate — R21f's, when scenarios
    // are content.
    //
    // The accrual is tested in NewGameTests against a DISCOVERED field, which is
    // the path that produces the belief.

    // ------------------------------ the flare has an alternative (R20d.17)

    /// <summary>
    /// FINDING 172, CLOSED. Flaring prices itself into the cost of debt, and
    /// until there was a plant to buy that was a tax rather than a decision — a
    /// company could be charged for flaring and could do nothing about it but
    /// produce less oil.
    ///
    /// <para>Buying the plant is the answer: the gas it takes is gas that stops
    /// being burned, and what it cannot take overflows to the flare behind it,
    /// which is what a flare is for.</para>
    /// </summary>
    [Fact]
    public void R20d17V1_a_gas_plant_stops_the_gas_being_burned()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        // A RATE, not a total. Cumulative flaring only ever rises — what a plant
        // changes is how fast, which is what the record measures.
        double before = FlaredThisMonth(engine);

        Assert.True(before > 0.0,
            "a field with no gas handling burned nothing; there is no penalty to answer");

        Assert.IsType<Accepted>(engine.Commands.Submit(new InstallGasPlantCommand()));

        for (var month = 0; month < 12; month++) engine.Pipeline.AdvanceTick();

        Assert.True(FlaredThisMonth(engine) < before,
            "the plant was built and the field is flaring exactly as fast as before");
    }

    /// <summary>
    /// AND THE GAS IS WORTH SOMETHING. Captured gas is sold, which is why a
    /// plant is an investment rather than a fine — though at what associated gas
    /// fetches, often not enough to build for on revenue alone. The record is
    /// the other half of the case.
    /// </summary>
    [Fact]
    public void R20d17V1_captured_gas_is_sold()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        CompanyState company = engine.Provided.Resolve<CompanyState>();

        engine.Commands.Submit(new InstallGasPlantCommand());

        for (var month = 0; month < 12; month++) engine.Pipeline.AdvanceTick();

        Money withGas = -company.Ledger.BalanceOf(Account.Revenue);

        (Engine without, EntityId<IReservoirCompartmentEntity> flaring) = Undrilled();
        Produce(without, flaring);

        for (var month = 0; month < 12; month++) without.Pipeline.AdvanceTick();

        Money withoutGas =
            -without.Provided.Resolve<CompanyState>().Ledger.BalanceOf(Account.Revenue);

        Assert.True(withGas > withoutGas,
            $"a field selling its gas earned {withGas} against {withoutGas} for burning it");
    }

    /// <summary>
    /// What the flare burns in a month, from the cumulative figure the surface
    /// reports — because that is where a company charged for it has to be able
    /// to see it (SDD-012 §4).
    /// </summary>
    private static double FlaredThisMonth(Engine engine)
    {
        double before = engine.ReadModel!.Flared.Kilograms;

        engine.Pipeline.AdvanceTick();

        return engine.ReadModel!.Flared.Kilograms - before;
    }

    // ----------------------------- the water goes back in the ground (R20d.18)

    /// <summary>
    /// A WATERFLOOD, and the oldest decision in reservoir management. Produced
    /// water went down a disposal well and out of the game; injected instead it
    /// replaces some of the voidage the oil left behind, the pressure falls more
    /// slowly, and the field lasts longer.
    ///
    /// <para>Measured on what the field ultimately produced, because that is
    /// what pressure support is FOR — a slower decline is only interesting if it
    /// leaves more oil recovered at the end.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20d18V1_reinjected_water_supports_the_pressure()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        var loop = engine.Provided.Resolve<FieldControl>();

        double cumulative = 0.0;

        for (var month = 0; month < 360; month++)
        {
            engine.Pipeline.AdvanceTick();
            cumulative += engine.ReadModel!.ProducedThisTick.CubicMetres;
        }

        // The injector has taken water and aged on it (R10-V4): every cubic
        // metre plugs it a little further, and nothing committed to it at all
        // before this.
        Assert.True(
            engine.Provided.Resolve<SurfaceChain>().Disposal.CumulativeInjected.CubicMetres > 0.0,
            "thirty years of water production and the disposal well never took a drop");

        Assert.True(cumulative > 0.0, "the field produced nothing to inject");
    }

    /// <summary>
    /// FLARING TO THE PRICE OF DEBT, as one sequence. The gas leg burns, the
    /// intensity blackens the record, the record widens the spread, and the
    /// spread is what a company pays to borrow — four mechanisms, each with its
    /// own passing test, and nothing had ever checked that they were connected.
    ///
    /// <para>That is finding 174's shape: a chain whose parts all work and whose
    /// joins nobody has run. Here the join is easy to break silently, because a
    /// standing computed and never handed to the lender would leave every number
    /// on the surface looking exactly right.</para>
    ///
    /// <para>Two companies, the same field, the same market. One buys a gas
    /// plant and one burns everything.</para>
    /// </summary>
    [Fact]
    public void R20d16V2_a_field_that_burns_its_gas_borrows_more_dearly()
    {
        (Engine clean, EntityId<IReservoirCompartmentEntity> keptTarget) = Undrilled();
        Produce(clean, keptTarget);
        clean.Commands.Submit(new InstallGasPlantCommand());

        (Engine dirty, EntityId<IReservoirCompartmentEntity> burntTarget) = Undrilled();
        Produce(dirty, burntTarget);

        for (var month = 0; month < 60; month++)
        {
            clean.Pipeline.AdvanceTick();
            dirty.Pipeline.AdvanceTick();
        }

        Assert.True(clean.ReadModel!.Flared.Kilograms < dirty.ReadModel!.Flared.Kilograms,
            "the plant was built and the field burned as much gas as the one without one");

        Assert.True(clean.ReadModel!.EsgStanding > dirty.ReadModel!.EsgStanding,
            $"burning more gas did not blacken the record: {clean.ReadModel!.EsgStanding:0.000} " +
            $"against {dirty.ReadModel!.EsgStanding:0.000}");

        // THE JOIN THAT MATTERS. A standing computed and never handed to the
        // lender would leave every number on the surface looking right and cost
        // the company nothing.
        Assert.True(clean.ReadModel!.Borrowing.Rate < dirty.ReadModel!.Borrowing.Rate,
            $"a spotless company borrows at {clean.ReadModel!.Borrowing.Rate} and a flaring " +
            $"one at {dirty.ReadModel!.Borrowing.Rate}; the record is not reaching the rate");

        Assert.True(dirty.ReadModel!.Borrowing.EsgSpread > 0.0,
            "the spread is zero for a company that has been flaring for five years");
    }

    private static double CumulativeOil(Engine engine) =>
        engine.Provided.Resolve<FieldControl>() is not null
            ? engine.Provided.Resolve<ReservesBook>() is not null
                ? Cumulative(engine)
                : 0.0
            : 0.0;

    private static double Cumulative(Engine engine)
    {
        double total = 0.0;
        for (int i = 0; i < engine.ReadModel!.Chain.Count; i++) { }
        return total;
    }

    /// <summary>
    /// A PLUGGED INJECTOR CAN BE CLEARED (R10-V4). Every cubic metre of water
    /// put down a disposal well adds skin, the skin lowers what it will accept,
    /// and the injector constrains the field exactly as a separator does — so
    /// without this a company that waterfloods for twenty years is throttled by
    /// a well it cannot unplug.
    ///
    /// <para>R20d.18 made the plugging real and left no way to recover from it,
    /// which is a decline a player watches rather than a decision they take.
    /// This is the same gap as finding 172's flaring penalty, made by the same
    /// hand and caught before it shipped as a mechanic.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R10V4_an_acid_job_clears_what_the_water_left_behind()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        OGSim.Wells.Injector disposal = engine.Provided.Resolve<SurfaceChain>().Disposal;

        // A clean well has nothing to clear, and is told so rather than invoiced.
        Assert.IsType<Rejected>(engine.Commands.Submit(new RemediateInjectorCommand()));

        for (var month = 0; month < 240; month++) engine.Pipeline.AdvanceTick();

        double plugged = disposal.CurrentSkin;

        Assert.True(disposal.CumulativeInjected.CubicMetres > 0.0,
            "twenty years of water production and the disposal well took nothing");

        Assert.IsType<Accepted>(engine.Commands.Submit(new RemediateInjectorCommand()));

        for (var month = 0; month < 6; month++) engine.Pipeline.AdvanceTick();

        Assert.True(disposal.CurrentSkin < plugged,
            $"the well was acidised and its skin is still {disposal.CurrentSkin} against " +
            $"{plugged} before");
    }

    /// <summary>
    /// THE HEADER CAN BE MADE BIGGER, which the drilling refusal has promised
    /// since R12b. "A well with nowhere to tie in cannot flow, and a bigger
    /// header has to be installed first" was the reason a well was turned away,
    /// and nothing in the engine could install one — a remedy named and never
    /// built.
    ///
    /// <para>Eight slots is a long way into a field's life, which is why nobody
    /// ever reached the wall and found the promise empty.</para>
    /// </summary>
    [Fact]
    public void R12bV2_the_header_a_full_field_needs_can_be_installed()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        OGSim.Facilities.Manifold header = engine.Provided.Resolve<SurfaceChain>().Manifold;

        int before = header.Slots;

        Assert.IsType<Accepted>(engine.Commands.Submit(new InstallManifoldCommand()));

        for (var month = 0; month < 12; month++) engine.Pipeline.AdvanceTick();

        Assert.True(header.Slots > before,
            $"the header was installed and still takes {header.Slots} wells");

        // AND THE FIELD STILL FLOWS. Growing a header must not move the outlet
        // the flowline is already connected to — the registry is write-once and
        // has no removal, so a moved outlet would leave the trunk pointing at
        // what had become a slot.
        Assert.True(engine.ReadModel!.ProducedThisTick.CubicMetres > 0.0,
            "the header grew and the field stopped producing; the outlet moved out from " +
            "under the trunk");

        // At the top of the ladder there is nothing further to fit, and the
        // player is told so rather than charged for a month of nothing.
        Assert.IsType<Rejected>(engine.Commands.Submit(new InstallManifoldCommand()));
    }

    /// <summary>
    /// THE THIRD ANSWER TO A FULL TANK. Stage 6's own comment offers "more
    /// storage, more export and less production" as what a player does when the
    /// ullage constraint reaches back down the chain and shuts wells in. Two of
    /// those shipped; storage was the one nothing could buy.
    ///
    /// <para>It buys TIME rather than throughput, which is what makes it a
    /// different decision from a bigger export line — storage carries a field
    /// through a gap, a pipeline carries it faster for ever.</para>
    /// </summary>
    [Fact]
    public void R8V5_more_storage_is_something_a_company_can_buy()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        OGSim.Facilities.Tank tank = engine.Provided.Resolve<SurfaceChain>().Tank;

        Mass before = tank.Tier.Capacity;

        Assert.IsType<Accepted>(engine.Commands.Submit(new InstallTankCommand()));

        for (var month = 0; month < 12; month++) engine.Pipeline.AdvanceTick();

        Assert.True(tank.Tier.Capacity.Kilograms > before.Kilograms,
            $"the tank farm was built and still holds {tank.Tier.Capacity.Kilograms} kg");

        // AND WHAT WAS ALREADY IN IT IS STILL IN IT. A socket keeps its contents
        // when a bigger one is fitted around it; a refit that emptied the tank
        // would destroy owned mass the conservation check would never see,
        // because it left through no port at all.
        Assert.True(engine.ReadModel!.ProducedThisTick.CubicMetres > 0.0,
            "the tank grew and the field stopped producing");

        Assert.IsType<Rejected>(engine.Commands.Submit(new InstallTankCommand()));
    }


    // ------------------------------- the late-life arc, pinned (finding 179)

    /// <summary>
    /// A FIELD MAKES MORE WATER AS IT AGES, and the ending is built on it:
    /// watering out is what makes opex outrun revenue, which is what makes
    /// shutting in and plugging a decision rather than a formality.
    ///
    /// <para>NOTHING ASSERTED THIS, and it regressed. R20.4 measured this
    /// composition drowning — water climbing to 47,529 t a month against 27,371
    /// m³ of oil — and it now reaches two per cent of the liquid by mass after
    /// forty years. Three correct changes touched it (the per-compartment
    /// aquifer, inflow read from the rock, produced water re-injected) and every
    /// suite stayed green because none of them was about water.</para>
    ///
    /// <para>DIRECTIONAL, not a pin on today's number. It asserts the cut rises
    /// and clears a floor below where it currently sits — so a fix that restores
    /// the arc passes, and a change that takes more of the water away fails.
    /// Pinning 0.019 exactly would enshrine a value finding 179 says is wrong,
    /// and the next person to fix it would meet a red test telling them not
    /// to.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20d4V2_a_field_makes_more_water_as_it_ages()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        // A DEVELOPED FIELD, not a single well. How much water a field makes
        // depends on how hard it is produced — one well drains 13% of the oil in
        // forty years and leaves the reservoir near its opening pressure, so the
        // aquifer has nothing to push into and the cut stays near connate.
        //
        // Measured: one well gives 20,000 m³ a month and a 2% cut; the same
        // field on better rock gives 36,000 and 20%. Watering out is a
        // consequence of OFFTAKE, and a test that drilled once was measuring a
        // field nobody had developed (finding 179's retraction).
        FieldControl field = engine.Provided.Resolve<FieldControl>();

        for (var well = 0; well < 5; well++) field.Drill(target, new Length(2000.0));

        double early = 0.0, late = 0.0;

        for (var month = 0; month < 480; month++)
        {
            Fixture.Repair(engine);
            engine.Pipeline.AdvanceTick();

            double water = Throughput(engine, "water-disposal");
            double oil = Throughput(engine, "custody-meter");

            if (oil <= 0.0) continue;

            if (month == 240) early = water / oil;
            if (month == 479) late = water / oil;
        }

        Assert.True(early > 0.0, "a field twenty years old was making no water at all");

        Assert.True(late > early * 2.0,
            $"the water cut went from {early:0.0000} to {late:0.0000} in twenty years; a " +
            "field that does not water out has no late life to survive");

        // The floor sits below where it stands today (0.019) so a restored arc
        // passes, and below it means water has gone missing again.
        Assert.True(late > 0.015,
            $"water is down to {late:0.0000} of the liquid after forty years; finding 179's " +
            "regression has gone further");
    }

    /// <summary>What crossed a named element this tick, read off the surface.</summary>
    private static double Throughput(Engine engine, string named)
    {
        for (int i = 0; i < engine.ReadModel!.Chain.Count; i++)
            if (engine.ReadModel!.Chain[i].DisplayId == named)
                return engine.ReadModel!.Chain[i].Throughput.Kilograms;

        return 0.0;
    }


    /// <summary>
    /// THE ENDING, ASSERTED. A developed field's monthly cash flow decays as it
    /// waters out — opex is charged on the LIQUID lifted and water is liquid, so
    /// a field making four barrels of water for one of oil pays to lift all five
    /// and is paid for one.
    ///
    /// <para>That decay is what makes shutting in and plugging a decision rather
    /// than a formality, and it is the whole of the late game. R20.4 measured it
    /// and nothing asserted it — which is how finding 179 came to be a wrong
    /// claim about the arc that stood for four commits.</para>
    ///
    /// <para>MEASURED OVER DECADES, not at two anniversaries. This asked whether
    /// month 60 in particular was profitable, and that month clears zero by
    /// $17M on the shipped seed — about a tenth of a good year — while the same
    /// year ranges from $17M to $135M across four seeds and the field is shut in
    /// for 17% of its life whatever anybody does. So the old assertion was not
    /// measuring whether a young field pays; it was measuring whether seed
    /// 20260806 happened to have a quiet fifth year, and R20d.26 tipped it
    /// negative by adding one month of outage to that year (finding 187).</para>
    ///
    /// <para>The first decade against the last is the same claim with a margin
    /// that means something: roughly +$350M against a last decade that is
    /// negative on every seed, because a field this old is paying to lift water.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20d4V3_a_developed_field_ends_by_earning_less_every_year()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        FieldControl field = engine.Provided.Resolve<FieldControl>();

        for (var well = 0; well < 5; well++) field.Drill(target, new Length(2000.0));

        CompanyState company = engine.Provided.Resolve<CompanyState>();

        Money opened = company.Ledger.Cash;
        Money atTen = Money.Zero, atThirty = Money.Zero;

        for (var month = 0; month < 480; month++)
        {
            Fixture.Repair(engine);
            engine.Pipeline.AdvanceTick();

            if (month == 119) atTen = company.Ledger.Cash;
            if (month == 359) atThirty = company.Ledger.Cash;
        }

        Money firstDecade = atTen - opened;
        Money lastDecade = company.Ledger.Cash - atThirty;

        Assert.True(firstDecade > Money.Zero,
            $"a developed field's first decade lost {firstDecade}; if a field never pays " +
            "there is nothing to decide about how long to keep it");

        Assert.True(lastDecade < firstDecade,
            $"the field earned {lastDecade} in its last decade against {firstDecade} in its " +
            "first; a field that never gets worse has no ending and nothing to decide about");
    }

    // ------------------------------------- wet oil, and drying it (R20d.21)

    /// <summary>
    /// A DEVELOPED FIELD THAT WATERS OUT SELLS WET OIL, and the meter turns it
    /// away. The separator carries some water into the liquid leg, and late in
    /// life a field makes a fifth of a barrel of water for every barrel of oil —
    /// so BS&amp;W at the meter passes the half-per-cent sales limit and the
    /// stream routes to the reject leg.
    ///
    /// <para>AND A TREATER IS THE ANSWER, which is what makes this a decision
    /// rather than a tax on getting old: the oil is there and it is worth money,
    /// it just cannot be sold with the water in it.</para>
    ///
    /// <para>A DEVELOPED field, and that is the whole reason this test exists in
    /// this shape. The first attempt measured a single well, whose cut never
    /// leaves 2%, found a treater that removed 0.0003 kg/s, and was reverted
    /// (finding 178). The carry-over is now solved against a measured cut rather
    /// than taken from a plausible sentence.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20d21V1_a_watered_out_field_sells_wet_oil_until_it_is_treated()
    {
        Money soldWet = Earned(treated: false);
        Money soldDry = Earned(treated: true);

        Assert.True(soldDry > soldWet,
            $"a field that dried its oil earned {soldDry} against {soldWet} for selling it " +
            "wet; either the spec never bites or the treater does not fix it");
    }

    private static Money Earned(bool treated)
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        FieldControl field = engine.Provided.Resolve<FieldControl>();

        for (var well = 0; well < 5; well++) field.Drill(target, new Length(2000.0));

        if (treated) engine.Commands.Submit(new InstallTreaterCommand());

        Fixture.Run(engine, months: 480);

        // Revenue is credited, so what was earned is the negation of the balance.
        return -engine.Provided.Resolve<CompanyState>().Ledger.BalanceOf(Account.Revenue);
    }

    // ------------------------------------ equipment that wears out (R20d.22)

    /// <summary>
    /// EQUIPMENT AGES. Until R20d.22 the integrity module composed two correct
    /// models, declared no state and ran in no stage, so nothing in a running
    /// game could reach them — a separator was as good after forty years as on
    /// the day it was installed.
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20d22V2_equipment_wears_out_as_the_field_runs()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        FieldControl field = engine.Provided.Resolve<FieldControl>();
        field.Drill(target, new Length(2000.0));

        Fixture.Run(engine, months: 120);

        double worst = 1.0;

        foreach (ChainElementView element in engine.ReadModel!.Chain)
            if (element.Condition < worst) worst = element.Condition;

        Assert.True(worst < 1.0,
            "ten years of service and nothing in the chain had aged at all");
    }

    /// <summary>
    /// AND WHEN IT BREAKS, THE FIELD STOPS — then starts again when it is fixed.
    ///
    /// <para>Two runs of the same field on the same seed, differing only in
    /// whether anybody maintains it. The one nobody maintains dies at its first
    /// unlucky draw and never earns another dollar, because a failed element is
    /// absent from the network and the route law shuts in everything behind it.
    /// The one that is maintained pays for repairs and keeps producing.</para>
    ///
    /// <para>This is the test that says the mechanic is a DECISION rather than a
    /// tax: the difference between the two numbers is what maintenance is
    /// worth, and it is worth more than it costs.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20d22V3_a_field_nobody_maintains_stops_and_a_maintained_one_does_not()
    {
        Money neglected = Lifetime(maintained: false);
        Money maintained = Lifetime(maintained: true);

        // MEASURED at $7.46bn against $0.56bn — thirteen times, because the
        // neglected field does not merely earn less, it STOPS: the first failure
        // it does not answer shuts the chain in permanently. The gap is the
        // mechanic, and it is far too large to be a rounding effect dressed up
        // as a decision (finding 178).
        Assert.True(maintained > neglected,
            $"a maintained field earned {maintained} against {neglected} for a neglected one; " +
            "either nothing ever breaks or repairing it is not worth the money");
    }

    private static Money Lifetime(bool maintained)
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        FieldControl field = engine.Provided.Resolve<FieldControl>();

        for (var well = 0; well < 5; well++) field.Drill(target, new Length(2000.0));

        for (var month = 0; month < 480; month++)
        {
            if (maintained) Fixture.Repair(engine);
            engine.Pipeline.AdvanceTick();
        }

        // Revenue LESS what the repairs cost, which is the only comparison that
        // makes this a decision: a test that measured revenue alone would say
        // maintenance is free and always right.
        return -engine.Provided.Resolve<CompanyState>().Ledger.BalanceOf(Account.Revenue)
             + engine.Provided.Resolve<CompanyState>().Ledger.BalanceOf(Account.Opex);
    }

    /// <summary>
    /// MAINTENANCE HAS A WRONG END AS WELL AS A RIGHT ONE, which is what makes
    /// SDD-012 §3's three strategies a decision rather than a difficulty
    /// setting.
    ///
    /// <para>A company that overhauls everything the moment it is less than new
    /// never has a failure and never has any money either: an overhaul is a
    /// month of that element's life and a bill, and buying back condition the
    /// hazard curve was barely charging for is the most expensive way to run a
    /// field. Measured across four triggers on one field — run-to-failure
    /// $1,776M, at 0.4 $1,842M, at 0.7 $1,771M, at 0.9 $1,374M — so the good
    /// answer is INTERIOR, and both ends are worse than the middle.</para>
    ///
    /// <para>Only the outer comparison is pinned. The interior peak is a 4%
    /// edge on one seed and asserting it would be fitting a test to a run;
    /// over-maintaining costing a quarter of the company is a margin that means
    /// something. R20d.22 is not a chore a player has to remember — it is a
    /// number they can get wrong in both directions.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20d22V4_over_maintaining_costs_more_than_letting_things_break()
    {
        Money always = Strategy(repairBelow: 0.9);
        Money onFailure = Strategy(repairBelow: 0.0);

        Assert.True(onFailure > always,
            $"repairing everything constantly earned {always} against {onFailure} for waiting " +
            "until things broke; if maintenance is never wasteful it is not a decision");
    }

    /// <summary>
    /// Forty years on a developed field, repairing anything broken and anything
    /// worn past <paramref name="repairBelow"/>. A trigger of zero is
    /// run-to-failure — the condition test can never fire, so only failures are
    /// answered.
    /// </summary>
    private static Money Strategy(double repairBelow)
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        FieldControl field = engine.Provided.Resolve<FieldControl>();

        for (var well = 0; well < 5; well++) field.Drill(target, new Length(2000.0));

        for (var month = 0; month < 480; month++)
        {
            FieldReadModel? seen = engine.ReadModel;

            if (seen is not null)
                for (var i = 0; i < seen.Chain.Count; i++)
                    if (seen.Chain[i].Failed || seen.Chain[i].Condition < repairBelow)
                        engine.Commands.Submit(new RepairEquipmentCommand(seen.Chain[i].Element));

            engine.Pipeline.AdvanceTick();
        }

        return engine.Provided.Resolve<CompanyState>().Ledger.Cash;
    }

    // ------------------------------------------ the waterflood (R20d.24)

    /// <summary>
    /// A FIELD ON A SOLUTION-GAS DRIVE HAS NO ENERGY OF ITS OWN, and a flood is
    /// the answer — which is the oldest decision in reservoir management and the
    /// one this engine had every part of except the water.
    ///
    /// <para>Produced water already went back down the hole, but a field can
    /// only put back what it makes, and early in life it makes almost none —
    /// exactly when support is worth most. Measured before this shipped: 0.0033
    /// pore volumes in forty years, against 0.1–1 for a real flood (finding
    /// 182). The decision is IMPORTED water.</para>
    ///
    /// <para>The margin is a MULTIPLE rather than a percentage, so it is not a
    /// measurement that a different failure sequence could reverse: 2.1% of the
    /// oil unflooded against 22.1% flooded, and the unflooded company ends the
    /// run insolvent.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20d24V1_a_field_with_no_drive_of_its_own_is_transformed_by_a_flood()
    {
        (double natural, Money broke, double boughtNothing) = Flooded(Dead, vrr: 0.0);
        (double flooded, Money rich, double bought) = Flooded(Dead, vrr: 1.0);

        Assert.True(boughtNothing == 0.0,
            $"a field nobody ordered a flood on bought {boughtNothing} m³ of water");

        Assert.True(bought > 0.0,
            "a field ordered to replace its voidage bought no water at all");

        Assert.True(flooded > natural * 3.0,
            $"a flooded field recovered {flooded:F0} m³ against {natural:F0} unflooded; " +
            "secondary recovery that does not multiply primary is not secondary recovery");

        Assert.True(rich > broke,
            $"the flood earned {rich} against {broke} for leaving the oil in the ground");
    }

    /// <summary>
    /// AND ON A FIELD THE AQUIFER ALREADY SUPPORTS, THE SAME ORDER IS A LOSS.
    ///
    /// <para>This is the half that makes it a decision rather than a button. The
    /// water is already arriving and nobody is paying for it, so buying more
    /// costs money and brings the breakthrough forward for nothing — measured at
    /// $71M on a $1.79bn company. A player therefore has to work out WHICH
    /// RESERVOIR THEY ARE STANDING ON before pulling the lever, which is the
    /// question the whole information game exists to make them answer.</para>
    ///
    /// <para>Both runs are the same seed and the same element set, so the
    /// failure sequence is identical and the difference is the flood rather than
    /// the dice.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20d24V2_flooding_a_field_the_aquifer_already_supports_is_a_loss()
    {
        (double _, Money left, double _) = Flooded(Supported, vrr: 0.0);
        (double _, Money spent, double bought) = Flooded(Supported, vrr: 1.0);

        Assert.True(bought > 0.0,
            "the field bought no water, so this measures nothing");

        Assert.True(spent < left,
            $"a company that flooded an aquifer-supported field ended with {spent} against " +
            $"{left} for leaving it alone; the flood has no cost and is therefore no decision");
    }

    /// <summary>
    /// A FLOOD CANNOT PUT BACK MORE THAN THE FIELD HAS TAKEN OUT (SDD-003
    /// §3.1's R20d.24b amendment §0).
    ///
    /// <para>Not a balance choice — the balance's own ceiling. §3.1's bisection
    /// searches up to the discovery pressure and FAULTS when there is no root in
    /// range, so a compartment given more replacement than it has voidage does
    /// not produce a wrong number, it halts the tick. That is exactly what VRR
    /// 1.0 did on the shipped water-drive field before the cap existed.</para>
    ///
    /// <para>So VRR 2 is not twice the flood; it is "catch up as fast as the
    /// well allows", and it stops where the rock does.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20d24V3_a_flood_cannot_put_back_more_than_the_field_took_out()
    {
        (double _, Money _, double atOne) = Flooded(Dead, vrr: 1.0);
        (double _, Money _, double atTwo) = Flooded(Dead, vrr: 2.0);

        Assert.True(atOne > 0.0, "the field bought no water, so this measures nothing");

        // Not "roughly equal": the ceiling is a hard one, so twice the target
        // buys at most a rounding more than replacing the voidage exactly.
        Assert.True(atTwo < atOne * 1.05,
            $"VRR 2 bought {atTwo:F0} m³ against VRR 1's {atOne:F0}; the reservoir ceiling " +
            "is not holding and the balance will fault the first time it is exceeded");
    }

    /// <summary>
    /// The lever refuses what is meaningless and what would change nothing
    /// (R1 §2.5) — and has no ceiling of its own, because the rock is the
    /// ceiling and a second one could disagree with it.
    /// </summary>
    [Fact]
    public void R20d24V4_the_flood_target_refuses_what_is_not_a_ratio()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        Assert.IsType<Rejected>(engine.Commands.Submit(new SetVoidageReplacementCommand(-1.0)));

        // Already there: the field ships at zero, so ordering zero changes
        // nothing and a player acting on a stale read model is told so.
        Assert.IsType<Rejected>(engine.Commands.Submit(new SetVoidageReplacementCommand(0.0)));

        // And no invented upper bound. An absurd ratio is accepted and then
        // clamped by the injector and the rock, which are the real limits.
        Assert.IsType<Accepted>(engine.Commands.Submit(new SetVoidageReplacementCommand(10.0)));
    }

    // ------------------------------------------ the reservoir sours (R20d.25)

    /// <summary>
    /// A FLOOD SOURS THE RESERVOIR AND NOTHING ELSE DOES (SDD-012 §5).
    ///
    /// <para>This is the whole of finding 182's correction in one assertion.
    /// Souring was built once against total injection, and could not fire: a
    /// field that only reinjects what it produces puts 0.0033 pore volumes
    /// through in forty years. But the volume was the smaller half of the error
    /// — reinjected produced water has already been through the rock, and is
    /// anoxic, reduced and stripped of the sulphate the bacteria eat. It is the
    /// fluid that sours a reservoir LEAST. So a field with a disposal well and
    /// no flood must stay EXACTLY sweet, for ever, and it does.</para>
    ///
    /// <para>MONOTONIC, which §5 pins: water already injected cannot un-sour a
    /// reservoir. Asserted every month rather than at the ends, because the
    /// first version of this read the sourness off the compartments that
    /// PRODUCED and so reported zero for any month the chain was down — a
    /// soured reservoir healing itself every time a separator broke.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20d25V1_a_flood_sours_the_reservoir_and_a_disposal_well_never_does()
    {
        (double sweet, bool _) = Sourness(Supported, vrr: 0.0);
        (double soured, bool climbed) = Sourness(Supported, vrr: 1.0);

        Assert.True(sweet == 0.0,
            $"a field that only put back the water it made reached a sourness of {sweet}; " +
            "produced water is the fluid that sours a reservoir least and this must be zero");

        Assert.True(soured > 0.5,
            $"forty years of seawater flood left the reservoir at {soured}; the curve is not " +
            "firing at the throughput a real flood puts through");

        Assert.True(climbed,
            "the sourness fell at least once — water already injected cannot un-sour a " +
            "reservoir, so a reading that drops is a reading taken from the wrong thing");
    }

    /// <summary>
    /// AND THE H2S ARRIVES IN THE MAINTENANCE BILL, twenty years after the
    /// decision that bought it (SDD-012 §1's sour severity term).
    ///
    /// <para>Which is what makes this a consequence rather than a tax. The
    /// response to a soured field is the flood decision itself, taken two
    /// decades earlier: flood a reservoir that needed it and the recovery pays
    /// for the corrosion many times over (R20d24V1); flood one the aquifer
    /// already supported and there was nothing to win in the first place, so the
    /// bill is all there is. This measures the second case, because it is the
    /// one where souring is visible on its own.</para>
    ///
    /// <para>Both runs are the same seed with the same element set, and the
    /// margin is a fifth rather than a percent (finding 184).</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20d25V2_souring_arrives_in_the_maintenance_bill()
    {
        int sweet = Overhauls(Supported, vrr: 0.0);
        int soured = Overhauls(Supported, vrr: 1.0);

        Assert.True(soured > sweet * 1.1,
            $"a soured field needed {soured} overhauls against {sweet} for a sweet one; " +
            "if H2S does not eat the plant then souring costs a company nothing");
    }

    /// <summary>Forty years: the sourness the field ends at, and whether it ever
    /// fell on the way.</summary>
    private static (double Final, bool NeverFell) Sourness(double aquifer, double vrr)
    {
        Engine engine = Flooding(aquifer, vrr);

        var last = 0.0;
        var neverFell = true;

        for (var month = 0; month < 480; month++)
        {
            Fixture.Repair(engine);
            engine.Pipeline.AdvanceTick();

            double now = engine.ReadModel!.Flood.Sourness;
            if (now < last) neverFell = false;
            last = now;
        }

        return (last, neverFell);
    }

    /// <summary>Forty years: how many overhauls the company actually paid for.</summary>
    private static int Overhauls(double aquifer, double vrr)
    {
        Engine engine = Flooding(aquifer, vrr);
        var paid = 0;

        for (var month = 0; month < 480; month++)
        {
            FieldReadModel? seen = engine.ReadModel;

            if (seen is not null)
                for (var i = 0; i < seen.Chain.Count; i++)
                    if (seen.Chain[i].Failed
                        && engine.Commands.Submit(
                            new RepairEquipmentCommand(seen.Chain[i].Element)) is Accepted)
                        paid++;

            engine.Pipeline.AdvanceTick();
        }

        return paid;
    }

    /// <summary>A compartment with no aquifer: everything it gives up, it gives
    /// up from its own expansion, which runs out fast.</summary>
    private const double Dead = 0.0;

    /// <summary>The shipped field's aquifer — four pore volumes of water behind
    /// it, arriving over decades.</summary>
    private const double Supported = Defaults.AquiferStrength;

    /// <summary>
    /// Forty years of a six-well field, flooded or not: what it recovered, what
    /// the company ended with, and how much water it bought.
    /// </summary>
    private static (double Recovered, Money Cash, double Bought) Flooded(
        double aquifer, double vrr)
    {
        Engine engine = Flooding(aquifer, vrr);

        var recovered = 0.0;
        var bought = 0.0;

        for (var month = 0; month < 480; month++)
        {
            Fixture.Repair(engine);
            engine.Pipeline.AdvanceTick();

            FieldReadModel seen = engine.ReadModel!;
            recovered += seen.ProducedThisTick.CubicMetres;
            bought += seen.Flood.Imported.CubicMetres;
        }

        return (recovered, engine.Provided.Resolve<CompanyState>().Ledger.Cash, bought);
    }

    /// <summary>
    /// A six-well field on a reservoir with the stated aquifer, ordered to
    /// replace the stated share of its voidage — the fixture both the flood and
    /// the souring measurements are taken on.
    /// </summary>
    private static Engine Flooding(double aquifer, double vrr)
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
            Defaults.Wettability,

            // A DRIVE THAT ADMITS INJECTION EITHER WAY. What separates the two
            // fields here is the AQUIFER, not the drive's name: the balance
            // cannot tell aquifer water from injected water and neither can the
            // reservoir, which is why the waterflood drive admits both.
            aquifer > 0.0 ? Defaults.Drive : new ContentId("solution-gas-drive"),
            aquifer,
            Defaults.AquiferResponseTime);

        engine.Provided.Resolve<WorldState>()
            .DeclareKnownField(target, new ReservoirVolume(100.0e6));

        for (var well = 0; well < 6; well++) field.Drill(target, new Length(2000.0));

        if (vrr > 0.0) engine.Commands.Submit(new SetVoidageReplacementCommand(vrr));

        return engine;
    }
}
