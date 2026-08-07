// R15.1 / R15.5 / R15.9 — the generation pipeline (SDD-010 §2–4).
//
// THE HANDOFF IS TYPED. The generator's only output channel is IWorldSink, so it
// never sees a module store and truth never travels sideways. That is what makes
// the slot moddable (design 03 §3.2) without opening the truth wall — a
// third-party generator can produce a world and still cannot reach into
// anybody's beliefs.
//
// AND BELIEFS ENTER THROUGH THE OBSERVATION DOOR. R15-V10's leak guarantee is
// not "the generator is careful"; it is that DeliverRegionalObservation takes an
// Observation, so a starting belief is constructed by the same conjugate update
// every in-game survey uses. There is no belief-copy path, because there is no
// method that would accept one.

using System.Collections.Immutable;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.World;

/// <summary>
/// SDD-010's pipeline. Each step draws from its own substream (§1).
///
/// <para>This is the SHIPPED generator, not the only possible one — the slot is
/// replaceable, and a scenario that wants a hand-built world supplies a
/// different implementation of the same interface.</para>
/// </summary>
public sealed class BasinWorldGenerator : IWorldGenerator
{
    /// <summary>How coarse a regional survey is. Deliberately BAD: regional data
    /// is a gravity and magnetics pass over a whole basin, and a player who
    /// could book reserves off it would never buy seismic (R15-V10).</summary>
    private const double RegionalSigmaLog = 1.2;

    /// <summary>Traps that receive no charge. SDD-010 §2.6's fill-spill produces
    /// these naturally — the fraction is an outcome, and R15-V7 checks it is
    /// meaningful rather than zero.</summary>
    private const double SpillFraction = 0.55;

    public ContentId Id { get; } = new("basin-generator");

    public void Generate(WorldParameters parameters, IWorldSink sink, IRandomStream worldGen)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(worldGen);

        Validate(parameters);

        // The world seed comes from the caller's stream, ONCE. Everything after
        // this is derived — so the whole world is a function of one number, and
        // PV7 is a property of the construction rather than of discipline.
        var streams = new StepStreams(unchecked((ulong)worldGen.NextInt(int.MaxValue)));

        IReadOnlyList<GeneratedAccumulation> accumulations = GenerateGeology(parameters, streams);

        for (int i = 0; i < accumulations.Count; i++) sink.AddAccumulation(accumulations[i]);

        sink.SetSurface(GenerateSurface(parameters, streams));
        sink.AddJurisdiction(GenerateJurisdiction(parameters, streams));

        DeliverRegionalData(accumulations, sink, streams);
    }

    // ---------------------------------------------------------- geology

    private static IReadOnlyList<GeneratedAccumulation> GenerateGeology(
        WorldParameters parameters, StepStreams streams)
    {
        IRandomStream traps = streams.For(WorldStep.Traps);
        IRandomStream charge = streams.For(WorldStep.Charge);
        IRandomStream sizing = streams.For(WorldStep.Accumulations);
        IRandomStream plays = streams.For(WorldStep.PlaysAndClasses);

        // Step 5: candidate traps, scaled by the basin's size.
        int candidates = 4 + traps.NextInt(Math.Max(1, parameters.WidthCells / 8));

        var accumulations = new List<GeneratedAccumulation>();

        for (int i = 0; i < candidates; i++)
        {
            // Step 5's subtlety class, drawn per trap — TRUTH, read by the
            // screening gate rather than the other way round. A below-tier
            // survey spawns nothing at all, which is why R15-V10 asks for zero
            // information about above-tier accumulations rather than vague
            // information.
            DetectClass subtlety = (DetectClass)traps.NextInt(4);

            // Step 6: fill-spill. A trap that receives no charge is empty, and
            // the classic algorithm produces those NATURALLY rather than by a
            // rule that says "make some empty" (R15-V7).
            if (charge.NextUnit() > (1.0 - SpillFraction) * parameters.ResourceRichness) continue;

            // Step 7: volume is log-normal — the size distribution real basins
            // show, and the reason one field in a play is worth ten of the rest.
            double logVolume = 12.0 + sizing.NextNormal() * 1.4;
            double volume = DetMath.Exp(logVolume) * parameters.ResourceRichness;

            var depth = new Length(1200.0 + sizing.NextUnit() * 3500.0);

            accumulations.Add(new GeneratedAccumulation(
                Play: new ContentId(plays.NextUnit() < 0.5 ? "play-a" : "play-b"),
                Closure: SquareAround(i),
                Subtlety: subtlety,
                Access: AccessFor(depth),
                Fluid: depth.Metres > 3200.0 ? FluidForm.ModifiedBlackOil : FluidForm.BlackOil,
                Compartments:
                [
                    new GeneratedCompartment(
                        PoreVolume: new ReservoirVolume(volume),
                        Porosity: 0.12 + sizing.NextUnit() * 0.15,
                        OilSaturation: 0.60 + sizing.NextUnit() * 0.25,
                        InitialPressure: new Pressure(depth.Metres * 1.0e4),
                        Temperature: new Temperature(288.0 + depth.Metres * 0.03),
                        Depth: depth),
                ]));
        }

        return accumulations;
    }

    /// <summary>
    /// SDD-010 §2.7 — access requirements DERIVED from generated depth, never
    /// authored. A deep discovery is hard because it is deep.
    /// </summary>
    private static AccessRequirements AccessFor(Length depth) =>
        new(Depth: depth.Metres switch
            {
                < 1500.0 => DepthClass.Shallow,
                < 3000.0 => DepthClass.Standard,
                < 4500.0 => DepthClass.Deep,
                _ => DepthClass.UltraDeep,
            },
            WaterDepth: WaterDepthClass.Onshore,
            Hpht: depth.Metres > 4000.0,
            Tight: false,
            Sour: false);

    // ---------------------------------------------------------- surface

    private static GeneratedSurface GenerateSurface(
        WorldParameters parameters, StepStreams streams)
    {
        IRandomStream surface = streams.For(WorldStep.Surface);

        int cells = parameters.WidthCells * parameters.HeightCells;
        var elevation = new double[cells];

        for (int i = 0; i < cells; i++)
            elevation[i] = (surface.NextUnit() - parameters.LandFraction) * 400.0;

        var heightfield = new Heightfield(
            new Length(1000.0), parameters.WidthCells, parameters.HeightCells,
            [.. elevation]);

        // Sea level is elevation zero, so bathymetry is the same field going
        // negative — harbour depth falls out rather than being authored.
        var classes = new int[cells];
        for (int i = 0; i < cells; i++) classes[i] = elevation[i] < 0.0 ? 0 : 1;

        var terrain = new GeneratedTerrain(
            heightfield, [.. classes], [new ContentId("sea"), new ContentId("land")], [], []);

        return new GeneratedSurface(terrain, [], [], [], [], []);
    }

    private static Jurisdiction GenerateJurisdiction(
        WorldParameters parameters, StepStreams streams)
    {
        IRandomStream jurisdictions = streams.For(WorldStep.Jurisdictions);

        return new Jurisdiction(
            new ContentId(jurisdictions.NextUnit() < parameters.BasinMaturity
                ? "concession" : "psc"),
            SquareAround(0));
    }

    // ---------------------------------------------------------- beliefs

    /// <summary>
    /// R15.9 / R15-V10. Starting beliefs, delivered as OBSERVATIONS.
    ///
    /// <para>Two guarantees, and the second is the one that matters:</para>
    ///
    /// <list type="number">
    /// <item>The sigma is large. Regional data is a gravity and magnetics pass
    /// over a whole basin; a player who could book reserves off it would never
    /// buy seismic.</item>
    /// <item><b>An above-tier accumulation gets NO observation at all.</b> Not a
    /// vague one — none. A vague reading still says "something is here", and the
    /// whole point of a subtlety class is that a subtle trap is invisible until
    /// the survey tier catches up with it.</item>
    /// </list>
    /// </summary>
    private static void DeliverRegionalData(
        IReadOnlyList<GeneratedAccumulation> accumulations,
        IWorldSink sink,
        StepStreams streams)
    {
        IRandomStream regional = streams.For(WorldStep.RegionalData);

        for (int i = 0; i < accumulations.Count; i++)
        {
            GeneratedAccumulation accumulation = accumulations[i];

            // Regional data sees D0 only. Everything subtler is silent.
            if (accumulation.Subtlety != DetectClass.D0) continue;

            GeneratedCompartment compartment = accumulation.Compartments[0];
            // Oil in place: pore volume × porosity × oil saturation. The BELIEF is
            // about the derived quantity, because that is what a company books
            // and argues about — not about the three factors separately.
            double inPlace = compartment.PoreVolume.CubicMetres
                           * compartment.Porosity * compartment.OilSaturation;

            double truth = DetMath.Ln(inPlace);

            sink.DeliverRegionalObservation(new Observation(
                Subject: new EntityRef(EntityKind.Compartment, (ulong)(i + 1)),
                PropertyKind: new ContentId("oil-in-place"),
                Value: truth + regional.NextNormal() * RegionalSigmaLog,
                Sigma: RegionalSigmaLog,
                Space: BeliefSpace.Log,
                Source: Provenance.Analogue));
        }
    }

    // ---------------------------------------------------------- validation

    /// <summary>
    /// SDD-010 §4: out-of-range is a refusal naming ALL violations, never a
    /// clamp. A clamped world would start playable and wrong, and the player
    /// would never learn which knob they had set impossibly.
    /// </summary>
    private static void Validate(WorldParameters parameters)
    {
        var problems = new List<string>();

        if (parameters.WidthCells <= 0 || parameters.HeightCells <= 0)
            problems.Add("grid dimensions must be positive");

        if (parameters.LandFraction is < 0.0 or > 1.0)
            problems.Add($"land fraction {Format(parameters.LandFraction)} is not in [0, 1]");

        if (parameters.ResourceRichness <= 0.0)
            problems.Add($"resource richness {Format(parameters.ResourceRichness)} is not positive");

        if (parameters.BasinMaturity is < 0.0 or > 1.0)
            problems.Add($"basin maturity {Format(parameters.BasinMaturity)} is not in [0, 1]");

        if (parameters.ClimateSeverity < 0.0)
            problems.Add($"climate severity {Format(parameters.ClimateSeverity)} is negative");

        if (parameters.RivalCount < 0)
            problems.Add($"rival count {parameters.RivalCount} is negative");

        if (problems.Count > 0)
            throw new ModelFault("SDD-010 §4", null,
                "world parameters are out of range: " + string.Join("; ", problems));
    }

    private static Polygon SquareAround(int index)
    {
        double x = index * 10.0;

        return new Polygon(
        [
            new Coordinate(x, 0.0), new Coordinate(x + 5.0, 0.0),
            new Coordinate(x + 5.0, 5.0), new Coordinate(x, 5.0),
        ]);
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
