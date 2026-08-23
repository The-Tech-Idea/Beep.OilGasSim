// R13.10 — the working-interest sale, the second of three player-side
// restructuring levers (SDD-011 §4's finding-275 amendment).
//
// THE SAME TRANSACTION AS A FARM-OUT, FROM THE SELLER'S SIDE. Design 08 §7
// names "asset sales" and "forced farm-outs" as two of insolvency's escape
// routes; from a distressed company's own books they are the one decision —
// give up a share of the field's future economics for cash now.
//
// PERMANENT, UNLIKE THE HEDGE OR INSURANCE. A collar or a policy settles
// fresh every tick and carries nothing between them; a sold stake does not
// expire at tick's end, which makes this the first of the three
// restructuring levers that needs its own persisted state.

using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>SDD-011 §4's finding-275 amendment — the one sale this
/// composition ships.</summary>
public sealed record WorkingInterestTerms(
    double MaxSellableFraction,   // cumulative, of the whole field
    double DistressDiscount);     // off the DCF-priced fair value

/// <summary>What the company has already sold.</summary>
public sealed class WorkingInterest : IStateOwner
{
    /// <summary>The fraction of the field's future economics already sold.
    /// Grows only — there is no mechanism to buy a share back (a repurchase
    /// is a materially larger further task, named rather than built).</summary>
    public double PartnerShare { get; private set; }

    /// <summary>
    /// What a fraction sells for today (SDD-011 §4): the reserves' present
    /// value BEFORE the advance-rate haircut — the SAME figure
    /// <c>Bank.Terms.ReserveValue</c> already carries, reused rather than a
    /// second DCF walk (a stated simplification of §4's own "P50 NPV"
    /// wording, since this walk prices 1P/Proved) — times the fraction sold,
    /// at a distress discount.
    /// </summary>
    public static Money Price(WorkingInterestTerms terms, Money reserveValue, double fraction)
    {
        ArgumentNullException.ThrowIfNull(terms);

        return Money.RoundHalfEven(reserveValue.Cents * fraction * (1.0 - terms.DistressDiscount));
    }

    public void Sell(double fraction) => PartnerShare += fraction;

    public static void Validate(WorkingInterestTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        if (terms.MaxSellableFraction is <= 0.0 or > 1.0)
            throw new ModelFault("SDD-011 §4", null,
                $"a sellable cap {terms.MaxSellableFraction} must be in (0, 1]");

        if (terms.DistressDiscount is < 0.0 or >= 1.0)
            throw new ModelFault("SDD-011 §4", null,
                $"a distress discount {terms.DistressDiscount} must be in [0, 1); at one the " +
                "field would sell for nothing");
    }

    // ------------------------------------------------------- SDD-013 §4

    public StateKey Key { get; } = new("company.working-interest");

    public int SchemaVersion => 1;

    /// <summary>Nothing: one scalar that depends on no other owner.</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteDouble("partner-share", PartnerShare);
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        PartnerShare = reader.ReadDouble("partner-share");
    }
}
