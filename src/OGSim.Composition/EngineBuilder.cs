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
        FixedOperatingCostPerTick: Money.FromMillions(0.3));

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
    ];

    /// <summary>
    /// How many materials this composition's catalogue carries. One — oil —
    /// until R20c.9 loads the nine of `content/materials/`. Stated once because
    /// three places must agree on it: the completion's stream width, an
    /// operation's mass report, and any zero composition either of them builds.
    /// </summary>
    public const int MaterialCount = 1;

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
        Outcomes: DrillingOutcomes);

    public static ActivityTerms WellTestTerms { get; } = new(
        Template: new ContentId("well-test-buildup"),
        Cost: Money.FromMillions(0.4),
        DurationTicks: 1,
        Rig: TheRig,
        Outcomes: WellTestOutcomes);

    /// <summary>Cheap, quick, and run on the rig that is already there.</summary>
    public static ActivityTerms WirelineLogTerms { get; } = new(
        Template: new ContentId("wireline-log"),
        Cost: Money.FromMillions(0.15),
        DurationTicks: 1,
        Rig: TheRig,
        Outcomes: WellTestOutcomes);

    /// <summary>Several times the price of a log for the same two properties,
    /// which is the decision.</summary>
    public static ActivityTerms CoringTerms { get; } = new(
        Template: new ContentId("cut-core"),
        Cost: Money.FromMillions(0.9),
        DurationTicks: 1,
        Rig: TheRig,
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
        ulong id, EntityId<IReservoirCompartmentEntity> compartment, Length totalDepth)
    {
        var tubing = new Wells.TubingGeometry(
            totalDepth, totalDepth, new Length(0.0889), 4.6e-5);

        return new Wells.Completion(
            new EntityId<ICompletion>(id),
            new EntityId<IWellbore>(id),
            [new Perforation(compartment, totalDepth, totalDepth + new Length(30.0),
                             Skin: 0.0, Isolated: false)],
            new Wells.CompositeInflowModel(Inflow),
            new Wells.HydrostaticFrictionOutflowModel(
                tubing, Density.FromSpecificGravity(0.85), lift: null),
            new Wells.CompletionFluid(
                SurfaceOilDensity,
                CompletionBo,
                Allocation.Validated(
                    [(new EntityRef(EntityKind.Compartment, compartment.Value), 1.0)]),
                new Pressure(30.0e6),
                ReservoirTemperature),
            Wells.ChokeSetting.Open,
            materialOrdinal: 0,
            materialCount: 1,
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
    /// Double the opening cash. Named so the goal and the opening position
    /// cannot drift apart silently.
    ///
    /// <para>Declared BEFORE the scenario that reads it: static initialisers run
    /// in declaration order, so a target declared below would be zero when the
    /// objective is built — and "cash at least zero" is met in month one, which
    /// is a scenario that cannot be lost rather than a compile error. The same
    /// trap the activity terms above carry a note about.</para>
    /// </summary>
    private static Money TargetCash { get; } = Money.FromMillions(100.0);

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
    /// this field delivers about 7 kg/s, so the first vessel carries one well
    /// comfortably and is over capacity on the second — the player sees the
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
        LiquidCapacity: new MassRate(10.0),
        Volume: new ReservoirVolume(30.0),
        RatedEfficiency: new SeparationEfficiency(
            LiquidFromGas: 0.0, GasFromLiquid: 0.0, WaterFromLiquid: 0.0),
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
    public static ActivityTerms InstallSeparatorTerms { get; } = new(
        Template: new ContentId("install-separator"),
        Cost: Money.FromMillions(6.0),
        DurationTicks: 3,
        Rig: null,
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
    public static Specification SalesSpec { get; } = new([]);

    /// <summary>What the meter reads off a stream. Every fraction is zero because
    /// the one material is oil: no water to be basic sediment, no H2S, no CO2, no
    /// light ends. It becomes a measurement when there is a stream to measure
    /// (R20d.3, R20d.4).</summary>
    public static Facilities.StreamProperties MeasureStream(MaterialStream stream) =>
        new(BasicSedimentAndWater: 0.0, H2SFraction: 0.0, Co2Fraction: 0.0,
            WaterInGasFraction: 0.0, LightEndsFraction: 0.0,
            Heating: new HeatingValue(0.0));

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

    public static Integrity.DegradationCoefficients Decay { get; } =
        new(BaseRatePerYear: 0.05, WaterCutFactor: 1.0, SourFactor: 2.0,
            DutyFactor: 0.5, TemperatureFactor: 1.5, ServiceIntervalFactor: 0.2);
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
        IAuditTrail audit, SimulationClock clock, IRandomSource random,
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
        new HseModule(),
        new ObjectivesModule(),
        new MaterialsModule(profile),
        new FieldModule(),
        new DiagnosticsModule(audit, clock, random),
    ];

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
