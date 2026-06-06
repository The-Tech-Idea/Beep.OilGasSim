using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Domain.Collaboration;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;

namespace Beep.OilGasSim.Application.GameSessions;

public sealed class NullGameRealtimeNotifier : IGameRealtimeNotifier
{
    public Task LobbyUpdatedAsync(Guid sessionId, GameSessionAggregate aggregate) => Task.CompletedTask;
    public Task TurnCommittedAsync(Guid sessionId, Guid companyId, int committedCount, int totalCompanies) => Task.CompletedTask;
    public Task TurnResolvedAsync(Guid sessionId, TurnResult result) => Task.CompletedTask;
    public Task ChatMessageAsync(Guid sessionId, ChatMessage message) => Task.CompletedTask;
    public Task GameStartedAsync(Guid sessionId, GameSessionAggregate aggregate) => Task.CompletedTask;
}
