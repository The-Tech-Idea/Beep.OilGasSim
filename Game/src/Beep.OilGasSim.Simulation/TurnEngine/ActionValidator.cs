using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;

namespace Beep.OilGasSim.Simulation.TurnEngine;

public interface IActionValidator
{
    ActionValidationResult Validate(TurnAction action, GameSessionAggregate aggregate);
}

public sealed class ActionValidator : IActionValidator
{
    public ActionValidationResult Validate(TurnAction action, GameSessionAggregate aggregate)
    {
        var result = new ActionValidationResult { IsValid = true, ActionSlotCost = 1 };
        var session = aggregate.Session;
        var company = session.Companies.FirstOrDefault(c => c.Id == action.CompanyId);

        if (company is null)
        {
            return Fail(result, "Company not found in this session.");
        }

        if (session.State is not (GameSessionState.Planning or GameSessionState.Lobby))
        {
            return Fail(result, "Turn is not open for actions.");
        }

        if (!action.TargetBlockId.HasValue && !action.TargetAssetId.HasValue && RequiresTarget(action.ActionType))
        {
            return Fail(result, "This action requires a target block or asset.");
        }

        if (action.TargetBlockId.HasValue)
        {
            var block = session.Basin.Blocks.FirstOrDefault(b => b.Id == action.TargetBlockId);
            if (block is null)
            {
                return Fail(result, "Target block not found.");
            }

            ValidateBlockRules(action, block, company, aggregate, result);
        }

        if (action.TargetAssetId.HasValue)
        {
            ValidateAssetRules(action, company, aggregate, result);
        }

        ValidateFinanceRules(action, company, session, result);

        result.ConfirmedCost = action.EstimatedCost;
        if (company.Finance.Cash < result.ConfirmedCost && action.ActionType != TurnActionType.TakeDebt)
        {
            result.Warnings.Add("Insufficient cash; company may need debt.");
        }

        return result;
    }

    private static void ValidateBlockRules(
        TurnAction action,
        Domain.Blocks.LicenseBlock block,
        Domain.Companies.Company company,
        GameSessionAggregate aggregate,
        ActionValidationResult result)
    {
        switch (action.ActionType)
        {
            case TurnActionType.BidForLicense:
                if (block.OwnerCompanyId.HasValue)
                {
                    Fail(result, "Block is already licensed.");
                }
                result.ConfirmedCost = action.BidAmount;
                break;

            case TurnActionType.GeologicalStudy:
            case TurnActionType.Acquire2DSeismic:
            case TurnActionType.DrillExplorationWell:
                if (block.OwnerCompanyId != company.Id)
                {
                    Fail(result, "Company must own the block.");
                }
                break;

            case TurnActionType.DrillAppraisalWell:
                if (!HasDiscovery(aggregate, company.Id, block.Id, action.TargetAssetId))
                {
                    Fail(result, "No discovery to appraise on this block.");
                }
                break;

            case TurnActionType.ApproveDevelopment:
                if (!HasAppraisableDiscovery(aggregate, company.Id, block.Id, action.TargetAssetId))
                {
                    Fail(result, "No commercial discovery ready for development.");
                }
                break;

            case TurnActionType.OptimizeField:
            case TurnActionType.AbandonField:
                if (!HasProducingField(aggregate, company.Id, block.Id, action.TargetAssetId))
                {
                    Fail(result, "No producing field on this block.");
                }
                break;
        }
    }

    private static void ValidateAssetRules(
        TurnAction action,
        Domain.Companies.Company company,
        GameSessionAggregate aggregate,
        ActionValidationResult result)
    {
        switch (action.ActionType)
        {
            case TurnActionType.DrillAppraisalWell:
                var discovery = aggregate.Discoveries.FirstOrDefault(d =>
                    d.Id == action.TargetAssetId && d.CompanyId == company.Id);
                if (discovery is null)
                {
                    Fail(result, "Discovery not found.");
                }
                break;

            case TurnActionType.ApproveDevelopment:
                var devDiscovery = aggregate.Discoveries.FirstOrDefault(d =>
                    d.Id == action.TargetAssetId && d.CompanyId == company.Id);
                if (devDiscovery is null || devDiscovery.SizeClass == DiscoverySizeClass.NonCommercial)
                {
                    Fail(result, "Discovery is not ready for development.");
                }
                break;

            case TurnActionType.OptimizeField:
            case TurnActionType.AbandonField:
                var field = aggregate.ProducingFields.FirstOrDefault(f =>
                    f.Id == action.TargetAssetId && f.CompanyId == company.Id);
                if (field is null || field.Stage == AssetStage.Abandoned)
                {
                    Fail(result, "Producing field not found.");
                }
                break;
        }
    }

    private static void ValidateFinanceRules(
        TurnAction action,
        Domain.Companies.Company company,
        GameSession session,
        ActionValidationResult result)
    {
        switch (action.ActionType)
        {
            case TurnActionType.TakeDebt:
                var amount = action.BidAmount > 0 ? action.BidAmount : 100_000_000m;
                if (company.Finance.Debt + amount > session.ModeProfile.MaxDebt)
                {
                    Fail(result, "Debt limit exceeded.");
                }
                break;

            case TurnActionType.RepayDebt:
                if (company.Finance.Debt <= 0)
                {
                    Fail(result, "No debt to repay.");
                }
                break;

            case TurnActionType.HedgeProduction:
                if (!session.ModeProfile.EnableHedging)
                {
                    Fail(result, "Hedging is disabled in this mode.");
                }
                break;
        }
    }

    private static bool HasDiscovery(
        GameSessionAggregate aggregate, Guid companyId, Guid blockId, Guid? assetId) =>
        aggregate.Discoveries.Any(d =>
            d.CompanyId == companyId && d.BlockId == blockId
            && (assetId is null || d.Id == assetId));

    private static bool HasAppraisableDiscovery(
        GameSessionAggregate aggregate, Guid companyId, Guid blockId, Guid? assetId) =>
        aggregate.Discoveries.Any(d =>
            d.CompanyId == companyId && d.BlockId == blockId
            && d.SizeClass != DiscoverySizeClass.NonCommercial
            && (assetId is null || d.Id == assetId)
            && !aggregate.DevelopmentProjects.Any(p => p.DiscoveryId == d.Id));

    private static bool HasProducingField(
        GameSessionAggregate aggregate, Guid companyId, Guid blockId, Guid? assetId) =>
        aggregate.ProducingFields.Any(f =>
            f.CompanyId == companyId && f.BlockId == blockId
            && f.Stage is AssetStage.Producing or AssetStage.LateLife
            && (assetId is null || f.Id == assetId));

    private static bool RequiresTarget(TurnActionType type) =>
        type is TurnActionType.BidForLicense
            or TurnActionType.GeologicalStudy
            or TurnActionType.Acquire2DSeismic
            or TurnActionType.DrillExplorationWell
            or TurnActionType.DrillAppraisalWell
            or TurnActionType.ApproveDevelopment
            or TurnActionType.OptimizeField
            or TurnActionType.AbandonField;

    private static ActionValidationResult Fail(ActionValidationResult result, string error)
    {
        result.IsValid = false;
        result.Errors.Add(error);
        return result;
    }
}
