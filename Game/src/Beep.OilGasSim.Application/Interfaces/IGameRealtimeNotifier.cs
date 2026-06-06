using Beep.OilGasSim.Domain.Collaboration;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;

namespace Beep.OilGasSim.Application.Interfaces;

public interface IGameRealtimeNotifier
{
    Task LobbyUpdatedAsync(Guid sessionId, GameSessionAggregate aggregate);
    Task TurnCommittedAsync(Guid sessionId, Guid companyId, int committedCount, int totalCompanies);
    Task TurnResolvedAsync(Guid sessionId, TurnResult result);
    Task ChatMessageAsync(Guid sessionId, ChatMessage message);
    Task GameStartedAsync(Guid sessionId, GameSessionAggregate aggregate);
}
