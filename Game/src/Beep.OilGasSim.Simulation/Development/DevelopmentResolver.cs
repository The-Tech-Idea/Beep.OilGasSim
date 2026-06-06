using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Development;
using Beep.OilGasSim.Domain.Exploration;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Production;
using Beep.OilGasSim.Domain.Turns;
using Beep.OilGasSim.Simulation.Development;

namespace Beep.OilGasSim.Simulation.Development;

public interface IDevelopmentResolver
{
    void ResolveActions(TurnResolutionContext context);
    void AdvanceConstruction(TurnResolutionContext context);
}

public sealed class DevelopmentResolver : IDevelopmentResolver
{
    public void ResolveActions(TurnResolutionContext context)
    {
        foreach (var action in context.Actions.Where(a => a.ActionType == TurnActionType.ApproveDevelopment))
        {
            var company = context.Aggregate.Session.Companies.First(c => c.Id == action.CompanyId);
            var discovery = FindDiscovery(context, action);
            if (discovery is null || discovery.SizeClass == DiscoverySizeClass.NonCommercial)
            {
                continue;
            }

            var conceptType = ParseConceptType(action.ParametersJson);
            var template = DevelopmentConceptCatalog.Get(
                conceptType, context.BalanceProfile, context.GameplayModeProfile);

            if (company.Finance.Cash < template.Capex)
            {
                context.Result.Events.Add(new TurnEventReport
                {
                    CompanyId = company.Id,
                    Category = "Development",
                    Headline = $"Development of {discovery.Name} failed — insufficient cash.",
                    Detail = $"Required CAPEX: ${template.Capex:N0}.",
                    IsPublic = false
                });
                continue;
            }

            company.Finance.Cash -= template.Capex;
            company.Finance.CapexThisTurn += template.Capex;
            company.Finance.AbandonmentLiability += template.AbandonmentLiability;

            var block = context.Aggregate.Session.Basin.Blocks.First(b => b.Id == discovery.BlockId);
            block.Stage = AssetStage.UnderConstruction;
            discovery.Stage = AssetStage.DevelopmentApproved;

            var project = new DevelopmentProject
            {
                Id = Guid.NewGuid(),
                DiscoveryId = discovery.Id,
                BlockId = discovery.BlockId,
                CompanyId = company.Id,
                FieldName = discovery.Name.Replace("Discovery", "Field").Trim(),
                ConceptType = conceptType,
                ConstructionTurnsRequired = template.ConstructionTurns,
                ConstructionTurnsCompleted = 0,
                CapexCommitted = template.Capex,
                TargetRecoverableMmboe = discovery.EstimatedMidVolumeMmboe,
                Stage = AssetStage.UnderConstruction
            };
            context.Aggregate.DevelopmentProjects.Add(project);

            context.Result.Events.Add(new TurnEventReport
            {
                CompanyId = company.Id,
                Category = "Development",
                Headline = $"{template.Name} approved for {discovery.Name}.",
                Detail = $"CAPEX ${template.Capex:N0}. First oil expected in {template.ConstructionTurns} turns.",
                IsPublic = true
            });
        }
    }

    public void AdvanceConstruction(TurnResolutionContext context)
    {
        var completed = new List<DevelopmentProject>();

        foreach (var project in context.Aggregate.DevelopmentProjects.Where(p => !p.IsComplete))
        {
            project.ConstructionTurnsCompleted++;
            if (project.IsComplete)
            {
                completed.Add(project);
            }
        }

        foreach (var project in completed)
        {
            ActivateField(context, project);
        }
    }

    private static void ActivateField(TurnResolutionContext context, DevelopmentProject project)
    {
        var template = DevelopmentConceptCatalog.Get(
            project.ConceptType, context.BalanceProfile, context.GameplayModeProfile);

        var potentialRate = project.TargetRecoverableMmboe * 200;
        var initialRate = Math.Min(template.FacilityCapacityBoePerDay, potentialRate);

        var field = new ProducingField
        {
            Id = Guid.NewGuid(),
            CompanyId = project.CompanyId,
            BlockId = project.BlockId,
            DiscoveryId = project.DiscoveryId,
            Name = project.FieldName,
            ConceptType = project.ConceptType,
            OriginalRecoverableMmboe = project.TargetRecoverableMmboe,
            RemainingRecoverableMmboe = project.TargetRecoverableMmboe,
            PeakProductionBoePerDay = initialRate,
            CurrentProductionBoePerDay = initialRate,
            FacilityCapacityBoePerDay = template.FacilityCapacityBoePerDay,
            DeclineRatePerTurn = template.NormalDeclineRatePerTurn,
            Uptime = template.BaseUptime,
            RampUpFactor = 0.70,
            FixedOpexPerTurn = template.FixedOpexPerTurn,
            VariableOpexPerBoe = template.VariableOpexPerBoe,
            AbandonmentLiability = template.AbandonmentLiability,
            ProductionPhase = ProductionPhase.RampUp,
            Stage = AssetStage.Producing
        };

        context.Aggregate.ProducingFields.Add(field);

        var block = context.Aggregate.Session.Basin.Blocks.First(b => b.Id == project.BlockId);
        block.Stage = AssetStage.Producing;

        var discovery = context.Aggregate.Discoveries.FirstOrDefault(d => d.Id == project.DiscoveryId);
        if (discovery is not null)
        {
            discovery.Stage = AssetStage.Producing;
        }

        context.Result.Events.Add(new TurnEventReport
        {
            CompanyId = project.CompanyId,
            Category = "Production",
            Headline = $"First oil at {field.Name}!",
            Detail = $"Initial rate: {initialRate:N0} boe/day.",
            IsPublic = true
        });
    }

    private static Discovery? FindDiscovery(TurnResolutionContext context, TurnAction action)
    {
        if (action.TargetAssetId.HasValue)
        {
            return context.Aggregate.Discoveries.FirstOrDefault(d => d.Id == action.TargetAssetId);
        }

        return action.TargetBlockId.HasValue
            ? context.Aggregate.Discoveries.FirstOrDefault(d =>
                d.BlockId == action.TargetBlockId && d.CompanyId == action.CompanyId)
            : null;
    }

    private static DevelopmentConceptType ParseConceptType(string parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return DevelopmentConceptType.Standard;
        }

        if (parametersJson.Contains("Small", StringComparison.OrdinalIgnoreCase))
        {
            return DevelopmentConceptType.Small;
        }

        if (parametersJson.Contains("Large", StringComparison.OrdinalIgnoreCase))
        {
            return DevelopmentConceptType.Large;
        }

        return DevelopmentConceptType.Standard;
    }
}
