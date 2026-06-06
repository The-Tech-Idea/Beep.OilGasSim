using Beep.OilGasSim.Domain.Common;

namespace Beep.OilGasSim.Domain.Turns;

public sealed class TurnAction
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid CompanyId { get; set; }
    public int TurnNumber { get; set; }
    public TurnActionType ActionType { get; set; }
    public Guid? TargetBlockId { get; set; }
    public Guid? TargetAssetId { get; set; }
    public decimal BidAmount { get; set; }
    public decimal EstimatedCost { get; set; }
    public int ActionSlotCost { get; set; } = 1;
    public TurnActionStatus Status { get; set; } = TurnActionStatus.Pending;
    public string ParametersJson { get; set; } = "";
}

public sealed class TurnResult
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public int TurnNumber { get; set; }
    public decimal OilPrice { get; set; }
    public List<TurnEventReport> Events { get; set; } = [];
    public List<CompanyTurnSummary> CompanySummaries { get; set; } = [];
    public DateTime ResolvedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TurnEventReport
{
    public Guid CompanyId { get; set; }
    public string Category { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Detail { get; set; } = "";
    public bool IsPublic { get; set; }
}

public sealed class CompanyTurnSummary
{
    public Guid CompanyId { get; set; }
    public decimal StartingCash { get; set; }
    public decimal EndingCash { get; set; }
    public decimal Revenue { get; set; }
    public decimal Capex { get; set; }
    public decimal CompanyValue { get; set; }
    public double ProductionBoePerDay { get; set; }
    public int Rank { get; set; }
}

public sealed class ActionValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public decimal ConfirmedCost { get; set; }
    public int ActionSlotCost { get; set; } = 1;
}
