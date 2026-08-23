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
        new(new Dictionary<ContentId, IFluidPropertyModel> { [FluidSystemId] = Fluid() },
            new SolutionGasDrive(), Souring.SweetRock, Souring.TheRock,
            Souring.SouringReference, maxTickPressureDropFraction: 0.2);

    private static readonly ContentId FluidSystemId = new("medium-crude");

    /// <summary>A million cubic metres of pore volume at 30 MPa — a small field
    /// that depletes visibly over a handful of years.</summary>
    private static GeneratedCompartment Generated() => new(
        PoreVolume: new ReservoirVolume(1.0e6),
        Porosity: 0.22,
        OilSaturation: 0.7,
        InitialPressure: new Pressure(30.0e6),
        Temperature: Temperature.FromCelsius(93.3),
        Depth: new Length(2000.0),
        FluidSystem: FluidSystemId);

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
            new ContentId("solution-gas-drive"),

            // NO AQUIFER. These tests supply influx directly as a withdrawal
            // term, which is what stage 6 does — a compartment that also made
            // its own would be counted twice (SDD-003 §3.3a).
            aquiferStrength: 0.0,
            Duration.FromTicks(1.0));

    private static CompartmentWithdrawal Produce(
        EntityId<IReservoirCompartmentEntity> id, double stockTankOil) =>
        new(id,
            Oil: new SurfaceVolume(stockTankOil),
            Gas: new StandardGasVolume(0.0),
            Water: new SurfaceVolume(0.0),
            Influx: new ReservoirVolume(0.0),
            Injected: new ReservoirVolume(0.0),
            Imported: new ReservoirVolume(0.0),
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
    /// R20d.9's finding-270 amendment: TWO compartments, identically shaped,
    /// naming DIFFERENT fluid systems. If <c>SubsurfaceState</c> silently fell
    /// back to one shared model, they would compute the same voidage room —
    /// this is the test that would fail if <c>FluidFor</c> were ever replaced
    /// with the old single <c>_fluid</c> field.
    /// </summary>
    [Fact]
    public void A_compartments_own_fluid_system_is_used_not_a_shared_default()
    {
        var light = new ContentId("light-crude");
        var heavy = new ContentId("heavy-crude");

        var state = new SubsurfaceState(
            new Dictionary<ContentId, IFluidPropertyModel>
            {
                [light] = Model(45.0),
                [heavy] = Model(18.0),
            },
            new SolutionGasDrive(), Souring.SweetRock, Souring.TheRock,
            Souring.SouringReference, maxTickPressureDropFraction: 0.2);

        EntityId<IReservoirCompartmentEntity> lightId = Add(state, light);
        EntityId<IReservoirCompartmentEntity> heavyId = Add(state, heavy);

        // Voidage room is zero at Pi for every compartment whatever its fluid
        // (SDD-003 §3.1: every expansion term is zero there) — so this has to
        // produce first, or the assertion below would pass for the wrong
        // reason (both sides trivially zero).
        state.CommitTick([Produce(lightId, 5_000.0), Produce(heavyId, 5_000.0)]);

        ReservoirVolume lightRoom = state.TrueVoidageRoomOf(lightId);
        ReservoirVolume heavyRoom = state.TrueVoidageRoomOf(heavyId);

        Assert.NotEqual(lightRoom.CubicMetres, heavyRoom.CubicMetres, 3);
    }

    private static IFluidPropertyModel Model(double apiGravityDegrees) => new BlackOilModel(
        new BlackOilInputs(
            OilGravity: new ApiGravity(apiGravityDegrees),
            GasSpecificGravity: 0.75,
            ReservoirTemperature: Temperature.FromCelsius(93.3),
            SolutionGorAtBubblePoint: 100.0,
            Form: FluidForm.BlackOil),
        new ValidityRange(
            new Pressure(500.0), new Pressure(60e6),
            Temperature.FromCelsius(10.0), Temperature.FromCelsius(180.0)));

    /// <summary>
    /// R20d.9's finding-270 amendment, the same "drive survives a reload"
    /// shape finding 192 already proves — substituting fluid system for drive
    /// mechanism. Two compartments, two different fluid systems: if the
    /// restore ever fell back onto one model for both, their voidage rooms
    /// would come back equal even though they went in different.
    /// </summary>
    [Fact]
    public void A_compartments_own_fluid_system_survives_a_reload()
    {
        var light = new ContentId("light-crude");
        var heavy = new ContentId("heavy-crude");

        SubsurfaceState Build() => new(
            new Dictionary<ContentId, IFluidPropertyModel>
            {
                [light] = Model(45.0),
                [heavy] = Model(18.0),
            },
            new SolutionGasDrive(), Souring.SweetRock, Souring.TheRock,
            Souring.SouringReference, maxTickPressureDropFraction: 0.2);

        SubsurfaceState captured = Build();
        EntityId<IReservoirCompartmentEntity> lightId = Add(captured, light);
        EntityId<IReservoirCompartmentEntity> heavyId = Add(captured, heavy);

        // Voidage room is zero at Pi whatever the fluid (SDD-003 §3.1) — has
        // to produce first, the same reason the sibling non-reload test does.
        captured.CommitTick([Produce(lightId, 5_000.0), Produce(heavyId, 5_000.0)]);

        double expectedLight = captured.TrueVoidageRoomOf(lightId).CubicMetres;
        double expectedHeavy = captured.TrueVoidageRoomOf(heavyId).CubicMetres;

        SubsurfaceState restored = Build();
        StateBlock.Restore(restored, StateBlock.Capture(captured).Written());

        Assert.Equal(expectedLight, restored.TrueVoidageRoomOf(lightId).CubicMetres, 3);
        Assert.Equal(expectedHeavy, restored.TrueVoidageRoomOf(heavyId).CubicMetres, 3);
        Assert.NotEqual(expectedLight, expectedHeavy, 3);
    }

    private static EntityId<IReservoirCompartmentEntity> Add(
        SubsurfaceState state, ContentId fluidSystem) =>
        state.Create(
            Generated() with { FluidSystem = fluidSystem },
            permeability: new Permeability(1.0e-13),
            netThickness: new Length(20.0),
            drainageArea: new Area(2.0e5),
            rockCompressibility: 4.5e-10,
            gasOilContact: new Length(1900.0),
            oilWaterContact: new Length(2100.0),
            RelativePermeabilityCurve.Validated(
                swc: 0.30, sor: 0.25, krwMax: 0.35, kroMax: 0.90, nw: 3.0, no: 2.0),
            new ContentId("solution-gas-drive"),
            aquiferStrength: 0.0,
            Duration.FromTicks(1.0));

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
    /// S013-9's decisive comparison, at the level it can actually be made.
    ///
    /// <para>A reloaded game plays differently while every state block is
    /// byte-identical and every RNG stream is in the same place, and reloading
    /// the reload reproduces it exactly — so the difference is not instability
    /// but something the RESTORE reconstructs differently from what the original
    /// built. This asks that question directly: a compartment made by
    /// <c>Create</c> and advanced by withdrawals, against one made by
    /// <c>RestoreTo</c> in a single step.</para>
    ///
    /// <para>WATER SATURATION, and exactly. The two tests above compare pressure
    /// to six decimals and nothing compares saturation at all — which is the gap
    /// this walks into, because water cut is <c>krw ∝ ((Sw−Swc)/(1−Swc−Sor))^nw</c>
    /// and just above connate a fractional difference in saturation moves the CUT
    /// by orders of magnitude. A tolerance of 1e-6 on saturation would hide a
    /// 60-fold difference in produced water.</para>
    /// </summary>
    [Fact]
    public void A_restored_compartment_holds_the_same_water_as_the_one_it_copied()
    {
        SubsurfaceState captured = Fresh();
        EntityId<IReservoirCompartmentEntity> id = Add(captured);

        // ENOUGH HISTORY TO MATTER, and water in it: saturation is what the two
        // reconstruction paths could disagree about, so a run that produced only
        // oil would compare two identical connate values and prove nothing.
        //
        // INJECTED rather than aquifer influx, because this fixture's drive is
        // solution-gas and §4.2b's coherence check refuses a compartment
        // carrying influx a drive does not admit (finding 192's guard, doing its
        // job on the first attempt at this test). Injection is a human act and
        // every drive admits it.
        for (var month = 0; month < 12; month++)
            captured.CommitTick(
            [
                new CompartmentWithdrawal(id,
                    Oil: new SurfaceVolume(4_000.0),
                    Gas: new StandardGasVolume(0.0),
                    Water: new SurfaceVolume(250.0),
                    Influx: new ReservoirVolume(0.0),
                    Injected: new ReservoirVolume(900.0),
                    Imported: new ReservoirVolume(0.0),
                    ReservoirVolume: new ReservoirVolume(5_000.0)),
            ]);

        SubsurfaceState restored = Fresh();
        StateBlock.Restore(restored, StateBlock.Capture(captured).Written());

        var water = new Viscosity(0.5e-3);
        var oil = new Viscosity(2.0e-3);

        double before = captured.TrueWaterCutOf(id, water, oil);
        double after = restored.TrueWaterCutOf(id, water, oil);

        // THEY AGREE, AND NOT TO THE LAST BIT — and the gap is S013-9's cause.
        //
        // The captured compartment reached this pressure through twelve
        // successive bisection solves; the restored one reached it in ONE. Both
        // converge inside the root-finder's tolerance and neither lands on the
        // same bits: 8.3891063623994589e-06 against 8.3891063623992776e-06, a
        // relative difference of about 2e-13.
        //
        // That is harmless here and is not harmless downstream, because the cut
        // ITSELF is near zero just above connate saturation. A relative wobble in
        // an almost-zero quantity is what a reloaded field turns into 3,644 kg of
        // produced water against the original's 233,955 — the `Chain` divergence
        // PV2 has admitted as its one exception, and the reason six families of
        // hypothesis were eliminated before this: every state block is identical,
        // because the difference was never IN a block (finding 206).
        // THEY AGREE TO THE LAST BIT, and getting here took closing an L5 breach
        // rather than anything about the save (finding 206).
        //
        // Connate water saturation had TWO OWNERS: the compartment derived
        // `Initial.ConnateWaterSaturation` as `1.0 - generated.OilSaturation`
        // while the rock's curve carried its own `swc` from content. For a field
        // generated at 0.7 against a curve declaring 0.30 those are
        // 0.30000000000000004 and 0.3 — one physical fact, two doubles. `Capture`
        // wrote one `swc` key and `Restore` read it into BOTH, so a reloaded
        // compartment had them agreeing where the original had them differing.
        //
        // THE SAVE NEVER LOST A VALUE. It unified two that were never equal,
        // which is why every state block compared byte-identical while a
        // reloaded game played differently — and why the digest diff, the
        // stream-position check and the double-reload self-consistency test were
        // all structurally unable to find it. `krw` normalises by
        // `(Sw − swc)`, and a producing field sits just above connate where a
        // last-bit change in the denominator's origin is a large RELATIVE change
        // in a near-zero cut: 3,644 kg of produced water against 233,955.
        //
        // Exact equality, not a tolerance. A tolerance here would have hidden the
        // original defect completely — the gap it produced was 2e-13 relative.
        Assert.Equal(before, after);

        // The pressure, exactly — it depends on the cumulative volumes and not on
        // the curve, and those DO round-trip. Stated to keep the finding narrow:
        // one input is wrong, not the restore in general.
        Assert.Equal(
            captured.TruePressureOf(id).Pascals,
            restored.TruePressureOf(id).Pascals);

        // The pressure carries the same signature, for the same reason.
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
