using Beep.OilGasSim.Domain.Basins;
using Beep.OilGasSim.Domain.Companies;
using Beep.OilGasSim.Domain.Development;
using Beep.OilGasSim.Domain.Economy;
using Beep.OilGasSim.Domain.Exploration;
using Beep.OilGasSim.Domain.GameplayModes;
using Beep.OilGasSim.Domain.Production;
using Beep.OilGasSim.Domain.Turns;

namespace Beep.OilGasSim.Domain.GameSessions;

public sealed partial class GameSession
{
    public Basin Basin { get; set; } = new();
    public GameplayModeProfile ModeProfile { get; set; } = new();
    public BalanceProfile BalanceProfile { get; set; } = new();
}

public sealed class GameSessionAggregate
{
    public GameSession Session { get; set; } = new();
    public List<TurnAction> PendingActions { get; set; } = [];
    public List<TurnResult> TurnResults { get; set; } = [];
    public Dictionary<Guid, List<BlockKnowledge>> CompanyBlockKnowledge { get; set; } = [];
    public List<Discovery> Discoveries { get; set; } = [];
    public List<Prospect> Prospects { get; set; } = [];
    public List<DevelopmentProject> DevelopmentProjects { get; set; } = [];
    public List<ProducingField> ProducingFields { get; set; } = [];
    public List<HedgePosition> HedgePositions { get; set; } = [];
    public Dictionary<Guid, decimal> FinalScores { get; set; } = [];
    public List<Collaboration.ChatMessage> ChatMessages { get; set; } = [];
}
