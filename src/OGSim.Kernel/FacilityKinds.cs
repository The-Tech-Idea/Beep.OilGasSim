// R20c.9 — the six facility-unit content kinds (SDD-004 §6's R20c.9 amendment).
//
// The engine's surface equipment was six ladders of C# records in composition's
// `Defaults`, which made "rebalancing is a content edit" (design 03, non-
// negotiable 11) false of the only equipment the game shipped: moving a
// separator's capacity meant editing an engine assembly and rebuilding.
//
// SIX KINDS AND NOT ONE, because a datasheet is closed. A separator states two
// independent leg capacities, a vessel volume, a rated efficiency and the rate
// that efficiency holds at; a manifold states a slot count. One record spanning
// all six would have every field optional and every reader branching on which
// arrived — the generic bag SDD-004 §6 forbids, wearing a record's clothes.
//
// THE GATE IS HERE AND `Fits` IS NOT (§6's R20c.9 amendment). For a facility
// unit the KIND IS THE SLOT, so a `Fits` would be the kind restated — L5's
// second owner. `Fits` stays with the unlockables that need it, where one socket
// accepts several kinds of thing.

using System.Text.Json;

namespace OGSim.Kernel;

/// <summary>
/// What every facility unit carries whatever it is: when a company may have it,
/// and where it sits on its own ladder.
///
/// <para><b>The rung is why this exists.</b> <see cref="ICatalog{TDef}.All"/> is
/// id-sorted, which is right for save stability and wrong for a progression —
/// <c>gas-plant-e1</c> sorts before <c>gas-plant-none</c>, and an upgrade path
/// read off that order would let a player install a plant by buying nothing. Era
/// cannot stand in either: two rungs of one ladder routinely share an era.</para>
///
/// <para>Rung 0 is the ABSENT state, and every ladder in this engine has one: a
/// field with no gas plant flares its gas, and "no gas plant" is a rung with a
/// capacity of zero rather than a null the readers would each have to handle.</para>
/// </summary>
public abstract record FacilityUnitDefinition(
    ContentId Id,
    ContentId? RequiresTech,
    Era AvailableFromEra,
    int Rung) : ContentDefinition(Id);

public sealed record SeparatorDefinition(
    ContentId Id, ContentId? RequiresTech, Era AvailableFromEra, int Rung,
    double GasCapacityKgPerSecond,
    double LiquidCapacityKgPerSecond,
    double VolumeCubicMetres,
    double LiquidFromGas,
    double GasFromLiquid,
    double WaterFromLiquid,

    // THE LOAD-BEARING ONE (SDD-006 §2's R20d.19 amendment, finding 173). The
    // other three move liquid and gas across the gas/liquid boundary or knock
    // water OUT of the oil leg, so without this every vessel produced dry crude
    // at every tier and the sales spec could not be breached. Transcribed here
    // as a field of its own rather than folded into the three, because it is the
    // one a rebalance will actually reach for.
    double WaterIntoLiquid,

    double DesignRateCubicMetresPerSecond,
    double OperatingPressurePascals)
    : FacilityUnitDefinition(Id, RequiresTech, AvailableFromEra, Rung);

public sealed record TankDefinition(
    ContentId Id, ContentId? RequiresTech, Era AvailableFromEra, int Rung,
    double CapacityKilograms,
    double VapourLossRatePerTick)
    : FacilityUnitDefinition(Id, RequiresTech, AvailableFromEra, Rung);

public sealed record TreaterDefinition(
    ContentId Id, ContentId? RequiresTech, Era AvailableFromEra, int Rung,
    double WaterRemoved)
    : FacilityUnitDefinition(Id, RequiresTech, AvailableFromEra, Rung);

public sealed record GasPlantDefinition(
    ContentId Id, ContentId? RequiresTech, Era AvailableFromEra, int Rung,
    double CapacityKgPerSecond)
    : FacilityUnitDefinition(Id, RequiresTech, AvailableFromEra, Rung);

public sealed record ExportLineDefinition(
    ContentId Id, ContentId? RequiresTech, Era AvailableFromEra, int Rung,
    double OfftakeKgPerSecond)
    : FacilityUnitDefinition(Id, RequiresTech, AvailableFromEra, Rung);

public sealed record ManifoldDefinition(
    ContentId Id, ContentId? RequiresTech, Era AvailableFromEra, int Rung,
    int Slots)
    : FacilityUnitDefinition(Id, RequiresTech, AvailableFromEra, Rung);

/// <summary>
/// SDD-006 §3c's compressor datasheet (R9.1's own composition, finding 257) —
/// the seventh rung-based socket, joining the six R20c.9 already wired.
///
/// <para><c>Discharge</c> is on the tier for the reason <see cref="
/// SeparatorDefinition.OperatingPressurePascals"/> already is: a boosting or
/// set-pressure element's target is "the player's decision, made when the
/// station is bought" (SDD-006 §3c/§3d), and every other such decision in
/// this engine is a tier field rather than a free-form command parameter.
/// Suction is NOT here — it is where the train sits in the network, supplied
/// at construction from the separator it feeds from, not a fact about which
/// train was bought.</para>
/// </summary>
public sealed record CompressorDefinition(
    ContentId Id, ContentId? RequiresTech, Era AvailableFromEra, int Rung,
    double RatedCapacityKgPerSecond,
    double MaxStageRatio,
    double PolytropicExponent,
    double PolytropicEfficiency,
    double MolarMassKgPerMol,
    double DerateFractionPerKelvin,
    double DerateReferenceKelvin,
    double DischargePascals)
    : FacilityUnitDefinition(Id, RequiresTech, AvailableFromEra, Rung);

/// <summary>
/// SDD-006 §3d's liquid pump station datasheet (R11.2's own composition,
/// finding 259) — the eighth rung-based socket, the compressor's shape
/// reused for the oil leg.
/// </summary>
public sealed record PumpStationDefinition(
    ContentId Id, ContentId? RequiresTech, Era AvailableFromEra, int Rung,
    double RatedCapacityKgPerSecond,
    double Efficiency,
    double DischargePascals)
    : FacilityUnitDefinition(Id, RequiresTech, AvailableFromEra, Rung);

/// <summary>
/// The gate and the rung, read once for all six (SDD-004 §6's R20c.9
/// amendment). A reader per kind would be six chances to spell
/// <c>availableFromEra</c> differently.
/// </summary>
internal readonly record struct FacilityGate(
    ContentId Id, ContentId? RequiresTech, Era AvailableFromEra, int Rung)
{
    public static FacilityGate Read(JsonElement element)
    {
        var id = new ContentId(PropertyKindContentKind.Required(element, "id").GetString()!);

        // OPTIONAL, and absent means ungated rather than unbuildable: the first
        // rung of every ladder is something a company starts able to have.
        ContentId? tech = element.TryGetProperty("requiresTech", out JsonElement t)
            ? new ContentId(t.GetString()!)
            : null;

        Era era = PropertyKindContentKind.ParseEnum<Era>(
            PropertyKindContentKind.Required(element, "availableFromEra"), "availableFromEra");

        int rung = PropertyKindContentKind.Required(element, "rung").GetInt32();

        return new FacilityGate(id, tech, era, rung);
    }
}

/// <summary>
/// What every facility kind does the same way: read the gate, validate the rung,
/// and declare the technology reference so stage 4 can resolve it.
///
/// <para>The tech reference is declared HERE rather than in each reader because
/// forgetting it is silent — a definition naming a technology nothing checks
/// would load, and the gate would never be enforced. One place, six kinds.</para>
/// </summary>
public abstract class FacilityContentKind<TDefinition> : IContentKind
    where TDefinition : FacilityUnitDefinition
{
    public abstract string Name { get; }

    public abstract ContentDefinition Read(JsonElement element);

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) =>
        definition is TDefinition unit && unit.RequiresTech is ContentId tech
            ? [new ContentReference("tech", tech, "$.requiresTech")]
            : [];

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not TDefinition unit) return problems;

        if (unit.Rung < 0)
            problems.Add($"rung {unit.Rung} must not be negative; rung 0 is the absent state");

        foreach (string problem in DatasheetProblems(unit)) problems.Add(problem);

        return problems;
    }

    /// <summary>What this kind alone can be wrong about.</summary>
    protected abstract IEnumerable<string> DatasheetProblems(TDefinition unit);

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];
}

// --------------------------------------------------------------- the six kinds
//
// Each reads its own datasheet in the unit grammar (design 10 §3): a capacity is
// "8 kg/s" and not a bare number, so a sheet cannot silently mean tonnes.

public sealed class SeparatorContentKind : FacilityContentKind<SeparatorDefinition>
{
    public override string Name => "separator";

    public override ContentDefinition Read(JsonElement element)
    {
        FacilityGate gate = FacilityGate.Read(element);

        return new SeparatorDefinition(
            gate.Id, gate.RequiresTech, gate.AvailableFromEra, gate.Rung,
            Si(element, "gasCapacity", Dimension.MassRate),
            Si(element, "liquidCapacity", Dimension.MassRate),
            Si(element, "volume", Dimension.ReservoirVolume),
            Si(element, "liquidFromGas", Dimension.Dimensionless),
            Si(element, "gasFromLiquid", Dimension.Dimensionless),
            Si(element, "waterFromLiquid", Dimension.Dimensionless),
            Si(element, "waterIntoLiquid", Dimension.Dimensionless),
            Si(element, "designRate", Dimension.ReservoirRate),
            Si(element, "operatingPressure", Dimension.Pressure));
    }

    protected override IEnumerable<string> DatasheetProblems(SeparatorDefinition unit)
    {
        if (unit.GasCapacityKgPerSecond < 0.0) yield return "gasCapacity must not be negative";
        if (unit.LiquidCapacityKgPerSecond < 0.0) yield return "liquidCapacity must not be negative";

        // A vessel with no volume has no residence time, and residence time is
        // what the separation efficiency is a function of.
        if (unit.VolumeCubicMetres <= 0.0) yield return "volume must be positive";
        if (unit.DesignRateCubicMetresPerSecond <= 0.0) yield return "designRate must be positive";

        // The vessel IMPOSES this on the network upstream rather than reading it
        // (finding 157), so a zero would be a vessel discharging to vacuum.
        if (unit.OperatingPressurePascals <= 0.0)
            yield return "operatingPressure must be positive";

        foreach ((string name, double value) in new[]
                 {
                     ("liquidFromGas", unit.LiquidFromGas),
                     ("gasFromLiquid", unit.GasFromLiquid),
                     ("waterFromLiquid", unit.WaterFromLiquid),
                     ("waterIntoLiquid", unit.WaterIntoLiquid),
                 })
            if (value is < 0.0 or > 1.0)
                yield return $"{name} {value} is a fraction and must be in [0, 1]";
    }

    internal static double Si(JsonElement element, string name, Dimension dimension) =>
        UnitGrammar.ParseToSi(
            PropertyKindContentKind.Required(element, name).GetString()!, dimension);
}

public sealed class TankContentKind : FacilityContentKind<TankDefinition>
{
    public override string Name => "tank";

    public override ContentDefinition Read(JsonElement element)
    {
        FacilityGate gate = FacilityGate.Read(element);

        return new TankDefinition(
            gate.Id, gate.RequiresTech, gate.AvailableFromEra, gate.Rung,
            SeparatorContentKind.Si(element, "capacity", Dimension.Mass),
            SeparatorContentKind.Si(element, "vapourLossRatePerTick", Dimension.Dimensionless));
    }

    protected override IEnumerable<string> DatasheetProblems(TankDefinition unit)
    {
        if (unit.CapacityKilograms <= 0.0) yield return "capacity must be positive";

        // A loss of 1 would empty the tank every month, which is not a tank.
        if (unit.VapourLossRatePerTick is < 0.0 or >= 1.0)
            yield return $"vapourLossRatePerTick {unit.VapourLossRatePerTick} must be in [0, 1)";
    }
}

public sealed class TreaterContentKind : FacilityContentKind<TreaterDefinition>
{
    public override string Name => "treater";

    public override ContentDefinition Read(JsonElement element)
    {
        FacilityGate gate = FacilityGate.Read(element);

        return new TreaterDefinition(
            gate.Id, gate.RequiresTech, gate.AvailableFromEra, gate.Rung,
            SeparatorContentKind.Si(element, "waterRemoved", Dimension.Dimensionless));
    }

    protected override IEnumerable<string> DatasheetProblems(TreaterDefinition unit)
    {
        // 1 is refused as well as excluded: a treater that removed ALL the water
        // would make the sales spec unfailable and the ladder pointless.
        if (unit.WaterRemoved is < 0.0 or >= 1.0)
            yield return $"waterRemoved {unit.WaterRemoved} must be in [0, 1)";
    }
}

public sealed class GasPlantContentKind : FacilityContentKind<GasPlantDefinition>
{
    public override string Name => "gas-plant";

    public override ContentDefinition Read(JsonElement element)
    {
        FacilityGate gate = FacilityGate.Read(element);

        return new GasPlantDefinition(
            gate.Id, gate.RequiresTech, gate.AvailableFromEra, gate.Rung,
            SeparatorContentKind.Si(element, "capacity", Dimension.MassRate));
    }

    protected override IEnumerable<string> DatasheetProblems(GasPlantDefinition unit)
    {
        // Zero is legal and is rung 0: no plant, everything flares.
        if (unit.CapacityKgPerSecond < 0.0) yield return "capacity must not be negative";
    }
}

public sealed class ExportLineContentKind : FacilityContentKind<ExportLineDefinition>
{
    public override string Name => "export-line";

    public override ContentDefinition Read(JsonElement element)
    {
        FacilityGate gate = FacilityGate.Read(element);

        return new ExportLineDefinition(
            gate.Id, gate.RequiresTech, gate.AvailableFromEra, gate.Rung,
            SeparatorContentKind.Si(element, "offtake", Dimension.MassRate));
    }

    protected override IEnumerable<string> DatasheetProblems(ExportLineDefinition unit)
    {
        // A line that takes nothing is a field with no route to market, which is
        // a state this engine reaches by an outage and never by content.
        if (unit.OfftakeKgPerSecond <= 0.0) yield return "offtake must be positive";
    }
}

public sealed class ManifoldContentKind : FacilityContentKind<ManifoldDefinition>
{
    public override string Name => "manifold";

    public override ContentDefinition Read(JsonElement element)
    {
        FacilityGate gate = FacilityGate.Read(element);

        return new ManifoldDefinition(
            gate.Id, gate.RequiresTech, gate.AvailableFromEra, gate.Rung,
            PropertyKindContentKind.Required(element, "slots").GetInt32());
    }

    protected override IEnumerable<string> DatasheetProblems(ManifoldDefinition unit)
    {
        if (unit.Slots <= 0) yield return $"slots {unit.Slots} must be positive";
    }
}

public sealed class CompressorContentKind : FacilityContentKind<CompressorDefinition>
{
    public override string Name => "compressor";

    public override ContentDefinition Read(JsonElement element)
    {
        FacilityGate gate = FacilityGate.Read(element);

        return new CompressorDefinition(
            gate.Id, gate.RequiresTech, gate.AvailableFromEra, gate.Rung,
            SeparatorContentKind.Si(element, "ratedCapacity", Dimension.MassRate),
            PropertyKindContentKind.Required(element, "maxStageRatio").GetDouble(),
            PropertyKindContentKind.Required(element, "polytropicExponent").GetDouble(),
            SeparatorContentKind.Si(element, "polytropicEfficiency", Dimension.Dimensionless),
            PropertyKindContentKind.Required(element, "molarMass").GetDouble(),
            SeparatorContentKind.Si(element, "derateFractionPerKelvin", Dimension.Dimensionless),
            SeparatorContentKind.Si(element, "derateReference", Dimension.Temperature),
            SeparatorContentKind.Si(element, "discharge", Dimension.Pressure));
    }

    protected override IEnumerable<string> DatasheetProblems(CompressorDefinition unit)
    {
        if (unit.RatedCapacityKgPerSecond < 0.0) yield return "ratedCapacity must not be negative";

        if (unit.MaxStageRatio <= 1.0)
            yield return "maxStageRatio must exceed 1; the stage count divides by its logarithm";

        if (unit.PolytropicExponent <= 1.0) yield return "polytropicExponent must exceed 1";

        if (unit.PolytropicEfficiency is <= 0.0 or > 1.0)
            yield return "polytropicEfficiency must be a fraction in (0, 1]";

        if (unit.MolarMassKgPerMol <= 0.0) yield return "molarMass must be positive";

        if (unit.DerateFractionPerKelvin < 0.0)
            yield return "derateFractionPerKelvin must not be negative";

        if (unit.DischargePascals <= 0.0) yield return "discharge must be positive";
    }
}

public sealed class PumpStationContentKind : FacilityContentKind<PumpStationDefinition>
{
    public override string Name => "pump-station";

    public override ContentDefinition Read(JsonElement element)
    {
        FacilityGate gate = FacilityGate.Read(element);

        return new PumpStationDefinition(
            gate.Id, gate.RequiresTech, gate.AvailableFromEra, gate.Rung,
            SeparatorContentKind.Si(element, "ratedCapacity", Dimension.MassRate),
            SeparatorContentKind.Si(element, "efficiency", Dimension.Dimensionless),
            SeparatorContentKind.Si(element, "discharge", Dimension.Pressure));
    }

    protected override IEnumerable<string> DatasheetProblems(PumpStationDefinition unit)
    {
        if (unit.RatedCapacityKgPerSecond < 0.0) yield return "ratedCapacity must not be negative";

        if (unit.Efficiency is <= 0.0 or > 1.0)
            yield return "efficiency must be a fraction in (0, 1]";

        if (unit.DischargePascals <= 0.0) yield return "discharge must be positive";
    }
}
