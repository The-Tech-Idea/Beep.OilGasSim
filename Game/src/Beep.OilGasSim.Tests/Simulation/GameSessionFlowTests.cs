using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Application.GameSessions;
using Beep.OilGasSim.Domain.Blocks;
using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Exploration;
using Beep.OilGasSim.Infrastructure.Content;
using Beep.OilGasSim.Infrastructure.Persistence;
using Beep.OilGasSim.Simulation.Abandonment;
using Beep.OilGasSim.Simulation.Appraisal;
using Beep.OilGasSim.Simulation.Auction;
using Beep.OilGasSim.Simulation.Development;
using Beep.OilGasSim.Simulation.Economy;
using Beep.OilGasSim.Simulation.Exploration;
using Beep.OilGasSim.Simulation.Market;
using Beep.OilGasSim.Simulation.Production;
using Beep.OilGasSim.Simulation.Randomness;
using Beep.OilGasSim.Simulation.Scoring;
using Beep.OilGasSim.Simulation.TurnEngine;

namespace Beep.OilGasSim.Tests.Simulation;

public class HiddenGeologyTests
{
    [Fact]
    public void CalculateTrueChanceOfSuccess_IsWithinMvpBounds()
    {
        var geology = new HiddenGeology
        {
            SourceRockQuality = 0.85,
            ReservoirQuality = 0.70,
            TrapIntegrity = 0.60,
            SealQuality = 0.80,
            TimingMigration = 0.75,
            DepthMeters = 3000
        };

        var chance = geology.CalculateTrueChanceOfSuccess();

        Assert.InRange(chance, 0.05, 0.60);
    }
}

public class GameSessionFlowTests
{
    private static GameSessionService CreateService(InMemoryGameSessionStore? store = null) =>
        TestServiceFactory.CreateService(store);

    [Fact]
    public async Task CreateStartBidStudyDrill_ResolvesTurn()
    {
        var service = CreateService();

        var aggregate = await service.CreateSessionAsync(new CreateGameSessionRequest
        {
            ScenarioId = "desert-frontier",
            GameplayModeProfileId = "balanced",
            CompanyName = "Test Energy"
        });

        await service.StartSessionAsync(aggregate.Session.Id);
        var companyId = aggregate.Session.Companies[0].Id;
        var block = aggregate.Session.Basin.Blocks[0];

        await service.SubmitActionAsync(aggregate.Session.Id, new SubmitActionRequest
        {
            CompanyId = companyId,
            ActionType = TurnActionType.BidForLicense,
            TargetBlockId = block.Id,
            BidAmount = 20_000_000m
        });

        var result = await service.CommitTurnAsync(aggregate.Session.Id, companyId);

        Assert.Equal(1, result.TurnNumber);
        Assert.Contains(result.Events, e => e.Category == "License");

        var updated = await service.GetSessionAsync(aggregate.Session.Id);
        Assert.NotNull(updated);
        Assert.Equal(companyId, updated!.Session.Basin.Blocks.First(b => b.Id == block.Id).OwnerCompanyId);
    }

    [Fact]
    public async Task DiscoveryToProduction_LifecycleAdvancesThroughResolvers()
    {
        var service = CreateService();
        var aggregate = await service.CreateSessionAsync(new CreateGameSessionRequest
        {
            ScenarioId = "desert-frontier",
            GameplayModeProfileId = "balanced",
            CompanyName = "Lifecycle Energy"
        });

        await service.StartSessionAsync(aggregate.Session.Id);
        var sessionId = aggregate.Session.Id;
        var companyId = aggregate.Session.Companies[0].Id;
        var block = aggregate.Session.Basin.Blocks.First(b =>
            b.HiddenGeology.RecoverableVolumeMmboe >= 80);

        block.OwnerCompanyId = companyId;
        block.Stage = AssetStage.Licensed;

        var discovery = new Discovery
        {
            Id = Guid.NewGuid(),
            BlockId = block.Id,
            CompanyId = companyId,
            Name = "North Ridge Discovery",
            SizeClass = DiscoverySizeClass.Commercial,
            EstimatedMidVolumeMmboe = block.HiddenGeology.RecoverableVolumeMmboe,
            EstimatedLowVolumeMmboe = block.HiddenGeology.RecoverableVolumeMmboe * 0.7,
            EstimatedHighVolumeMmboe = block.HiddenGeology.RecoverableVolumeMmboe * 1.3,
            Confidence = 55,
            Stage = AssetStage.Discovery
        };

        aggregate.Discoveries.Add(discovery);
        var store = new InMemoryGameSessionStore();
        await store.SaveAsync(aggregate);

        service = CreateService(store);

        await service.SubmitActionAsync(sessionId, new SubmitActionRequest
        {
            CompanyId = companyId,
            ActionType = TurnActionType.DrillAppraisalWell,
            TargetAssetId = discovery.Id
        });
        await service.CommitTurnAsync(sessionId, companyId);

        var afterAppraisal = await service.GetSessionAsync(sessionId);
        Assert.NotNull(afterAppraisal);
        Assert.True(afterAppraisal!.Discoveries[0].Confidence >= 55);

        await service.SubmitActionAsync(sessionId, new SubmitActionRequest
        {
            CompanyId = companyId,
            ActionType = TurnActionType.ApproveDevelopment,
            TargetAssetId = discovery.Id,
            ParametersJson = "Standard"
        });
        await service.CommitTurnAsync(sessionId, companyId);

        var afterDev = await service.GetSessionAsync(sessionId);
        Assert.NotNull(afterDev);
        Assert.Single(afterDev!.DevelopmentProjects);

        for (var turn = 0; turn < 4; turn++)
        {
            await service.CommitTurnAsync(sessionId, companyId);
        }

        var afterConstruction = await service.GetSessionAsync(sessionId);
        Assert.NotNull(afterConstruction);
        Assert.NotEmpty(afterConstruction!.ProducingFields);
        Assert.Contains(afterConstruction.TurnResults.SelectMany(r => r.Events), e => e.Category == "Production");
    }
}

internal static class TestServiceFactory
{
    public static GameSessionService CreateService(InMemoryGameSessionStore? store = null, bool includeFunMode = false)
    {
        store ??= new InMemoryGameSessionStore();
        var turnEngine = new Beep.OilGasSim.Simulation.TurnEngine.TurnEngine(
            new ActionValidator(),
            new AuctionResolver(),
            new ExplorationResolver(),
            new AppraisalResolver(),
            new DevelopmentResolver(),
            new ProductionResolver(),
            new MarketResolver(),
            new EconomyResolver(),
            new AbandonmentResolver(),
            new ScoringService(),
            new GameRandomFactory());

        return new GameSessionService(new FakeContentLoader(includeFunMode), store, turnEngine, new ActionValidator(), new NullGameRealtimeNotifier());
    }
}

internal sealed class FakeContentLoader : IContentLoader
{
    private readonly bool _includeFunMode;

    public FakeContentLoader(bool includeFunMode = false) => _includeFunMode = includeFunMode;

    public Task<IReadOnlyList<Domain.Scenarios.ScenarioDefinition>> GetScenariosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Domain.Scenarios.ScenarioDefinition>>([DesertFrontierGenerator.Create()]);

    public Task<Domain.Scenarios.ScenarioDefinition?> GetScenarioAsync(string scenarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Domain.Scenarios.ScenarioDefinition?>(DesertFrontierGenerator.Create());

    public Task<IReadOnlyList<Domain.GameplayModes.GameplayModeProfile>> GetGameplayModesAsync(CancellationToken cancellationToken = default)
    {
        var modes = new List<Domain.GameplayModes.GameplayModeProfile> { CreateBalancedMode() };
        if (_includeFunMode)
        {
            modes.Insert(0, CreateFunMode());
        }
        return Task.FromResult<IReadOnlyList<Domain.GameplayModes.GameplayModeProfile>>(modes);
    }

    public Task<Domain.GameplayModes.GameplayModeProfile?> GetGameplayModeAsync(string profileId, CancellationToken cancellationToken = default)
    {
        if (profileId.Equals("fun", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<Domain.GameplayModes.GameplayModeProfile?>(CreateFunMode());
        }
        return Task.FromResult<Domain.GameplayModes.GameplayModeProfile?>(CreateBalancedMode());
    }

    public Task<Domain.GameplayModes.BalanceProfile> GetBalanceProfileAsync(string profileId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Domain.GameplayModes.BalanceProfile());

    private static Domain.GameplayModes.GameplayModeProfile CreateFunMode() => new()
    {
        Id = "fun",
        ModeType = GameplayModeType.Fun,
        Name = "Fun Mode",
        TotalTurns = 12,
        ActionSlotsPerTurn = 2,
        StartingCash = 700_000_000m,
        MaxDebt = 300_000_000m,
        ExplorationChanceModifier = 1.35,
        CostModifier = 0.85,
        DevelopmentTimeModifier = 0.6,
        AbandonmentPenaltyModifier = 0.5,
        EnableHedging = false,
        UiComplexityLevel = UiComplexityLevel.Simple,
        AiAssistanceLevel = AiAssistanceLevel.Guided
    };

    private static Domain.GameplayModes.GameplayModeProfile CreateBalancedMode() => new()
    {
        Id = "balanced",
        ModeType = GameplayModeType.Balanced,
        Name = "Balanced Mode",
        TotalTurns = 20,
        ActionSlotsPerTurn = 3,
        StartingCash = 500_000_000m,
        MaxDebt = 500_000_000m,
        EnableHedging = true,
        UiComplexityLevel = UiComplexityLevel.Standard,
        AiAssistanceLevel = AiAssistanceLevel.FullAdvisor
    };
}
