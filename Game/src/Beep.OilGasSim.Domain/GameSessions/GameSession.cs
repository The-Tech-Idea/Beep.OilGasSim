using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Companies;
using Beep.OilGasSim.Domain.Economy;

namespace Beep.OilGasSim.Domain.GameSessions;

public sealed partial class GameSession
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public GameplayModeType GameplayMode { get; set; }
    public string GameplayModeProfileId { get; set; } = "";
    public GameSessionState State { get; set; } = GameSessionState.Lobby;
    public int CurrentTurnNumber { get; set; }
    public int TotalTurns { get; set; }
    public int GameSeed { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public List<Company> Companies { get; set; } = [];
    public MarketState Market { get; set; } = new();
}
