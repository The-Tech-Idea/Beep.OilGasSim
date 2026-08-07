// R20c.7 — the subsurface module's own state and its stage
// (SDD-001 §9-10, SDD-003 §3, design 03 §6 stage 6).
//
// This is the first module in the engine that owns entities, saves them, and
// does per-tick work on them. The three arrive together on purpose: a stage with
// nothing to act on is law L3's declaration-with-no-behaviour, and state with no
// stage is a fact nothing ever changes.
//
// WHAT LEAVES THIS ASSEMBLY IS NOTHING — not even this. The owner is INTERNAL
// like everything else here, and composition reaches it through the
// InternalsVisibleTo door that already exists for naming a concrete drive.
// Making it public to be registrable would have breached the truth boundary for
// convenience, and the architecture test said so on the first run: a state owner
// is registered as an IStateOwner and contributed as an ITickStage, both public
// interfaces, so nothing about being composable requires being public.

using OGSim.Contracts;
using OGSim.Kernel;

using InPlace = OGSim.Kernel.MaterialInventory;

namespace OGSim.Subsurface;

/// <summary>
/// Owner of <c>subsurface.compartments</c>, and the only way in or out of the
/// truth model.
/// </summary>
internal sealed class SubsurfaceState : IStateOwner
{
    // Issue order, not a Dictionary walk: rule D-5 forbids enumerating a
    // Dictionary, and the order compartments are solved in must be fixed or two
    // runs of one seed produce different saves.
    private readonly List<ReservoirCompartment> _compartments = [];
    private readonly Dictionary<EntityId<IReservoirCompartmentEntity>, ReservoirCompartment> _byId = [];

    private readonly IFluidPropertyModel _fluid;
    private readonly IDriveMechanism _defaultDrive;
    private readonly double _maxTickPressureDropFraction;

    private ulong _nextId;

    public SubsurfaceState(
        IFluidPropertyModel fluid,
        IDriveMechanism drive,
        double maxTickPressureDropFraction)
    {
        ArgumentNullException.ThrowIfNull(fluid);
        ArgumentNullException.ThrowIfNull(drive);

        if (maxTickPressureDropFraction is <= 0.0 or >= 1.0)
            throw new ModelFault("SDD-003 §3.1", null,
                "the per-tick pressure step limit is a fraction in (0, 1); a limit of 0 " +
                "would forbid production and a limit of 1 would permit a reservoir to " +
                "reach zero pressure in a single month");

        _fluid = fluid;
        _defaultDrive = drive;
        _maxTickPressureDropFraction = maxTickPressureDropFraction;
    }

    public StateKey Key { get; } = new("subsurface.compartments");

    public int SchemaVersion => 1;

    public int Count => _compartments.Count;

    /// <summary>
    /// Creates a compartment from world generation's handoff (SDD-010 §2.7).
    ///
    /// <para>The generator emits VALUES and this builds the truth object from
    /// them — the generator never touches module truth directly, which is what
    /// keeps "a world can be regenerated from its seed" a property of the
    /// handoff rather than a promise about two code paths.</para>
    /// </summary>
    /// <param name="permeability">Rock and contact truth arrive as VALUES rather
    /// than as <c>RockTruth</c> and <c>ContactSet</c>, which are internal: a
    /// caller outside this assembly can hand over numbers, and only this
    /// assembly assembles them into truth. Making those two types public to
    /// tidy the signature would have put a compartment's true porosity in a
    /// shape any consumer could hold.</param>
    public EntityId<IReservoirCompartmentEntity> Create(
        GeneratedCompartment generated,
        Permeability permeability,
        Length netThickness,
        Area drainageArea,
        double rockCompressibility,
        Length gasOilContact,
        Length oilWaterContact)
    {
        ArgumentNullException.ThrowIfNull(generated);

        var rock = new RockTruth(
            generated.Porosity, permeability, netThickness, drainageArea, rockCompressibility);

        var contacts = new ContactSet(gasOilContact, oilWaterContact);

        var id = new EntityId<IReservoirCompartmentEntity>(++_nextId);

        // Stock-tank oil in place from the pore volume the generator drew:
        // PV × So ÷ Bo at initial pressure. Bo is asked of the fluid rather than
        // assumed, because a volatile oil and a heavy oil shrink very differently
        // on the way to the tank and the difference is the size of the field.
        var reservoirOil = new ReservoirVolume(
            generated.PoreVolume.CubicMetres * generated.OilSaturation);

        // Shrink, not a division: the FVF owns the conversion, which is what
        // stops a reservoir volume and a stock-tank volume from being added.
        SurfaceVolume stockTankOil = _fluid.Bo(generated.InitialPressure).Shrink(reservoirOil);

        var initial = new InitialConditions(
            Pressure: generated.InitialPressure,
            OilInPlace: stockTankOil,
            GasInPlace: new StandardGasVolume(0.0),
            GasCapRatio: 0.0,
            ConnateWaterSaturation: 1.0 - generated.OilSaturation,
            WaterCompressibility: WaterCompressibility,
            Mass: InPlace.Empty(materialCount: 0));

        var compartment = new ReservoirCompartment(
            id, initial, contacts, rock, _defaultDrive, []);

        _compartments.Add(compartment);
        _byId.Add(id, compartment);

        return id;
    }

    /// <summary>
    /// Water compressibility at reservoir conditions — 4.4e-10 1/Pa, the
    /// standard value for brine (SDD-003 §3.1). A physical property of water
    /// rather than a tuning number, which is why it is here and not in content.
    /// </summary>
    private const double WaterCompressibility = 4.4e-10;

    /// <summary>
    /// Stage 6. Each compartment re-solves its pressure from the whole cumulative
    /// history, in id order.
    ///
    /// <para>Withdrawal is supplied per compartment by the caller rather than
    /// read from the solver here: stage 6 duration-weights every segment's
    /// results before committing (SDD-002 §9), and this is what it commits
    /// INTO.</para>
    /// </summary>
    public void CommitTick(IReadOnlyList<CompartmentWithdrawal> withdrawals)
    {
        ArgumentNullException.ThrowIfNull(withdrawals);

        for (int i = 0; i < withdrawals.Count; i++)
        {
            CompartmentWithdrawal withdrawal = withdrawals[i];

            if (!_byId.TryGetValue(withdrawal.Compartment, out ReservoirCompartment? compartment))
                throw new InvariantFault("INV3", null,
                    $"withdrawal names compartment {withdrawal.Compartment.Value}, " +
                    "which does not exist");

            compartment.CommitWithdrawal(
                withdrawal.Oil, withdrawal.Gas, withdrawal.Water,
                withdrawal.Influx, withdrawal.Injected, withdrawal.ReservoirVolume,
                _fluid, _maxTickPressureDropFraction);
        }
    }

    /// <summary>
    /// The ONE door pressure leaves by (SDD-008 §1): a sample for the observation
    /// model to add error to. Not a getter for the truth — the caller receives a
    /// number it must still push through <c>Observation</c> to learn anything.
    /// </summary>
    internal Pressure TruePressureOf(EntityId<IReservoirCompartmentEntity> compartment) =>
        _byId.TryGetValue(compartment, out ReservoirCompartment? found)
            ? found.Pr
            : throw new InvariantFault("INV3", null,
                $"compartment {compartment.Value} does not exist");

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("count", _compartments.Count);
        writer.WriteInt64("next-id", (long)_nextId);

        for (int i = 0; i < _compartments.Count; i++)
        {
            ReservoirCompartment compartment = _compartments[i];
            string at = Prefix(i);

            writer.WriteInt64(at + "id", (long)compartment.Id.Value);

            // INITIAL conditions and cumulative production only. Pressure is
            // DERIVED (SDD-013 §4) and is not written: saving it would let a
            // hand-edited file assert a pressure the material balance never
            // produced, and the restore re-solves it anyway.
            writer.WriteDouble(at + "initial-pressure", compartment.Initial.Pressure.Pascals);
            writer.WriteDouble(at + "ooip", compartment.Initial.OilInPlace.CubicMetres);
            writer.WriteDouble(at + "gas-cap-ratio", compartment.Initial.GasCapRatio);
            writer.WriteDouble(at + "swc", compartment.Initial.ConnateWaterSaturation);
            writer.WriteDouble(at + "cw", compartment.Initial.WaterCompressibility);

            writer.WriteDouble(at + "porosity", compartment.Rock.Porosity);
            writer.WriteDouble(at + "permeability", compartment.Rock.Permeability.SquareMetres);
            writer.WriteDouble(at + "net-thickness", compartment.Rock.NetThickness.Metres);
            writer.WriteDouble(at + "drainage-area", compartment.Rock.DrainageArea.SquareMetres);
            writer.WriteDouble(at + "cf", compartment.Rock.RockCompressibility);

            writer.WriteDouble(at + "goc", compartment.Contacts.GasOilContact.Metres);
            writer.WriteDouble(at + "owc", compartment.Contacts.OilWaterContact.Metres);

            writer.WriteDouble(at + "np", compartment.Cumulative.Oil.CubicMetres);
            writer.WriteDouble(at + "gp", compartment.Cumulative.Gas.CubicMetres);
            writer.WriteDouble(at + "wp", compartment.Cumulative.Water.CubicMetres);
            writer.WriteDouble(at + "we", compartment.Cumulative.WaterInflux.CubicMetres);
            writer.WriteDouble(at + "vinj", compartment.Cumulative.Injected.CubicMetres);
        }
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _compartments.Clear();
        _byId.Clear();

        long count = reader.ReadInt64("count");
        if (count < 0)
            throw new SaveDataFault("SDD-003 §3", null,
                $"the subsurface declares {count} compartments");

        _nextId = (ulong)reader.ReadInt64("next-id");

        for (long i = 0; i < count; i++)
        {
            string at = Prefix(i);
            var id = new EntityId<IReservoirCompartmentEntity>((ulong)reader.ReadInt64(at + "id"));

            var initial = new InitialConditions(
                Pressure: new Pressure(reader.ReadDouble(at + "initial-pressure")),
                OilInPlace: new SurfaceVolume(reader.ReadDouble(at + "ooip")),
                GasInPlace: new StandardGasVolume(0.0),
                GasCapRatio: reader.ReadDouble(at + "gas-cap-ratio"),
                ConnateWaterSaturation: reader.ReadDouble(at + "swc"),
                WaterCompressibility: reader.ReadDouble(at + "cw"),
                Mass: InPlace.Empty(materialCount: 0));

            var rock = new RockTruth(
                reader.ReadDouble(at + "porosity"),
                new Permeability(reader.ReadDouble(at + "permeability")),
                new Length(reader.ReadDouble(at + "net-thickness")),
                new Area(reader.ReadDouble(at + "drainage-area")),
                reader.ReadDouble(at + "cf"));

            var contacts = new ContactSet(
                new Length(reader.ReadDouble(at + "goc")),
                new Length(reader.ReadDouble(at + "owc")));

            var compartment = new ReservoirCompartment(
                id, initial, contacts, rock, _defaultDrive, []);

            compartment.RestoreTo(
                new CumulativeProduction(
                    new SurfaceVolume(reader.ReadDouble(at + "np")),
                    new StandardGasVolume(reader.ReadDouble(at + "gp")),
                    new SurfaceVolume(reader.ReadDouble(at + "wp")),
                    new ReservoirVolume(reader.ReadDouble(at + "we")),
                    new ReservoirVolume(reader.ReadDouble(at + "vinj"))),
                contacts,
                initial.Mass,
                _fluid);

            _compartments.Add(compartment);
            _byId.Add(id, compartment);
        }
    }

    /// <summary>Zero-padded so ordinal key order and index order agree (SDD-013 §3).</summary>
    private static string Prefix(long index) =>
        "compartment." + index.ToString("D6", System.Globalization.CultureInfo.InvariantCulture) + ".";
}

/// <summary>
/// What one compartment gave up over a whole tick — every segment's share
/// already duration-weighted (SDD-002 §9).
/// </summary>
internal sealed record CompartmentWithdrawal(
    EntityId<IReservoirCompartmentEntity> Compartment,
    SurfaceVolume Oil,
    StandardGasVolume Gas,
    SurfaceVolume Water,
    ReservoirVolume Influx,
    ReservoirVolume Injected,
    ReservoirVolume ReservoirVolume);

/// <summary>
/// Design 03 §6 stage 6. Solve first, then commit — so a failed solve commits
/// nothing and the tick is abandoned whole.
/// </summary>
internal sealed class MaterialBalanceStage(
    SubsurfaceState state,
    Func<IReadOnlyList<CompartmentWithdrawal>> withdrawalsForTick) : ITickStage
{
    public StageId Id => StageId.MaterialBalance;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        state.CommitTick(withdrawalsForTick());
    }
}
