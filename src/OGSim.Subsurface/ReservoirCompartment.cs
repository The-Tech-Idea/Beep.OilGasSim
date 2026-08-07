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

using InPlace = OGSim.Kernel.MaterialInventory;

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
        _links = [.. links];

        // A compartment opens AT its initial pressure holding its initial mass:
        // nothing has been produced, so §3.1's solve at zero withdrawal returns
        // Pi and this is that answer without running it.
        Pr = initial.Pressure;
        InPlace = initial.Mass;
        Cumulative = CumulativeProduction.None;
    }

    public EntityId<IReservoirCompartmentEntity> Id { get; }

    public Pressure Pr { get; private set; }

    public InPlace InPlace { get; private set; }

    public ContactSet Contacts { get; private set; }

    public RockTruth Rock { get; }

    public IDriveMechanism Drive { get; }

    public IReadOnlyList<CompartmentLink> Links => _links;

    public InitialConditions Initial { get; }

    public CumulativeProduction Cumulative { get; private set; }

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
        ReservoirVolume withdrawnThisTick,
        IFluidPropertyModel fluid,
        double maxTickPressureDropFraction)
    {
        ArgumentNullException.ThrowIfNull(fluid);

        Pressure startOfTick = Pr;
        CumulativeProduction next = Cumulative.Plus(oil, gas, water, influx, injected);

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
            WithdrawnThisTick: withdrawnThisTick);

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
    /// Restores the position a save recorded, then re-derives the pressure from
    /// it rather than reading a stored one — the loaded reservoir is the one the
    /// material balance says it must be, so a save cannot carry a pressure the
    /// running engine would never have produced.
    /// </summary>
    public void RestoreTo(
        CumulativeProduction cumulative,
        ContactSet contacts,
        InPlace inPlace,
        IFluidPropertyModel fluid)
    {
        ArgumentNullException.ThrowIfNull(fluid);

        Cumulative = cumulative;
        Contacts = contacts;
        InPlace = inPlace;

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
            StartPressure: Initial.Pressure,
            WithdrawnThisTick: new ReservoirVolume(0.0));

        Pr = Drive.SolveEndPressure(input, fluid);
    }

    /// <summary>Where the contacts move to as volume is replaced (SDD-003 §3.2).</summary>
    public void MoveContacts(ContactSet contacts) => Contacts = contacts;
}
