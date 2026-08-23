// R20c.7 — the compartment as a living entity (SDD-003 §3, design 02 §2.1).
//
// IReservoirCompartment was declared at R5.1 and never implemented: the material
// balance was proven against inputs a test assembled, and nothing held a
// reservoir between two ticks. This is that thing — the first entity in the
// engine that persists across a tick and changes because of what happened.
//
// STILL INTERNAL, and that is the point. The player's belief about a reservoir
// is the game; if any consumer could read Pr directly, every exploration and
// appraisal decision downstream would be theatre. Pressure leaves this assembly
// only as an Observation, through the same door every in-game measurement uses.
//
// Pressure is RE-SOLVED from initial conditions every tick, never stepped from
// last tick's value: §3.1 measures every expansion term from Pi, so a rounding
// error in one month cannot compound into the next, and a save that restores
// cumulative production restores the pressure exactly rather than approximately.

using OGSim.Contracts;
using OGSim.Kernel;


namespace OGSim.Subsurface;

internal sealed class ReservoirCompartment : IReservoirCompartment
{
    private readonly List<CompartmentLink> _links;

    public ReservoirCompartment(
        EntityId<IReservoirCompartmentEntity> id,
        InitialConditions initial,
        ContactSet contacts,
        RockTruth rock,
        IDriveMechanism drive,
        ContentId fluidSystem,
        IReadOnlyList<CompartmentLink> links)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(drive);
        ArgumentNullException.ThrowIfNull(links);

        Id = id;
        Initial = initial;
        Contacts = contacts;
        Rock = rock;
        Drive = drive;
        FluidSystem = fluidSystem;
        _links = [.. links];

        // A compartment opens AT its initial pressure holding its initial mass:
        // nothing has been produced, so §3.1's solve at zero withdrawal returns
        // Pi and this is that answer without running it.
        Pr = initial.Pressure;
        Cumulative = CumulativeProduction.None;
    }

    public EntityId<IReservoirCompartmentEntity> Id { get; }

    public Pressure Pr { get; private set; }


    public ContactSet Contacts { get; private set; }

    public RockTruth Rock { get; }

    public IDriveMechanism Drive { get; }

    /// <summary>Which fluid system this compartment's oil is (SDD-003 §3.0b's
    /// finding-270 amendment) — a truth attribute world generation draws, the
    /// same standing <see cref="Drive"/> has.</summary>
    public ContentId FluidSystem { get; }

    public IReadOnlyList<CompartmentLink> Links => _links;

    public InitialConditions Initial { get; }

    public CumulativeProduction Cumulative { get; private set; }

    /// <summary>
    /// Sw at the producer — DERIVED from the cumulative terms, never stored
    /// (SDD-003 §3.1c).
    ///
    /// <para>The connate water the compartment started with, plus everything
    /// that has come in from an aquifer or an injector, less everything produced
    /// — over the pore volume. A saturation held as its own field would be a
    /// second owner of what the material balance already determines, and the two
    /// would drift the first time one was updated without the other (law
    /// L5).</para>
    ///
    /// <para>Clamped to the mobile range: the arithmetic can leave it a
    /// floating-point whisker outside on a compartment that has produced almost
    /// all of its movable water, and a saturation of 1.0000000001 would make the
    /// Corey normalisation report a negative oil permeability.</para>
    /// </summary>
    /// <summary>
    /// How much sea water this compartment has taken, as a fraction of its pore
    /// volume — SDD-012 §5's throughput ratio.
    ///
    /// <para>The IMPORTED share and not the injected one. A compartment that
    /// only ever reinjected the water it produced has been circulating a fluid
    /// already stripped of sulphate, and does not sour however long it runs
    /// (finding 182). This number is what separates a waterflood from a disposal
    /// well.</para>
    /// </summary>
    public double ImportedPoreVolumes
    {
        get
        {
            double pore = Initial.PoreVolume.CubicMetres;

            return pore <= 0.0 ? 0.0 : Cumulative.Imported.CubicMetres / pore;
        }
    }

    public double WaterSaturation
    {
        get
        {
            double pore = Initial.PoreVolume.CubicMetres;
            if (pore <= 0.0) return Initial.ConnateWaterSaturation;

            // Produced water is a STOCK-TANK volume and the others are reservoir
            // volumes, so it swells before it can be subtracted from them.
            double producedReservoir =
                Cumulative.Water.CubicMetres * WaterFormationVolumeFactor;

            double water =
                pore * Initial.ConnateWaterSaturation
                + Cumulative.WaterInflux.CubicMetres
                + Cumulative.Injected.CubicMetres
                - producedReservoir;

            double saturation = water / pore;

            if (saturation < Initial.ConnateWaterSaturation)
                return Initial.ConnateWaterSaturation;

            return saturation > 1.0 ? 1.0 : saturation;
        }
    }

    /// <summary>
    /// Bw at reservoir conditions. Water is very nearly incompressible, and
    /// SDD-003 §3.1 treats it as such everywhere else in the balance — a factor
    /// that varied here and nowhere else would make the saturation and the
    /// balance disagree about the same barrels.
    /// </summary>
    private const double WaterFormationVolumeFactor = 1.0;

    /// <summary>
    /// Stage 6: record what crossed the boundary this tick and re-solve the
    /// pressure the whole cumulative history implies.
    ///
    /// <para>The two happen together because they are one fact. A compartment
    /// whose production had been recorded but whose pressure had not been
    /// re-solved would be readable, in that window, as a reservoir that gave up
    /// oil for nothing.</para>
    /// </summary>
    public void CommitWithdrawal(
        SurfaceVolume oil,
        StandardGasVolume gas,
        SurfaceVolume water,
        ReservoirVolume influx,
        ReservoirVolume injected,

        // PART OF `injected`, not extra to it (SDD-012 §5's R20d.25 amendment).
        // The balance counts every cubic metre of water that arrives whatever
        // its provenance; this says how much of it was sea water, which is the
        // only water that sours the rock.
        ReservoirVolume imported,
        ReservoirVolume withdrawnThisTick,
        IFluidPropertyModel fluid,
        double maxTickPressureDropFraction)
    {
        ArgumentNullException.ThrowIfNull(fluid);

        if (imported.CubicMetres > injected.CubicMetres)
            throw new InvariantFault("SDD-012 §5", null,
                $"compartment {Id.Value} was committed {Format(imported.CubicMetres)} m³ of " +
                $"bought water inside {Format(injected.CubicMetres)} m³ of injection; the " +
                "imported share is part of the injected volume, never additional to it");

        Pressure startOfTick = Pr;
        CumulativeProduction next = Cumulative.Plus(oil, gas, water, influx, injected, imported);

        var input = new MaterialBalanceInput(
            InitialPressure: Initial.Pressure,
            OriginalOilInPlace: Initial.OilInPlace,
            GasCapRatio: Initial.GasCapRatio,
            ConnateWaterSaturation: Initial.ConnateWaterSaturation,
            WaterCompressibility: Initial.WaterCompressibility,
            RockCompressibility: Rock.RockCompressibility,
            CumulativeOilProduced: next.Oil,
            CumulativeGasProduced: next.Gas,
            CumulativeWaterProduced: next.Water,
            CumulativeWaterInflux: next.WaterInflux,
            CumulativeInjected: next.Injected,
            StartPressure: startOfTick,
            WithdrawnThisTick: withdrawnThisTick,
            GasInPlace: Initial.GasInPlace,
            ReservoirTemperature: Initial.ReservoirTemperature);

        // The DRIVE solves, not this class. Which expansion terms are admitted
        // is the mechanism's answer (SDD-003 §4.2b), and it is the reason
        // recovery factor emerges rather than being configured.
        Pressure solved = Drive.SolveEndPressure(input, fluid);

        // Committed only after the solve succeeds. A drive that refuses the step
        // — a pressure drop the model cannot honestly represent — must leave the
        // compartment exactly as it was, or an abandoned tick would still have
        // moved the reservoir.
        Cumulative = next;
        Pr = solved;
    }

    /// <summary>
    /// How much more replacement volume this compartment can take before it is
    /// back at discovery pressure (SDD-003 §3.1's R20d.24b amendment).
    ///
    /// <para><c>−Φ(Pi)</c>, and every expansion term is zero at Pi — so this is
    /// the withdrawal the compartment has made, less the water that has already
    /// come back to it from an aquifer or an injector. Floored at nothing: a
    /// compartment that has produced nothing has no room, which is not a
    /// negative amount of room.</para>
    /// </summary>
    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    public ReservoirVolume VoidageRoom(IFluidPropertyModel fluid)
    {
        ArgumentNullException.ThrowIfNull(fluid);

        var input = new MaterialBalanceInput(
            InitialPressure: Initial.Pressure,
            OriginalOilInPlace: Initial.OilInPlace,
            GasCapRatio: Initial.GasCapRatio,
            ConnateWaterSaturation: Initial.ConnateWaterSaturation,
            WaterCompressibility: Initial.WaterCompressibility,
            RockCompressibility: Rock.RockCompressibility,
            CumulativeOilProduced: Cumulative.Oil,
            CumulativeGasProduced: Cumulative.Gas,
            CumulativeWaterProduced: Cumulative.Water,
            CumulativeWaterInflux: Cumulative.WaterInflux,
            CumulativeInjected: Cumulative.Injected,

            // Neither reaches Φ. Named rather than defaulted because the record
            // has no defaults (law L2), and the honest values for "how much room
            // is there right now" are the position this compartment is in.
            StartPressure: Pr,
            WithdrawnThisTick: new ReservoirVolume(0.0),
            GasInPlace: Initial.GasInPlace,
            ReservoirTemperature: Initial.ReservoirTemperature);

        double room = -MaterialBalance.Residual(input, fluid, Initial.Pressure);

        return new ReservoirVolume(room > 0.0 ? room : 0.0);
    }

    /// <summary>
    /// Restores the position a save recorded, then re-derives the pressure from
    /// it rather than reading a stored one — the loaded reservoir is the one the
    /// material balance says it must be, so a save cannot carry a pressure the
    /// running engine would never have produced.
    /// </summary>
    public void RestoreTo(
        CumulativeProduction cumulative,
        ContactSet contacts,
        IFluidPropertyModel fluid)
    {
        ArgumentNullException.ThrowIfNull(fluid);

        Cumulative = cumulative;
        Contacts = contacts;

        var input = new MaterialBalanceInput(
            InitialPressure: Initial.Pressure,
            OriginalOilInPlace: Initial.OilInPlace,
            GasCapRatio: Initial.GasCapRatio,
            ConnateWaterSaturation: Initial.ConnateWaterSaturation,
            WaterCompressibility: Initial.WaterCompressibility,
            RockCompressibility: Rock.RockCompressibility,
            CumulativeOilProduced: cumulative.Oil,
            CumulativeGasProduced: cumulative.Gas,
            CumulativeWaterProduced: cumulative.Water,
            CumulativeWaterInflux: cumulative.WaterInflux,
            CumulativeInjected: cumulative.Injected,

            // The step limit is a statement about ONE tick, and a restore is not
            // a tick: the whole history arrives at once, so the step is measured
            // from initial conditions and no per-tick limit applies to it.
            //
            // THE SECOND HALF OF THAT WAS NOT IMPLEMENTED until R20d.12.17. The
            // start pressure was set here correctly and the limit ran anyway, so
            // a restore measured the ENTIRE depletion as one month and refused
            // it — a compartment past a quarter of its initial pressure could not
            // be loaded at all, which is every field that is small or old
            // (finding 205). `SolveFromInitial` below is the half that was
            // missing.
            StartPressure: Initial.Pressure,
            WithdrawnThisTick: new ReservoirVolume(0.0),
            GasInPlace: Initial.GasInPlace,
            ReservoirTemperature: Initial.ReservoirTemperature);

        Pr = Drive.SolveFromInitial(input, fluid);
    }

    /// <summary>Where the contacts move to as volume is replaced (SDD-003 §3.2).</summary>
    public void MoveContacts(ContactSet contacts) => Contacts = contacts;
}
