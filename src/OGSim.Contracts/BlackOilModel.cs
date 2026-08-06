// R2.7 — the black-oil fluid property model (SDD-003 §4.1, design 05 §2).
//
// FIELD UNITS INSIDE, SI AT THE BOUNDARY — SDD-003 §2's R2.7 amendment. These
// five correlations are EMPIRICAL: their constants are regression coefficients
// fitted to field-unit data, not unit conversions, so there is no SI form of
// them. Absorbing the conversions into new coefficients would produce numbers
// appearing in no paper and checkable against nothing. Each body below is
// transcribed so a reader holding the paper can follow it line by line.
//
// Every transcendental goes through DetMath (rule D-2): these run inside the
// per-tick solve, and Math.Pow would make a save digest platform-dependent.

using OGSim.Kernel;

namespace OGSim.Contracts;

/// <summary>
/// The fluid system's declared inputs — what a `fluid-system` content entry
/// carries. All SI; the conversions to field units happen inside.
/// </summary>
public sealed record BlackOilInputs(
    ApiGravity OilGravity,
    double GasSpecificGravity,          // γg, air = 1
    Temperature ReservoirTemperature,
    double SolutionGorAtBubblePoint,    // Rsb, sm³/sm³
    FluidForm Form);

/// <summary>
/// Standing / Vazquez-Beggs / Beggs-Robinson / Lee et al. / Dranchuk-Abou-Kassem,
/// behind the R2.5 plugin contract. A table-lookup and a constant-properties
/// implementation ship alongside as alternative fidelity levels (R2 §2.5) — and
/// the constant-properties one is not a stub: it is a legitimate simplification
/// for arcade fidelity, complete and tested.
/// </summary>
public sealed class BlackOilModel : IFluidPropertyModel
{
    // ---- field-unit conversion, the declared boundary (SDD-003 §2)
    private const double PascalsPerPsi = 6894.757293168;
    private const double RankinePerKelvin = 1.8;
    private const double FahrenheitOffset = 459.67;
    private const double ScfPerStbPerSm3PerSm3 = 5.614583;   // sm³/sm³ → scf/STB
    private const double CentipoisePerPascalSecond = 1000.0;

    // ---- standard conditions (SDD-004): 60 °F, 1 atm
    private const double StandardPressurePa = 101_325.0;
    private const double StandardTemperatureK = 288.706;

    // ---- Dranchuk-Abou-Kassem, the eleven (SDD-003 §4.1)
    private const double A1 = 0.3265, A2 = -1.0700, A3 = -0.5339, A4 = 0.01569;
    private const double A5 = -0.05165, A6 = 0.5475, A7 = -0.7361, A8 = 0.1844;
    private const double A9 = 0.1056, A10 = 0.6134, A11 = 0.7210;

    private readonly BlackOilInputs _inputs;
    private readonly double _api;              // °API
    private readonly double _gammaG;
    private readonly double _reservoirF;       // °F
    private readonly double _reservoirR;       // °R
    private readonly double _rsbScf;           // scf/STB
    private readonly double _bubblePointPsia;
    private readonly double _plateauRs;        // sm³/sm³ — Rs at Pb by the forward form
    private readonly double _bobAtBubblePoint;

    public BlackOilModel(BlackOilInputs inputs, ValidityRange validity)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(validity);

        _inputs = inputs;
        _api = inputs.OilGravity.Degrees;
        _gammaG = inputs.GasSpecificGravity;
        _reservoirR = inputs.ReservoirTemperature.Kelvin * RankinePerKelvin;
        _reservoirF = _reservoirR - FahrenheitOffset;
        _rsbScf = inputs.SolutionGorAtBubblePoint * ScfPerStbPerSm3PerSm3;

        if (_gammaG <= 0.0)
            throw new ModelFault("SDD-003 §4.1", null,
                $"Gas specific gravity {_gammaG} must be positive.");

        _bubblePointPsia = StandingBubblePointPsia();
        _plateauRs = ForwardRs(_bubblePointPsia);
        _bobAtBubblePoint = SaturatedBo(_plateauRs * ScfPerStbPerSm3PerSm3);

        Form = inputs.Form;
        Validity = validity;
        Pb = new Pressure(_bubblePointPsia * PascalsPerPsi);
    }

    public FluidForm Form { get; }
    public Pressure Pb { get; }
    public ValidityRange Validity { get; }

    // ------------------------------------------------------------- Rs

    /// <summary>
    /// Standing (1947). Constant at Rsb above the bubble point — the
    /// undersaturated plateau, and the reason GOR climbs only once Pr crosses Pb.
    /// </summary>
    public double Rs(Pressure p)
    {
        double psia = Psia(p);

        // The plateau is Rs evaluated AT Pb by the forward form — not the
        // declared Rsb. Standing's two published forms are only approximate
        // inverses: the Pb form's exponent 0.83 is a rounding of 1/1.2048 =
        // 0.8299468, so round-tripping Rsb → Pb → Rs lands ~1e-4 low. Anchoring
        // the plateau to the declared value instead would put a real
        // discontinuity in dissolved gas exactly at the bubble point, and
        // crossing Pb would create or destroy solution gas. Continuity is a
        // physical requirement; agreeing with the input to five digits is not.
        if (psia >= _bubblePointPsia) return _plateauRs;

        return ForwardRs(psia);
    }

    private double ForwardRs(double psia)
    {
        double exponent = 0.0125 * _api - 0.00091 * _reservoirF;
        double bracket = (psia / 18.2 + 1.4) * DetMath.Pow(10.0, exponent);
        double scf = _gammaG * DetMath.Pow(bracket, 1.2048);
        return scf / ScfPerStbPerSm3PerSm3;
    }

    /// <summary>Modified-black-oil condensate-gas ratio. BlackOil ⇒ 0 (05 §2).</summary>
    public double Rv(Pressure p) =>
        Form == FluidForm.ModifiedBlackOil
            ? throw new ModelFault("SDD-003 §4.1", null,
                "Modified-black-oil Rv is not implemented; a condensate fluid " +
                "system must not be run under the plain black-oil form (05 §2).")
            : 0.0;

    // ------------------------------------------------------------- Bo

    /// <summary>
    /// Vazquez-Beggs saturated, with the undersaturated branch compressing above
    /// Pb — which is what makes Bo PEAK at the bubble point rather than rise
    /// forever (05 §2).
    /// </summary>
    public FormationVolumeFactor Bo(Pressure p)
    {
        double psia = Psia(p);
        if (psia <= _bubblePointPsia)
            return new FormationVolumeFactor(SaturatedBo(Rs(p) * ScfPerStbPerSm3PerSm3));

        double co = (-1433.0 + 5.0 * _rsbScf + 17.2 * _reservoirF
                     - 1180.0 * _gammaG + 12.61 * _api) / (1.0e5 * psia);
        return new FormationVolumeFactor(
            _bobAtBubblePoint * DetMath.Exp(co * (_bubblePointPsia - psia)));
    }

    // ------------------------------------------------------------- Bw

    /// <summary>
    /// McCain (1990), transcribed from SDD-003 §4.1. Gas-free pure water: the two
    /// volume changes are thermal and pressure, applied multiplicatively.
    ///
    /// <para>Small — Bw stays within a few percent of 1 — but not 1. Substituting
    /// unity would put a systematic error straight into §3.1's withdrawal term
    /// for every compartment producing water, growing with water cut, which is
    /// exactly when the material balance matters most.</para>
    /// </summary>
    public FormationVolumeFactor Bw(Pressure p)
    {
        double psia = Psia(p);
        double t = _reservoirF;

        double thermal = -1.0001e-2 + 1.33391e-4 * t + 5.50654e-7 * t * t;

        double compression = -1.95301e-9 * psia * t
                             - 1.72834e-13 * psia * psia * t
                             - 3.58922e-7 * psia
                             - 2.25341e-10 * psia * psia;

        return new FormationVolumeFactor((1.0 + compression) * (1.0 + thermal));
    }

    private double SaturatedBo(double rsScf)
    {
        // API ≤ 30 and API > 30 have different fitted sets — this is the
        // correlation's own discontinuity, not a modelling choice.
        (double c1, double c2, double c3) = _api <= 30.0
            ? (4.677e-4, 1.751e-5, -1.811e-8)
            : (4.670e-4, 1.100e-5, 1.337e-9);

        return 1.0 + c1 * rsScf
             + (_reservoirF - 60.0) * (_api / _gammaG) * (c2 + c3 * rsScf);
    }

    private double StandingBubblePointPsia()
    {
        double exponent = 0.00091 * _reservoirF - 0.0125 * _api;
        double bracket = DetMath.Pow(_rsbScf / _gammaG, 0.83) * DetMath.Pow(10.0, exponent) - 1.4;

        if (bracket <= 0.0)
            throw new ModelFault("SDD-003 §4.1", null,
                $"Standing bubble point is non-physical for Rsb={_rsbScf:R} scf/STB, " +
                $"API={_api:R}, T={_reservoirF:R} °F.");

        return 18.2 * bracket;
    }

    // ------------------------------------------------------------- viscosity

    /// <summary>
    /// Beggs-Robinson. The sharp RISE below Pb is not special-cased: it falls
    /// out of the saturated branch as Rs drops and the oil loses its light ends.
    /// </summary>
    public Viscosity MuOil(Pressure p)
    {
        double psia = Psia(p);
        double rsScf = Rs(p) * ScfPerStbPerSm3PerSm3;

        double z = 3.0324 - 0.02023 * _api;
        double y = DetMath.Pow(10.0, z);
        double x = y * DetMath.Pow(_reservoirF, -1.163);
        double dead = DetMath.Pow(10.0, x) - 1.0;

        double a = 10.715 * DetMath.Pow(rsScf + 100.0, -0.515);
        double b = 5.44 * DetMath.Pow(rsScf + 150.0, -0.338);
        double saturated = a * DetMath.Pow(dead, b);

        if (psia <= _bubblePointPsia)
            return new Viscosity(saturated / CentipoisePerPascalSecond);

        double m = 2.6 * DetMath.Pow(psia, 1.187)
                 * DetMath.Exp(-11.513 - 8.98e-5 * psia);
        double undersaturated = saturated * DetMath.Pow(psia / _bubblePointPsia, m);
        return new Viscosity(undersaturated / CentipoisePerPascalSecond);
    }

    /// <summary>Lee-Gonzalez-Eakin (1966).</summary>
    public Viscosity MuGas(Pressure p)
    {
        double molecularWeight = 28.9647 * _gammaG;             // air's MW × γg
        double z = Z(p, _inputs.ReservoirTemperature);

        // Gas density at (p, T) in g/cm³, from the real-gas law.
        double densityGPerCm3 = Psia(p) * molecularWeight
                              / (z * 10.732 * _reservoirR) * 0.0160185;

        double k = (9.4 + 0.02 * molecularWeight) * DetMath.Pow(_reservoirR, 1.5)
                 / (209.0 + 19.0 * molecularWeight + _reservoirR);
        double x = 3.5 + 986.0 / _reservoirR + 0.01 * molecularWeight;
        double y = 2.4 - 0.2 * x;

        double centipoise = 1.0e-4 * k * DetMath.Exp(x * DetMath.Pow(densityGPerCm3, y));
        return new Viscosity(centipoise / CentipoisePerPascalSecond);
    }

    // ------------------------------------------------------------- Z and Bg

    /// <summary>
    /// Dranchuk-Abou-Kassem, implicit in Z. Solved by BISECTION on [0.2, 2.0] for
    /// the reason §3.1 gives for the material balance: it cannot diverge and
    /// needs no derivative, and 60 deterministic iterations cost nothing.
    /// </summary>
    public double Z(Pressure p, Temperature t)
    {
        double rankine = t.Kelvin * RankinePerKelvin;
        double pseudoCriticalT = 168.0 + 325.0 * _gammaG - 12.5 * _gammaG * _gammaG;
        double pseudoCriticalP = 677.0 + 15.0 * _gammaG - 37.5 * _gammaG * _gammaG;

        double tpr = rankine / pseudoCriticalT;
        double ppr = Psia(p) / pseudoCriticalP;

        if (ppr <= 0.0) return 1.0;      // vacuum limit: ideal, and DAK is undefined there

        double low = 0.2;
        double high = 2.0;
        for (int i = 0; i < 60; i++)
        {
            double mid = (low + high) * 0.5;
            double residual = DakResidual(mid, tpr, ppr);
            if (residual > 0.0) low = mid; else high = mid;
            if (high - low < 1.0e-10) break;
        }
        return (low + high) * 0.5;
    }

    private static double DakResidual(double z, double tpr, double ppr)
    {
        double rhoR = 0.27 * ppr / (z * tpr);
        double r2 = rhoR * rhoR;

        double tpr2 = tpr * tpr;
        double tpr3 = tpr2 * tpr;
        double tpr4 = tpr3 * tpr;
        double tpr5 = tpr4 * tpr;

        double predicted =
            1.0
            + (A1 + A2 / tpr + A3 / tpr3 + A4 / tpr4 + A5 / tpr5) * rhoR
            + (A6 + A7 / tpr + A8 / tpr2) * r2
            - A9 * (A7 / tpr + A8 / tpr2) * DetMath.Pow(rhoR, 5.0)
            + A10 * (1.0 + A11 * r2) * (r2 / tpr3) * DetMath.Exp(-A11 * r2);

        return predicted - z;
    }

    /// <summary>
    /// From Z, exactly — no correlation. Gas bridges to StandardGasVolume and
    /// never to the stock-tank family (finding 77).
    /// </summary>
    public GasFormationVolumeFactor Bg(Pressure p)
    {
        double z = Z(p, _inputs.ReservoirTemperature);
        double rm3PerSm3 = z * _inputs.ReservoirTemperature.Kelvin * StandardPressurePa
                         / (p.Pascals * StandardTemperatureK);
        return new GasFormationVolumeFactor(rm3PerSm3);
    }

    // ------------------------------------------------------------- split

    /// <summary>
    /// Per-material phase fractions at (P, T). Black oil is three pseudo-
    /// components, so the split is by the material's declared standard-condition
    /// phase — there is no compositional flash here, deliberately (05 §2).
    /// </summary>
    public PhaseSplit SplitAt(Composition composition, Pressure p, Temperature t)
    {
        ArgumentNullException.ThrowIfNull(_materials);

        var fractions = new List<(MaterialId, double, double, double)>(composition.Length);
        for (int ordinal = 0; ordinal < composition.Length; ordinal++)
        {
            var id = new MaterialId(ordinal);
            (double gas, double liquid, double aqueous) = _materials[id].Phase switch
            {
                PhaseAtStandardConditions.Gas => (1.0, 0.0, 0.0),
                PhaseAtStandardConditions.Liquid => (0.0, 1.0, 0.0),
                PhaseAtStandardConditions.Aqueous => (0.0, 0.0, 1.0),
                _ => (0.0, 1.0, 0.0),
            };
            fractions.Add((id, gas, liquid, aqueous));
        }
        return new PhaseSplit(fractions);
    }

    private IMaterialCatalog? _materials;

    /// <summary>
    /// The catalogue the split reads. Supplied after construction because the
    /// fluid system and the material catalogue both load from content and
    /// neither can be built first — the one deliberate two-phase construction
    /// here, and it faults loudly rather than defaulting (law L2).
    /// </summary>
    public void BindMaterials(IMaterialCatalog materials)
    {
        ArgumentNullException.ThrowIfNull(materials);
        if (_materials is not null)
            throw new InvariantFault("L5", null, "Material catalogue is already bound.");
        _materials = materials;
    }

    private static double Psia(Pressure p) => p.Pascals / PascalsPerPsi;
}
