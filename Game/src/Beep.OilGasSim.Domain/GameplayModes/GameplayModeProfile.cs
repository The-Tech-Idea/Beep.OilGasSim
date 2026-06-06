using Beep.OilGasSim.Domain.Common;

namespace Beep.OilGasSim.Domain.GameplayModes;

public sealed class GameplayModeProfile
{
    public string Id { get; set; } = "";
    public GameplayModeType ModeType { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public int TotalTurns { get; set; }
    public int ActionSlotsPerTurn { get; set; }

    public decimal StartingCash { get; set; }
    public decimal MaxDebt { get; set; }

    public double ExplorationChanceModifier { get; set; } = 1.0;
    public double DevelopmentTimeModifier { get; set; } = 1.0;
    public double CostModifier { get; set; } = 1.0;
    public double OilPriceVolatilityModifier { get; set; } = 1.0;
    public double AbandonmentPenaltyModifier { get; set; } = 1.0;
    public double EventIntensityModifier { get; set; } = 1.0;

    public bool EnableAdvancedFinance { get; set; }
    public bool EnableDetailedGeology { get; set; }
    public bool EnableDetailedAbandonment { get; set; }
    public bool EnableHedging { get; set; }
    public bool EnablePlayerTrading { get; set; }
    public bool EnableTeamMode { get; set; }

    public AiAssistanceLevel AiAssistanceLevel { get; set; }
    public UiComplexityLevel UiComplexityLevel { get; set; }
}
