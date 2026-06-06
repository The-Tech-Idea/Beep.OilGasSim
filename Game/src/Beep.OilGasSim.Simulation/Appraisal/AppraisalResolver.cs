using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Exploration;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;
using Beep.OilGasSim.Simulation.Randomness;

namespace Beep.OilGasSim.Simulation.Appraisal;

public interface IAppraisalResolver
{
    void ResolveActions(TurnResolutionContext context, IGameRandom random);
}

public sealed class AppraisalResolver : IAppraisalResolver
{
    public void ResolveActions(TurnResolutionContext context, IGameRandom random)
    {
        var cost = context.BalanceProfile.Costs.AppraisalWell * (decimal)context.GameplayModeProfile.CostModifier;

        foreach (var action in context.Actions.Where(a => a.ActionType == TurnActionType.DrillAppraisalWell))
        {
            var company = context.Aggregate.Session.Companies.First(c => c.Id == action.CompanyId);
            var discovery = FindDiscovery(context, action);
            if (discovery is null)
            {
                continue;
            }

            company.Finance.Cash -= cost;
            company.Finance.CapexThisTurn += cost;

            var adjustment = (random.NextDouble() - 0.45) * 0.2;
            var mid = discovery.EstimatedMidVolumeMmboe * (1 + adjustment);
            var spread = Math.Max(10, mid * (0.35 - discovery.Confidence / 300.0));

            discovery.EstimatedMidVolumeMmboe = mid;
            discovery.EstimatedLowVolumeMmboe = Math.Max(0, mid - spread);
            discovery.EstimatedHighVolumeMmboe = mid + spread;
            discovery.Confidence = Math.Min(95, discovery.Confidence + 20 + random.NextInt(0, 11));
            discovery.Stage = AssetStage.Appraisal;
            discovery.CommercialityScore = mid * 4.0;

            context.Result.Events.Add(new TurnEventReport
            {
                CompanyId = company.Id,
                Category = "Appraisal",
                Headline = $"Appraisal completed on {discovery.Name}.",
                Detail = $"Volume estimate: {discovery.EstimatedLowVolumeMmboe:F0}–{discovery.EstimatedHighVolumeMmboe:F0} MMbbl. Confidence: {discovery.Confidence:F0}.",
                IsPublic = false
            });
        }
    }

    private static Discovery? FindDiscovery(TurnResolutionContext context, TurnAction action)
    {
        if (action.TargetAssetId.HasValue)
        {
            return context.Aggregate.Discoveries.FirstOrDefault(d => d.Id == action.TargetAssetId);
        }

        if (action.TargetBlockId.HasValue)
        {
            return context.Aggregate.Discoveries.FirstOrDefault(d =>
                d.BlockId == action.TargetBlockId && d.CompanyId == action.CompanyId);
        }

        return null;
    }
}
