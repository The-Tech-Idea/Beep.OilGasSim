using Beep.OilGasSim.Domain.GameplayModes;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Scenarios;

namespace Beep.OilGasSim.Application.Interfaces;

public interface IContentLoader
{
    Task<IReadOnlyList<ScenarioDefinition>> GetScenariosAsync(CancellationToken cancellationToken = default);
    Task<ScenarioDefinition?> GetScenarioAsync(string scenarioId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameplayModeProfile>> GetGameplayModesAsync(CancellationToken cancellationToken = default);
    Task<GameplayModeProfile?> GetGameplayModeAsync(string profileId, CancellationToken cancellationToken = default);
    Task<BalanceProfile> GetBalanceProfileAsync(string profileId, CancellationToken cancellationToken = default);
}

public interface IGameSessionStore
{
    Task<GameSessionAggregate?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<GameSessionAggregate?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default);
    Task SaveAsync(GameSessionAggregate aggregate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameSessionAggregate>> ListAsync(CancellationToken cancellationToken = default);
}
