using System.Text.Json;
using Beep.OilGasSim.AI.Advisors;
using Beep.OilGasSim.AI.Context;
using Beep.OilGasSim.AI.Services;
using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Domain.Blocks;
using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Companies;
using Beep.OilGasSim.Domain.Economy;
using Beep.OilGasSim.Domain.Exploration;
using Beep.OilGasSim.Domain.GameplayModes;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Infrastructure.Persistence;

namespace Beep.OilGasSim.Tests.AI;

public class AiContextSafetyTests
{
    [Fact]
    public void BuildContext_DoesNotIncludeHiddenGeologyFields()
    {
        var aggregate = CreateAggregateWithHiddenGeology();
        var companyId = aggregate.Session.Companies[0].Id;

        var context = new AiContextBuilder().Build(aggregate, companyId, null, null);
        var json = JsonSerializer.Serialize(context);

        foreach (var token in AiVisibilityFilter.GetForbiddenTokens())
        {
            Assert.DoesNotContain(token, json);
        }
    }

    [Fact]
    public void BuildContext_IncludesPlayerVisibleKnowledge()
    {
        var aggregate = CreateAggregateWithHiddenGeology();
        var companyId = aggregate.Session.Companies[0].Id;
        var blockId = aggregate.Session.Basin.Blocks[0].Id;

        aggregate.CompanyBlockKnowledge[companyId] =
        [
            new BlockKnowledge
            {
                CompanyId = companyId,
                BlockId = blockId,
                EstimatedChanceOfSuccess = 0.34,
                Confidence = 55,
                MainRisk = "Trap closure"
            }
        ];

        var context = new AiContextBuilder().Build(aggregate, companyId, blockId, null);

        Assert.NotNull(context.Selected);
        Assert.Equal(0.34, context.Selected!.EstimatedChanceOfSuccess);
        Assert.Contains(context.Assets, a => a.EstimatedChanceOfSuccess == 0.34);
    }

    [Fact]
    public async Task AskAdvisor_ReturnsGeologistAdviceWithoutHiddenData()
    {
        var store = new InMemoryGameSessionStore();
        var aggregate = CreateAggregateWithHiddenGeology();
        await store.SaveAsync(aggregate);

        var service = new AiAdvisorService(store, new AiContextBuilder(), new RuleBasedAdvisorEngine());
        var blockId = aggregate.Session.Basin.Blocks[0].Id;

        var response = await service.AskAsync(aggregate.Session.Id, new AiAdvisorRequest
        {
            CompanyId = aggregate.Session.Companies[0].Id,
            AdvisorType = AiAdvisorType.Geologist,
            Message = "Should we drill this block?",
            SelectedBlockId = blockId
        });

        Assert.Equal("Geologist", response.AdvisorType);
        Assert.NotEmpty(response.Message);
        Assert.DoesNotContain("SourceRockQuality", response.Message);
        Assert.DoesNotContain("RecoverableVolumeMmboe", response.Message);
    }

    private static GameSessionAggregate CreateAggregateWithHiddenGeology()
    {
        var companyId = Guid.NewGuid();
        var blockId = Guid.NewGuid();

        var block = new LicenseBlock
        {
            Id = blockId,
            BlockCode = "D-01",
            Name = "Block D-01",
            OwnerCompanyId = companyId,
            Stage = AssetStage.Licensed,
            HiddenGeology = new HiddenGeology
            {
                SourceRockQuality = 0.95,
                ReservoirQuality = 0.88,
                RecoverableVolumeMmboe = 150
            },
            PublicData = new BlockPublicData
            {
                PublicGeologyHint = "Moderate source potential.",
                PublicRiskRating = PublicRiskRating.Moderate
            }
        };

        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            CurrentTurnNumber = 3,
            TotalTurns = 20,
            ModeProfile = new GameplayModeProfile { ActionSlotsPerTurn = 3, EnableHedging = true },
            BalanceProfile = new BalanceProfile(),
            Market = new MarketState { OilPrice = 75m },
            Companies =
            [
                new Company
                {
                    Id = companyId,
                    Name = "Test Co",
                    Finance = new CompanyFinance { Cash = 400_000_000m, Debt = 50_000_000m },
                    Reputation = new CompanyReputation { Overall = 55 }
                }
            ]
        };
        session.Basin.Blocks.Add(block);

        return new GameSessionAggregate { Session = session };
    }
}
