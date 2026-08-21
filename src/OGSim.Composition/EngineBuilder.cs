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
    /// The catalogue, as content would carry it — all nine of
    /// `content/materials/`, hardcoded here rather than read through it
    /// (SDD-004 §6's amendment, finding 261). Oil, gas and water are what this
    /// composition's own chain moves; the other six exist in the catalogue and
    /// nowhere else — no completion, drive or facility produces carbon
    /// dioxide, condensate, hydrogen sulphide, nitrogen, sales gas or sulphur,
    /// so every stream this engine solves carries six ordinals that stay
    /// exactly zero.
    ///
    /// <para>The PHASE is what makes this more than a name: `SplitAt` reads it to
    /// decide which leg of a separator a material leaves by, so "oil is a liquid
    /// at standard conditions" is the statement that sends every kilogram down
    /// the liquid leg to the meter. `Properties` stays empty for every entry,
    /// the same as the three it replaces — nothing reads a material's own
    /// properties today.</para>
    /// </summary>
    public static IReadOnlyList<(ContentId Id, PhaseAtStandardConditions Phase,
                                 IReadOnlyList<IProperty> Properties)> Materials { get; } =
    [
        (new ContentId("carbon-dioxide"), PhaseAtStandardConditions.Gas, []),
        (new ContentId("condensate"), PhaseAtStandardConditions.Liquid, []),
        (new ContentId("crude-oil"), PhaseAtStandardConditions.Liquid, []),
        (new ContentId("hydrogen-sulphide"), PhaseAtStandardConditions.Gas, []),
        (new ContentId("natural-gas"), PhaseAtStandardConditions.Gas, []),
        (new ContentId("nitrogen"), PhaseAtStandardConditions.Gas, []),
        (new ContentId("produced-water"), PhaseAtStandardConditions.Aqueous, []),
        (new ContentId("sales-gas"), PhaseAtStandardConditions.Gas, []),
        (new ContentId("sulphur"), PhaseAtStandardConditions.Solid, []),
    ];

    /// <summary>
    /// Ordinals are assigned by the CATALOGUE from the id-sorted list, never
    /// here (SDD-004 §6) — DERIVED from `Materials` itself rather than
    /// hand-typed, so a future widening cannot leave one of these three out
    /// of sync with what the catalogue actually assigned (finding 261: the
    /// bug §6's own text warns an implementer against committing "in week
    /// two"). Widening moved oil from ordinal 0 to 2 and gas from 1 to 4;
    /// nothing reads a raw literal instead of these three properties, which
    /// is what makes the move invisible to every caller.
    /// </summary>
    private static readonly MaterialCatalogue OrdinalCatalogue = new(Materials);

    public static MaterialId OilOrdinal { get; } =
        OrdinalCatalogue.Resolve(new ContentId("crude-oil")).Ordinal;

    public static MaterialId GasOrdinal { get; } =
        OrdinalCatalogue.Resolve(new ContentId("natural-gas")).Ordinal;

    /// <summary>"carbon-dioxide" &lt; ... &lt; "produced-water" &lt; ... by
    /// ordinal comparison, which is the sort the catalogue uses (SDD-004
    /// §6) — water is no longer third, it is seventh (ordinal 6).</summary>
    public static MaterialId WaterOrdinal { get; } =
        OrdinalCatalogue.Resolve(new ContentId("produced-water")).Ordinal;

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
    /// How many materials this composition's catalogue carries — nine, the
    /// full width of `Materials` above (finding 261), not the three this
    /// composition's own chain actually moves. Stated once because three
    /// places must agree on it: the completion's stream width, an operation's
    /// mass report, and any zero composition either of them builds.
    /// </summary>
    public const int MaterialCount = 9;

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
        // a rig has to be brought to location.
        RequiresAccess: true,

        Outcomes: DrillingOutcomes);

    public static ActivityTerms WellTestTerms { get; } = new(
        Template: new ContentId("well-test-buildup"),
        Cost: Money.FromMillions(0.4),
        DurationTicks: 1,
        Rig: TheRig,
        WeatherLimit: 7.5,   // the well is shut in and a gauge is reading; little to do on deck
        // the well is shut in and a gauge on site reads it.
        RequiresAccess: false,

        Outcomes: WellTestOutcomes);

    /// <summary>Cheap, quick, and run on the rig that is already there.</summary>
    public static ActivityTerms WirelineLogTerms { get; } = new(
        Template: new ContentId("wireline-log"),
        Cost: Money.FromMillions(0.15),
        DurationTicks: 1,
        Rig: TheRig,
        WeatherLimit: 6.5,   // a wireline unit needs a stable deck to run tools on a thin cable
        // a logging unit and its cable arrive by boat.
        RequiresAccess: true,

        Outcomes: WellTestOutcomes);

    /// <summary>Several times the price of a log for the same two properties,
    /// which is the decision.</summary>
    public static ActivityTerms CoringTerms { get; } = new(
        Template: new ContentId("cut-core"),
        Cost: Money.FromMillions(0.9),
        DurationTicks: 1,
        Rig: TheRig,
        WeatherLimit: 6.0,   // coring is drilling, and slower
        // cut from a rig that is already being mobilised.
        RequiresAccess: true,

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
        // a survey vessel and its streamers.
        RequiresAccess: true,

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
    private static Money TargetCash { get; } = Money.FromMillions(360.0);

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

    /// <summary>Where the custody spec gate's Reject leg goes (SDD-006 §7d,
    /// finding 252) — a permanent loss, accounted rather than silent.</summary>
    public static EntityId<IFlowElement> TheOffSpecSink { get; } = new(1_000_011);

    /// <summary>Between the separator's gas leg and the gas plant (SDD-006
    /// §3c, R9.1's own composition, finding 257) — the seventh socket.</summary>
    public static EntityId<IFlowElement> TheCompressor { get; } = new(1_000_012);

    /// <summary>Between the separator's liquid leg and the treater (SDD-006
    /// §3d, R11.2's own composition, finding 259) — the eighth socket.</summary>
    public static EntityId<IFlowElement> TheLiquidPumpStation { get; } = new(1_000_013);

    /// <summary>
    /// Where per-well gathering lines start numbering (SDD-006 §1c). Above the
    /// fixed chain elements by a clear margin, so a line laid for the
    /// two-thousandth well still cannot collide with the header or the trunk.
    /// </summary>
    public const ulong FirstGatheringLine = 2_000_000UL;


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
        // a lay barge, which is the largest mobilisation here.
        RequiresAccess: true,

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

    /// <summary>SDD-006 §3c's Z̄ — the average compressibility factor the
    /// polytropic formula takes as a property of the stream a train is built
    /// for, exactly as a liquid pump's ρ̄ is (§3d). 0.9 is a typical value for
    /// associated gas across the pressure range this composition ships.</summary>
    public const double GasCompressibilityFactor = 0.9;




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
        // the same rig, and a cement unit with it.
        RequiresAccess: true,

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
        // a module lifted onto the deck.
        RequiresAccess: true,

        Outcomes: SurveyOutcomes);



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
        // the same, and heavier.
        RequiresAccess: true,

        Outcomes: SurveyOutcomes);


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
        // a subsea structure set from a vessel.
        RequiresAccess: true,

        Outcomes: SurveyOutcomes);

    /// <summary>What a treater costs and how long it takes.</summary>
    public static ActivityTerms InstallTreaterTerms { get; } = new(
        Template: new ContentId("install-treater"),
        Cost: Money.FromMillions(5.0),
        DurationTicks: 3,
        Rig: null,
        WeatherLimit: 5.0,   // heavy lift
        // a skid delivered and tied in.
        RequiresAccess: true,

        Outcomes: SurveyOutcomes);

    /// <summary>SDD-006 §3c's own capital item (R9.1's own composition,
    /// finding 257) — a real compression train, priced against a gas plant
    /// module rather than a vessel: it is heavier iron than a separator and
    /// lighter than the plant it feeds.</summary>
    public static ActivityTerms InstallCompressorTerms { get; } = new(
        Template: new ContentId("install-compressor"),
        Cost: Money.FromMillions(15.0),
        DurationTicks: 4,
        Rig: null,
        WeatherLimit: 5.0,   // heavy lift
        // a skid delivered and tied in.
        RequiresAccess: true,

        Outcomes: SurveyOutcomes);

    /// <summary>SDD-006 §3d's own capital item (R11.2's own composition,
    /// finding 259) — C11's own capex band prices a pump station one tier
    /// below a compressor station ($$$ against $$$$), which is what its
    /// cost is set against here.</summary>
    public static ActivityTerms InstallLiquidPumpStationTerms { get; } = new(
        Template: new ContentId("install-pump-station"),
        Cost: Money.FromMillions(10.0),
        DurationTicks: 3,
        Rig: null,
        WeatherLimit: 5.0,   // heavy lift
        // a skid delivered and tied in.
        RequiresAccess: true,

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
        // chemicals pumped from stock held on the platform.
        RequiresAccess: false,

        Outcomes: SurveyOutcomes);

    /// <summary>
    /// What an acid job on a PRODUCER costs (R12b.7, finding 253). Same
    /// wellsite-intervention shape as remediating an injector — no rig, a
    /// wireline and pump crew — priced a little dearer because this one
    /// leaves an asset behind rather than restoring one.
    /// </summary>
    public static ActivityTerms StimulateWellTerms { get; } = new(
        Template: new ContentId("stimulate-well"),
        Cost: Money.FromMillions(1.8),
        DurationTicks: 1,
        Rig: null,
        WeatherLimit: 6.5,
        RequiresAccess: false,

        Outcomes: SurveyOutcomes);

    /// <summary>
    /// What an acid job removes from a producer's skin (SDD-003 §6's R12b.7
    /// amendment, finding 253) — a first-pass engineering estimate, typical
    /// of matrix acidising near-wellbore damage, and not iterated against a
    /// fixture: nothing in this composition currently produces a skin value
    /// close enough to a physical floor to need calibrating one.
    /// </summary>
    public static double StimulationSkinReduction { get; } = 3.0;

    /// <summary>
    /// What fitting a lift method costs (R12b.2, finding 255). Same
    /// wellsite-intervention shape as stimulation and remediating an
    /// injector — no rig — priced as a real capital install, between an
    /// acid job and a facility unit: a pump is more than a wireline job and
    /// far less than a vessel. FOUR distinct templates, not one shared
    /// across the four activities: `ActivityState` keys its catalogue by
    /// `Template` in a dictionary, so a shared id would let the last one
    /// registered silently replace the other three.
    ///
    /// <para>Costs differ by what the equipment actually is — a rod pump
    /// and a PCP are close cousins (§6.2's own "the same relation"), an ESP
    /// carries a cable, a downhole motor and a surface VSD, and gas lift
    /// needs a surface interface to a compressed supply — first-pass
    /// estimates, not calibrated against a fixture.</para>
    /// </summary>
    public static ActivityTerms InstallRodPumpTerms { get; } = new(
        Template: new ContentId("install-rod-pump"),
        Cost: Money.FromMillions(2.5),
        DurationTicks: 1,
        Rig: null,
        WeatherLimit: 6.5,
        RequiresAccess: false,

        Outcomes: SurveyOutcomes);

    public static ActivityTerms InstallPcpTerms { get; } = new(
        Template: new ContentId("install-pcp"),
        Cost: Money.FromMillions(2.7),
        DurationTicks: 1,
        Rig: null,
        WeatherLimit: 6.5,
        RequiresAccess: false,

        Outcomes: SurveyOutcomes);

    public static ActivityTerms InstallEspTerms { get; } = new(
        Template: new ContentId("install-esp"),
        Cost: Money.FromMillions(4.5),
        DurationTicks: 1,
        Rig: null,
        WeatherLimit: 6.5,
        RequiresAccess: false,

        Outcomes: SurveyOutcomes);

    public static ActivityTerms InstallGasLiftTerms { get; } = new(
        Template: new ContentId("install-gas-lift"),
        Cost: Money.FromMillions(3.2),
        DurationTicks: 1,
        Rig: null,
        WeatherLimit: 6.5,
        RequiresAccess: false,

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
        // planned work by the crew who are already there.
        RequiresAccess: false,

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
        // kit that has to be carried out and fitted.
        RequiresAccess: true,

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
        // an emergency fix with the spares already aboard.
        RequiresAccess: false,

        Outcomes: SurveyOutcomes);

    public static ActivityTerms InstallSeparatorTerms { get; } = new(
        Template: new ContentId("install-separator"),
        Cost: Money.FromMillions(6.0),
        DurationTicks: 3,
        Rig: null,
        WeatherLimit: 5.0,   // heavy lift
        // a vessel craned into a socket.
        RequiresAccess: true,

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

    public static IReadOnlyList<Integrity.Barrier> Barriers { get; } =
    [
        new(new ContentId("containment"), [TheSeparator, TheTank], IsPreventive: true),
        new(new ContentId("shutdown"), [TheManifold], IsPreventive: true),
        new(new ContentId("response"), [TheTreater, TheGasPlant], IsPreventive: false),
    ];

    public static ContentId ContainmentThreat { get; } = new("loss-of-containment");

    public const double ThreatRateAtFailure = 0.15;

    /// <summary>What a top event costs when every mitigating barrier held
    /// (SDD-012 §4b's finding-263 amendment). Unchanged from this
    /// composition's original flat cost, so nothing already tuned against it
    /// moves.</summary>
    public const double TopEventPointsMitigated = 25.0;

    /// <summary>What it costs when none did — three times the mitigated
    /// figure, the same asymmetry already priced between planned and
    /// emergency maintenance (`repair-equipment` at 3× `service-equipment`,
    /// R20d.26.2): a consequence nothing stood against costs more than one
    /// every defence somewhat blunted.</summary>
    public const double TopEventPointsUnmitigated = 75.0;

    /// <summary>An untrained crew's strength on the barrier's own [0, 1]
    /// scale (SDD-007 §4.1's finding-265 amendment) — noticeably below the
    /// flat 0.9 this composition charged every crew before there was a lever
    /// to raise it, so "lean crewing is measurably less safe" (R12 §2.8) is
    /// something a company starts owing rather than something a purchase
    /// only ever improves on.</summary>
    public const double CrewCompetencyUntrained = 0.75;

    /// <summary>What training buys — better than the old flat figure, so the
    /// investment is a genuine improvement and not merely a return to
    /// baseline.</summary>
    public const double CrewCompetencyTrained = 0.95;

    /// <summary>An untrained crew's multiplier on an operation's base
    /// duration, layered onto the outcome table's own grade rather than
    /// replacing it (SDD-007 §4.1).</summary>
    public const double CrewDurationFactorUntrained = 1.15;

    /// <summary>What training buys on duration — faster than nominal,
    /// because R12-V9 asks skill to reduce duration, not merely restore
    /// it.</summary>
    public const double CrewDurationFactorTrained = 0.95;

    /// <summary>One-time, like a technology acquisition — comparable to a
    /// facility's first upgrade rung.</summary>
    public static Money CrewTrainingCost { get; } = Money.FromMillions(2.0);

    public const double ProcedureCompliance = 0.9;

    /// <summary>
    /// How long an incident stays on the record (SDD-012 §4b). Three years, so a
    /// company that hurts someone carries it across several borrowing
    /// redeterminations and a clean decade genuinely rehabilitates — the two
    /// exits CI4 asks for, rather than a punishment with no way out.
    /// </summary>
    public const double EsgIncidentHalfLifeTicks = 36.0;

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
    /// <summary>
    /// When each era starts (SDD-005 §2's R20d.10 amendment).
    ///
    /// <para>TECH_TREE's own line — *Eras: E1 1950s–60s · E2 70s–80s · E3
    /// 90s–2000s · E4 2010s+* — transcribed rather than chosen here, because the
    /// registry that assigns every node an era is the document entitled to say
    /// when those eras are. A 1965 start begins fifteen years into E1 and reaches
    /// E3 inside a forty-year run.</para>
    /// </summary>
    public static Capabilities.EraCalendar Eras { get; } =
        new([(Era.E1, 1950), (Era.E2, 1970), (Era.E3, 1990), (Era.E4, 2010)]);

    /// <summary>
    /// The one licence this composition's company holds (SDD-011 §1's R20d.9
    /// amendment) — on the same footing as <see cref="Climate"/> and
    /// <see cref="Eras"/>: a single hand-authored instance of a mechanic this
    /// composition ships one of, ahead of R20's jurisdiction content.
    ///
    /// <para>Proportional to what already exists rather than invented: the
    /// bond is a small multiple of one well's cost
    /// (<see cref="DrillWellTerms"/>), and the term spans this composition's
    /// own forty-year test horizon so no shipped run expires the licence
    /// mid-game and finds out what that means — a question this amendment
    /// deliberately leaves unanswered (SDD-011 §1's R20d.9 amendment only
    /// wires the commitment deadline, not bare expiry).</para>
    ///
    /// <para><b>The commitment falls due at month 60, not month 24</b> —
    /// widened from a first cut that was measured rather than assumed to be
    /// too tight. A lost licence refuses all further drilling (SDD-011's
    /// R20d.9 amendment), so a company whose FIRST well came back dry — a real
    /// outcome the shipped table gives real weight to — had no recourse at
    /// all under a 24-month deadline: one early roll of bad luck ended the
    /// game's development for good, which is a harsher reading of "loses the
    /// acreage" than the mechanic is meant to enforce. Sixty months is margin
    /// for several real attempts (each `DrillWellTerms.DurationTicks` long)
    /// before the commitment binds, while staying meaningfully inside the
    /// 480-month term rather than a formality.</para>
    ///
    /// <para><c>Relinquishment</c> is empty and stays that way: this field is
    /// <c>DeclareKnownField</c>d from the first tick (SDD-010 §4b), so there is
    /// no unexplored acreage to hand back.</para>
    /// </summary>
    public static Contracts.LicenceTerms LicenceTerms { get; } =
        new(
            TermMonths: 480,
            WorkCommitment:
            [
                new Contracts.CommitmentItem(
                    DrillWellTerms.Template, Quantity: 1.0, Due: new Tick(60)),
            ],
            Bond: Money.FromMillions(12.0),
            Relinquishment: [],
            FiscalRegime: new ContentId("concession"),

            // NO CONSUMER YET (rule 7's own test: what reads this to make a
            // decision). R16.6's own row already says the rules an HSE regime
            // would name are R23's; this states plainly that naming it is not
            // the same as enforcing it.
            HseRegime: new ContentId("standard"));

    public static Environment.ClimateProfile Climate { get; } =
        new(new ContentId("temperate-offshore"),
            Persistence: 0.75,
            Baseline: [5.2, 5.0, 4.4, 3.6, 2.9, 2.4, 2.2, 2.5, 3.2, 4.1, 4.8, 5.1],
            Amplitude: [1.6, 1.6, 1.4, 1.2, 1.0, 0.9, 0.9, 1.0, 1.2, 1.4, 1.5, 1.6],
            TemperatureBaseline: [6.0, 5.6, 6.4, 8.2, 10.8, 13.4,
                                  15.1, 15.4, 14.0, 11.6, 9.0, 7.0],
            TemperatureAmplitude: -1.8,

            // OPEN ALL YEAR, and that is a statement about this climate rather
            // than a mechanic switched off. A temperate offshore field is reached
            // by boat and helicopter in every month; what stops work there is the
            // sea state on the day, which is what `WeatherLimit` already prices.
            // A window belongs to an ice road or a monsoon coast, and this
            // composition ships neither — so no shipped climate closes, and the
            // refusal is proved against an arctic profile in the tests until R20
            // authors a scenario that has one (SDD-016 §5b's R22.6 amendment).
            AccessOpen: [true, true, true, true, true, true,
                         true, true, true, true, true, true],

            // EMPTY, and correct rather than missing (SDD-005 §4.2's R22.2
            // amendment): a temperate-offshore climate restricts nothing a
            // technology would extend — that is the arctic-window story, and
            // this composition ships no arctic climate.
            Effects: []);

    /// <summary>
    /// SDD-016's R20d.8.10 amendment (finding 244):
    /// <c>WorldParameters.ClimateSeverity</c> — "weather amplitude/extreme-rate
    /// multiplier" — validated and saved since R15 and read by nothing
    /// (CLAUDE.md rule 7). Scales how variable this basin's weather is, not its
    /// average: the seasonal baselines are what a climate typically looks like,
    /// and severity is how far a given day can depart from that, so only the
    /// two amplitude curves move.
    /// </summary>
    public static Environment.ClimateProfile ScaledClimate(double climateSeverity)
    {
        var amplitude = new double[Climate.Amplitude.Count];
        for (int i = 0; i < amplitude.Length; i++)
            amplitude[i] = Climate.Amplitude[i] * climateSeverity;

        return Climate with
        {
            Amplitude = amplitude,
            TemperatureAmplitude = Climate.TemperatureAmplitude * climateSeverity,
        };
    }

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
internal sealed class RegionalObservationModel(WorldState world) : IObservationModel
{
    public ContentId Id { get; } = new("regional-observation");

    /// <summary>
    /// Design 06 §2.3's table — the only source of a detect ceiling anywhere
    /// (SDD-005 §5's R12b.19 amendment). Hand-authored beside this model
    /// rather than content-driven, the same relationship `Defaults.Climate`
    /// has to a `ClimateContentKind` that does not exist either: three shipped
    /// sources reach two of the table's four rows, and no `information-source`
    /// kind exists to load a fifth. Null for a source the table has no ceiling
    /// for — every non-survey source (a log, a core, a well test, a discovery
    /// well) never queries a prospect's subtlety at all, so this is never
    /// consulted for them regardless.
    /// </summary>
    private static DetectClass? CeilingOf(ContentId source) => source.Value switch
    {
        "regional" => DetectClass.D0,
        "seismic-2d" => DetectClass.D0,
        "seismic-3d" => DetectClass.D1,
        _ => null,
    };

    public double? SigmaFor(ContentId source, ContentId propertyKind, EntityRef subject)
    {
        // BELOW THE TIER, A SURVEY SEES NOTHING (design 06 §2.3) — checked
        // only when the subject IS the structure: `Subtlety` describes the
        // trap's own geometry, not what turns out to be in it, so a discovery
        // well, log, core or well test (always a compartment, always
        // post-discovery) never reaches this branch.
        if (subject.Kind == EntityKind.Prospect
            && CeilingOf(source) is DetectClass ceiling
            && world.SubtletyOf(new EntityId<IProspect>(subject.Value)) > ceiling)
            return null;

        return (source.Value, propertyKind.Value) switch
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
    ContentId RealityProfile,

    /// <summary>
    /// Where the game's content comes from (SDD-004 §7, and §6's R20c.9
    /// amendment).
    ///
    /// <para><b>A host supplies these and the engine never touches a disk.</b>
    /// An <see cref="IContentSource"/> hands over files it has already read, so
    /// the engine assemblies stay free of I/O and a host is free to ship content
    /// from a directory, an archive or a download without composition knowing
    /// which.</para>
    ///
    /// <para>Required, with no default: law L2, and the reason it matters here is
    /// that a default would be an empty list, and an empty list is a game with no
    /// equipment that composes anyway and fails later as a missing separator.
    /// Content that will not load is a REFUSAL to start (design 10 §3, G2).</para>
    ///
    /// <para>Order fixes override precedence — base content is
    /// <c>DeclaredOrder</c> 0 and a mod is higher (§7).</para>
    /// </summary>
    IReadOnlyList<IContentSource> Content)
{
    // Finding 131: the compiler compares a collection member by REFERENCE, so
    // two settings naming the same sources would differ. Element equality is
    // still reference equality here and correctly so — a source is a live object
    // a host owns, not a value — but the LIST is compared by content, which is
    // what the rule is about.
    public bool Equals(EngineSettings? other) =>
        other is not null
        && Epoch == other.Epoch && WorldSeed == other.WorldSeed
        && Retention == other.Retention && ReferenceEquals(LogSink, other.LogSink)
        && MinimumLogLevel == other.MinimumLogLevel
        && FaultHandling == other.FaultHandling
        && RealityProfile == other.RealityProfile
        && Structural.Equal(Content, other.Content);

    public override int GetHashCode() =>
        HashCode.Combine(
            Epoch, WorldSeed, Retention, MinimumLogLevel, FaultHandling, RealityProfile,
            Structural.HashOf(Content));
}

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

/// <summary>
/// The content would not load, so there is no engine (design 10 §3's G2,
/// SDD-004 §6's R20c.9 amendment).
///
/// <para>ITS OWN CASE rather than a <see cref="CompositionProblem"/>, because a
/// <see cref="LoadFailure"/> names the file, the JSON path and the stage it
/// failed at, and flattening that into a module-and-detail pair would throw away
/// the half an author needs to fix it. A composition problem is about the module
/// SET; this is about a datasheet.</para>
///
/// <para>Every failure, never just the first: §5's loader accumulates across all
/// six stages and all files precisely so one broken sheet does not hide four
/// more.</para>
/// </summary>
public sealed record BuildRefusedByContent(IReadOnlyList<LoadFailure> Failures) : BuildResult
{
    // Finding 131: a record carrying a collection compares it by reference.
    public bool Equals(BuildRefusedByContent? other) =>
        other is not null && Structural.Equal(Failures, other.Failures);

    public override int GetHashCode() => Structural.HashOf(Failures);
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
    /// <summary>
    /// The facility catalogues, or null if the content will not load
    /// (SDD-004 §6's R20c.9 amendment).
    ///
    /// <para>BEFORE THE MODULES, because a module's <c>Compose</c> fits its
    /// equipment from a ladder and there is nothing to fit until the sheets are
    /// read. <c>PluginRegistry</c>'s own header states the ordering the other way
    /// round — a plugin must not be built during content load, "which happens
    /// before the engine exists" — and this is that sentence's other half.</para>
    /// </summary>
    private static (FacilityLadders Ladders,
                    IReadOnlyList<OGSim.Capabilities.TechnologyNode> Registry,
                    IReadOnlyList<OGSim.World.TerrainClassDefinition> TerrainClasses,
                    OGSim.Company.TakeOrPayTerms TakeOrPay,
                    OGSim.Wells.LiftTiers LiftTiers)? Ladders(
        EngineSettings settings)
    {
        ContentLoadResult result = FacilityContent(settings);

        return result is ContentLoaded loaded
            ? (FacilityLadders.From(loaded.Catalogues), Registry(loaded.Catalogues),
               loaded.Catalogues.Of<OGSim.World.TerrainClassDefinition>().All,
               TakeOrPayFrom(loaded.Catalogues),
               LiftTiersFrom(loaded.Catalogues))
            : null;
    }

    /// <summary>Why it would not load, for the refusal to carry.</summary>
    private static IReadOnlyList<LoadFailure> Failures(EngineSettings settings) =>
        FacilityContent(settings) is ContentFailures failed ? failed.Failures : [];

    /// <summary>
    /// SDD-009 §7's R13.3 amendment (finding 250, revised). ONE contract, the
    /// same relationship <see cref="Defaults.LicenceTerms"/> has to the one
    /// licence — no ladder, because a sales contract is not a purchasable
    /// progression.
    /// </summary>
    private static OGSim.Company.TakeOrPayTerms TakeOrPayFrom(ICatalogSet catalogues)
    {
        TakeOrPayDefinition definition =
            catalogues.Of<TakeOrPayDefinition>()[new ContentId("oil-take-or-pay")];

        return new OGSim.Company.TakeOrPayTerms(
            definition.CommittedVolume, definition.WindowMonths, definition.PenaltyRate);
    }

    /// <summary>
    /// SDD-003 §6.2's R12b.2 amendment (finding 255). Four pump tiers, the
    /// same relationship <see cref="TakeOrPayFrom"/> has to the one sales
    /// contract — each is installed once, not upgraded through a ladder this
    /// composition has no second rung for yet.
    /// </summary>
    private static OGSim.Wells.LiftTiers LiftTiersFrom(ICatalogSet catalogues)
    {
        DisplacementPumpDefinition rod =
            catalogues.Of<DisplacementPumpDefinition>()[new ContentId("rod-pump-a")];
        DisplacementPumpDefinition pcp =
            catalogues.Of<DisplacementPumpDefinition>()[new ContentId("pcp-a")];
        EspDefinition esp = catalogues.Of<EspDefinition>()[new ContentId("esp-a")];
        GasLiftDefinition gasLift =
            catalogues.Of<GasLiftDefinition>()[new ContentId("gas-lift-a")];

        return new OGSim.Wells.LiftTiers(
            DisplacementPumpFrom(rod), DisplacementPumpFrom(pcp),
            new OGSim.Wells.EspTier(
                esp.Id, EnvelopeOf(esp.MinRate, esp.MaxRate, esp.MaxDepth, esp.MaxDeviationDegrees,
                                    esp.MaxGasFraction, esp.MaxTemperature, esp.MaxSolidsFraction),
                esp.HeadCurve, esp.Efficiency),
            new OGSim.Wells.GasLiftTier(
                gasLift.Id,
                EnvelopeOf(gasLift.MinRate, gasLift.MaxRate, gasLift.MaxDepth,
                           gasLift.MaxDeviationDegrees, gasLift.MaxGasFraction,
                           gasLift.MaxTemperature, gasLift.MaxSolidsFraction),
                gasLift.InjectionRate.CubicMetresPerSecond, gasLift.GasDensityKgPerM3));
    }

    private static OGSim.Wells.DisplacementPumpTier DisplacementPumpFrom(
        DisplacementPumpDefinition definition) =>
        new(definition.Id,
            EnvelopeOf(definition.MinRate, definition.MaxRate, definition.MaxDepth,
                       definition.MaxDeviationDegrees, definition.MaxGasFraction,
                       definition.MaxTemperature, definition.MaxSolidsFraction),
            definition.Displacement.CubicMetresPerSecond);

    private static LiftEnvelope EnvelopeOf(
        ReservoirRate minRate, ReservoirRate maxRate, Length maxDepth,
        double maxDeviationDegrees, double maxGasFraction, Temperature maxTemperature,
        double maxSolidsFraction) =>
        new(minRate, maxRate, maxDepth, maxDeviationDegrees, maxGasFraction, maxTemperature,
            maxSolidsFraction);

    /// <summary>
    /// The technology registry, as a graph (SDD-005 §2's R20d.10 amendment).
    ///
    /// <para>Sixty-five nodes have shipped in <c>content/technologies/</c> since
    /// R20c.9 and the ENGINE never read one: `CapabilitiesModule` composed
    /// `AllCapabilities`, so the graph existed for a fixture test alone. This is
    /// the same door the facility sheets come through.</para>
    /// </summary>
    private static IReadOnlyList<OGSim.Capabilities.TechnologyNode> Registry(
        ICatalogSet catalogues)
    {
        IReadOnlyList<OGSim.Capabilities.TechnologyDefinition> all =
            catalogues.Of<OGSim.Capabilities.TechnologyDefinition>().All;

        var graph = new List<OGSim.Capabilities.TechnologyNode>(all.Count);

        for (int i = 0; i < all.Count; i++)
        {
            OGSim.Capabilities.TechnologyDefinition node = all[i];

            var prerequisites = new List<TechnologyId>(node.Prerequisites.Count);

            for (int p = 0; p < node.Prerequisites.Count; p++)
                prerequisites.Add(new TechnologyId(node.Prerequisites[p]));

            graph.Add(new OGSim.Capabilities.TechnologyNode(
                new TechnologyId(node.Id),
                node.AvailableFrom,
                node.DiffusionLagTicks,
                prerequisites,
                node.Effects,
                node.GrantsDetectClass,
                node.Routes));
        }

        return graph;
    }

    /// <summary>
    /// The seven facility kinds and the technology registry over the host's
    /// sources.
    ///
    /// <para>No plugin binding: a facility datasheet names no model, so the
    /// registry handed to the loader answers for nothing. It is still REQUIRED
    /// rather than optional — stage 6 exists whether or not this content uses it,
    /// and a loader that skipped a stage because today's content did not need it
    /// would skip it tomorrow when the content did.</para>
    /// </summary>
    private static ContentLoadResult FacilityContent(EngineSettings settings) =>
        new ContentLoader(
            [
                new SeparatorContentKind(), new TankContentKind(), new TreaterContentKind(),
                new GasPlantContentKind(), new ExportLineContentKind(), new ManifoldContentKind(),
                new CompressorContentKind(), new PumpStationContentKind(),
                new OGSim.Capabilities.TechnologyContentKind(),
                new OGSim.World.TerrainClassContentKind(),
                new TakeOrPayContentKind(),
                new DisplacementPumpContentKind("rod-pump"),
                new DisplacementPumpContentKind("pcp"),
                new EspContentKind(),
                new GasLiftContentKind(),
            ],
            new PluginRegistry())
            .LoadAll(settings.Content);

    internal static IReadOnlyList<IModule> ShippedModules(
        AuditTrail audit, SimulationClock clock, IRandomSource random,
        RealityProfile profile, FacilityLadders ladders,
        IReadOnlyList<OGSim.Capabilities.TechnologyNode> registry,
        IReadOnlyList<OGSim.World.TerrainClassDefinition> terrainClasses,
        OGSim.Company.TakeOrPayTerms takeOrPay,
        OGSim.Wells.LiftTiers liftTiers) =>
    [
        new SubsurfaceModule(),
        new WellsModule(),
        new FlowModule(),
        new FacilitiesModule(ladders),
        new OperationsModule(),
        new CompanyModule(),
        new InformationModule(),
        new WorldModule(terrainClasses, Defaults.Climate.Id),
        new CapabilitiesModule(registry, Defaults.Eras, clock),
        new IntegrityModule(),
        new EnvironmentModule(Defaults.Climate),
        new HseModule(),
        new ObjectivesModule(),
        new MaterialsModule(profile),
        new FieldModule(ladders, takeOrPay, liftTiers),
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

        // AND WEATHER'S ONE REGION IS SEALED THE SAME INSTANT (SDD-016's
        // R20d.8.10 amendment) — composed from Defaults.Climate unscaled so a
        // hand-built engine that never reaches this line still has weather,
        // replaced here with what this basin's ClimateSeverity actually asks
        // for, which is the value world generation just declared a region FOR.
        ready.Engine.Provided.Resolve<Environment.WeatherState>()
            .SealGeneration([Defaults.ScaledClimate(world.ClimateSeverity)]);

        return built;
    }

    /// <summary>Composes the shipped set.</summary>
    public static BuildResult Build(EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (Ladders(settings) is not var (ladders, registry, terrainClasses, takeOrPay, liftTiers))
            return new BuildRefusedByContent(Failures(settings));

        var clock = new SimulationClock(settings.Epoch);
        var audit = new AuditTrail(clock, settings.Retention);

        return Build(
            settings,
            ShippedModules(audit, clock, new RandomSource(settings.WorldSeed),
                           Defaults.ProfileNamed(settings.RealityProfile), ladders, registry,
                           terrainClasses, takeOrPay, liftTiers),
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

        if (Ladders(settings) is not var (ladders, registry, terrainClasses, takeOrPay, liftTiers))
            return new BuildRefusedByContent(Failures(settings));

        var clock = new SimulationClock(settings.Epoch);
        clock.RestoreTo(tick);

        var audit = new AuditTrail(clock, settings.Retention);

        return Build(
            settings,
            ShippedModules(audit, clock, new RandomSource(settings.WorldSeed),
                           Defaults.ProfileNamed(settings.RealityProfile), ladders, registry,
                           terrainClasses, takeOrPay, liftTiers),
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
