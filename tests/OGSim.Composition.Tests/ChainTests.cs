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
                oilWaterContact: new Length(2100.0));

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
        well.SetReservoirConditions(new Pressure(30.0e6), Defaults.ReservoirTemperature);

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
