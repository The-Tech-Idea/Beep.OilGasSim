using Beep.OilGasSim.Domain.GameplayModes;

namespace Beep.OilGasSim.Domain.GameSessions;

public sealed class TurnResolutionContext
{
    public GameSessionAggregate Aggregate { get; set; } = new();
    public int TurnNumber { get; set; }
    public GameplayModeProfile GameplayModeProfile { get; set; } = new();
    public BalanceProfile BalanceProfile { get; set; } = new();
    public List<Turns.TurnAction> Actions { get; set; } = [];
    public Turns.TurnResult Result { get; set; } = new();
}
