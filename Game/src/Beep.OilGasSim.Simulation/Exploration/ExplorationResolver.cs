using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Exploration;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;
using Beep.OilGasSim.Simulation.Randomness;

namespace Beep.OilGasSim.Simulation.Exploration;

public interface IExplorationResolver
{
    void Resolve(TurnResolutionContext context, IGameRandom random);
}

public sealed class ExplorationResolver : IExplorationResolver
{
    public void Resolve(TurnResolutionContext context, IGameRandom random)
    {
        var aggregate = context.Aggregate;
        var mode = context.GameplayModeProfile;
        var costs = context.BalanceProfile.Costs;
        var costMod = (decimal)mode.CostModifier;

        foreach (var action in context.Actions.Where(a => IsExplorationAction(a.ActionType)))
        {
            var company = aggregate.Session.Companies.First(c => c.Id == action.CompanyId);
            var block = aggregate.Session.Basin.Blocks.First(b => b.Id == action.TargetBlockId);

            switch (action.ActionType)
            {
                case TurnActionType.GeologicalStudy:
                    ResolveStudy(context, company, block, costs.GeologicalStudy * costMod);
                    break;
                case TurnActionType.Acquire2DSeismic:
                    ResolveSeismic(context, company, block, costs.TwoDSeismic * costMod);
                    break;
                case TurnActionType.DrillExplorationWell:
                    ResolveExplorationWell(context, company, block, costs.ExplorationWell * costMod, random, mode);
                    break;
            }
        }
    }

    private static bool IsExplorationAction(TurnActionType type) =>
        type is TurnActionType.GeologicalStudy
            or TurnActionType.Acquire2DSeismic
            or TurnActionType.DrillExplorationWell;

    private static BlockKnowledge GetOrCreateKnowledge(TurnResolutionContext context, Guid companyId, Guid blockId)
    {
        if (!context.Aggregate.CompanyBlockKnowledge.TryGetValue(companyId, out var list))
        {
            list = [];
            context.Aggregate.CompanyBlockKnowledge[companyId] = list;
        }

        var knowledge = list.FirstOrDefault(k => k.BlockId == blockId);
        if (knowledge is not null)
        {
            return knowledge;
        }

        knowledge = new BlockKnowledge { CompanyId = companyId, BlockId = blockId };
        list.Add(knowledge);
        return knowledge;
    }

    private static void ResolveStudy(
        TurnResolutionContext context,
        Domain.Companies.Company company,
        Domain.Blocks.LicenseBlock block,
        decimal cost)
    {
        company.Finance.Cash -= cost;
        company.Finance.CapexThisTurn += cost;
        block.Stage = AssetStage.Studied;

        var knowledge = GetOrCreateKnowledge(context, company.Id, block.Id);
        GeologicalChanceCalculator.ApplyStudyKnowledge(
            knowledge, block, KnowledgeLevel.GeologicalStudy, context.GameplayModeProfile.ExplorationChanceModifier);

        context.Result.Events.Add(new TurnEventReport
        {
            CompanyId = company.Id,
            Category = "Exploration",
            Headline = $"Geological study completed on {block.Name}.",
            Detail = knowledge.InterpretationSummary,
            IsPublic = false
        });
    }

    private static void ResolveSeismic(
        TurnResolutionContext context,
        Domain.Companies.Company company,
        Domain.Blocks.LicenseBlock block,
        decimal cost)
    {
        company.Finance.Cash -= cost;
        company.Finance.CapexThisTurn += cost;
        block.Stage = AssetStage.SeismicEvaluated;

        var knowledge = GetOrCreateKnowledge(context, company.Id, block.Id);
        GeologicalChanceCalculator.ApplyStudyKnowledge(
            knowledge, block, KnowledgeLevel.TwoDSeismic, context.GameplayModeProfile.ExplorationChanceModifier);

        var prospect = new Prospect
        {
            Id = Guid.NewGuid(),
            BlockId = block.Id,
            CompanyId = company.Id,
            Name = $"{block.BlockCode} Prospect",
            EstimatedChanceOfSuccess = knowledge.EstimatedChanceOfSuccess,
            Confidence = knowledge.Confidence,
            MainRisk = knowledge.MainRisk,
            IsDrillReady = true
        };
        context.Aggregate.Prospects.Add(prospect);

        context.Result.Events.Add(new TurnEventReport
        {
            CompanyId = company.Id,
            Category = "Exploration",
            Headline = $"2D seismic acquired on {block.Name}.",
            Detail = $"Estimated chance of success: {knowledge.EstimatedChanceOfSuccess:P0}. {knowledge.InterpretationSummary}",
            IsPublic = false
        });
    }

    private static void ResolveExplorationWell(
        TurnResolutionContext context,
        Domain.Companies.Company company,
        Domain.Blocks.LicenseBlock block,
        decimal cost,
        IGameRandom random,
        Domain.GameplayModes.GameplayModeProfile mode)
    {
        company.Finance.Cash -= cost;
        company.Finance.CapexThisTurn += cost;
        block.Stage = AssetStage.ExplorationDrilling;

        var trueChance = block.HiddenGeology.CalculateTrueChanceOfSuccess()
                         * mode.ExplorationChanceModifier;
        trueChance = Math.Clamp(trueChance, 0.05, 0.60);

        var roll = random.NextDouble();
        if (roll <= trueChance && block.HiddenGeology.FluidType != FluidType.Dry)
        {
            CreateDiscovery(context, company, block);
        }
        else
        {
            CreateDryHole(context, company, block);
        }
    }

    private static void CreateDiscovery(
        TurnResolutionContext context,
        Domain.Companies.Company company,
        Domain.Blocks.LicenseBlock block)
    {
        block.Stage = AssetStage.Discovery;
        var volume = block.HiddenGeology.RecoverableVolumeMmboe;
        var sizeClass = volume switch
        {
            < 30 => DiscoverySizeClass.NonCommercial,
            > 150 => DiscoverySizeClass.Major,
            _ => DiscoverySizeClass.Commercial
        };

        var discovery = new Discovery
        {
            Id = Guid.NewGuid(),
            BlockId = block.Id,
            CompanyId = company.Id,
            Name = $"{block.BlockCode} Discovery",
            FluidType = block.HiddenGeology.FluidType,
            EstimatedLowVolumeMmboe = volume * 0.6,
            EstimatedMidVolumeMmboe = volume,
            EstimatedHighVolumeMmboe = volume * 1.4,
            Confidence = 35,
            CommercialityScore = sizeClass == DiscoverySizeClass.NonCommercial ? 0 : volume * 4,
            MainRisk = "Reservoir continuity",
            SizeClass = sizeClass,
            Stage = AssetStage.Discovery
        };
        context.Aggregate.Discoveries.Add(discovery);

        var headline = sizeClass == DiscoverySizeClass.Major
            ? $"Major discovery at {block.Name}!"
            : sizeClass == DiscoverySizeClass.Commercial
                ? $"Commercial discovery at {block.Name}."
                : $"Non-commercial discovery at {block.Name}.";

        context.Result.Events.Add(new TurnEventReport
        {
            CompanyId = company.Id,
            Category = "Exploration",
            Headline = headline,
            Detail = $"Estimated recoverable volume: {discovery.EstimatedLowVolumeMmboe:F0}–{discovery.EstimatedHighVolumeMmboe:F0} MMbbl.",
            IsPublic = sizeClass >= DiscoverySizeClass.Commercial
        });
    }

    private static void CreateDryHole(
        TurnResolutionContext context,
        Domain.Companies.Company company,
        Domain.Blocks.LicenseBlock block)
    {
        block.Stage = AssetStage.DryHole;
        var mainRisk = GeologicalChanceCalculator.IdentifyMainRisk(block.HiddenGeology);

        context.Result.Events.Add(new TurnEventReport
        {
            CompanyId = company.Id,
            Category = "Exploration",
            Headline = $"Dry hole on {block.Name}.",
            Detail = $"The well did not find commercial hydrocarbons. Main risk factor: {mainRisk}.",
            IsPublic = true
        });
    }
}
