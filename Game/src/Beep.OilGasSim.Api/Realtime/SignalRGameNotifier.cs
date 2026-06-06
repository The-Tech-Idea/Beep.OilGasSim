using Beep.OilGasSim.Api.Hubs;
using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Domain.Collaboration;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;
using Microsoft.AspNetCore.SignalR;

namespace Beep.OilGasSim.Api.Realtime;

public sealed class SignalRGameNotifier(IHubContext<GameHub> hub) : IGameRealtimeNotifier
{
    private static string SessionGroup(Guid sessionId) => $"session:{sessionId}";

    public Task LobbyUpdatedAsync(Guid sessionId, GameSessionAggregate aggregate) =>
        hub.Clients.Group(SessionGroup(sessionId)).SendAsync("LobbyUpdated", MapLobby(aggregate));

    public Task TurnCommittedAsync(Guid sessionId, Guid companyId, int committedCount, int totalCompanies) =>
        hub.Clients.Group(SessionGroup(sessionId)).SendAsync("TurnCommitted", new
        {
            companyId,
            committedCount,
            totalCompanies
        });

    public Task TurnResolvedAsync(Guid sessionId, TurnResult result) =>
        hub.Clients.Group(SessionGroup(sessionId)).SendAsync("TurnResolved", new
        {
            turnNumber = result.TurnNumber,
            eventCount = result.Events.Count
        });

    public Task ChatMessageAsync(Guid sessionId, ChatMessage message) =>
        hub.Clients.Group(SessionGroup(sessionId)).SendAsync("ChatMessage", new
        {
            id = message.Id,
            companyId = message.CompanyId,
            senderName = message.SenderName,
            channel = message.Channel,
            text = message.Text,
            sentAtUtc = message.SentAtUtc
        });

    public Task GameStartedAsync(Guid sessionId, GameSessionAggregate aggregate) =>
        hub.Clients.Group(SessionGroup(sessionId)).SendAsync("GameStarted", new
        {
            sessionId,
            state = aggregate.Session.State.ToString(),
            currentTurnNumber = aggregate.Session.CurrentTurnNumber
        });

    private static object MapLobby(GameSessionAggregate aggregate)
    {
        var session = aggregate.Session;
        return new
        {
            sessionId = session.Id,
            joinCode = session.JoinCode,
            playerCount = session.Companies.Count,
            maxPlayers = session.MaxPlayers,
            minPlayers = session.MinPlayers,
            hostCompanyId = session.HostCompanyId,
            players = session.Companies.SelectMany(c => c.Players.Select(p => new
            {
                companyId = c.Id,
                playerId = p.PlayerId,
                companyName = c.Name,
                displayName = p.DisplayName,
                colorHex = c.ColorHex,
                isHost = p.IsHost,
                isReady = p.IsReady
            }))
        };
    }
}
