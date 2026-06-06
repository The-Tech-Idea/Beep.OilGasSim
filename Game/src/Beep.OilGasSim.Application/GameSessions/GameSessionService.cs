using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Domain.Basins;
using Beep.OilGasSim.Domain.Blocks;
using Beep.OilGasSim.Domain.Collaboration;
using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Companies;
using Beep.OilGasSim.Domain.Economy;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Scenarios;
using Beep.OilGasSim.Domain.Turns;
using Beep.OilGasSim.Simulation.TurnEngine;

namespace Beep.OilGasSim.Application.GameSessions;

public sealed class GameSessionService : IGameSessionService
{
    private const int MaxChatMessages = 100;

    private readonly IContentLoader _contentLoader;
    private readonly IGameSessionStore _store;
    private readonly ITurnEngine _turnEngine;
    private readonly IActionValidator _actionValidator;
    private readonly IGameRealtimeNotifier _notifier;

    public GameSessionService(
        IContentLoader contentLoader,
        IGameSessionStore store,
        ITurnEngine turnEngine,
        IActionValidator actionValidator,
        IGameRealtimeNotifier? notifier = null)
    {
        _contentLoader = contentLoader;
        _store = store;
        _turnEngine = turnEngine;
        _actionValidator = actionValidator;
        _notifier = notifier ?? new NullGameRealtimeNotifier();
    }

    public async Task<GameSessionAggregate> CreateSessionAsync(
        CreateGameSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var scenario = await _contentLoader.GetScenarioAsync(request.ScenarioId, cancellationToken)
                       ?? throw new InvalidOperationException($"Scenario '{request.ScenarioId}' not found.");

        var mode = await _contentLoader.GetGameplayModeAsync(request.GameplayModeProfileId, cancellationToken)
                   ?? throw new InvalidOperationException($"Mode '{request.GameplayModeProfileId}' not found.");

        var balance = await _contentLoader.GetBalanceProfileAsync(scenario.BalanceProfileId, cancellationToken);
        var sessionId = Guid.NewGuid();
        var basinId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var isMultiplayer = request.IsMultiplayer;

        var basin = MapBasin(scenario, sessionId, basinId);
        var session = new GameSession
        {
            Id = sessionId,
            Name = isMultiplayer
                ? $"{scenario.Name} — Multiplayer Lobby"
                : $"{scenario.Name} — {request.CompanyName}",
            ScenarioId = scenario.Id,
            GameplayMode = mode.ModeType,
            GameplayModeProfileId = mode.Id,
            State = GameSessionState.Lobby,
            CurrentTurnNumber = 0,
            TotalTurns = mode.TotalTurns,
            GameSeed = Random.Shared.Next(1, int.MaxValue),
            ModeProfile = mode,
            BalanceProfile = balance,
            Basin = basin,
            Market = new MarketState { OilPrice = scenario.StartingOilPrice },
            IsMultiplayer = isMultiplayer,
            JoinCode = isMultiplayer ? JoinCodeGenerator.Create() : "",
            MaxPlayers = isMultiplayer ? 6 : 1,
            MinPlayers = isMultiplayer ? 2 : 1,
            HostCompanyId = companyId
        };

        var company = new Company
        {
            Id = companyId,
            GameSessionId = sessionId,
            Name = request.CompanyName,
            ColorHex = CompanyColorPalette.ForIndex(0),
            Finance = new CompanyFinance
            {
                CompanyId = companyId,
                Cash = mode.StartingCash,
                CreditRating = balance.Economy.StartingCreditRating
            },
            Reputation = new CompanyReputation { Overall = balance.Economy.StartingReputation },
            Players =
            [
                new CompanyPlayer
                {
                    PlayerId = playerId,
                    CompanyId = companyId,
                    DisplayName = request.PlayerDisplayName,
                    IsHost = true,
                    IsReady = !isMultiplayer
                }
            ]
        };

        session.Companies.Add(company);

        var aggregate = new GameSessionAggregate { Session = session };
        await _store.SaveAsync(aggregate, cancellationToken);
        return aggregate;
    }

    public Task<GameSessionAggregate?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _store.GetAsync(sessionId, cancellationToken);

    public Task<GameSessionAggregate?> GetSessionByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default) =>
        _store.GetByJoinCodeAsync(joinCode, cancellationToken);

    public async Task<JoinSessionResult> JoinSessionAsync(
        JoinGameSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        GameSessionAggregate? aggregate;
        if (request.SessionId != Guid.Empty)
        {
            aggregate = await _store.GetAsync(request.SessionId, cancellationToken);
        }
        else
        {
            aggregate = await _store.GetByJoinCodeAsync(request.JoinCode, cancellationToken);
        }

        if (aggregate is null)
        {
            throw new InvalidOperationException("Session not found.");
        }

        var session = aggregate.Session;
        if (!session.IsMultiplayer)
        {
            throw new InvalidOperationException("This session is not a multiplayer lobby.");
        }

        if (session.State != GameSessionState.Lobby)
        {
            throw new InvalidOperationException("Game has already started.");
        }

        if (!string.IsNullOrWhiteSpace(request.JoinCode) &&
            !session.JoinCode.Equals(request.JoinCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid join code.");
        }

        if (session.Companies.Count >= session.MaxPlayers)
        {
            throw new InvalidOperationException("Lobby is full.");
        }

        var companyId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var company = new Company
        {
            Id = companyId,
            GameSessionId = session.Id,
            Name = string.IsNullOrWhiteSpace(request.CompanyName)
                ? $"Company {session.Companies.Count + 1}"
                : request.CompanyName.Trim(),
            ColorHex = CompanyColorPalette.ForIndex(session.Companies.Count),
            Finance = new CompanyFinance
            {
                CompanyId = companyId,
                Cash = session.ModeProfile.StartingCash,
                CreditRating = session.BalanceProfile.Economy.StartingCreditRating
            },
            Reputation = new CompanyReputation { Overall = session.BalanceProfile.Economy.StartingReputation },
            Players =
            [
                new CompanyPlayer
                {
                    PlayerId = playerId,
                    CompanyId = companyId,
                    DisplayName = request.PlayerDisplayName,
                    IsHost = false,
                    IsReady = false
                }
            ]
        };

        session.Companies.Add(company);
        await _store.SaveAsync(aggregate, cancellationToken);
        await _notifier.LobbyUpdatedAsync(session.Id, aggregate);
        return new JoinSessionResult
        {
            SessionId = session.Id,
            CompanyId = companyId,
            PlayerId = playerId
        };
    }

    public async Task<GameSessionAggregate> SetPlayerReadyAsync(
        Guid sessionId,
        Guid companyId,
        Guid playerId,
        bool isReady,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await _store.GetAsync(sessionId, cancellationToken)
                        ?? throw new InvalidOperationException("Session not found.");

        if (aggregate.Session.State != GameSessionState.Lobby)
        {
            throw new InvalidOperationException("Cannot change ready state after the game has started.");
        }

        var company = aggregate.Session.Companies.FirstOrDefault(c => c.Id == companyId)
                      ?? throw new InvalidOperationException("Company not found.");
        var player = company.Players.FirstOrDefault(p => p.PlayerId == playerId)
                     ?? throw new InvalidOperationException("Player not found.");

        player.IsReady = isReady;
        await _store.SaveAsync(aggregate, cancellationToken);
        await _notifier.LobbyUpdatedAsync(sessionId, aggregate);
        return aggregate;
    }

    public async Task<GameSessionAggregate> StartSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var aggregate = await _store.GetAsync(sessionId, cancellationToken)
                        ?? throw new InvalidOperationException("Session not found.");

        var session = aggregate.Session;
        if (session.State != GameSessionState.Lobby)
        {
            throw new InvalidOperationException("Session has already started.");
        }

        if (session.IsMultiplayer)
        {
            if (session.Companies.Count < session.MinPlayers)
            {
                throw new InvalidOperationException($"Need at least {session.MinPlayers} players to start.");
            }

            var allReady = session.Companies
                .SelectMany(c => c.Players)
                .All(p => p.IsReady);
            if (!allReady)
            {
                throw new InvalidOperationException("All players must be ready before starting.");
            }
        }

        session.State = GameSessionState.Planning;
        session.StartedAtUtc = DateTime.UtcNow;
        session.CurrentTurnNumber = 1;
        await _store.SaveAsync(aggregate, cancellationToken);
        await _notifier.GameStartedAsync(sessionId, aggregate);
        return aggregate;
    }

    public async Task<TurnAction> SubmitActionAsync(
        Guid sessionId,
        SubmitActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await _store.GetAsync(sessionId, cancellationToken)
                        ?? throw new InvalidOperationException("Session not found.");

        var session = aggregate.Session;
        var turnNumber = session.CurrentTurnNumber;
        var cost = GetActionCost(request.ActionType, request.BidAmount, session.BalanceProfile, session.ModeProfile);

        var action = new TurnAction
        {
            Id = Guid.NewGuid(),
            GameSessionId = sessionId,
            CompanyId = request.CompanyId,
            TurnNumber = turnNumber,
            ActionType = request.ActionType,
            TargetBlockId = request.TargetBlockId,
            TargetAssetId = request.TargetAssetId,
            BidAmount = request.BidAmount,
            ParametersJson = request.ParametersJson ?? "",
            EstimatedCost = cost,
            Status = TurnActionStatus.Pending
        };

        var validation = _actionValidator.Validate(action, aggregate);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join("; ", validation.Errors));
        }

        var slotCount = aggregate.PendingActions.Count(a =>
            a.CompanyId == request.CompanyId && a.TurnNumber == turnNumber);
        if (slotCount >= session.ModeProfile.ActionSlotsPerTurn)
        {
            throw new InvalidOperationException("Action slots full for this turn.");
        }

        aggregate.PendingActions.Add(action);
        await _store.SaveAsync(aggregate, cancellationToken);
        return action;
    }

    public async Task<TurnResult> CommitTurnAsync(
        Guid sessionId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await _store.GetAsync(sessionId, cancellationToken)
                        ?? throw new InvalidOperationException("Session not found.");

        var company = aggregate.Session.Companies.First(c => c.Id == companyId);
        if (company.TurnCommitted)
        {
            throw new InvalidOperationException("Turn already committed for this company.");
        }

        company.TurnCommitted = true;
        var totalCompanies = aggregate.Session.Companies.Count;
        var committedCount = aggregate.Session.Companies.Count(c => c.TurnCommitted);

        var allCommitted = committedCount == totalCompanies;
        if (!allCommitted)
        {
            await _store.SaveAsync(aggregate, cancellationToken);
            await _notifier.TurnCommittedAsync(sessionId, companyId, committedCount, totalCompanies);
            return new TurnResult { GameSessionId = sessionId, TurnNumber = aggregate.Session.CurrentTurnNumber };
        }

        var result = _turnEngine.ResolveTurn(aggregate, aggregate.Session.CurrentTurnNumber);

        if (aggregate.Session.State == GameSessionState.Planning)
        {
            aggregate.Session.CurrentTurnNumber++;
        }

        await _store.SaveAsync(aggregate, cancellationToken);
        await _notifier.TurnResolvedAsync(sessionId, result);
        return result;
    }

    public async Task<ChatMessage> SendChatAsync(
        SendChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new InvalidOperationException("Message cannot be empty.");
        }

        var aggregate = await _store.GetAsync(request.SessionId, cancellationToken)
                        ?? throw new InvalidOperationException("Session not found.");

        var channel = request.Channel.Equals("company", StringComparison.OrdinalIgnoreCase)
            ? "company"
            : "public";

        if (channel == "company" && !request.CompanyId.HasValue)
        {
            throw new InvalidOperationException("Company channel requires companyId.");
        }

        var message = new ChatMessage
        {
            GameSessionId = request.SessionId,
            CompanyId = channel == "company" ? request.CompanyId : null,
            SenderName = request.SenderName,
            Channel = channel,
            Text = request.Text.Trim()
        };

        aggregate.ChatMessages.Add(message);
        if (aggregate.ChatMessages.Count > MaxChatMessages)
        {
            aggregate.ChatMessages.RemoveRange(0, aggregate.ChatMessages.Count - MaxChatMessages);
        }

        await _store.SaveAsync(aggregate, cancellationToken);
        await _notifier.ChatMessageAsync(request.SessionId, message);
        return message;
    }

    private static decimal GetActionCost(
        TurnActionType type,
        decimal bidAmount,
        Domain.GameplayModes.BalanceProfile balance,
        Domain.GameplayModes.GameplayModeProfile mode)
    {
        var mod = (decimal)mode.CostModifier;
        return type switch
        {
            TurnActionType.BidForLicense => bidAmount,
            TurnActionType.GeologicalStudy => balance.Costs.GeologicalStudy * mod,
            TurnActionType.Acquire2DSeismic => balance.Costs.TwoDSeismic * mod,
            TurnActionType.DrillExplorationWell => balance.Costs.ExplorationWell * mod,
            TurnActionType.DrillAppraisalWell => balance.Costs.AppraisalWell * mod,
            TurnActionType.ApproveDevelopment => balance.Costs.StandardDevelopment * mod,
            TurnActionType.OptimizeField => balance.Costs.OptimizeField * mod,
            _ => 0
        };
    }

    private static Basin MapBasin(ScenarioDefinition scenario, Guid sessionId, Guid basinId)
    {
        var basin = new Basin
        {
            Id = basinId,
            GameSessionId = sessionId,
            Name = scenario.Basin.Name,
            BasinType = scenario.Basin.BasinType,
            GeologicalPotential = scenario.Basin.GeologicalPotential,
            InfrastructureMaturity = scenario.Basin.InfrastructureMaturity,
            ServiceCostIndex = scenario.Basin.ServiceCostIndex
        };

        foreach (var blockDef in scenario.Blocks)
        {
            basin.Blocks.Add(new LicenseBlock
            {
                Id = Guid.NewGuid(),
                GameSessionId = sessionId,
                BasinId = basinId,
                BlockCode = blockDef.BlockId,
                Name = blockDef.Name,
                GridX = blockDef.GridX,
                GridY = blockDef.GridY,
                PublicData = new BlockPublicData
                {
                    PublicGeologyHint = blockDef.PublicData.PublicGeologyHint,
                    InfrastructureAccess = blockDef.PublicData.InfrastructureAccess,
                    SurfaceRisk = blockDef.PublicData.SurfaceRisk,
                    EnvironmentalSensitivity = blockDef.PublicData.EnvironmentalSensitivity,
                    PublicRiskRating = blockDef.PublicData.PublicRiskRating
                },
                HiddenGeology = new HiddenGeology
                {
                    SourceRockQuality = blockDef.HiddenGeology.SourceRockQuality,
                    ReservoirQuality = blockDef.HiddenGeology.ReservoirQuality,
                    TrapIntegrity = blockDef.HiddenGeology.TrapIntegrity,
                    SealQuality = blockDef.HiddenGeology.SealQuality,
                    TimingMigration = blockDef.HiddenGeology.TimingMigration,
                    FluidType = Enum.TryParse<FluidType>(blockDef.HiddenGeology.FluidType, out var ft)
                        ? ft : FluidType.Oil,
                    RecoverableVolumeMmboe = blockDef.HiddenGeology.RecoverableVolumeMmboe,
                    DepthMeters = blockDef.HiddenGeology.DepthMeters,
                    DevelopmentComplexity = blockDef.HiddenGeology.DevelopmentComplexity
                },
                Stage = AssetStage.Unlicensed
            });
        }

        return basin;
    }
}
