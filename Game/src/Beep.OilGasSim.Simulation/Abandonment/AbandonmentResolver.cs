using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;

namespace Beep.OilGasSim.Simulation.Abandonment;

public interface IAbandonmentResolver
{
    void ResolveActions(TurnResolutionContext context);
}

public sealed class AbandonmentResolver : IAbandonmentResolver
{
    public void ResolveActions(TurnResolutionContext context)
    {
        foreach (var action in context.Actions.Where(a => a.ActionType == TurnActionType.AbandonField))
        {
            var field = FindField(context, action);
            if (field is null)
            {
                continue;
            }

            var company = context.Aggregate.Session.Companies.First(c => c.Id == action.CompanyId);
            var cost = field.AbandonmentLiability;

            if (company.Finance.Cash < cost)
            {
                context.Result.Events.Add(new TurnEventReport
                {
                    CompanyId = company.Id,
                    Category = "Abandonment",
                    Headline = $"Cannot abandon {field.Name} — insufficient cash.",
                    Detail = $"Required: ${cost:N0}.",
                    IsPublic = false
                });
                continue;
            }

            company.Finance.Cash -= cost;
            company.Finance.CapexThisTurn += cost;
            company.Finance.AbandonmentLiability -= cost;
            company.Reputation.Overall = Math.Min(100, company.Reputation.Overall + 2);

            field.Stage = AssetStage.Abandoned;
            var block = context.Aggregate.Session.Basin.Blocks.First(b => b.Id == field.BlockId);
            block.Stage = AssetStage.Abandoned;

            context.Result.Events.Add(new TurnEventReport
            {
                CompanyId = company.Id,
                Category = "Abandonment",
                Headline = $"{field.Name} abandoned responsibly.",
                Detail = $"Abandonment cost ${cost:N0}. Liability cleared.",
                IsPublic = true
            });
        }
    }

    private static Domain.Production.ProducingField? FindField(TurnResolutionContext context, TurnAction action)
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
