namespace Beep.OilGasSim.Domain.GameplayModes;

public sealed class BalanceProfile
{
    public string Id { get; set; } = "mvp-balance";
    public int ActionSlotsPerTurn { get; set; } = 3;
    public ActionCosts Costs { get; set; } = new();
    public EconomySettings Economy { get; set; } = new();
    public MarketSettings Market { get; set; } = new();
}

public sealed class ActionCosts
{
    public decimal GeologicalStudy { get; set; } = 5_000_000m;
    public decimal TwoDSeismic { get; set; } = 15_000_000m;
    public decimal ExplorationWell { get; set; } = 40_000_000m;
    public decimal AppraisalWell { get; set; } = 30_000_000m;
    public decimal SmallDevelopment { get; set; } = 120_000_000m;
    public decimal StandardDevelopment { get; set; } = 220_000_000m;
    public decimal LargeDevelopment { get; set; } = 350_000_000m;
    public decimal OptimizeField { get; set; } = 20_000_000m;
    public decimal LicenseFeePerBlockPerTurn { get; set; } = 1_000_000m;
}

public sealed class EconomySettings
{
    public decimal RoyaltyRate { get; set; } = 0.10m;
    public decimal MaxDebt { get; set; } = 500_000_000m;
    public int StartingCreditRating { get; set; } = 70;
    public int StartingReputation { get; set; } = 50;
    public double DaysPerTurn { get; set; } = 182.5;
}

public sealed class MarketSettings
{
    public decimal StartingOilPrice { get; set; } = 75m;
    public decimal MinNormalPrice { get; set; } = 45m;
    public decimal MaxNormalPrice { get; set; } = 110m;
    public decimal HedgePriceDiscount { get; set; } = 3m;
}
