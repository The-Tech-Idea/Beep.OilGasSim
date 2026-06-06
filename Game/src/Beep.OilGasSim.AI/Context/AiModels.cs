namespace Beep.OilGasSim.AI.Context;

public enum AiAdvisorType
{
    Strategy,
    Geologist,
    Cfo,
    Hse
}

public sealed class AiGameContext
{
    public Guid GameSessionId { get; set; }
    public Guid CompanyId { get; set; }
    public int CurrentTurn { get; set; }
    public int TotalTurns { get; set; }
    public string GameplayMode { get; set; } = "";
    public bool EnableHedging { get; set; }
    public AiCompanySnapshot Company { get; set; } = new();
    public AiMarketSnapshot Market { get; set; } = new();
    public List<AiAssetSummary> Assets { get; set; } = [];
    public List<AiRecentEvent> RecentEvents { get; set; } = [];
    public AiSelectedContext? Selected { get; set; }
    public string KnownLimitations { get; set; } =
        "Advisor uses only player-visible data. Hidden geology and competitor secrets are excluded.";
}

public sealed class AiCompanySnapshot
{
    public string CompanyName { get; set; } = "";
    public decimal Cash { get; set; }
    public decimal Debt { get; set; }
    public decimal CompanyValue { get; set; }
    public double ProductionBoePerDay { get; set; }
    public double ReservesMmboe { get; set; }
    public decimal AbandonmentLiability { get; set; }
    public int Reputation { get; set; }
    public int CreditRating { get; set; }
    public int CurrentRank { get; set; }
    public int ActionSlotsRemaining { get; set; }
}

public sealed class AiMarketSnapshot
{
    public decimal OilPrice { get; set; }
    public string Trend { get; set; } = "";
    public string Summary { get; set; } = "";
}

public sealed class AiAssetSummary
{
    public Guid AssetId { get; set; }
    public Guid? BlockId { get; set; }
    public string Name { get; set; } = "";
    public string AssetType { get; set; } = "";
    public string Stage { get; set; } = "";
    public string KnownSummary { get; set; } = "";
    public string MainRisk { get; set; } = "";
    public double? EstimatedChanceOfSuccess { get; set; }
    public double? EstimatedVolumeMmboe { get; set; }
    public double Confidence { get; set; }
    public decimal? EstimatedCostToNextStep { get; set; }
}

public sealed class AiRecentEvent
{
    public int TurnNumber { get; set; }
    public string Category { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Detail { get; set; } = "";
}

public sealed class AiSelectedContext
{
    public Guid? BlockId { get; set; }
    public string BlockCode { get; set; } = "";
    public string Stage { get; set; } = "";
    public string PublicGeologyHint { get; set; } = "";
    public string PublicRiskRating { get; set; } = "";
    public double? EstimatedChanceOfSuccess { get; set; }
}

public sealed class AiAdvisorRequest
{
    public Guid CompanyId { get; set; }
    public AiAdvisorType AdvisorType { get; set; } = AiAdvisorType.Strategy;
    public string Message { get; set; } = "";
    public Guid? SelectedBlockId { get; set; }
    public Guid? SelectedAssetId { get; set; }
}

public sealed class AiAdvisorResponse
{
    public string AdvisorType { get; set; } = "";
    public string Message { get; set; } = "";
    public string RecommendationType { get; set; } = "Advice";
    public List<string> SuggestedActions { get; set; } = [];
    public List<string> Risks { get; set; } = [];
}

public sealed class AiTurnReport
{
    public int TurnNumber { get; set; }
    public string Summary { get; set; } = "";
    public List<string> Highlights { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
}
