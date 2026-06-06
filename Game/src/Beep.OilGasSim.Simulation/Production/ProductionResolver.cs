using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Production;
using Beep.OilGasSim.Domain.Turns;

namespace Beep.OilGasSim.Simulation.Production;

public interface IProductionResolver
{
    void ResolveActions(TurnResolutionContext context);
    void RunProduction(TurnResolutionContext context);
}

public sealed class ProductionResolver : IProductionResolver
{
    public void ResolveActions(TurnResolutionContext context)
    {
        var cost = context.BalanceProfile.Costs.OptimizeField * (decimal)context.GameplayModeProfile.CostModifier;

        foreach (var action in context.Actions.Where(a => a.ActionType == TurnActionType.OptimizeField))
        {
            var field = FindField(context, action);
            if (field is null || field.Stage != AssetStage.Producing)
            {
                continue;
            }

            var company = context.Aggregate.Session.Companies.First(c => c.Id == action.CompanyId);
            company.Finance.Cash -= cost;
            company.Finance.CapexThisTurn += cost;
            field.OptimizationBoostNextTurn = true;

            context.Result.Events.Add(new TurnEventReport
            {
                CompanyId = company.Id,
                Category = "Production",
                Headline = $"Optimization planned for {field.Name}.",
                Detail = "Expected +10% production and improved uptime next turn.",
                IsPublic = false
            });
        }
    }

    public void RunProduction(TurnResolutionContext context)
    {
        var daysPerTurn = context.BalanceProfile.Economy.DaysPerTurn;
        var oilPrice = context.Aggregate.Session.Market.OilPrice;

        foreach (var field in context.Aggregate.ProducingFields.Where(f => f.Stage == AssetStage.Producing))
        {
            var company = context.Aggregate.Session.Companies.First(c => c.Id == field.CompanyId);

            var effectiveRate = field.CurrentProductionBoePerDay * field.RampUpFactor;
            if (field.OptimizationBoostNextTurn)
            {
                effectiveRate *= 1.10;
                field.Uptime = Math.Min(1.0, field.Uptime + 0.03);
                field.OptimizationBoostNextTurn = false;
            }

            var producedBoe = effectiveRate * daysPerTurn * field.Uptime;
            if (producedBoe > field.RemainingRecoverableMmboe * 1_000_000)
            {
                producedBoe = field.RemainingRecoverableMmboe * 1_000_000;
            }

            var producedMmboe = producedBoe / 1_000_000.0;
            field.RemainingRecoverableMmboe -= producedMmboe;

            var revenue = (decimal)producedBoe * oilPrice;
            var opex = field.FixedOpexPerTurn + (decimal)producedBoe * field.VariableOpexPerBoe;
            var royalty = revenue * context.BalanceProfile.Economy.RoyaltyRate;

            company.Finance.RevenueThisTurn += revenue;
            company.Finance.OpexThisTurn += opex + royalty;

            field.ProductionTurnsActive++;
            if (field.ProductionTurnsActive >= 2)
            {
                field.RampUpFactor = 1.0;
                field.ProductionPhase = ProductionPhase.Plateau;
            }

            ApplyDecline(field);

            if (field.IsLateLife)
            {
                field.ProductionPhase = ProductionPhase.LateLife;
                field.Stage = AssetStage.LateLife;
            }

            company.TotalProductionBoePerDay = context.Aggregate.ProducingFields
                .Where(f => f.CompanyId == company.Id && f.Stage is AssetStage.Producing or AssetStage.LateLife)
                .Sum(f => f.CurrentProductionBoePerDay);

            company.TotalReservesMmboe = context.Aggregate.ProducingFields
                .Where(f => f.CompanyId == company.Id)
                .Sum(f => f.RemainingRecoverableMmboe);

            context.Result.Events.Add(new TurnEventReport
            {
                CompanyId = company.Id,
                Category = "Production",
                Headline = $"{field.Name} produced {producedMmboe:F2} MMboe.",
                Detail = $"Revenue ${revenue:N0} at ${oilPrice:F0}/bbl. OPEX+royalty ${opex + royalty:N0}.",
                IsPublic = true
            });
        }
    }

    private static void ApplyDecline(ProducingField field)
    {
        field.CurrentProductionBoePerDay *= 1 - field.DeclineRatePerTurn;
        if (field.CurrentProductionBoePerDay < 0)
        {
            field.CurrentProductionBoePerDay = 0;
        }
    }

    private static ProducingField? FindField(TurnResolutionContext context, TurnAction action)
    {
        if (action.TargetAssetId.HasValue)
        {
            return context.Aggregate.ProducingFields.FirstOrDefault(f => f.Id == action.TargetAssetId);
        }

        return action.TargetBlockId.HasValue
            ? context.Aggregate.ProducingFields.FirstOrDefault(f => f.BlockId == action.TargetBlockId)
            : null;
    }
}
