using Beep.OilGasSim.Domain.Collaboration;
using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;

namespace Beep.OilGasSim.Application.Interfaces;

public interface IGameSessionService
{
    Task<GameSessionAggregate> CreateSessionAsync(
        CreateGameSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<GameSessionAggregate?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<GameSessionAggregate?> GetSessionByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default);

    Task<JoinSessionResult> JoinSessionAsync(
        JoinGameSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<GameSessionAggregate> SetPlayerReadyAsync(
        Guid sessionId,
        Guid companyId,
        Guid playerId,
        bool isReady,
        CancellationToken cancellationToken = default);

    Task<GameSessionAggregate> StartSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<TurnAction> SubmitActionAsync(
        Guid sessionId,
        SubmitActionRequest request,
        CancellationToken cancellationToken = default);

    Task<TurnResult> CommitTurnAsync(
        Guid sessionId,
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<ChatMessage> SendChatAsync(
        SendChatRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CreateGameSessionRequest
{
    public string ScenarioId { get; set; } = "desert-frontier";
    public string GameplayModeProfileId { get; set; } = "balanced";
    public string CompanyName { get; set; } = "Beep Energy";
    public string PlayerDisplayName { get; set; } = "Player";
    public bool IsMultiplayer { get; set; }
}

public sealed class JoinGameSessionRequest
{
    public Guid SessionId { get; set; }
    public string JoinCode { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string PlayerDisplayName { get; set; } = "Player";
}

public sealed class JoinSessionResult
{
    public Guid SessionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PlayerId { get; set; }
}

public sealed class SendChatRequest
{
    public Guid SessionId { get; set; }
    public Guid? CompanyId { get; set; }
    public string SenderName { get; set; } = "";
    public string Channel { get; set; } = "public";
    public string Text { get; set; } = "";
}

public sealed class SubmitActionRequest
{
    public Guid CompanyId { get; set; }
    public TurnActionType ActionType { get; set; }
    public Guid? TargetBlockId { get; set; }
    public Guid? TargetAssetId { get; set; }
    public decimal BidAmount { get; set; }
    public string? ParametersJson { get; set; }
}
