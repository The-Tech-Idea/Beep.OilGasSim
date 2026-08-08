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
                permeability: new Permeability(2.0e-13),
                netThickness: new Length(30.0),
                drainageArea: new Area(2.0e5),
                rockCompressibility: 4.5e-10,
                gasOilContact: new Length(1900.0),
                oilWaterContact: new Length(2100.0),
                Defaults.Wettability, Defaults.Drive);

        return (built.Engine, target);
    }

    /// <summary>Opens a well directly, the way world generation will — no rig, no
    /// four months, no dice. What is under test is the chain, not drilling.</summary>
    private static void Produce(Engine engine, EntityId<IReservoirCompartmentEntity> target)
    {
        FieldControl field = engine.Provided.Resolve<FieldControl>();

        field.OpenWell(Defaults.CompletionFor(field.NextWellId(), target, new Length(2000.0)),
                       target);
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
        OGSim.Wells.Completion well = Defaults.CompletionFor(1, target, new Length(2000.0));
        well.SetReservoirConditions(
            new Pressure(30.0e6), Defaults.ReservoirTemperature,
            engine.Provided.Resolve<IFluidPropertyModel>().Rs(new Pressure(30.0e6)));

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
            ["well-1", "manifold", "separator", "custody-meter", "flare"],
            engine.ReadModel!.Chain.Select(element => element.DisplayId));
    }

    /// <summary>Every element reports what crossed it, so a host can show the
    /// flow rather than only its two ends.</summary>
    [Fact]
    public void R20dV1_every_element_reports_what_crossed_it()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();
        Produce(engine, target);

        engine.Pipeline.AdvanceTick();

        foreach (ChainElementView element in engine.ReadModel!.Chain)
            Assert.True(element.Throughput.Kilograms > 0.0,
                $"{element.DisplayId} shows no throughput, so a host cannot draw the flow");
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
            engine.Commands.Submit(new DrillWellCommand(target, new Length(2000.0))));

        Assert.Contains(rejected.Reasons,
            reason => reason.LocId == "$loc:reject.no-manifold-slot");
    }
}
