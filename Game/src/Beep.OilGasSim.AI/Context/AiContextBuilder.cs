using System.Text.Json;
using Beep.OilGasSim.Domain.GameSessions;

namespace Beep.OilGasSim.AI.Context;

public interface IAiContextBuilder
{
    AiGameContext Build(GameSessionAggregate aggregate, Guid companyId, Guid? selectedBlockId, Guid? selectedAssetId);
}

public sealed class AiContextBuilder : IAiContextBuilder
{
    public AiGameContext Build(
        GameSessionAggregate aggregate,
        Guid companyId,
        Guid? selectedBlockId,
        Guid? selectedAssetId)
    {
        var session = aggregate.Session;
        var company = session.Companies.First(c => c.Id == companyId);
        var pendingCount = aggregate.PendingActions.Count(a =>
            a.CompanyId == companyId && a.TurnNumber == session.CurrentTurnNumber);

        var context = new AiGameContext
        {
            GameSessionId = session.Id,
            CompanyId = companyId,
            CurrentTurn = session.CurrentTurnNumber,
            TotalTurns = session.TotalTurns,
            GameplayMode = session.GameplayMode.ToString(),
            EnableHedging = session.ModeProfile.EnableHedging,
            Company = new AiCompanySnapshot
            {
                CompanyName = company.Name,
                Cash = company.Finance.Cash,
                Debt = company.Finance.Debt,
                CompanyValue = company.CompanyValue,
                ProductionBoePerDay = company.TotalProductionBoePerDay,
                ReservesMmboe = company.TotalReservesMmboe,
                AbandonmentLiability = company.Finance.AbandonmentLiability,
                Reputation = company.Reputation.Overall,
                CreditRating = company.Finance.CreditRating,
                CurrentRank = company.Rank,
                ActionSlotsRemaining = Math.Max(0, session.ModeProfile.ActionSlotsPerTurn - pendingCount)
            },
            Market = new AiMarketSnapshot
            {
                OilPrice = session.Market.OilPrice,
                Trend = session.Market.Trend.ToString(),
                Summary = session.Market.MarketSummary
            }
        };

        foreach (var block in session.Basin.Blocks.Where(b => b.OwnerCompanyId == companyId))
        {
            var knowledge = aggregate.CompanyBlockKnowledge.GetValueOrDefault(companyId)?
                .FirstOrDefault(k => k.BlockId == block.Id);

            context.Assets.Add(new AiAssetSummary
            {
                AssetId = block.Id,
                BlockId = block.Id,
                Name = block.Name,
                AssetType = "LicenseBlock",
                Stage = block.Stage.ToString(),
                KnownSummary = block.PublicData.PublicGeologyHint,
                MainRisk = knowledge?.MainRisk ?? block.PublicData.PublicRiskRating.ToString(),
                EstimatedChanceOfSuccess = knowledge?.EstimatedChanceOfSuccess,
                Confidence = knowledge?.Confidence ?? 0,
                EstimatedCostToNextStep = EstimateNextStepCost(block.Stage.ToString(), session.BalanceProfile, session.ModeProfile)
            });
        }

        foreach (var discovery in aggregate.Discoveries.Where(d => d.CompanyId == companyId))
        {
            context.Assets.Add(new AiAssetSummary
            {
                AssetId = discovery.Id,
                BlockId = discovery.BlockId,
                Name = discovery.Name,
                AssetType = "Discovery",
                Stage = discovery.Stage.ToString(),
                KnownSummary = $"{discovery.SizeClass} discovery, {discovery.EstimatedMidVolumeMmboe:F0} MMbbl mid estimate",
                MainRisk = discovery.MainRisk,
                EstimatedVolumeMmboe = discovery.EstimatedMidVolumeMmboe,
                Confidence = discovery.Confidence,
                EstimatedCostToNextStep = session.BalanceProfile.Costs.AppraisalWell * (decimal)session.ModeProfile.CostModifier
            });
        }

        foreach (var field in aggregate.ProducingFields.Where(f => f.CompanyId == companyId))
        {
            context.Assets.Add(new AiAssetSummary
            {
                AssetId = field.Id,
                BlockId = field.BlockId,
                Name = field.Name,
                AssetType = "ProducingField",
                Stage = field.Stage.ToString(),
                KnownSummary = $"{field.CurrentProductionBoePerDay:N0} boe/d, {field.RemainingRecoverableMmboe:F1} MMboe remaining",
                MainRisk = field.IsLateLife ? "Late-life decline" : "Production decline",
                Confidence = 90,
                EstimatedCostToNextStep = session.BalanceProfile.Costs.OptimizeField * (decimal)session.ModeProfile.CostModifier
            });
        }

        foreach (var result in aggregate.TurnResults.OrderByDescending(r => r.TurnNumber).Take(3))
        {
            foreach (var evt in result.Events.Where(e => e.CompanyId == companyId || e.IsPublic))
            {
                context.RecentEvents.Add(new AiRecentEvent
                {
                    TurnNumber = result.TurnNumber,
                    Category = evt.Category,
                    Headline = evt.Headline,
                    Detail = evt.Detail
                });
            }
        }

        if (selectedBlockId.HasValue)
        {
            var block = session.Basin.Blocks.FirstOrDefault(b => b.Id == selectedBlockId);
            if (block is not null)
            {
                var knowledge = aggregate.CompanyBlockKnowledge.GetValueOrDefault(companyId)?
                    .FirstOrDefault(k => k.BlockId == block.Id);

                context.Selected = new AiSelectedContext
                {
                    BlockId = block.Id,
                    BlockCode = block.BlockCode,
                    Stage = block.Stage.ToString(),
                    PublicGeologyHint = block.PublicData.PublicGeologyHint,
                    PublicRiskRating = block.PublicData.PublicRiskRating.ToString(),
                    EstimatedChanceOfSuccess = knowledge?.EstimatedChanceOfSuccess
                };
            }
        }
        else if (selectedAssetId.HasValue)
        {
            var discovery = aggregate.Discoveries.FirstOrDefault(d => d.Id == selectedAssetId && d.CompanyId == companyId);
            var field = aggregate.ProducingFields.FirstOrDefault(f => f.Id == selectedAssetId && f.CompanyId == companyId);
            var blockId = discovery?.BlockId ?? field?.BlockId;
            if (blockId.HasValue)
            {
                var block = session.Basin.Blocks.First(b => b.Id == blockId);
                context.Selected = new AiSelectedContext
                {
                    BlockId = block.Id,
                    BlockCode = block.BlockCode,
                    Stage = discovery?.Stage.ToString() ?? field?.Stage.ToString() ?? block.Stage.ToString(),
                    PublicGeologyHint = block.PublicData.PublicGeologyHint,
                    PublicRiskRating = block.PublicData.PublicRiskRating.ToString()
                };
            }
        }

        AiVisibilityFilter.Validate(context);
        return context;
    }

    private static decimal? EstimateNextStepCost(
        string stage,
        Domain.GameplayModes.BalanceProfile balance,
        Domain.GameplayModes.GameplayModeProfile mode)
    {
        var mod = (decimal)mode.CostModifier;
        return stage switch
        {
            "Unlicensed" => 20_000_000m,
            "Licensed" => balance.Costs.GeologicalStudy * mod,
            "Studied" => balance.Costs.TwoDSeismic * mod,
            "SeismicEvaluated" => balance.Costs.ExplorationWell * mod,
            "Discovery" or "Appraisal" => balance.Costs.AppraisalWell * mod,
            "CommercialDiscovery" or "DevelopmentPlanning" => balance.Costs.StandardDevelopment * mod,
            _ => null
        };
    }
}

public static class AiVisibilityFilter
{
    private static readonly string[] ForbiddenTokens =
    [
        "HiddenGeology",
        "SourceRockQuality",
        "ReservoirQuality",
        "TrapIntegrity",
        "SealQuality",
        "TimingMigration",
        "RecoverableVolumeMmboe",
        "GameSeed",
        "HiddenGeologyDefinition"
    ];

    public static void Validate(AiGameContext context)
    {
        var json = JsonSerializer.Serialize(context);
        foreach (var token in ForbiddenTokens)
        {
            if (json.Contains(token, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"AI context leaked forbidden field: {token}");
            }
        }
    }

    public static IReadOnlyList<string> GetForbiddenTokens() => ForbiddenTokens;
}
