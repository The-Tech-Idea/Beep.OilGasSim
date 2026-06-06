using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Application.GameSessions;
using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;
using Beep.OilGasSim.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Beep.OilGasSim.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    private readonly PersistenceOptions _persistence;
    private readonly IHostEnvironment _environment;

    public HealthController(PersistenceOptions persistence, IHostEnvironment environment)
    {
        _persistence = persistence;
        _environment = environment;
    }

    [HttpGet("/health")]
    public IActionResult Get()
    {
        var sqlitePath = _persistence.Provider == PersistenceProvider.Sqlite
            ? PersistenceRegistration.ResolveSqlitePath(_environment, _persistence.SqlitePath)
            : null;

        return Ok(new
        {
            status = "healthy",
            service = "Beep.OilGasSim.Api",
            persistence = _persistence.Provider.ToString(),
            sqlitePath
        });
    }
}

[ApiController]
[Route("api/game-sessions")]
public sealed class GameSessionsController : ControllerBase
{
    private readonly IGameSessionService _gameSessions;

    public GameSessionsController(IGameSessionService gameSessions) => _gameSessions = gameSessions;

    [HttpPost]
    public async Task<ActionResult<GameSessionResponse>> Create([FromBody] CreateGameSessionRequest request, CancellationToken ct)
    {
        var aggregate = await _gameSessions.CreateSessionAsync(request, ct);
        return Ok(MapSession(aggregate));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GameSessionResponse>> Get(Guid id, CancellationToken ct)
    {
        var aggregate = await _gameSessions.GetSessionAsync(id, ct);
        return aggregate is null ? NotFound() : Ok(MapSession(aggregate));
    }

    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<GameSessionResponse>> GetByJoinCode(string code, CancellationToken ct)
    {
        var aggregate = await _gameSessions.GetSessionByJoinCodeAsync(code, ct);
        return aggregate is null ? NotFound() : Ok(MapSession(aggregate));
    }

    [HttpPost("join")]
    public async Task<ActionResult<JoinSessionResponse>> Join([FromBody] JoinGameSessionRequest request, CancellationToken ct)
    {
        var result = await _gameSessions.JoinSessionAsync(request, ct);
        var aggregate = await _gameSessions.GetSessionAsync(result.SessionId, ct);
        return Ok(new JoinSessionResponse
        {
            SessionId = result.SessionId,
            CompanyId = result.CompanyId,
            PlayerId = result.PlayerId,
            Session = aggregate is null ? null! : MapSession(aggregate)
        });
    }

    [HttpPost("{id:guid}/companies/{companyId:guid}/players/{playerId:guid}/ready")]
    public async Task<ActionResult<GameSessionResponse>> SetReady(
        Guid id,
        Guid companyId,
        Guid playerId,
        [FromBody] SetReadyRequest request,
        CancellationToken ct)
    {
        var aggregate = await _gameSessions.SetPlayerReadyAsync(id, companyId, playerId, request.IsReady, ct);
        return Ok(MapSession(aggregate));
    }

    [HttpPost("{id:guid}/chat")]
    public async Task<ActionResult<ChatMessageDto>> SendChat(Guid id, [FromBody] SendChatRequest request, CancellationToken ct)
    {
        request.SessionId = id;
        var message = await _gameSessions.SendChatAsync(request, ct);
        return Ok(new ChatMessageDto
        {
            Id = message.Id,
            CompanyId = message.CompanyId,
            SenderName = message.SenderName,
            Channel = message.Channel,
            Text = message.Text,
            SentAtUtc = message.SentAtUtc
        });
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<GameSessionResponse>> Start(Guid id, CancellationToken ct)
    {
        var aggregate = await _gameSessions.StartSessionAsync(id, ct);
        return Ok(MapSession(aggregate));
    }

    [HttpPost("{id:guid}/actions")]
    public async Task<ActionResult<TurnActionResponse>> SubmitAction(
        Guid id,
        [FromBody] SubmitActionRequest request,
        CancellationToken ct)
    {
        var action = await _gameSessions.SubmitActionAsync(id, request, ct);
        return Ok(new TurnActionResponse
        {
            Id = action.Id,
            ActionType = action.ActionType,
            TargetBlockId = action.TargetBlockId,
            TargetAssetId = action.TargetAssetId,
            EstimatedCost = action.EstimatedCost,
            Status = action.Status
        });
    }

    [HttpPost("{id:guid}/companies/{companyId:guid}/commit")]
    public async Task<ActionResult<TurnResultResponse>> CommitTurn(Guid id, Guid companyId, CancellationToken ct)
    {
        var result = await _gameSessions.CommitTurnAsync(id, companyId, ct);
        return Ok(new TurnResultResponse
        {
            TurnNumber = result.TurnNumber,
            Events = result.Events.Select(e => new TurnEventDto
            {
                Category = e.Category,
                Headline = e.Headline,
                Detail = e.Detail,
                IsPublic = e.IsPublic
            }).ToList(),
            CompanySummaries = result.CompanySummaries.Select(s => new CompanyTurnSummaryDto
            {
                CompanyId = s.CompanyId,
                EndingCash = s.EndingCash,
                Capex = s.Capex,
                CompanyValue = s.CompanyValue,
                Rank = s.Rank
            }).ToList()
        });
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<SessionHistoryResponse>> GetHistory(
        Guid id,
        [FromQuery] Guid? companyId,
        CancellationToken ct)
    {
        var aggregate = await _gameSessions.GetSessionAsync(id, ct);
        if (aggregate is null)
        {
            return NotFound();
        }

        var viewerId = companyId ?? aggregate.Session.Companies.FirstOrDefault()?.Id;
        var oilPricePoints = aggregate.TurnResults
            .OrderBy(r => r.TurnNumber)
            .Select(r => new HistoryPointDto { TurnNumber = r.TurnNumber, Value = r.OilPrice })
            .ToList();

        if (oilPricePoints.Count == 0)
        {
            oilPricePoints.Add(new HistoryPointDto
            {
                TurnNumber = aggregate.Session.CurrentTurnNumber,
                Value = aggregate.Session.Market.OilPrice
            });
        }

        var productionPoints = new List<HistoryPointDto>();
        var cashPoints = new List<HistoryPointDto>();
        var valuePoints = new List<HistoryPointDto>();

        if (viewerId.HasValue)
        {
            foreach (var result in aggregate.TurnResults.OrderBy(r => r.TurnNumber))
            {
                var summary = result.CompanySummaries.FirstOrDefault(s => s.CompanyId == viewerId.Value);
                if (summary is null) continue;

                productionPoints.Add(new HistoryPointDto
                {
                    TurnNumber = result.TurnNumber,
                    Value = (decimal)summary.ProductionBoePerDay
                });
                cashPoints.Add(new HistoryPointDto
                {
                    TurnNumber = result.TurnNumber,
                    Value = summary.EndingCash
                });
                valuePoints.Add(new HistoryPointDto
                {
                    TurnNumber = result.TurnNumber,
                    Value = summary.CompanyValue
                });
            }
        }

        return Ok(new SessionHistoryResponse
        {
            OilPrice = oilPricePoints,
            ProductionBoePerDay = productionPoints,
            Cash = cashPoints,
            CompanyValue = valuePoints
        });
    }

    [HttpGet("{id:guid}/map")]
    public async Task<ActionResult<MapResponse>> GetMap(Guid id, [FromQuery] Guid? companyId, CancellationToken ct)
    {
        var aggregate = await _gameSessions.GetSessionAsync(id, ct);
        if (aggregate is null)
        {
            return NotFound();
        }

        var viewerCompanyId = companyId ?? aggregate.Session.Companies.FirstOrDefault()?.Id;
        return Ok(new MapResponse
        {
            Blocks = aggregate.Session.Basin.Blocks.Select(b => new BlockMapDto
            {
                Id = b.Id,
                BlockCode = b.BlockCode,
                Name = b.Name,
                GridX = b.GridX,
                GridY = b.GridY,
                OwnerCompanyId = b.OwnerCompanyId,
                Stage = b.Stage.ToString(),
                PublicGeologyHint = b.PublicData.PublicGeologyHint,
                PublicRiskRating = b.PublicData.PublicRiskRating.ToString(),
                EstimatedChanceOfSuccess = viewerCompanyId.HasValue
                    ? aggregate.CompanyBlockKnowledge.GetValueOrDefault(viewerCompanyId.Value)?
                        .FirstOrDefault(k => k.BlockId == b.Id)?.EstimatedChanceOfSuccess
                    : null
            }).ToList()
        });
    }

    [HttpGet("{id:guid}/fields")]
    public async Task<ActionResult<IReadOnlyList<ProducingFieldDto>>> GetFields(Guid id, CancellationToken ct)
    {
        var aggregate = await _gameSessions.GetSessionAsync(id, ct);
        if (aggregate is null)
        {
            return NotFound();
        }

        return Ok(aggregate.ProducingFields.Select(f => new ProducingFieldDto
        {
            Id = f.Id,
            BlockId = f.BlockId,
            CompanyId = f.CompanyId,
            Name = f.Name,
            Stage = f.Stage.ToString(),
            CurrentProductionBoePerDay = f.CurrentProductionBoePerDay,
            RemainingRecoverableMmboe = f.RemainingRecoverableMmboe,
            ProductionPhase = f.ProductionPhase.ToString()
        }).ToList());
    }

    [HttpGet("{id:guid}/leaderboard")]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntryDto>>> GetLeaderboard(Guid id, CancellationToken ct)
    {
        var aggregate = await _gameSessions.GetSessionAsync(id, ct);
        if (aggregate is null)
        {
            return NotFound();
        }

        return Ok(aggregate.Session.Companies
            .OrderBy(c => c.Rank)
            .Select(c => new LeaderboardEntryDto
            {
                CompanyId = c.Id,
                Name = c.Name,
                Rank = c.Rank,
                CompanyValue = c.CompanyValue,
                Cash = c.Finance.Cash,
                Debt = c.Finance.Debt,
                ProductionBoePerDay = c.TotalProductionBoePerDay
            }).ToList());
    }

    [HttpGet("{id:guid}/final-score")]
    public async Task<ActionResult<FinalScoreResponse>> GetFinalScore(Guid id, CancellationToken ct)
    {
        var aggregate = await _gameSessions.GetSessionAsync(id, ct);
        if (aggregate is null)
        {
            return NotFound();
        }

        if (aggregate.Session.State != GameSessionState.Completed)
        {
            return BadRequest(new { message = "Game is not completed yet." });
        }

        return Ok(new FinalScoreResponse
        {
            Scores = aggregate.FinalScores.Select(kv => new FinalScoreEntryDto
            {
                CompanyId = kv.Key,
                CompanyName = aggregate.Session.Companies.First(c => c.Id == kv.Key).Name,
                FinalScore = kv.Value
            }).OrderByDescending(s => s.FinalScore).ToList()
        });
    }

    private static GameSessionResponse MapSession(GameSessionAggregate aggregate)
    {
        var session = aggregate.Session;
        return new GameSessionResponse
        {
            Id = session.Id,
            Name = session.Name,
            ScenarioId = session.ScenarioId,
            GameplayMode = session.GameplayMode.ToString(),
            State = session.State.ToString(),
            CurrentTurnNumber = session.CurrentTurnNumber,
            TotalTurns = session.TotalTurns,
            OilPrice = session.Market.OilPrice,
            ActionSlotsPerTurn = session.ModeProfile.ActionSlotsPerTurn,
            IsMultiplayer = session.IsMultiplayer,
            JoinCode = session.JoinCode,
            MaxPlayers = session.MaxPlayers,
            MinPlayers = session.MinPlayers,
            HostCompanyId = session.HostCompanyId,
            ModeProfile = new ModeProfileDto
            {
                UiComplexityLevel = session.ModeProfile.UiComplexityLevel.ToString(),
                AiAssistanceLevel = session.ModeProfile.AiAssistanceLevel.ToString(),
                EnableHedging = session.ModeProfile.EnableHedging,
                EnableAdvancedFinance = session.ModeProfile.EnableAdvancedFinance,
                StartingCash = session.ModeProfile.StartingCash,
                MaxDebt = session.ModeProfile.MaxDebt
            },
            Companies = session.Companies.Select(c => new CompanyDto
            {
                Id = c.Id,
                Name = c.Name,
                ColorHex = c.ColorHex,
                Cash = c.Finance.Cash,
                Debt = c.Finance.Debt,
                CompanyValue = c.CompanyValue,
                Rank = c.Rank,
                ProductionBoePerDay = c.TotalProductionBoePerDay,
                TurnCommitted = c.TurnCommitted
            }).ToList(),
            LobbyPlayers = session.Companies.SelectMany(c => c.Players.Select(p => new LobbyPlayerDto
            {
                CompanyId = c.Id,
                PlayerId = p.PlayerId,
                CompanyName = c.Name,
                DisplayName = p.DisplayName,
                ColorHex = c.ColorHex,
                IsHost = p.IsHost,
                IsReady = p.IsReady,
                TurnCommitted = c.TurnCommitted
            })).ToList(),
            Discoveries = aggregate.Discoveries.Select(d => new DiscoveryDto
            {
                Id = d.Id,
                BlockId = d.BlockId,
                CompanyId = d.CompanyId,
                Name = d.Name,
                SizeClass = d.SizeClass.ToString(),
                EstimatedMidVolumeMmboe = d.EstimatedMidVolumeMmboe,
                Stage = d.Stage.ToString(),
                Confidence = d.Confidence
            }).ToList(),
            ProducingFields = aggregate.ProducingFields.Select(f => new ProducingFieldDto
            {
                Id = f.Id,
                BlockId = f.BlockId,
                CompanyId = f.CompanyId,
                Name = f.Name,
                Stage = f.Stage.ToString(),
                CurrentProductionBoePerDay = f.CurrentProductionBoePerDay,
                RemainingRecoverableMmboe = f.RemainingRecoverableMmboe,
                ProductionPhase = f.ProductionPhase.ToString()
            }).ToList(),
            PendingActions = aggregate.PendingActions
                .Where(a => a.TurnNumber == session.CurrentTurnNumber)
                .Select(a => new PendingActionDto
                {
                    Id = a.Id,
                    CompanyId = a.CompanyId,
                    ActionType = a.ActionType.ToString(),
                    TargetBlockId = a.TargetBlockId,
                    TargetAssetId = a.TargetAssetId,
                    EstimatedCost = a.EstimatedCost,
                    Status = a.Status.ToString()
                }).ToList(),
            ChatMessages = aggregate.ChatMessages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                CompanyId = m.CompanyId,
                SenderName = m.SenderName,
                Channel = m.Channel,
                Text = m.Text,
                SentAtUtc = m.SentAtUtc
            }).ToList()
        };
    }
}

[ApiController]
[Route("api/scenarios")]
public sealed class ScenariosController : ControllerBase
{
    private readonly IContentLoader _content;

    public ScenariosController(IContentLoader content) => _content = content;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _content.GetScenariosAsync(ct));
}

[ApiController]
[Route("api/game-modes")]
public sealed class GameModesController : ControllerBase
{
    private readonly IContentLoader _content;

    public GameModesController(IContentLoader content) => _content = content;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _content.GetGameplayModesAsync(ct));
}

public sealed class GameSessionResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public string GameplayMode { get; set; } = "";
    public string State { get; set; } = "";
    public int CurrentTurnNumber { get; set; }
    public int TotalTurns { get; set; }
    public decimal OilPrice { get; set; }
    public int ActionSlotsPerTurn { get; set; }
    public bool IsMultiplayer { get; set; }
    public string JoinCode { get; set; } = "";
    public int MaxPlayers { get; set; }
    public int MinPlayers { get; set; }
    public Guid? HostCompanyId { get; set; }
    public ModeProfileDto ModeProfile { get; set; } = new();
    public List<CompanyDto> Companies { get; set; } = [];
    public List<LobbyPlayerDto> LobbyPlayers { get; set; } = [];
    public List<DiscoveryDto> Discoveries { get; set; } = [];
    public List<ProducingFieldDto> ProducingFields { get; set; } = [];
    public List<PendingActionDto> PendingActions { get; set; } = [];
    public List<ChatMessageDto> ChatMessages { get; set; } = [];
}

public sealed class LobbyPlayerDto
{
    public Guid CompanyId { get; set; }
    public Guid PlayerId { get; set; }
    public string CompanyName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ColorHex { get; set; } = "";
    public bool IsHost { get; set; }
    public bool IsReady { get; set; }
    public bool TurnCommitted { get; set; }
}

public sealed class JoinSessionResponse
{
    public Guid SessionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PlayerId { get; set; }
    public GameSessionResponse Session { get; set; } = new();
}

public sealed class SetReadyRequest
{
    public bool IsReady { get; set; }
}

public sealed class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public string SenderName { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime SentAtUtc { get; set; }
}

public sealed class ModeProfileDto
{
    public string UiComplexityLevel { get; set; } = "";
    public string AiAssistanceLevel { get; set; } = "";
    public bool EnableHedging { get; set; }
    public bool EnableAdvancedFinance { get; set; }
    public decimal StartingCash { get; set; }
    public decimal MaxDebt { get; set; }
}

public sealed class CompanyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "";
    public decimal Cash { get; set; }
    public decimal Debt { get; set; }
    public decimal CompanyValue { get; set; }
    public int Rank { get; set; }
    public double ProductionBoePerDay { get; set; }
    public bool TurnCommitted { get; set; }
}

public sealed class DiscoveryDto
{
    public Guid Id { get; set; }
    public Guid BlockId { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = "";
    public string SizeClass { get; set; } = "";
    public double EstimatedMidVolumeMmboe { get; set; }
    public string Stage { get; set; } = "";
    public double Confidence { get; set; }
}

public sealed class ProducingFieldDto
{
    public Guid Id { get; set; }
    public Guid BlockId { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = "";
    public string Stage { get; set; } = "";
    public double CurrentProductionBoePerDay { get; set; }
    public double RemainingRecoverableMmboe { get; set; }
    public string ProductionPhase { get; set; } = "";
}

public sealed class LeaderboardEntryDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = "";
    public int Rank { get; set; }
    public decimal CompanyValue { get; set; }
    public decimal Cash { get; set; }
    public decimal Debt { get; set; }
    public double ProductionBoePerDay { get; set; }
}

public sealed class FinalScoreResponse
{
    public List<FinalScoreEntryDto> Scores { get; set; } = [];
}

public sealed class FinalScoreEntryDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = "";
    public decimal FinalScore { get; set; }
}

public sealed class TurnActionResponse
{
    public Guid Id { get; set; }
    public TurnActionType ActionType { get; set; }
    public Guid? TargetBlockId { get; set; }
    public Guid? TargetAssetId { get; set; }
    public decimal EstimatedCost { get; set; }
    public TurnActionStatus Status { get; set; }
}

public sealed class PendingActionDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string ActionType { get; set; } = "";
    public Guid? TargetBlockId { get; set; }
    public Guid? TargetAssetId { get; set; }
    public decimal EstimatedCost { get; set; }
    public string Status { get; set; } = "";
}

public sealed class TurnResultResponse
{
    public int TurnNumber { get; set; }
    public List<TurnEventDto> Events { get; set; } = [];
    public List<CompanyTurnSummaryDto> CompanySummaries { get; set; } = [];
}

public sealed class TurnEventDto
{
    public string Category { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Detail { get; set; } = "";
    public bool IsPublic { get; set; }
}

public sealed class CompanyTurnSummaryDto
{
    public Guid CompanyId { get; set; }
    public decimal EndingCash { get; set; }
    public decimal Capex { get; set; }
    public decimal CompanyValue { get; set; }
    public int Rank { get; set; }
}

public sealed class MapResponse
{
    public List<BlockMapDto> Blocks { get; set; } = [];
}

public sealed class BlockMapDto
{
    public Guid Id { get; set; }
    public string BlockCode { get; set; } = "";
    public string Name { get; set; } = "";
    public int GridX { get; set; }
    public int GridY { get; set; }
    public Guid? OwnerCompanyId { get; set; }
    public string Stage { get; set; } = "";
    public string PublicGeologyHint { get; set; } = "";
    public string PublicRiskRating { get; set; } = "";
    public double? EstimatedChanceOfSuccess { get; set; }
}

public sealed class SessionHistoryResponse
{
    public List<HistoryPointDto> OilPrice { get; set; } = [];
    public List<HistoryPointDto> ProductionBoePerDay { get; set; } = [];
    public List<HistoryPointDto> Cash { get; set; } = [];
    public List<HistoryPointDto> CompanyValue { get; set; } = [];
}

public sealed class HistoryPointDto
{
    public int TurnNumber { get; set; }
    public decimal Value { get; set; }
}
