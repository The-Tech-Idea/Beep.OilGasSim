using Beep.OilGasSim.Domain.Common;

namespace Beep.OilGasSim.Domain.Economy;

public sealed partial class CompanyFinance
{
    public Guid CompanyId { get; set; }
    public decimal Cash { get; set; }
    public decimal Debt { get; set; }
    public decimal RevenueThisTurn { get; set; }
    public decimal OpexThisTurn { get; set; }
    public decimal CapexThisTurn { get; set; }
    public decimal InterestThisTurn { get; set; }
    public decimal RoyaltyThisTurn { get; set; }
    public decimal NetIncomeThisTurn { get; set; }
    public decimal FreeCashFlowThisTurn { get; set; }
    public decimal AssetValue { get; set; }
    public decimal AbandonmentLiability { get; set; }
    public int CreditRating { get; set; } = 70;
}

public sealed class MarketState
{
    public int TurnNumber { get; set; }
    public decimal OilPrice { get; set; } = 75m;
    public MarketTrend Trend { get; set; } = MarketTrend.Stable;
    public string MarketSummary { get; set; } = "Oil prices stable.";
}
