// R13.4 — fiscal regimes (SDD-009 §3).
//
// The engine calls Assess once per licence per tick at stage 8 and books ONLY
// what comes back. There is no fiscal arithmetic anywhere else — which is why
// four regimes over one field give four materially different answers without a
// single branch outside these classes (R13-V4).
//
// Every regime is a PURE FUNCTION of its input. That is why CostPoolCarry
// appears on both sides of the PSC: a regime holding its own pool would be state
// outside the save's module blocks, and the under-recovery fixture could not be
// tested in isolation.
//
// All arithmetic is integer. The FiscalInput has already crossed the
// double→Money boundary, so nothing here rounds anything.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>
/// SDD-009 §3.1. Royalty on gross, tax on what is left after costs.
///
/// <para>ROYALTY IS DUE EVEN AT A LOSS (design 08 §4) — it is a share of
/// production, not of profit, and that is exactly what makes a marginal field
/// marginal. Tax is not: a loss carries forward indefinitely, with no uplift.</para>
/// </summary>
public sealed class RoyaltyTaxRegime : IFiscalRegime
{
    private readonly double _royaltyRate;
    private readonly double _taxRate;

    private Money _lossCarry = Money.Zero;

    public RoyaltyTaxRegime(ContentId id, double royaltyRate, double taxRate)
    {
        Validate(royaltyRate, nameof(royaltyRate));
        Validate(taxRate, nameof(taxRate));

        Id = id;
        _royaltyRate = royaltyRate;
        _taxRate = taxRate;
    }

    public ContentId Id { get; }

    /// <summary>The accumulated loss. Exposed because the player needs to know
    /// what shelter they are carrying — it is worth real money.</summary>
    public Money LossCarry => _lossCarry;

    public FiscalResult Assess(FiscalInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Money royalty = Money.RoundHalfEven(input.GrossRevenue.Cents * _royaltyRate);

        Money costs = input.RecoverableOpex + input.Depreciation;
        Money taxable = input.GrossRevenue - royalty - costs - _lossCarry;

        Money tax = taxable.Cents > 0
            ? Money.RoundHalfEven(taxable.Cents * _taxRate)
            : Money.Zero;

        // The carryforward is what the loss was, and it REPLACES rather than
        // accumulates on a profitable tick: a period that used up its shelter
        // has none left, and one that made a loss carries exactly that loss.
        _lossCarry = taxable.Cents < 0 ? new Money(-taxable.Cents) : Money.Zero;

        return new FiscalResult(
            Royalty: royalty,
            Tax: tax,
            ContractorTake: input.GrossRevenue - royalty - tax,
            CostPoolCarry: Money.Zero);      // a concession has no cost pool
    }

    internal static void Validate(double rate, string name)
    {
        if (rate is < 0.0 or > 1.0 || double.IsNaN(rate))
            throw new ModelFault("SDD-009 §3", null,
                $"{name} is " +
                rate.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ", not a fraction in [0, 1]");
    }
}

/// <summary>One R-factor tranche: above <see cref="From"/>, the contractor keeps
/// <see cref="ContractorShare"/> of profit oil.</summary>
public sealed record ProfitTranche(double From, double ContractorShare);

/// <summary>
/// SDD-009 §3.2 — production sharing, pinned step by step.
///
/// <para><b>Step 5 is the part implementations get wrong.</b> An UNDER-RECOVERED
/// period carries its unrecovered cost forward in full, with no interest,
/// forever. A regime that wrote it off, or that capped the carry, would quietly
/// hand the contractor's money to the state and no test of a single profitable
/// period would notice.</para>
///
/// <para>The R-factor tranche is evaluated on the PRIOR tick's cumulative values
/// (pinned). Using this tick's would make the share depend on the take and the
/// take depend on the share — a circularity with no fixed point worth
/// defending.</para>
/// </summary>
public sealed class ProductionSharingRegime : IFiscalRegime
{
    private readonly double _royaltyRate;
    private readonly double _costOilCapFraction;
    private readonly double _taxRate;
    private readonly bool _taxesProfitOil;
    private readonly IReadOnlyList<ProfitTranche> _tranches;

    public ProductionSharingRegime(
        ContentId id,
        double royaltyRate,
        double costOilCapFraction,
        double taxRate,
        bool taxesProfitOil,
        IReadOnlyList<ProfitTranche> tranches)
    {
        ArgumentNullException.ThrowIfNull(tranches);

        RoyaltyTaxRegime.Validate(royaltyRate, nameof(royaltyRate));
        RoyaltyTaxRegime.Validate(costOilCapFraction, nameof(costOilCapFraction));
        RoyaltyTaxRegime.Validate(taxRate, nameof(taxRate));

        if (tranches.Count == 0)
            throw new ModelFault("SDD-009 §3.2", null,
                $"PSC {id.Value} declares no profit tranches; there would be no share to apply");

        for (int i = 1; i < tranches.Count; i++)
            if (tranches[i].From <= tranches[i - 1].From)
                throw new ModelFault("SDD-009 §3.2", null,
                    $"PSC {id.Value}: tranches must ascend in R-factor; " +
                    $"entry {i} starts at {Format(tranches[i].From)}");

        Id = id;
        _royaltyRate = royaltyRate;
        _costOilCapFraction = costOilCapFraction;
        _taxRate = taxRate;
        _taxesProfitOil = taxesProfitOil;
        _tranches = tranches;
    }

    public ContentId Id { get; }

    public FiscalResult Assess(FiscalInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // 1. Royalty on gross.
        Money royalty = Money.RoundHalfEven(input.GrossRevenue.Cents * _royaltyRate);

        // 2. The cost-oil ceiling — a fraction of what is left after royalty.
        Money net = input.GrossRevenue - royalty;
        Money costOilCap = Money.RoundHalfEven(net.Cents * _costOilCapFraction);

        // 3. This period's recoverable costs join the pool.
        Money pool = input.CostPoolCarry + input.RecoverableOpex + input.RecoverableCapex;

        // 4. Recover what the cap allows.
        Money costOil = pool.Cents < costOilCap.Cents ? pool : costOilCap;

        // 5. THE CARRYFORWARD. In full, no interest, forever.
        Money carry = pool - costOil;

        // 6–7. Profit oil, split by the tranche the PRIOR R-factor lands in.
        Money profitOil = net - costOil;
        double share = ContractorShareAt(input.PriorRFactor);
        Money contractorProfitOil = Money.RoundHalfEven(profitOil.Cents * share);

        // 8. Tax, where the regime taxes profit oil.
        Money tax = _taxesProfitOil && contractorProfitOil.Cents > 0
            ? Money.RoundHalfEven(contractorProfitOil.Cents * _taxRate)
            : Money.Zero;

        return new FiscalResult(
            Royalty: royalty,
            Tax: tax,
            ContractorTake: costOil + contractorProfitOil - tax,
            CostPoolCarry: carry);
    }

    /// <summary>
    /// The contractor's share at an R-factor: the last tranche whose threshold
    /// it has reached.
    ///
    /// <para>A STEP function, not an interpolation. Real PSCs are written as
    /// steps and the discontinuity is the point — crossing a tranche is an event
    /// the contractor can see coming and can time development around.</para>
    /// </summary>
    public double ContractorShareAt(double rFactor)
    {
        double share = _tranches[0].ContractorShare;

        for (int i = 0; i < _tranches.Count; i++)
            if (rFactor >= _tranches[i].From) share = _tranches[i].ContractorShare;

        return share;
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// SDD-009 §3.3. The contractor is paid a fee per delivered volume and takes no
/// share of the oil at all — so the state carries the price risk and the
/// contractor carries the cost risk.
///
/// <para>That asymmetry is the whole reason a service contract feels different
/// to play: a price crash does not touch the fee, and a cost overrun is not
/// recoverable from production.</para>
/// </summary>
public sealed class ServiceContractRegime : IFiscalRegime
{
    private readonly Money _feePerUnit;

    public ServiceContractRegime(ContentId id, Money feePerUnit)
    {
        if (feePerUnit.Cents < 0)
            throw new ModelFault("SDD-009 §3.3", null,
                $"service contract {id.Value} declares a negative fee");

        Id = id;
        _feePerUnit = feePerUnit;
    }

    public ContentId Id { get; }

    /// <summary>Volume delivered this tick, set by the caller before Assess —
    /// the fee is per unit and the input record carries revenue, not volume.</summary>
    public double DeliveredUnits { get; set; }

    /// <summary>Escalation applies to the fee at accrual (SDD-009 §3.3).</summary>
    public double CostIndex { get; set; } = 1.0;

    public FiscalResult Assess(FiscalInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Money fee = Money.RoundHalfEven(_feePerUnit.Cents * DeliveredUnits * CostIndex);

        // The contractor takes the fee; everything else is the state's. There is
        // no royalty and no cost pool, because there is no share of production
        // to apply either to.
        return new FiscalResult(
            Royalty: Money.Zero,
            Tax: Money.Zero,
            ContractorTake: fee,
            CostPoolCarry: Money.Zero);
    }
}
