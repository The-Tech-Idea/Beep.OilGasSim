// Composition — building the engine (design 03 §3.1, §8).
//
// COMPOSITION IS ALL-OR-NOTHING. ModuleComposer validates the whole set before
// anything is constructed: every Requires met, no contract provided twice, no
// state key owned twice, no dependency cycle, no two modules in one stage slot,
// every declared slot filled. Either the engine builds, or it refuses naming
// EVERY problem.
//
// There is no partially-composed engine and no degraded mode, because an engine
// missing a module is an engine whose failure surfaces fifty ticks later as an
// inexplicable number rather than at startup as a sentence.
//
// This file constructs concrete types on purpose: design 03 §8 makes Layer 4
// "the ONLY project naming concrete types", and somebody has to know what
// implements what. Confining that knowledge here is what keeps every other
// assembly depending on contracts alone.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// The parameterisation the shipped modules are built with.
///
/// <para>These are values, not fallbacks: law L2 forbids a defaulted
/// dependency, and every one of them is passed explicitly at the one place
/// entitled to name a concrete type. Content replaces them wholesale once the
/// pipeline populates modules (R3 §5).</para>
/// </summary>
internal static class Defaults
{
    public static BlackOilInputs Fluid { get; } = new(
        OilGravity: new ApiGravity(35.0),
        GasSpecificGravity: 0.75,
        ReservoirTemperature: Temperature.FromCelsius(93.3),
        SolutionGorAtBubblePoint: 100.0,
        Form: FluidForm.BlackOil);

    public static ValidityRange Validity { get; } = new(
        new Pressure(500.0), new Pressure(60e6),
        Temperature.FromCelsius(10.0), Temperature.FromCelsius(180.0));

    public static Wells.InflowConditions Inflow { get; } = new(
        new Permeability(1.0e-13), new Length(20.0), new Area(2.0e5),
        new Length(0.108), new Viscosity(2.0e-3), new Pressure(10.0e6));

    public static Wells.TubingGeometry Tubing { get; } = new(
        new Length(2000.0), new Length(2000.0), new Length(0.0889), 4.6e-5);

    /// <summary>
    /// SDD-003 §3.1's per-tick step limit. A reservoir that lost more than a
    /// fifth of its pressure in one month is not being modelled, it is being
    /// extrapolated — the solve refuses rather than reporting a number nobody
    /// should trust.
    /// </summary>
    public const double MaxTickPressureDropFraction = 0.2;

    /// <summary>Opening cash — what a new company starts the game with.</summary>
    public static Money OpeningCash { get; } = Money.FromMillions(50.0);

    /// <summary>
    /// Starting prices and costs. Balance content in a finished game (R20.4);
    /// stated here so the loop is playable and revisable rather than absent.
    ///
    /// <para>$377/m³ is ~$60/bbl at 6.29 bbl/m³. The fixed cost is $300k a month:
    /// a small onshore field's standing charge — people, power, chemicals, the
    /// road. It was $2M on the first pass, which is a multi-platform figure, and
    /// it made a single-well field lose money every month it produced. That is a
    /// real dynamic and it will be some fields' story, but as the SHIPPED
    /// starting point it says the game is unwinnable, which is a different claim
    /// from the one intended.</para>
    /// </summary>
    public static FieldEconomics Economics { get; } = new(
        // $377/m³ ÷ 0.85 t/m³ — the same money for the same oil. Custody meters
        // mass, so the price is per tonne and the density is applied once, where
        // mass becomes the barrels a player reads.
        OilPricePerTonne: Money.FromMillions(377.0 / 0.85 / 1_000_000.0),
        FixedOperatingCostPerTick: Money.FromMillions(0.3),

        // ~$15/bbl of LIQUID lifted, which is an ordinary onshore figure. It is
        // charged on water as readily as on oil, because the pumps and the power
        // do not care which — and that is what eventually makes a watered-out
        // field uneconomic while it is still producing.
        LiftingCostPerTonne: Money.FromMillions(15.0 * 6.29 / 0.85 / 1_000_000.0),

        // ~$1/bbl of IMPORTED water — lift, filter, deaerate, pump. An ordinary
        // figure, and a small one against a barrel of oil, which is honest: what
        // makes a flood a decision in this engine is not mainly its bill. It is
        // that the water raises the saturation and brings the water cut forward,
        // that it shares one injectivity with the disposal duty and plugs the
        // well faster, and that everything it buys arrives years after it is
        // paid for.
        InjectionWaterCostPerCubicMetre: Money.FromMillions(6.29 / 1_000_000.0));

    /// <summary>
    /// The market this game is played in (SDD-009 §6).
    ///
    /// <para>Reversion 0.02 a month is a half-life of about three years — long
    /// enough that a downturn is something a company sits through rather than
    /// waits out, and short enough that it ends inside a field's life.
    /// Volatility 0.09 in log space is roughly a 9% monthly move, which is the
    /// order oil actually does.</para>
    ///
    /// <para>A shock one month in fifty, at three times the ordinary move. Rare
    /// enough to be an event a player remembers, big enough to change what they
    /// were going to do — and drawn every month whether or not it fires, so
    /// adding jumps cannot shift the sequence of ordinary moves.</para>
    /// </summary>
    public static IPriceModel Market { get; } = new OGSim.Company.MeanRevertingPrice(
        longRunMean: Economics.OilPricePerTonne,
        reversion: 0.02,
        volatility: 0.09,
        jumpChance: 0.02,
        jumpScale: 0.27);

    /// <summary>
    /// The development type-curve this composition ships (SDD-009 §4). Content
    /// in a finished game, like every other catalogue entry here.
    ///
    /// <para>18% a year with a hyperbolic exponent of 0.5 is an ordinary
    /// onshore waterflood: steep at first and long-tailed, which is the shape
    /// that makes a field's last decade produce a fifth of what its first did
    /// and still be worth running.</para>
    ///
    /// <para>A recovery factor of 0.35 — a third of the oil, which is what a
    /// supported reservoir actually gives up. The other two thirds staying in
    /// the ground is not a rounding error, it is the single largest fact about
    /// this industry.</para>
    /// </summary>
    public static OGSim.Company.ArpsReserves TypeCurve { get; } =
        new(declinePerYear: 0.18, exponent: 0.5, recoveryFactor: 0.35);

    /// <summary>
    /// The facility this composition ships (SDD-009 §5).
    ///
    /// <para>Sixty per cent advance against the PV of proved reserves at a 10%
    /// discount over fifteen years — a conventional borrowing base. The advance
    /// rate is the bank's margin for the reserves being wrong, and reserves are
    /// an estimate by construction.</para>
    ///
    /// <para>8% base, and up to 4% more for a company nobody wants to lend to.
    /// Carried separately so a player can see WHY their debt got dearer.</para>
    /// </summary>
    public static OGSim.Company.ReserveBasedLending Lender { get; } = new(
        advanceRate: 0.60,
        discountPerYear: 0.10,
        years: 15,
        baseRate: 0.08,
        esgSpreadAtWorst: 0.04,
        TypeCurve,
        () => Netback);

    /// <summary>
    /// What a cubic metre is worth to a lender after lifting it — the margin the
    /// loan is actually secured on, not the headline price.
    /// </summary>
    private static Money Netback =>
        Money.RoundHalfEven(
            (Economics.OilPricePerTonne.Cents - Economics.LiftingCostPerTonne.Cents)
            * SurfaceOilDensity.KgPerCubicMetre / 1000.0);

    /// <summary>
    /// The gas plant a company can buy (SDD-006 §3b, finding 172).
    ///
    /// <para>The first rung is NOTHING — a field ships with no gas handling and
    /// flares everything, which is how a development actually starts and what
    /// makes the first plant a decision rather than a formality. The rungs above
    /// take 3 and 8 kg/s, against a shipped field's roughly 2 kg/s of associated
    /// gas at plateau: enough that one plant covers a field and a second is
    /// needed only by a company that has grown past it.</para>
    /// </summary>
    public static IReadOnlyList<Facilities.GasPlantTier> GasPlantLadder { get; } =
    [
        new(new ContentId("gas-plant-none"), new MassRate(0.0)),
        new(new ContentId("gas-plant-e1"), new MassRate(3.0)),
        new(new ContentId("gas-plant-e2"), new MassRate(8.0)),
    ];

    /// <summary>
    /// What a tonne of sales gas fetches. Well below oil on an energy basis,
    /// which is the whole reason associated gas gets flared: it is worth
    /// something, and often not enough to build for.
    /// </summary>
    public static Money GasPricePerTonne { get; } = Money.FromMillions(120.0 / 1_000_000.0);

    /// <summary>
    /// What a well-run field flares, and what a badly-run one does
    /// (SDD-012 §4), in kilograms of gas per cubic metre of oil.
    ///
    /// <para>MEASURED, not estimated. These were first set from a guess of
    /// 0.09 kg of gas per cubic metre of oil, which was wrong by more than two
    /// orders of magnitude: a solution ratio of 100 sm³/sm³ at gas density puts
    /// it near 90, and a field flaring everything measures about 30 over five
    /// years. Every company sat below the worst edge and scored zero, so the
    /// record could not tell a clean operator from a filthy one and the penalty
    /// priced nothing.</para>
    ///
    /// <para>Five is a field capturing most of its associated gas; forty is one
    /// burning essentially all of it. Some flaring is unavoidable, and a scale on
    /// which perfection was the only clean score would be unreachable and
    /// therefore ignorable.</para>
    /// </summary>
    public static OGSim.Company.EsgRecord Record { get; } =
        new(cleanIntensity: 5.0, worstIntensity: 40.0);

    /// <summary>
    /// How hard service costs follow the oil price (SDD-009 §6's ED4). At 0.35,
    /// a doubling of oil over a year lifts the cost of everything by about a
    /// third — enough that building into a boom hurts, short of making it
    /// impossible.
    /// </summary>
    public const double CostElasticity = 0.35;

    /// <summary>
    /// The slow upward creep underneath it, per month. Small, and the reason a
    /// field left undeveloped for twenty years is dearer to develop than it
    /// looked.
    /// </summary>
    public const double CostDrift = 0.0008;

    /// <summary>
    /// The catalogue, as content would carry it. ONE material, because the chain
    /// this composition ships has one thing to move; the nine of
    /// `content/materials/` arrive with R20c.9.
    ///
    /// <para>The PHASE is what makes this more than a name: `SplitAt` reads it to
    /// decide which leg of a separator a material leaves by, so "oil is a liquid
    /// at standard conditions" is the statement that sends every kilogram down
    /// the liquid leg to the meter.</para>
    /// </summary>
    public static IReadOnlyList<(ContentId Id, PhaseAtStandardConditions Phase,
                                 IReadOnlyList<IProperty> Properties)> Materials { get; } =
    [
        (new ContentId("crude-oil"), PhaseAtStandardConditions.Liquid, []),
        (new ContentId("natural-gas"), PhaseAtStandardConditions.Gas, []),
        (new ContentId("produced-water"), PhaseAtStandardConditions.Aqueous, []),
    ];

    /// <summary>
    /// Ordinals are assigned by the CATALOGUE from the id-sorted list, never
    /// here (SDD-004 §6) — "crude-oil" sorts before "natural-gas". Named so a
    /// completion is built with the ordinal the catalogue chose rather than one
    /// this file assumed.
    /// </summary>
    public static MaterialId OilOrdinal { get; } = new(0);

    public static MaterialId GasOrdinal { get; } = new(1);

    /// <summary>"crude-oil" &lt; "natural-gas" &lt; "produced-water" by ordinal
    /// comparison, which is the sort the catalogue uses (SDD-004 §6).</summary>
    public static MaterialId WaterOrdinal { get; } = new(2);

    /// <summary>
    /// Which materials a pump has to lift. Oil and water — gas comes up with
    /// them and is separated off, and charging a lifting cost on it would be
    /// billing the field twice for the same barrel.
    /// </summary>
    public static IReadOnlyList<int> LiquidOrdinals { get; } =
        [OilOrdinal.Ordinal, WaterOrdinal.Ordinal];

    /// <summary>
    /// ρ_sc of the produced gas — <c>γg · ρ_air,sc</c> (SDD-003 §6.1b). The
    /// specific gravity is the fluid system's; the air density is the kernel's
    /// derived constant.
    /// </summary>
    public static Density GasSurfaceDensity { get; } = new(
        Fluid.GasSpecificGravity * PhysicalConstants.AirDensityAtStandardKgPerM3);

    /// <summary>
    /// How many materials this composition's catalogue carries. One — oil —
    /// until R20c.9 loads the nine of `content/materials/`. Stated once because
    /// three places must agree on it: the completion's stream width, an
    /// operation's mass report, and any zero composition either of them builds.
    /// </summary>
    public const int MaterialCount = 3;

    /// <summary>
    /// The company's one rig. **One**, deliberately: a rig drills a single well
    /// at a time, so a company that wants two wells at once needs a second rig,
    /// and that is a decision rather than an accounting entry. The bespoke timer
    /// this replaced had no rig at all, which made cash the only limit on how
    /// fast a field could be developed.
    /// </summary>
    public static EntityId<IRig> TheRig { get; } = new(1);

    /// <summary>
    /// SDD-007 §4's outcome table for a development well, as content would carry
    /// it. Probabilities sum to 1.0 (load-checked).
    ///
    /// <para>Success is the 0.60 the bespoke path drew directly — but it is now
    /// the sum of three grades rather than a single number, so a well can come
    /// in late or over budget instead of only being dry. `DisasterDay` on the
    /// disaster row is the day a blowout would occur; R18 consumes it, and until
    /// then a disaster is simply the worst kind of dry hole.</para>
    /// </summary>
    public static OutcomeTable DrillingOutcomes { get; } = new(
    [
        new OutcomeRow(OutcomeGrade.OnTime, Probability: 0.40,
                       DurationFactor: 1.00, CostFactor: 1.00, DisasterDay: null),
        new OutcomeRow(OutcomeGrade.Delayed, Probability: 0.14,
                       DurationFactor: 1.50, CostFactor: 1.15, DisasterDay: null),
        new OutcomeRow(OutcomeGrade.OverBudget, Probability: 0.06,
                       DurationFactor: 1.10, CostFactor: 1.60, DisasterDay: null),
        new OutcomeRow(OutcomeGrade.Failure, Probability: 0.38,
                       DurationFactor: 0.80, CostFactor: 0.90, DisasterDay: null),
        new OutcomeRow(OutcomeGrade.Disaster, Probability: 0.02,
                       DurationFactor: 1.80, CostFactor: 2.50, DisasterDay: 45),
    ]);

    /// <summary>
    /// A build-up's outcome table. A test is short, cheap and usually works —
    /// but it can fail, and a failed test is the honest bad outcome: the money
    /// is gone and the company knows nothing new, which is what makes buying
    /// information a decision rather than a formality.
    /// </summary>
    public static OutcomeTable WellTestOutcomes { get; } = new(
    [
        new OutcomeRow(OutcomeGrade.OnTime, Probability: 0.85,
                       DurationFactor: 1.00, CostFactor: 1.00, DisasterDay: null),
        new OutcomeRow(OutcomeGrade.Delayed, Probability: 0.10,
                       DurationFactor: 2.00, CostFactor: 1.20, DisasterDay: null),
        new OutcomeRow(OutcomeGrade.Failure, Probability: 0.05,
                       DurationFactor: 1.00, CostFactor: 1.00, DisasterDay: null),
    ]);

    /// <summary>
    /// How deep the company can drill before it earns the technology to go
    /// further. 4,000 m is what rotary drilling opens; deep drilling is E2 and
    /// has to be gone and got (TECH_TREE).
    /// </summary>
    public static Length MaximumDrillingDepth { get; } = new(4000.0);

    /// <summary>
    /// A survey's outcome table. Nothing about shooting seismic is difficult in
    /// the way drilling is; what can go wrong is that the data comes back too
    /// noisy to process, and then the money is simply gone.
    /// </summary>
    public static OutcomeTable SurveyOutcomes { get; } = new(
    [
        new OutcomeRow(OutcomeGrade.OnTime, Probability: 0.80,
                       DurationFactor: 1.00, CostFactor: 1.00, DisasterDay: null),
        new OutcomeRow(OutcomeGrade.Delayed, Probability: 0.12,
                       DurationFactor: 1.50, CostFactor: 1.10, DisasterDay: null),
        new OutcomeRow(OutcomeGrade.Failure, Probability: 0.08,
                       DurationFactor: 1.00, CostFactor: 1.00, DisasterDay: null),
    ]);

    // ------------------------------------------------------- property kinds
    //
    // Content in a finished game — `content/property-kinds/` carries the same
    // ids, their dimensions and their spaces. Named here so no activity holds a
    // bare string and two of them cannot disagree about which kind they measure.

    public static ContentId PressureKind { get; } = new("reservoir-pressure");

    public static ContentId PorosityKind { get; } = new("porosity");

    public static ContentId PermeabilityKind { get; } = new("permeability");

    public static ContentId OilInPlaceKind { get; } = new("oil-in-place");

    /// <summary>
    /// How big the STRUCTURE is, which is a different question from how much is
    /// in it (SDD-010 §4b). Every closed high has one; only some hold oil, and
    /// which is which is what probability of success answers.
    /// </summary>
    public static ContentId StructureCapacityKind { get; } = new("structure-capacity");

    // ------------------------------------------------- measurement sources
    //
    // What the observation model prices a reading at (SDD-008 §3). Distinct from
    // a template id on purpose: a template is a job that can be ordered, a source
    // is a way of seeing, and the same source could one day be reached by more
    // than one job.

    public static ContentId WellTestSource { get; } = new("well-test");

    public static ContentId WellLogSource { get; } = new("well-log");

    public static ContentId CoreSource { get; } = new("core");

    public static ContentId SeismicSource { get; } = new("seismic-3d");

    /// <summary>
    /// THE HOLE ITSELF. A discovery well penetrates the column, logs it and
    /// tests it, and a company walks away from a strike knowing roughly how much
    /// is down there — which is a different and much sharper question than how
    /// big the trap was.
    /// </summary>
    public static ContentId DiscoverySource { get; } = new("discovery-well");

    /// <summary>
    /// Which space a kind's belief lives in (SDD-008 §2, content's
    /// <c>space</c> field).
    ///
    /// <para>Additive kinds Linear, multiplicative kinds Log. It THROWS on a kind
    /// it does not know rather than assuming Linear: a multiplicative quantity
    /// sampled additively can go negative, and a volume of −4 million m³ would be
    /// a belief nobody could act on (law L2 — no dependency has a default).</para>
    /// </summary>
    public static BeliefSpace SpaceOf(ContentId kind) =>
        kind.Value switch
        {
            "reservoir-pressure" => BeliefSpace.Linear,
            "porosity" => BeliefSpace.Linear,
            "permeability" => BeliefSpace.Log,
            "oil-in-place" => BeliefSpace.Log,

            // A volume, so multiplicative — and a structure that could hold a
            // NEGATIVE amount is not a belief anybody could act on.
            "structure-capacity" => BeliefSpace.Log,
            _ => throw new ModelFault("SDD-008 §2", null,
                $"no belief space is declared for property kind '{kind.Value}'"),
        };

    /// <summary>
    /// INV8's sigma floor, per kind and in that kind's space.
    ///
    /// <para>Per kind because one flat number cannot be both: 0.02 against a
    /// porosity erases the difference between a core and a log, and against a
    /// pressure in pascals it is no floor at all. The floor is what stops
    /// repeated observation driving sigma to zero — without it a player logs the
    /// same compartment ten times and becomes certain of a rock nobody can be
    /// certain of.</para>
    /// </summary>
    /// <summary>
    /// SDD-008 §2d.1 — how fast a belief about this kind goes stale, per year of
    /// PRODUCTION (not of calendar: §2d.2).
    ///
    /// <para>The test for any kind is one question: does production change it? A
    /// reservoir's pressure moves as fluid is withdrawn and its contacts rise and
    /// fall with it. The porosity of a rock does not, nor does how much oil the
    /// structure held to begin with — so those are ZERO, and zero is the answer
    /// rather than the absence of one. A player must never have to re-log a well
    /// to learn what its core already told them.</para>
    ///
    /// <para>Beside <see cref="SigmaFloorFor"/> because it is the same kind of
    /// fact — content about a property kind, reaching the store as a lookup — and
    /// the two are read together often enough that separating them would invite
    /// a kind that has a floor and no drift by oversight.</para>
    /// </summary>
    public static double DriftPerYearFor(ContentId kind) =>
        kind.Value switch
        {
            // Pascals per year. A gauge read two years ago is still a reading;
            // it is a reading of a reservoir that has since been produced.
            "reservoir-pressure" => 2.0e5,

            // CONTACTS BELONG HERE AND ARE NOT LISTED, because they are not a
            // belief kind yet: the five kinds this engine files beliefs under are
            // pressure, porosity, permeability, oil-in-place and
            // structure-capacity. §2d.1's table names GOC and OWC as drifting,
            // and they join this switch the day something delivers an
            // observation of one — listing them now would be content for a kind
            // nothing can produce.

            // AND EVERYTHING ELSE IS ZERO, deliberately. Rock properties, volumes
            // in place and structure capacity are facts about a thing that is not
            // changing — a company that stops looking at them does not know less.
            _ => 0.0,
        };

    public static double SigmaFloorFor(ContentId kind) =>
        kind.Value switch
        {
            // A gauge is good, but the compartment behind it is not one number.
            "reservoir-pressure" => 5.0e4,
            "porosity" => 0.005,
            "permeability" => 0.10,

            // Nobody ever knows STOIIP to better than about 15%, and a game that
            // let a player get there would have no reason for appraisal.
            "oil-in-place" => 0.15,

            // How big a STRUCTURE is, which is what regional gravity and
            // seismic actually see (SDD-010 §4b). Looser than oil-in-place is
            // tight: mapping a closure is geometry and gets genuinely good with
            // better data, whereas how much oil is in it never does.
            //
            // A separate kind from oil-in-place on purpose. Knowing the trap
            // could hold a hundred million barrels says nothing about whether it
            // holds any — that second question is what probability of success
            // answers, and merging the two would let a big structure read as a
            // big discovery.
            "structure-capacity" => 0.10,
            _ => throw new ModelFault("INV8", null,
                $"no sigma floor is declared for property kind '{kind.Value}'"),
        };

    /// <summary>
    /// The activity templates this composition ships. Content in a finished game
    /// (R20c.9); here they are the five the loop can honestly support.
    ///
    /// <para>Declared AFTER the rig and the outcome tables: static initialisers
    /// run in declaration order, and reading them from above takes a null table
    /// and a default rig id. The compiler said so, and `EngineCorpus.Subsurface`
    /// carries a note about the same trap.</para>
    /// </summary>
    public static ActivityTerms DrillWellTerms { get; } = new(
        Template: new ContentId("drill-development-well"),
        Cost: Money.FromMillions(8.0),
        DurationTicks: 4,
        Rig: TheRig,
        WeatherLimit: 6.0,   // a rig on location: heave stops tripping pipe long before it stops the platform
        Outcomes: DrillingOutcomes);

    public static ActivityTerms WellTestTerms { get; } = new(
        Template: new ContentId("well-test-buildup"),
        Cost: Money.FromMillions(0.4),
        DurationTicks: 1,
        Rig: TheRig,
        WeatherLimit: 7.5,   // the well is shut in and a gauge is reading; little to do on deck
        Outcomes: WellTestOutcomes);

    /// <summary>Cheap, quick, and run on the rig that is already there.</summary>
    public static ActivityTerms WirelineLogTerms { get; } = new(
        Template: new ContentId("wireline-log"),
        Cost: Money.FromMillions(0.15),
        DurationTicks: 1,
        Rig: TheRig,
        WeatherLimit: 6.5,   // a wireline unit needs a stable deck to run tools on a thin cable
        Outcomes: WellTestOutcomes);

    /// <summary>Several times the price of a log for the same two properties,
    /// which is the decision.</summary>
    public static ActivityTerms CoringTerms { get; } = new(
        Template: new ContentId("cut-core"),
        Cost: Money.FromMillions(0.9),
        DurationTicks: 1,
        Rig: TheRig,
        WeatherLimit: 6.0,   // coring is drilling, and slower
        Outcomes: WellTestOutcomes);

    /// <summary>
    /// NO RIG (SDD-007 §1's null case) and no wellbore: a survey is shot from the
    /// surface, so it can run while the rig is turning elsewhere. That
    /// independence is what lets a company explore and develop in the same month
    /// — and what makes seismic the opening move rather than a queue behind
    /// drilling.
    /// </summary>
    public static ActivityTerms SeismicSurveyTerms { get; } = new(
        Template: new ContentId("seismic-3d"),
        Cost: Money.FromMillions(2.5),
        DurationTicks: 2,
        Rig: null,
        WeatherLimit: 5.5,   // streamers in the water are the most weather-limited thing offshore
        Outcomes: SurveyOutcomes);

    /// <summary>
    /// The formation volume factor a shipped completion converts with.
    ///
    /// <para><b>It disagrees with the composed <c>BlackOilModel</c> by about 9%</b>,
    /// which is one physical fact with two owners (law L5). A completion design
    /// is a catalogue entry and its fluid block belongs to the fluid system, so
    /// closing it is R20c.9's loader rather than a number changed here — named
    /// as a constant so the two places are at least visible to each other
    /// (finding 160).</para>
    /// </summary>
    public static FormationVolumeFactor CompletionBo { get; } = new(1.2);

    /// <summary>
    /// The well a drilling command produces. One completion on one compartment,
    /// naturally flowing, wide open — the E1 well, and the only one the current
    /// content can describe.
    /// </summary>
    public static Wells.Completion CompletionFor(
        ulong id,
        EntityId<IReservoirCompartmentEntity> compartment,
        Length totalDepth,
        Wells.InflowConditions rock)
    {
        var tubing = new Wells.TubingGeometry(
            totalDepth, totalDepth, new Length(0.0889), 4.6e-5);

        return new Wells.Completion(
            new EntityId<ICompletion>(id),
            new EntityId<IWellbore>(id),
            [new Perforation(compartment, totalDepth, totalDepth + new Length(30.0),
                             Skin: 0.0, Isolated: false)],
            // THE ROCK THIS WELL IS ACTUALLY IN (SDD-008 §2c). It was
            // `Defaults.Inflow` — one fixed set for every well ever drilled, so a
            // marginal structure produced exactly like a giant one and the
            // material balance eventually refused the step (finding 170).
            new Wells.CompositeInflowModel(rock),
            new Wells.HydrostaticFrictionOutflowModel(
                tubing, Density.FromSpecificGravity(0.85), lift: null),
            new Wells.CompletionFluid(
                SurfaceOilDensity,
                CompletionBo,
                Allocation.Validated(
                    [(new EntityRef(EntityKind.Compartment, compartment.Value), 1.0)]),
                new Pressure(30.0e6),
                ReservoirTemperature,
                GasSurfaceDensity,

                // Rs is REFRESHED from the compartment before every solve, like
                // the pressure it is a function of. Opening at the bubble-point
                // ratio is the value a well starts at, not a placeholder.
                Fluid.SolutionGorAtBubblePoint,

                WaterSurfaceDensity,

                // And so is the water cut: a new well on an unflooded
                // compartment is dry, and stays dry until water reaches it.
                0.0),
            Wells.ChokeSetting.Open,
            oilOrdinal: OilOrdinal.Ordinal,
            gasOrdinal: GasOrdinal.Ordinal,
            waterOrdinal: WaterOrdinal.Ordinal,
            materialCount: MaterialCount,
            lift: null);
    }

    // ------------------------------------------------------- the read model's paths
    //
    // SDD-014 §2's registry, as the paths THIS read model can fill. An objective
    // naming anything else is refused when the engine composes, rather than
    // evaluated against a zero nobody computed.
    //
    // Money is in CENTS: a path is a double and the ledger is a scaled integer,
    // so the projection carries the integer's own unit rather than introducing a
    // second rounding rule (SDD-001 §1.3).

    public static IReadOnlyList<ProjectedPath> ProjectedPaths { get; } =
    [
        new("company.cash", position => position.Cash.Cents),
        new("company.insolvent", position => position.Insolvent ? 1.0 : 0.0),
        new("field.wells", position => position.Wells),
        new("field.activitiesRunning", position => position.ActivitiesRunning),
        new("field.producedThisTick", position => position.ProducedThisTick.CubicMetres),
    ];

    /// <summary>
    /// What the decade has to be worth. Named so the goal and the opening
    /// position cannot drift apart silently.
    ///
    /// <para><b>Out of reach without debottlenecking, and that is the point.</b>
    /// A player who drills and waits is capped by the first separator and ends
    /// the decade under $500M; one who fits the bigger vessel, develops the
    /// field and keeps it flowing clears it with room. The target sits between them, so
    /// the run is decided by whether the constraints were answered rather than
    /// by how long it was left alone.</para>
    ///
    /// <para>It was $100M — met in month six by a field that had not been
    /// developed at all, which made every decision after the first one
    /// decoration (R20.4's first measurement).</para>
    ///
    /// <para>AND IT WAS $600M UNTIL WEATHER COST DAYS (R22.2, finding 214). That
    /// number was calibrated in a world where an operation never stood down: the
    /// developed decade earned $918M against a bottlenecked $449M, and $600M sat
    /// between them. With standby days the same two runs earn <b>$553.9M and
    /// $271.1M</b> — so $600M stopped discriminating and simply always failed,
    /// which is a scenario that cannot be won rather than one that is hard.</para>
    ///
    /// <para>Re-struck at the SAME GEOMETRY rather than at whatever made the test
    /// pass: 65% of the developed run and about 134% of the bottlenecked one, the
    /// proportions $600M held before. That keeps the property the target exists
    /// for — answering the constraint wins, ignoring it loses — and it is the
    /// only honest reason to move a goal. Both numbers above are measured, and
    /// the comparison test beside this one asserts the ordering directly rather
    /// than trusting either.</para>
    ///
    /// <para>Declared BEFORE the scenario that reads it: static initialisers run
    /// in declaration order, so a target declared below would be zero when the
    /// objective is built — and "cash at least zero" is met in month one, which
    /// is a scenario that cannot be lost rather than a compile error. The same
    /// trap the activity terms above carry a note about.</para>
    /// </summary>
    private static Money TargetCash { get; } = Money.FromMillions(600.0);

    /// <summary>
    /// The scenario this composition ships (SDD-014 §5). Content in a finished
    /// game — R21f authors the twelve missions — and stated here as the records a
    /// loader will produce, so the runner that reads it is the shipped one rather
    /// than a path only content will ever take.
    ///
    /// <para>What it asks for: double the opening cash inside ten years.
    /// Reachable with a few good wells and out of reach if the early holes are
    /// dry, which is what makes the first drilling decision matter. Running out
    /// of money is a `Never` in <c>Failures</c> — the hard limit that ends the
    /// run rather than a goal the player works toward.</para>
    ///
    /// <para><c>Scoring</c> is EMPTY, and honestly: SDD-014 §4's eight formulas
    /// read ledger and registry values this loop does not publish yet, and that
    /// document says an empty scoring list is what a sandbox is. A weight here
    /// would be a promise of a number nothing computes.</para>
    /// </summary>
    public static Scenario FirstField { get; } = new(
        Id: new ContentId("first-field"),
        World: new GeneratedWorld(Seed: 1),
        StartingState: new ContentId("opening-position"),
        Objectives:
        [
            new Objective(
                new ContentId("double-the-opening-cash"),
                new Compare(
                    new Metric(new ReadModelPath("company.cash")),
                    CompareOp.Ge,
                    new Const(TargetCash.Cents)),
                Deadline: null,
                Weight: 1.0,
                Visible: true),
        ],
        Failures:
        [
            new Objective(
                new ContentId("stay-solvent"),
                new Never(
                    new Compare(
                        new Metric(new ReadModelPath("company.insolvent")),
                        CompareOp.Ge,
                        new Const(1.0))),
                Deadline: null,
                Weight: 1.0,
                Visible: true),
        ],
        Scoring: [],
        RealityProfile: new ContentId("standard"),
        Script: [],
        Deadline: new Tick(120));


    public static Temperature ReservoirTemperature { get; } = Temperature.FromCelsius(93.3);

    // ------------------------------------------------------- the surface chain
    //
    // Wellheads → header → vessel → meter. Four elements, and they are the
    // difference between a chain that is described and one a barrel travels
    // down: a well needs something to flow against, two wells need somewhere to
    // meet, and revenue needs a metered point to originate at (SDD-009 §1).
    // Content in a finished game (R20c.9).

    public static EntityId<IFlowElement> TheManifold { get; } = new(1_000_001);

    public static EntityId<IFlowElement> TheSeparator { get; } = new(1_000_002);

    public static EntityId<IFlowElement> TheCustodyPoint { get; } = new(1_000_003);

    public static EntityId<IFlowElement> TheFlare { get; } = new(1_000_004);

    public static EntityId<ICompletion> TheDisposalWell { get; } = new(1_000_005);

    public static EntityId<IFlowElement> TheFlowline { get; } = new(1_000_006);

    public static EntityId<IFlowElement> TheTank { get; } = new(1_000_007);

    public static EntityId<IFlowElement> TheGasPlant { get; } = new(1_000_008);

    public static EntityId<IFlowElement> TheTreater { get; } = new(1_000_009);

    public static EntityId<IFlowElement> TheWaterIntake { get; } = new(1_000_010);

    /// <summary>
    /// Where per-well gathering lines start numbering (SDD-006 §1c). Above the
    /// fixed chain elements by a clear margin, so a line laid for the
    /// two-thousandth well still cannot collide with the header or the trunk.
    /// </summary>
    public const ulong FirstGatheringLine = 2_000_000UL;

    /// <summary>
    /// Storage at the terminal (SDD-006 §5, catalogue C12), 150,000 tonnes.
    ///
    /// <para><b>Sized against a MONTH, because the ullage constraint is a
    /// rate.</b> A tank offers "remaining capacity ÷ the segment's seconds" as
    /// the mass rate it can still accept, so a tank holding less than a tick's
    /// throughput binds on the first tick whatever else is happening — which is
    /// storage behaving like a restriction rather than a buffer. This is several
    /// months of an E1 field, so it fills only when production genuinely runs
    /// ahead of export.</para>
    ///
    /// <para>The boil-off is why storage is not free: oil sitting in a tank
    /// evaporates slowly, so a field that stores rather than exports loses a
    /// little of what it stored.</para>
    /// </summary>
    public static Facilities.TankTier TankTier { get; } = new(
        new ContentId("tank-terminal-e1"),
        Capacity: new Mass(150.0e6),
        VapourLossRatePerTick: 0.001);

    /// <summary>
    /// What the export line contracts to take, kg/s.
    ///
    /// <para><b>Above the first separator and below a bigger one</b>, and that is
    /// the progression: an E1 field is vessel-limited, so the tank never fills
    /// and export is invisible. Fit the bigger vessel and the field can make more
    /// than the pipeline will take — the tank starts filling, and when it is full
    /// the ullage constraint reaches back down the chain and shuts wells in
    /// (R8-V5). One bottleneck solved is the next one met, which is the shape an
    /// operations game is played on.</para>
    /// </summary>
    public static MassRate ExportOfftake { get; } = new(20.0);

    /// <summary>
    /// The export ladder (SDD-006 §7b). The shipped line is what a company
    /// signs for before it knows what it has found; the rungs above are what a
    /// field earns the right to build.
    ///
    /// <para>DOUBLING, not creeping, because the decision has to be big enough
    /// to be wrong. A rung that added a tenth would be an obvious yes at every
    /// field size and therefore not a decision at all.</para>
    /// </summary>
    public static IReadOnlyList<Facilities.ExportTier> ExportLadder { get; } =
    [
        new(new ContentId("export-line-e1"), ExportOfftake),
        new(new ContentId("export-line-e2"), new MassRate(40.0)),
        new(new ContentId("export-line-e3"), new MassRate(80.0)),
    ];

    /// <summary>
    /// What a bigger line costs and how long it takes. DEARER AND SLOWER than a
    /// vessel: a separator is a unit dropped onto a pad, an export line is a
    /// route — and the money committed before a barrel moves through it is what
    /// makes overbuilding a small field hurt.
    /// </summary>
    public static ActivityTerms ExpandExportTerms { get; } = new(
        Template: new ContentId("expand-export"),
        Cost: Money.FromMillions(45.0),
        DurationTicks: 9,
        Rig: null,
        WeatherLimit: 5.0,   // pipeline and terminal work is heavy lift
        Outcomes: SurveyOutcomes);

    /// <summary>
    /// The gathering line from the header to the vessel (design 04 §5 stage 3,
    /// catalogue C06's size ladder).
    ///
    /// <para><b>This is what makes commingling bite.</b> A header passes its
    /// downstream demand to every well equally, and the demand is the vessel's
    /// set point PLUS whatever this line loses at the rate going through it — so
    /// a new high-rate well raises the throughput, raises the drop, raises the
    /// header pressure, and the weakest well on the line suffers for it. That is
    /// R6-V14, and nobody codes it: it is backpressure arithmetic that finally
    /// has a term.</para>
    ///
    /// <para>Six inches over two kilometres. C06's ladder runs 3–8 in, and the
    /// line size is the classic trade — cheap steel with expensive
    /// consequences.</para>
    /// </summary>
    public static PipeGeometry Flowline { get; } = new(
        PipeLength: new Length(2_000.0),
        InnerDiameter: new Length(0.1524),
        Roughness: 4.6e-5,
        ElevationRise: new Length(0.0));

    public static Pressure FlowlineRating { get; } = Pressure.FromBar(100.0);

    /// <summary>
    /// The disposal well's completion (SDD-003 §3.1d). Its INJECTIVITY is what
    /// throttles a watered-out field, and the plugging term is why that gets
    /// worse: the formation takes less every year, so disposal is an ongoing
    /// problem rather than a one-time build.
    /// </summary>
    public static Wells.InjectionConditions Disposal { get; } = new(
        Permeability: new Permeability(5.0e-13),
        InjectionInterval: new Length(40.0),
        DrainageArea: new Area(2.0e5),
        WellboreRadius: new Length(0.108),
        WaterViscosity: new Viscosity(0.5e-3),
        InitialSkin: 0.0,
        PluggingPerReferenceVolume: 2.0,
        ReferenceVolume: new ReservoirVolume(1.0e6));

    /// <summary>What the disposal pump delivers at. Above the formation's own
    /// pressure, or nothing goes in at all (SDD-003 §3.1d).</summary>
    public static Pressure DisposalPressure { get; } = new(28.0e6);

    /// <summary>
    /// The pressure of the formation being disposed INTO — a different rock from
    /// the one being produced, which is what a disposal well is.
    ///
    /// <para>It does not move with the field's depletion, and that is the point:
    /// a disposal well is somewhere to put water, not pressure support. Injection
    /// that supports the reservoir is the same element committing a RECEIPT
    /// against the compartment instead of disposing, and SDD-002 §9 keeps the two
    /// apart so the same mass cannot land on both sides of the tick
    /// balance.</para>
    /// </summary>
    public static Pressure DisposalFormationPressure { get; } = new(20.0e6);

    /// <summary>
    /// The flare's throughput, generous because a flare that binds would shut a
    /// field in for want of somewhere to put its gas — a real late-life failure
    /// and a balance decision R20.4 owns, not one to ship by accident.
    /// </summary>
    public static MassRate FlareCapacity { get; } = new(200.0);

    /// <summary>SDD-006 §3's combustion efficiency. 0.98 is the industry figure
    /// for a well-run flare; the 2% unburnt is methane and is what makes routine
    /// flaring an emissions problem rather than merely a waste one.</summary>
    public const double FlareCombustionEfficiency = 0.98;

    /// <summary>
    /// The field's header. Eight slots — a fixed manifold from catalogue C06's
    /// bottom rung, which is a real limit on how many wells this field can carry
    /// and the reason a ninth well is refused before it is paid for rather than
    /// after.
    /// </summary>
    public static Facilities.ManifoldTier ManifoldTier { get; } =
        new(new ContentId("manifold-fixed-8"), Slots: 8);

    /// <summary>
    /// The field's one vessel. 15 bar is the separator inlet pressure the loop
    /// used to hard-code as a wellhead backpressure — the same number, now held
    /// by the element that imposes it rather than by the stage that read it.
    ///
    /// <para><b>The liquid capacity BINDS, and that is the point.</b> A well on
    /// this field delivers about 10 kg/s of liquid once it makes water as well
    /// as oil, so the first vessel carries one well and is over capacity on the
    /// second — the player sees the
    /// separator refusing production on the read model and has to do something
    /// about it. A vessel sized never to bind would make every downstream
    /// mechanic — the throttle, the deferral attribution, the bottleneck report —
    /// machinery that runs and is never felt.</para>
    ///
    /// <para>Which is the whole shape of an operations game: the constraint is
    /// visible, it is nameable, and it is bought past. R20.4 owns where exactly
    /// the number lands; that it binds at all is a design decision, not a balance
    /// one.</para>
    ///
    /// <para>The efficiencies are perfect because there is one material and
    /// nothing to carry over — a number below 1.0 would describe a separation
    /// this content cannot express.</para>
    /// </summary>
    public static Facilities.SeparatorTier SeparatorTier { get; } = new(
        new ContentId("separator-3phase-e1"),
        GasCapacity: new MassRate(50.0),
        LiquidCapacity: new MassRate(12.0),
        Volume: new ReservoirVolume(30.0),
        RatedEfficiency: new SeparationEfficiency(
            LiquidFromGas: 0.0, GasFromLiquid: 0.0, WaterFromLiquid: 0.0,

            // SOLVED AGAINST THE CHAIN, at the third attempt, and the first two
            // are why this comment is long. BS&W = c·W/(O + c·W), so c is the
            // fraction of produced water the vessel leaves in the oil leg.
            //
            // 0.005 came from a plausible sentence and produced a treater that
            // removed 0.0003 kg/s (finding 178). 0.03 was solved from a W/O of
            // 0.203 — a number that was never what this chain delivers: METERED
            // at the treater's own inlet over forty years on six wells, the
            // developed field reaches W/O = 0.127, so BS&W peaked at 0.379%
            // against a 0.5% limit and the gate could not fire in 460 flowing
            // months (finding 183). A mechanism composed, connected, priced and
            // unreachable — twice.
            //
            // 0.07 is measured rather than solved backwards: BS&W crosses the
            // limit in year 34 and reaches 0.88% by year forty, so a field sells
            // on spec for two thirds of its life and cannot sell at all in the
            // last third without a treater. It is also the more honest number on
            // its own terms — crude off a three-phase separator carries 5–15%
            // water before dehydration, and 0.03 sat below that band.
            WaterIntoLiquid: 0.07),
        DesignRate: new ReservoirRate(0.05),
        OperatingPressure: Pressure.FromBar(15.0));

    /// <summary>
    /// The vessel ladder (catalogue C07), in the order a field climbs it.
    ///
    /// <para>DECLARED ORDER, not sorted by capacity: the rungs are a progression
    /// a designer authored, and a bigger-is-later rule would silently reorder a
    /// ladder whose rungs trade throughput against something else. The first rung
    /// is what a field is built with.</para>
    /// </summary>
    public static IReadOnlyList<Facilities.SeparatorTier> SeparatorLadder { get; } =
    [
        SeparatorTier,
        SeparatorTier with
        {
            Id = new ContentId("separator-3phase-e2"),
            GasCapacity = new MassRate(150.0),
            LiquidCapacity = new MassRate(40.0),
            Volume = new ReservoirVolume(90.0),
            DesignRate = new ReservoirRate(0.15),
        },
    ];

    /// <summary>
    /// What a bigger vessel costs and how long it takes. NO RIG (SDD-007 §1's
    /// null case): construction crews are not the drilling rig, so a field can
    /// debottleneck and drill in the same months — which is what makes the
    /// decision interesting rather than merely sequential.
    /// </summary>
    /// <summary>
    /// What plugging a well costs, and how long it takes. NO RIG in this
    /// composition's terms — a plugging crew is not the drilling rig, so a
    /// company can close down one well while drilling another.
    ///
    /// <para>$3M is a real onshore figure and it is deliberately not small: the
    /// obligation is registered the moment the well is drilled, so a player who
    /// has run their field into the ground may find they cannot afford to leave.
    /// That is the authentic and uncomfortable shape of late life.</para>
    /// </summary>
    public static ActivityTerms AbandonWellTerms { get; } = new(
        Template: new ContentId("abandon-well"),
        Cost: Money.FromMillions(3.0),
        DurationTicks: 2,
        Rig: null,
        WeatherLimit: 6.0,   // rig-based, like the drilling it undoes
        Outcomes: SurveyOutcomes);

    /// <summary>
    /// What an obligation is estimated at, by template (SDD-007 §6). Content in
    /// a finished game; here the abandonment activity's own price, so the
    /// liability on the books and the bill when it falls due cannot disagree.
    /// </summary>
    public static Money AbandonmentCostOf(ContentId template) =>
        template == AbandonWellTerms.Template
            ? AbandonWellTerms.Cost
            : throw new ContentFault("SDD-007 §6", null,
                $"no abandonment template '{template.Value}' is priced; an obligation " +
                "nobody can cost is a liability nobody can plan for");

    /// <summary>
    /// What a gas plant costs and how long it takes. DEARER THAN A SEPARATOR:
    /// compression, dehydration and a tie-in to somewhere that will take the
    /// gas, which is most of a small facility.
    /// </summary>
    public static ActivityTerms InstallGasPlantTerms { get; } = new(
        Template: new ContentId("install-gas-plant"),
        Cost: Money.FromMillions(18.0),
        DurationTicks: 5,
        Rig: null,
        WeatherLimit: 5.0,   // heavy lift
        Outcomes: SurveyOutcomes);

    /// <summary>
    /// The treating ladder (catalogue C08). The first rung takes NOTHING out — a
    /// field ships without a treater, sells dry oil while it is young, and only
    /// needs one when the water arrives.
    ///
    /// <para>Ninety per cent is what a heater-treater does and a desalter behind
    /// it does more. Neither is perfect, which matters: a field far enough into
    /// its water cut eventually sells wet oil whatever it buys, and that is the
    /// end of a field rather than a failure to shop.</para>
    /// </summary>
    public static IReadOnlyList<Facilities.TreaterTier> TreaterLadder { get; } =
    [
        new(new ContentId("treater-none"), WaterRemoved: 0.0),
        new(new ContentId("heater-treater"), WaterRemoved: 0.90),
        new(new ContentId("heater-treater-desalter"), WaterRemoved: 0.98),
    ];

    /// <summary>
    /// The storage ladder (catalogue C09). Stage 6 names three answers to a full
    /// tank — "more storage, more export and less production" — and storage was
    /// the one nothing could buy.
    ///
    /// <para>Doubling, like the export line and for the same reason: a rung that
    /// added a tenth would be an obvious yes at every field size and therefore
    /// not a decision. Storage buys TIME rather than throughput — it is what
    /// keeps a field producing through a shipping gap, and what a company
    /// chooses instead of a bigger pipeline when the constraint is lumpy rather
    /// than steady.</para>
    /// </summary>
    public static IReadOnlyList<Facilities.TankTier> TankLadder { get; } =
    [
        TankTier,
        TankTier with
        {
            Id = new ContentId("tank-farm-e2"),
            Capacity = new Mass(TankTier.Capacity.Kilograms * 2.0),
        },
    ];

    /// <summary>
    /// What more storage costs and how long it takes. Cheap per tonne against a
    /// pipeline, and slow — a tank is civil work, and the field goes on
    /// producing around it.
    /// </summary>
    public static ActivityTerms InstallTankTerms { get; } = new(
        Template: new ContentId("install-tank"),
        Cost: Money.FromMillions(7.0),
        DurationTicks: 6,
        Rig: null,
        WeatherLimit: 5.0,   // heavy lift
        Outcomes: SurveyOutcomes);

    /// <summary>
    /// The header ladder (catalogue C06). Eight slots, then sixteen.
    ///
    /// <para>The drilling command has refused a well with "a bigger header has
    /// to be installed first" since R12b, and until now nothing could install
    /// one — a refusal that named an answer the engine did not have. Eight wells
    /// is a long way into a field's life, which is exactly why it went
    /// unnoticed.</para>
    /// </summary>
    public static IReadOnlyList<Facilities.ManifoldTier> ManifoldLadder { get; } =
    [
        ManifoldTier,
        ManifoldTier with { Id = new ContentId("manifold-16slot"), Slots = 16 },
    ];

    /// <summary>
    /// What a bigger header costs and how long it takes. A header is steel and
    /// tie-ins on a site that is already producing, so the work is done around a
    /// live field — dearer than a vessel for its size, and slower.
    /// </summary>
    public static ActivityTerms InstallManifoldTerms { get; } = new(
        Template: new ContentId("install-manifold"),
        Cost: Money.FromMillions(9.0),
        DurationTicks: 4,
        Rig: null,
        WeatherLimit: 4.5,   // a subsea lift is the least tolerant work in the catalogue
        Outcomes: SurveyOutcomes);

    /// <summary>What a treater costs and how long it takes.</summary>
    public static ActivityTerms InstallTreaterTerms { get; } = new(
        Template: new ContentId("install-treater"),
        Cost: Money.FromMillions(5.0),
        DurationTicks: 3,
        Rig: null,
        WeatherLimit: 5.0,   // heavy lift
        Outcomes: SurveyOutcomes);

    /// <summary>
    /// What an acid job costs and how long it takes (R10-V4). Cheap against a
    /// vessel and quick against a well — which is the point: it is the sort of
    /// maintenance a company defers because it always looks affordable later,
    /// and the plugging goes on either way.
    /// </summary>
    public static ActivityTerms RemediateInjectorTerms { get; } = new(
        Template: new ContentId("remediate-injector"),
        Cost: Money.FromMillions(1.2),
        DurationTicks: 1,
        Rig: null,
        WeatherLimit: 6.5,   // a wellsite intervention, lighter than a rig job
        Outcomes: SurveyOutcomes);

    /// <summary>
    /// A PLANNED OVERHAUL (SDD-012 §3). A month and rather less than a new
    /// vessel, because it is the same vessel: what is bought is the years of
    /// hazard the decay curve was about to charge, not a capability the field
    /// did not have.
    ///
    /// <para>Priced under every install in this catalogue on purpose. If keeping
    /// equipment cost what replacing it costs, maintenance would never be the
    /// answer to anything and §3's three strategies would collapse to one.</para>
    /// </summary>
    public static ActivityTerms ServiceEquipmentTerms { get; } = new(
        Template: new ContentId("service-equipment"),
        Cost: Money.FromMillions(0.8),
        DurationTicks: 1,
        Rig: null,
        WeatherLimit: 8.0,   // planned maintenance happens inside the module it maintains
        Outcomes: SurveyOutcomes);

    /// <summary>
    /// A CONDITION-MONITORING KIT (catalogue C14, SDD-012 §3). Vibration and
    /// temperature on one asset, which is what makes that asset's wear a fact
    /// the company holds rather than a thing the engine happens to know.
    ///
    /// <para>A quarter of a planned service and a month to fit, because it is a
    /// sensor and a wire rather than a crew and a crane. The decision is not
    /// whether one kit is affordable — it obviously is — but whether
    /// instrumenting a dozen elements is worth it before any of them has told
    /// you anything.</para>
    /// </summary>
    public static ActivityTerms InstallMonitoringTerms { get; } = new(
        Template: new ContentId("install-monitoring"),
        Cost: Money.FromMillions(0.2),
        DurationTicks: 1,
        Rig: null,
        WeatherLimit: 8.0,   // fitting a kit to equipment already in place, under cover
        Outcomes: SurveyOutcomes);

    /// <summary>
    /// AN EMERGENCY REPAIR (SDD-012 §3, R20d.26.2 amendment). Three times the
    /// planned job, which is inside the 2–5× that unplanned industrial work
    /// genuinely runs — parts freighted rather than stocked, a crew mobilised
    /// rather than scheduled — and the same multiple the duration experiment
    /// used before finding 187 reverted it.
    ///
    /// <para>The same month as the planned job, deliberately: the duration half
    /// of the asymmetry is the lever this field cannot afford — a month of
    /// outage is ~$12M at plateau against a worst-seed fifth-year margin of
    /// $17M — so the asymmetry ships in money alone, and the measurement decides
    /// whether that is enough to make preventive work a strategy.</para>
    /// </summary>
    public static ActivityTerms RepairEquipmentTerms { get; } = new(
        Template: new ContentId("repair-equipment"),
        Cost: Money.FromMillions(2.4),
        DurationTicks: 1,
        Rig: null,
        WeatherLimit: 7.5,   // emergency work is done in weather planned work would wait out
        Outcomes: SurveyOutcomes);

    public static ActivityTerms InstallSeparatorTerms { get; } = new(
        Template: new ContentId("install-separator"),
        Cost: Money.FromMillions(6.0),
        DurationTicks: 3,
        Rig: null,
        WeatherLimit: 5.0,   // heavy lift
        Outcomes: SurveyOutcomes);

    /// <summary>
    /// What the sales contract requires. EMPTY, and honestly: a specification is
    /// a list of limits on measured stream properties, this composition ships one
    /// material with no contaminants, and every limit that could be written would
    /// bound a fraction that is structurally zero.
    ///
    /// <para>The gate is still real — it meters, it has its Reject leg, and a
    /// spec arrives as content the day there is a sour or wet stream to fail it
    /// (R20d.3). An empty spec passing everything is the correct answer for a
    /// stream that cannot be off-spec, not a disabled check.</para>
    /// </summary>
    public static Specification SalesSpec { get; } = new(
    [
        // HALF A PER CENT OF WATER, the ordinary crude sales limit. The spec was
        // EMPTY and honestly so: until a separator could carry water into the
        // oil, every limit that could have been written would have bounded a
        // fraction that was structurally zero (finding 173).
        new SpecLimit(SpecProperty.BasicSedimentAndWater, 0.005),
    ]);

    /// <summary>
    /// What the meter reads off a stream.
    ///
    /// <para>BS&amp;W IS MEASURED NOW — water mass over liquid mass, both of
    /// which the catalogue carries. It read a hard zero with a comment promising
    /// "it becomes a measurement when there is a stream to measure", and there
    /// was one as soon as a separator could carry water into the oil.</para>
    ///
    /// <para>The rest stay zero because they remain structurally zero: no H2S
    /// until souring makes some, no CO2, no light ends. That is the right answer
    /// for a stream that cannot carry them, not a disabled check.</para>
    /// </summary>
    public static Facilities.StreamProperties MeasureStream(MaterialStream stream)
    {
        double water = stream.MassRates[WaterOrdinal].KgPerSecond;
        double oil = stream.MassRates[OilOrdinal].KgPerSecond;
        double liquid = water + oil;

        return new Facilities.StreamProperties(
            BasicSedimentAndWater: liquid <= 0.0 ? 0.0 : water / liquid,
            H2SFraction: 0.0, Co2Fraction: 0.0,
            WaterInGasFraction: 0.0, LightEndsFraction: 0.0,
            Heating: new HeatingValue(0.0));
    }

    /// <summary>
    /// Surface ambient, for the segment context. A stated value until R22 builds
    /// an environment to supply one (R20d.13) — stated HERE rather than defaulted
    /// inside the solve, so the day weather arrives there is exactly one place
    /// that stops being a constant.
    /// </summary>
    public static Temperature SurfaceAmbient { get; } = Temperature.FromCelsius(15.0);

    /// <summary>Stock-tank oil density — what custody mass divides by to become
    /// the barrels a player reads.</summary>
    public static Density SurfaceOilDensity { get; } = Density.FromSpecificGravity(0.85);

    /// <summary>Produced water at standard conditions — brine, a little denser
    /// than fresh.</summary>
    public static Density WaterSurfaceDensity { get; } = Density.FromSpecificGravity(1.05);

    /// <summary>
    /// The shipped rock's Corey curve (SDD-003 §3.1c) — what turns a water
    /// saturation into a water cut, and therefore when a field waters out.
    ///
    /// <para>A water-wet sandstone at its ordinary numbers: exponents of 2 and 3,
    /// endpoints that leave a broad mobile range. The MOBILITY RATIO these imply
    /// is what makes the S-curve steep or gentle, and it is the number a player
    /// is really looking at when a field waters out early.</para>
    /// </summary>
    /// <summary>
    /// The shipped field's drive (SDD-003 §4.2b). WATER DRIVE, and that is a
    /// game decision as much as a geological one: a water-drive compartment
    /// admits aquifer influx, so it holds its pressure up, produces longer — and
    /// waters out. Solution-gas drive refuses influx and would give a field that
    /// simply declines and stops, which is a shorter and quieter story.
    /// </summary>
    public static ContentId Drive { get; } = new("water-drive");

    // ------------------------------------------------------------- the aquifer
    //
    // WHAT MAKES THE ARC FORTY YEARS RATHER THAN FOUR. The first numbers here
    // were a token aquifer — a maximum influx of 1% of the pore volume and a
    // productivity index an order of magnitude too small to replace what eight
    // wells take. The field held its plateau for two months, peaked, and was
    // finished inside four years, which is not a field's life; it is a tank
    // being emptied.
    //
    // Sized as a real one, the aquifer holds the pressure up, the plateau lasts,
    // and the water it pushes in arrives at the producers — which is the story
    // the late game is made of and the reason any of the water work matters.

    /// <summary>
    /// What a company believes about a petroleum system before it has drilled
    /// anything (SDD-008 §4). Mean 0.7 per factor — and five of those multiply
    /// to about one chance in six, which is the honest arithmetic of exploration
    /// and the reason a player reasoning factor by factor over-estimates.
    ///
    /// <para>The MAGNITUDE is the deliberate part: α+β = 4 is four wells' worth
    /// of conviction, so the first result moves the number visibly and the
    /// tenth barely does. A heavy prior would make drilling uninformative, which
    /// would take the campaign out of exploration.</para>
    /// </summary>
    public static FactorBelief ExplorationPrior { get; } = new(Alpha: 2.8, Beta: 1.2);

    /// <summary>
    /// How confidently a trap of each subtlety class is mapped (design 06 §2.2).
    /// A four-way dome on good seismic is nearly certain to be there; a subtle
    /// stratigraphic pinch-out is a interpretation somebody could be wrong
    /// about — which is exactly what a detect class means, expressed as risk
    /// rather than as visibility alone.
    /// </summary>
    public static double TrapConfidenceOf(DetectClass subtlety) => subtlety switch
    {
        DetectClass.D0 => 0.95,
        DetectClass.D1 => 0.80,
        DetectClass.D2 => 0.60,
        DetectClass.D3 => 0.40,
        _ => throw new ContentFault("SDD-008 §4", null,
            $"no trap confidence is stated for detect class {subtlety}"),
    };

    /// <summary>The pressure a fresh field and its aquifer both start at.</summary>
    public static Pressure InitialReservoirPressure { get; } = new(30.0e6);

    /// <summary>
    /// HOW BIG THE WATER LEG IS, as a multiple of the field's own pore volume
    /// (SDD-003 §3.3a) — so the same number means the same thing whatever the
    /// field, which an absolute volume cannot do.
    ///
    /// <para>SEVERAL TIMES the pore volume, because a Fetkovich aquifer's own
    /// pressure falls in proportion to what it has delivered: one sized like the
    /// field runs itself down to the reservoir's pressure after a tenth of its
    /// water has arrived, and then stops. The field held its pressure and never
    /// watered out, which took the late game with it. A regional aquifer keeps
    /// pushing, so the water reaches the producers, the cut climbs the S-curve
    /// and the field ends by drowning rather than by running dry — which is how
    /// most fields actually end.</para>
    /// </summary>
    public const double AquiferStrength = 4.0;

    /// <summary>
    /// τ — how QUICKLY that water arrives, held separate from how much of it
    /// there is (SDD-003 §3.3a). Forty years: a regional aquifer is slow, which
    /// is why a field's early pressure is its own expansion and the water only
    /// becomes the story a decade or two in.
    ///
    /// <para>The two numbers are independent on purpose. A big slow aquifer and
    /// a small fast one are different fields to develop, and one strength
    /// parameter could not tell them apart.</para>
    /// </summary>
    public static Duration AquiferResponseTime { get; } = Duration.FromTicks(40.0 * 12.0);

    public static RelativePermeabilityCurve Wettability { get; } =
        RelativePermeabilityCurve.Validated(
            swc: 0.30, sor: 0.25, krwMax: 0.35, kroMax: 0.90, nw: 3.0, no: 2.0);

    // ------------------------------------------------------ reality profiles
    //
    // Design 18 §5b's fidelity axis, at its two shipped ends. Content in a
    // finished game (R25.1); stated here as the records a loader will produce.

    public static ModelSlot FluidSlot { get; } = new("fluid-properties");

    /// <summary>
    /// The full model everywhere. The EMPTY bundle, and correctly so: a profile
    /// names departures from the shipped set, and simulation IS the shipped set.
    /// </summary>
    public static RealityProfile Simulation { get; } = new(new ContentId("simulation"), []);

    /// <summary>
    /// Fluid properties computed simply (SDD-005 §7b). Everything a decision is
    /// made on survives — oil shrinks, gas comes out of solution, the chain and
    /// the meter behave identically. What goes is the pressure dependence a
    /// player cannot perceive.
    /// </summary>
    public static RealityProfile Arcade { get; } = new(
        new ContentId("arcade"),
        [new SetModelSelection(FluidSlot, new ContentId("arcade-fluid"))]);

    /// <summary>
    /// The shipped profiles, walked in declared order (D-5). A run naming one
    /// that is not here is refused when the engine composes, rather than
    /// silently played at whatever the modules happened to choose.
    /// </summary>
    public static IReadOnlyList<RealityProfile> Profiles { get; } = [Simulation, Arcade];

    public static RealityProfile ProfileNamed(ContentId id)
    {
        for (int i = 0; i < Profiles.Count; i++)
            if (Profiles[i].Id == id) return Profiles[i];

        throw new ContentFault("SDD-005 §7b", null,
            $"no reality profile '{id.Value}' is composed; a run cannot be played at a " +
            "fidelity nobody defined");
    }

    /// <summary>
    /// The one rock this composition ships (SDD-012 §5). Named rather than
    /// implied, so the day there is a second one the curve is selected by an id
    /// that already exists instead of by a field being invented.
    /// </summary>
    public static ContentId TheRock { get; } = new("sandstone-e1");

    /// <summary>
    /// SDD-012 §5's souring curve — <c>ppm = ultimate·r/(half + r)</c> against
    /// the sea water a compartment has taken, in pore volumes.
    ///
    /// <para>2,000 ppm ultimate is a properly sour field: anything above about
    /// 100 ppm needs sour-service metallurgy, and North Sea fields soured by
    /// seawater flooding have reached thousands. A half-ratio of 0.25 pore
    /// volumes puts the knee in the middle of a flood's life — R20d.24's own
    /// measurement is 0.18 PV over forty years at VRR 1, so a flooded field
    /// arrives at roughly 42% of the ultimate and an unflooded one at nothing.
    /// That is the shape §5 asks for: the H2S turns up two decades after the
    /// decision that bought it, and never on a field that never flooded.</para>
    /// </summary>
    public static ISouringModel SourCurve { get; } =
        new Integrity.SaturatingSourCurve(
            new ContentId("sour-curve-sandstone"), ultimatePpm: 2_000.0, halfRatio: 0.25);

    /// <summary>
    /// What this model calls fully sour service, in ppm (SDD-012 §5's R20d.25
    /// amendment).
    ///
    /// <para>1,000 ppm — the concentration at which a wetted carbon-steel plant
    /// is in genuinely aggressive service rather than merely sour. §1's
    /// `SourFactor` is then a coefficient on a 0..1 fraction like every other
    /// term, instead of a number of order a thousand whose only job would be to
    /// undo the choice of ppm as a unit.</para>
    /// </summary>
    public const double SouringReferencePpm = 1_000.0;

    public static Integrity.DegradationCoefficients Decay { get; } =
        new(BaseRatePerYear: 0.05, WaterCutFactor: 1.0, SourFactor: 2.0,
            DutyFactor: 0.5, TemperatureFactor: 1.5, ServiceIntervalFactor: 0.2);

    /// <summary>
    /// The one climate this world has (SDD-016 §1). A temperate offshore basin:
    /// rough winters, workable summers, and a temperature curve that runs the
    /// other way — the amplitude is NEGATIVE because a rough day is a cold one,
    /// which is what makes the two curves over one x describe real weather
    /// rather than two independent noises.
    /// </summary>
    public static Environment.ClimateProfile Climate { get; } =
        new(new ContentId("temperate-offshore"),
            Persistence: 0.75,
            Baseline: [5.2, 5.0, 4.4, 3.6, 2.9, 2.4, 2.2, 2.5, 3.2, 4.1, 4.8, 5.1],
            Amplitude: [1.6, 1.6, 1.4, 1.2, 1.0, 0.9, 0.9, 1.0, 1.2, 1.4, 1.5, 1.6],
            TemperatureBaseline: [6.0, 5.6, 6.4, 8.2, 10.8, 13.4,
                                  15.1, 15.4, 14.0, 11.6, 9.0, 7.0],
            TemperatureAmplitude: -1.8);

    /// <summary>
    /// The severity an operation can work through (SDD-016 §3). ONE limit for
    /// every template, which is a simplification the SDD names: §3 puts a
    /// `weatherClass` on each template and each weather-exposed element, and that
    /// arrives with the content pipeline (R20c.9). Until then a single limit is
    /// honest about being one number rather than pretending to be a table.
    ///
    /// <para>Set at 6.0 against the climate above: a winter month averages 5.2
    /// with an amplitude of 1.6, so roughly a third of January is lost and a
    /// July is not — which is the seasonality the mechanic exists to create.</para>
    /// </summary>
    public const double OperationWeatherLimit = 6.0;
}

/// <summary>
/// SDD-008 §3's slot, at its shipped setting.
///
/// <para>A TABLE OVER (SOURCE, KIND), not over source alone. Keying on the source
/// gave every source sight of every kind at one number, which let a build-up
/// measure the size of an accumulation better than seismic could and made
/// shooting a survey pointless (finding 149). What distinguishes a core from a
/// log is which kinds it can see and how small its sigma is — both halves.</para>
///
/// <para>Every σ is ABSOLUTE IN THE KIND'S DECLARED SPACE: porosity in porosity
/// units, pressure in pascals, permeability and oil-in-place in natural-log units
/// where an absolute σ is a relative one.</para>
/// </summary>
internal sealed class RegionalObservationModel : IObservationModel
{
    public ContentId Id { get; } = new("regional-observation");

    public double? SigmaFor(ContentId source, ContentId propertyKind, EntityRef subject) =>
        (source.Value, propertyKind.Value) switch
        {
            // Regional data is deliberately coarse: a player who could book
            // reserves off a gravity and magnetics pass would never buy seismic
            // (R15-V10). 1.2 in log units is a factor of three either way.
            ("regional", "oil-in-place") => 1.2,

            // Seismic sees SIZE and nothing else — no downhole measurement can
            // reach the areal extent of an accumulation, and nothing on the
            // surface can read the rock at the wellbore. That split is what makes
            // the two worth buying separately (design 05 §2).
            ("seismic-2d", "oil-in-place") => 0.6,
            ("seismic-3d", "oil-in-place") => 0.35,

            // AND IT SEES A STRUCTURE WHETHER OR NOT ANYTHING IS IN IT
            // (SDD-010 §4b). This is the pair a survey over an undrilled
            // prospect actually uses — the one above needs an accumulation to
            // measure, which is exactly what has not been established yet.
            //
            // Sharper than the same source's oil-in-place row, because it is an
            // easier question: mapping a closure is geometry, and how much oil
            // sits in it is not.
            ("seismic-2d", "structure-capacity") => 0.45,
            ("seismic-3d", "structure-capacity") => 0.25,

            // A DISCOVERY WELL sees the oil itself — the column, the contacts,
            // a sample of the rock — so it answers the question no surface
            // survey can: how much is actually in there. Sharper than anything
            // shot from above and still nowhere near certain, because one hole
            // has seen one point of a field and the other kilometre of it is
            // inference.
            ("discovery-well", "oil-in-place") => 0.30,

            // A log reads porosity well and permeability only through a
            // transform, which is why 0.5 in log units — a factor of 1.65 — is
            // the honest number rather than a small one.
            ("well-log", "porosity") => 0.02,
            ("well-log", "permeability") => 0.5,

            // The laboratory has the rock in its hands. An order of magnitude
            // sharper than the log on both, and several times the price.
            ("core", "porosity") => 0.005,
            ("core", "permeability") => 0.15,

            // A build-up is the sharpest measurement of a compartment there is:
            // it watches the reservoir answer for itself over days. It is the
            // ONLY source that can see pressure at all, and it beats even a core
            // on permeability because it measures what the reservoir flows at
            // rather than what one plug does. That is why it is worth shutting a
            // well in for (SDD-008 §3).
            ("well-test", "reservoir-pressure") => 1.0e5,
            ("well-test", "permeability") => 0.10,

            // NULL, not a wide sigma: a source that cannot see a kind sees
            // NOTHING, and the difference is what makes a subtle trap invisible
            // rather than merely uncertain (SDD-008 §3).
            _ => null,
        };
}

/// <summary>
/// Which fault policy the engine runs under — the one composition-time choice
/// the host must make and cannot be defaulted (law L2).
/// </summary>
public enum FaultHandling
{
    /// <summary>Every fault halts. CI and scenario runs, where a fault is a
    /// test failure and continuing would hide it.</summary>
    Strict,

    /// <summary>Model faults abandon the tick; invariant faults still halt.
    /// Shipping games, where one bad month is not a reason to end the save.</summary>
    Resilient,
}

/// <summary>What the host must supply. No member has a default.</summary>
public sealed record EngineSettings(
    GameDate Epoch,
    ulong WorldSeed,
    AuditRetention Retention,
    ILogSink LogSink,
    LogLevel MinimumLogLevel,
    FaultHandling FaultHandling,

    /// <summary>
    /// Which reality profile the run is played at (design 18 §5b, SDD-005 §7b) —
    /// arcade, standard or simulation.
    ///
    /// <para>A composition-time choice like the fault policy, and for the same
    /// reason: it decides which implementation of each replaceable slot is
    /// registered, so it has to be known before anything is built. Changing it
    /// mid-game is a recompose, which is why 18 §5b.5 calls it "allowed and
    /// logged" rather than free.</para>
    /// </summary>
    ContentId RealityProfile);

/// <summary>
/// A composed engine: the validated module set, the tick it runs, and the two
/// things a player does — issue a command and read what happened.
/// </summary>
public sealed record Engine(
    IReadOnlyList<IModule> Modules,
    TickPipeline Pipeline,
    IAuditTrail Audit,
    EventBus Events,
    StateRegistry State,
    IResolvedContracts Provided,
    ICommandBus Commands)
{
    /// <summary>
    /// The tick just closed, as the player sees it — null before the first tick,
    /// because a game that has not started has nothing to show and a zeroed
    /// model would be a lie about a month that never happened.
    /// </summary>
    public FieldReadModel? ReadModel => Provided.Resolve<CloseStage>().Published;


    // Finding 131.
    public bool Equals(Engine? other) =>
        other is not null && ReferenceEquals(Pipeline, other.Pipeline)
        && ReferenceEquals(Audit, other.Audit) && ReferenceEquals(Events, other.Events)
        && ReferenceEquals(State, other.State) && ReferenceEquals(Provided, other.Provided)
        && ReferenceEquals(Commands, other.Commands)
        && Structural.Equal(Modules, other.Modules);

    public override int GetHashCode() =>
        HashCode.Combine(Pipeline, Audit, Events, State, Provided, Commands,
            Structural.HashOf(Modules));
}

/// <summary>
/// The build outcome. <c>Built</c> is composition's own — it carries an
/// <see cref="Engine"/>, which is a Layer 4 type the contract layer cannot name
/// — but a refusal is reported with the CONTRACT's
/// <see cref="EngineCompositionRefused"/> rather than a second record saying the
/// same thing. Two names for one concept is what glossary rule N1 forbids, and
/// a host that had to translate between them would be doing so at exactly the
/// moment it is trying to print why the engine would not start.
/// </summary>
public abstract record BuildResult;

public sealed record Built(Engine Engine) : BuildResult;

/// <summary>Every problem, never just the first (R1 §2.9).</summary>
public sealed record BuildRefused(EngineCompositionRefused Refusal) : BuildResult
{
    public IReadOnlyList<CompositionProblem> Problems => Refusal.Problems;
}

/// <summary>Design 03 §8's Layer 4 — the one place that knows what implements what.</summary>
public static class EngineBuilder
{
    /// <summary>
    /// The fourteen shipped modules, in declaration order.
    ///
    /// <para>Declaration order determines NOTHING. Construction order comes from
    /// the dependency graph and execution order from the stage numbering, both
    /// derived by the composer — so listing them here in any order yields the
    /// same engine, which is the property that makes Requires worth declaring
    /// (SDD-001 §9, finding 126).</para>
    /// </summary>
    internal static IReadOnlyList<IModule> ShippedModules(
        AuditTrail audit, SimulationClock clock, IRandomSource random,
        RealityProfile profile) =>
    [
        new SubsurfaceModule(),
        new WellsModule(),
        new FlowModule(),
        new FacilitiesModule(),
        new OperationsModule(),
        new CompanyModule(),
        new InformationModule(),
        new WorldModule(),
        new CapabilitiesModule(),
        new IntegrityModule(),
        new EnvironmentModule(),
        new HseModule(),
        new ObjectivesModule(),
        new MaterialsModule(profile),
        new FieldModule(),
        new DiagnosticsModule(audit, clock, random),
    ];

    /// <summary>
    /// START A NEW GAME (SDD-010 §4, SDD-017 §1b). Composes the engine, then runs
    /// world generation into it — in that order, because the generator writes
    /// truth into module stores and there are no stores until composition has
    /// built them.
    ///
    /// <para>THIS IS WHERE A GAME LEARNS WHAT IT IS ABOUT. Everything that
    /// follows — how much there is to find, how deep and how hot it is, how hard
    /// it is to see, how big a well's drainage is, what the company believes on
    /// the first morning — comes from what the generator drew here. Nothing
    /// downstream states a field's size or its position; they are consequences of
    /// this call, which is the whole point of generating a world rather than
    /// authoring one.</para>
    ///
    /// <para>A refusal to compose is returned untouched rather than generated
    /// into: an engine that could not be built has nowhere to put a world.</para>
    /// </summary>
    public static BuildResult CreateNew(EngineSettings settings, WorldParameters world)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(world);

        BuildResult built = Build(settings);

        if (built is not Built ready) return built;

        ready.Engine.Provided.Resolve<IWorldGenerator>().Generate(
            world,

            // Built HERE rather than composed: a sink writes truth once, at
            // creation, and then has nothing left to do. Composing it would put
            // a one-shot writer in the container for the life of the game and
            // make the world module depend on the field module to get it.
            new WorldSink(
                ready.Engine.Provided.Resolve<FieldControl>(),
                ready.Engine.Provided.Resolve<IBeliefStore>(),
                ready.Engine.Provided.Resolve<WorldState>(),
                ready.Engine.Provided.Resolve<OGSim.Information.ProspectRisks>()),

            // The world-generation stream, and only it. Adding a draw to any
            // other subsystem can never shift what this world contains
            // (SDD-001 §4's eight named streams).
            ready.Engine.Provided.Resolve<IRandomSource>().Stream(StreamId.WorldGen));

        // AND THE LINE IS DRAWN HERE (SDD-010 §4c). Everything the generator has
        // just placed is a function of the seed and is regenerated on load;
        // everything placed after this moment is a decision the game made and is
        // replayed from the save. Sealed at the one instant both statements are
        // true — after generation, before the first tick.
        // AND WHAT IT WAS DRAWN FROM travels with the line (SDD-010 §4c.1),
        // because a reload has to call this same generator with the same
        // parameters and the save is the only thing that can tell it what they
        // were. Recorded here rather than anywhere else for the same reason the
        // boundary is: this is the one instant the caller is holding both.
        ready.Engine.Provided.Resolve<WorldState>().SealGeneration(world);

        return built;
    }

    /// <summary>Composes the shipped set.</summary>
    public static BuildResult Build(EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var clock = new SimulationClock(settings.Epoch);
        var audit = new AuditTrail(clock, settings.Retention);

        return Build(
            settings,
            ShippedModules(audit, clock, new RandomSource(settings.WorldSeed),
                           Defaults.ProfileNamed(settings.RealityProfile)),
            clock, audit);
    }

    /// <summary>
    /// Composes the shipped set AS OF a tick — the door a load comes through
    /// (SDD-013, R20d.12).
    ///
    /// <para>The clock is restored BEFORE the modules compose, not after. A
    /// module that reads the date while being constructed would otherwise build
    /// itself in month zero and then be told it is month sixty, and the
    /// difference between those two is exactly the "restored as a value, not as
    /// a live dependency" failure SDD-013 §4 opens with.</para>
    ///
    /// <para>Internal because a host does not choose a tick — a SAVE does.
    /// Composition is the only layer entitled to build an engine that starts
    /// anywhere but the beginning.</para>
    /// </summary>
    internal static BuildResult BuildAt(EngineSettings settings, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var clock = new SimulationClock(settings.Epoch);
        clock.RestoreTo(tick);

        var audit = new AuditTrail(clock, settings.Retention);

        return Build(
            settings,
            ShippedModules(audit, clock, new RandomSource(settings.WorldSeed),
                           Defaults.ProfileNamed(settings.RealityProfile)),
            clock, audit);
    }

    /// <summary>
    /// Composes a DECLARED set — the door a scenario uses to compose a variant,
    /// and the door tests use to compose an incomplete set and read the refusal.
    /// </summary>
    public static BuildResult Build(EngineSettings settings, IReadOnlyList<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(modules);

        var clock = new SimulationClock(settings.Epoch);
        var audit = new AuditTrail(clock, settings.Retention);

        return Build(settings, modules, clock, audit);
    }

    private static BuildResult Build(
        EngineSettings settings,
        IReadOnlyList<IModule> modules,
        SimulationClock clock,
        AuditTrail audit)
    {
        // Validation and resolution both happen here, once. A refusal returns
        // before any pipeline exists — an engine that could not compose must not
        // be half-constructed and handed back.
        CompositionResult result = new ModuleComposer().Compose(modules);

        if (result is CompositionRefused refused)
            return new BuildRefused(new EngineCompositionRefused(refused.Problems));

        var composed = (Composed)result;

        var log = new Log(settings.LogSink, settings.MinimumLogLevel);
        var events = new EventBus(clock);

        IFaultPolicy faults = settings.FaultHandling switch
        {
            FaultHandling.Strict => new StrictFaultPolicy(log, audit),
            FaultHandling.Resilient => new ResilientFaultPolicy(log, audit),

            // Not a fallback: an unhandled enum member means someone added a
            // policy and did not compose it, which must fail at startup.
            _ => throw new InvariantFault("SDD-001 §5", null,
                $"Unhandled fault handling mode {settings.FaultHandling}."),
        };

        var pipeline = new TickPipeline(clock, events, audit, faults, log, composed.Stages);

        // The bus binds what composition already validated. It could not be
        // built any earlier — it needs the audit trail and the event bus, which
        // are themselves composed — so the modules declared and handed over
        // their handlers, the composer checked the set, and this is the last
        // step: attaching pairs that are already known to exist and to match.
        var commands = new CommandBus(audit, events);

        for (int i = 0; i < composed.Commands.Count; i++) composed.Commands[i].BindTo(commands);

        return new Built(new Engine(
            composed.OrderedModules, pipeline, audit, events, composed.State,
            composed.Provided, commands));
    }
}
