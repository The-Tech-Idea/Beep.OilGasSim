// R20c.7 — the reservoir as a living entity across ticks (SDD-003 §3).
//
// Everything before this proved the material balance against inputs a test
// assembled. These prove the thing the game needs: a reservoir that remembers
// what it gave up, loses pressure because of it, and comes back from a save as
// the same reservoir.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Persistence;
using OGSim.Subsurface;

namespace OGSim.Subsurface.Tests;

public sealed class SubsurfaceStateTests
{
    private static IFluidPropertyModel Fluid() =>
        new BlackOilModel(
            new BlackOilInputs(
                OilGravity: new ApiGravity(35.0),
                GasSpecificGravity: 0.75,
                ReservoirTemperature: Temperature.FromCelsius(93.3),
                SolutionGorAtBubblePoint: 100.0,
                Form: FluidForm.BlackOil),
            new ValidityRange(
                new Pressure(500.0), new Pressure(60e6),
                Temperature.FromCelsius(10.0), Temperature.FromCelsius(180.0)));

    private static SubsurfaceState Fresh() =>
        new(Fluid(), new SolutionGasDrive(), maxTickPressureDropFraction: 0.2);

    /// <summary>A million cubic metres of pore volume at 30 MPa — a small field
    /// that depletes visibly over a handful of years.</summary>
    private static GeneratedCompartment Generated() => new(
        PoreVolume: new ReservoirVolume(1.0e6),
        Porosity: 0.22,
        OilSaturation: 0.7,
        InitialPressure: new Pressure(30.0e6),
        Temperature: Temperature.FromCelsius(93.3),
        Depth: new Length(2000.0));

    private static EntityId<IReservoirCompartmentEntity> Add(SubsurfaceState state) =>
        state.Create(
            Generated(),
            permeability: new Permeability(1.0e-13),
            netThickness: new Length(20.0),
            drainageArea: new Area(2.0e5),
            rockCompressibility: 4.5e-10,
            gasOilContact: new Length(1900.0),
            oilWaterContact: new Length(2100.0),

            // A water-wet sandstone. These tests are about the material balance
            // rather than the S-curve, but a compartment without a curve could
            // not answer what its water cut is at all.
            RelativePermeabilityCurve.Validated(
                swc: 0.30, sor: 0.25, krwMax: 0.35, kroMax: 0.90, nw: 3.0, no: 2.0),
            new ContentId("solution-gas-drive"));

    private static CompartmentWithdrawal Produce(
        EntityId<IReservoirCompartmentEntity> id, double stockTankOil) =>
        new(id,
            Oil: new SurfaceVolume(stockTankOil),
            Gas: new StandardGasVolume(0.0),
            Water: new SurfaceVolume(0.0),
            Influx: new ReservoirVolume(0.0),
            Injected: new ReservoirVolume(0.0),
            ReservoirVolume: new ReservoirVolume(stockTankOil * 1.2));

    [Fact]
    public void A_new_compartment_opens_at_its_initial_pressure()
    {
        SubsurfaceState state = Fresh();
        EntityId<IReservoirCompartmentEntity> id = Add(state);

        Assert.Equal(1, state.Count);
        Assert.Equal(30.0e6, state.TruePressureOf(id).Pascals, 6);
    }

    /// <summary>
    /// The headline: producing oil costs pressure. Without this the reservoir is
    /// an infinite tank and every decision in the game is free.
    /// </summary>
    [Fact]
    public void Producing_oil_drops_the_pressure()
    {
        SubsurfaceState state = Fresh();
        EntityId<IReservoirCompartmentEntity> id = Add(state);

        state.CommitTick([Produce(id, stockTankOil: 5_000.0)]);

        Assert.True(state.TruePressureOf(id).Pascals < 30.0e6,
            "a compartment that gave up oil must have lost pressure");
    }

    /// <summary>
    /// Depletion accumulates: the second identical month costs more pressure in
    /// total than the first, because the balance is solved from CUMULATIVE
    /// production rather than stepped from last month's answer.
    /// </summary>
    [Fact]
    public void Depletion_accumulates_across_ticks()
    {
        SubsurfaceState state = Fresh();
        EntityId<IReservoirCompartmentEntity> id = Add(state);

        state.CommitTick([Produce(id, 5_000.0)]);
        double afterOne = state.TruePressureOf(id).Pascals;

        state.CommitTick([Produce(id, 5_000.0)]);
        double afterTwo = state.TruePressureOf(id).Pascals;

        Assert.True(afterTwo < afterOne,
            "the second month of production must leave the reservoir lower than the first");
    }

    /// <summary>
    /// A tick in which nothing was produced must not move the reservoir. This is
    /// the composed engine's current state — no completion exists yet — and it
    /// has to be a real answer rather than a skipped stage.
    /// </summary>
    [Fact]
    public void A_tick_with_no_withdrawal_leaves_the_pressure_alone()
    {
        SubsurfaceState state = Fresh();
        EntityId<IReservoirCompartmentEntity> id = Add(state);

        state.CommitTick([]);
        state.CommitTick([]);

        Assert.Equal(30.0e6, state.TruePressureOf(id).Pascals, 6);
    }

    [Fact] // INV3: a withdrawal naming nothing is a fault, not a no-op
    public void A_withdrawal_naming_an_unknown_compartment_faults()
    {
        SubsurfaceState state = Fresh();
        Add(state);

        Assert.Throws<InvariantFault>(() => state.CommitTick(
            [Produce(new EntityId<IReservoirCompartmentEntity>(99), 100.0)]));
    }

    /// <summary>
    /// The reservoir survives a save — and comes back at the pressure the
    /// material balance says it must be, because the restore re-solves rather
    /// than reading a stored number.
    /// </summary>
    [Fact]
    public void A_depleted_reservoir_restores_to_the_same_pressure()
    {
        SubsurfaceState captured = Fresh();
        EntityId<IReservoirCompartmentEntity> id = Add(captured);

        captured.CommitTick([Produce(id, 5_000.0)]);
        captured.CommitTick([Produce(id, 3_000.0)]);

        double expected = captured.TruePressureOf(id).Pascals;

        SubsurfaceState restored = Fresh();
        StateBlock.Restore(restored, StateBlock.Capture(captured).Written());

        Assert.Equal(1, restored.Count);
        Assert.Equal(expected, restored.TruePressureOf(id).Pascals, 6);
    }

    /// <summary>
    /// And it keeps producing correctly afterwards: the cumulative history came
    /// back, not just the pressure it implied.
    /// </summary>
    [Fact]
    public void A_restored_reservoir_continues_depleting_from_where_it_stopped()
    {
        SubsurfaceState captured = Fresh();
        EntityId<IReservoirCompartmentEntity> id = Add(captured);
        captured.CommitTick([Produce(id, 5_000.0)]);

        SubsurfaceState restored = Fresh();
        StateBlock.Restore(restored, StateBlock.Capture(captured).Written());

        captured.CommitTick([Produce(id, 5_000.0)]);
        restored.CommitTick([Produce(id, 5_000.0)]);

        Assert.Equal(
            captured.TruePressureOf(id).Pascals,
            restored.TruePressureOf(id).Pascals, 6);
    }

    /// <summary>
    /// SDD-013 §4: pressure is DERIVED and is not written. A save that could
    /// assert a pressure would let a hand-edited file claim one the material
    /// balance never produced.
    /// </summary>
    [Fact]
    public void The_save_carries_no_pressure()
    {
        SubsurfaceState state = Fresh();
        EntityId<IReservoirCompartmentEntity> id = Add(state);
        state.CommitTick([Produce(id, 5_000.0)]);

        string written = CanonicalJson.Write(StateBlock.Capture(state).Written());

        Assert.DoesNotContain("\"compartment.000000.pressure\"", written, StringComparison.Ordinal);
        Assert.Contains("initial-pressure", written, StringComparison.Ordinal);
        Assert.Contains("np", written, StringComparison.Ordinal);
    }

    [Fact] // Two compartments deplete independently
    public void Compartments_deplete_independently()
    {
        SubsurfaceState state = Fresh();
        EntityId<IReservoirCompartmentEntity> drained = Add(state);
        EntityId<IReservoirCompartmentEntity> untouched = Add(state);

        state.CommitTick([Produce(drained, 5_000.0)]);

        Assert.True(state.TruePressureOf(drained).Pascals < 30.0e6);
        Assert.Equal(30.0e6, state.TruePressureOf(untouched).Pascals, 6);
    }

    /// <summary>
    /// A step the model cannot honestly represent is refused, and refusing must
    /// leave the compartment untouched — an abandoned tick that had already
    /// moved the reservoir would be a partial commit.
    /// </summary>
    [Fact]
    public void A_refused_step_leaves_the_compartment_unchanged()
    {
        SubsurfaceState state = Fresh();
        EntityId<IReservoirCompartmentEntity> id = Add(state);

        double before = state.TruePressureOf(id).Pascals;

        // Far more than the reservoir holds: the solve cannot land inside the
        // per-tick step limit.
        Assert.ThrowsAny<FaultException>(() => state.CommitTick([Produce(id, 10_000_000.0)]));

        Assert.Equal(before, state.TruePressureOf(id).Pascals, 6);
    }
}
