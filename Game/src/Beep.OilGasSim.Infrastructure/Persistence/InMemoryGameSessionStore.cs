using System.Collections.Concurrent;
using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Domain.GameSessions;

namespace Beep.OilGasSim.Infrastructure.Persistence;

public sealed class InMemoryGameSessionStore : IGameSessionStore
{
    private readonly ConcurrentDictionary<Guid, GameSessionAggregate> _sessions = new();

    public Task<GameSessionAggregate?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sessions.GetValueOrDefault(sessionId));

    public Task<GameSessionAggregate?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default)
    {
        var normalized = joinCode.Trim().ToUpperInvariant();
        var match = _sessions.Values.FirstOrDefault(a =>
            a.Session.JoinCode.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task SaveAsync(GameSessionAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _sessions[aggregate.Session.Id] = aggregate;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GameSessionAggregate>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GameSessionAggregate>>(_sessions.Values.ToList());
}
