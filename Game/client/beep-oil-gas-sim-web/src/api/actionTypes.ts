import type { SubmitActionRequest } from './types';

/** Matches Beep.OilGasSim.Domain.Common.TurnActionType */
export const TurnActionType = {
  BidForLicense: 'BidForLicense',
  RelinquishLicense: 'RelinquishLicense',
  GeologicalStudy: 'GeologicalStudy',
  Acquire2DSeismic: 'Acquire2DSeismic',
  Acquire3DSeismic: 'Acquire3DSeismic',
  DrillExplorationWell: 'DrillExplorationWell',
  DrillAppraisalWell: 'DrillAppraisalWell',
  ApproveDevelopment: 'ApproveDevelopment',
  OptimizeField: 'OptimizeField',
  HedgeProduction: 'HedgeProduction',
  TakeDebt: 'TakeDebt',
  RepayDebt: 'RepayDebt',
  SellAsset: 'SellAsset',
  AbandonField: 'AbandonField',
} as const;

export type TurnActionTypeName = (typeof TurnActionType)[keyof typeof TurnActionType];

export interface ActionRequestOptions {
  targetBlockId?: string;
  targetAssetId?: string;
  bidAmount?: number;
  parametersJson?: string;
}

export function buildSubmitActionRequest(
  companyId: string,
  actionType: TurnActionTypeName,
  options: ActionRequestOptions = {},
): SubmitActionRequest {
  const body: SubmitActionRequest = { companyId, actionType };

  switch (actionType) {
    case TurnActionType.BidForLicense:
      if (!options.targetBlockId) throw new Error('Select a block to license.');
      body.targetBlockId = options.targetBlockId;
      body.bidAmount = options.bidAmount ?? 20_000_000;
      break;

    case TurnActionType.GeologicalStudy:
    case TurnActionType.Acquire2DSeismic:
    case TurnActionType.Acquire3DSeismic:
    case TurnActionType.DrillExplorationWell:
    case TurnActionType.RelinquishLicense:
      if (!options.targetBlockId) throw new Error('Select a block for this action.');
      body.targetBlockId = options.targetBlockId;
      break;

    case TurnActionType.DrillAppraisalWell:
    case TurnActionType.ApproveDevelopment:
    case TurnActionType.SellAsset:
      if (!options.targetAssetId) throw new Error('Select an asset for this action.');
      body.targetAssetId = options.targetAssetId;
      if (options.targetBlockId) body.targetBlockId = options.targetBlockId;
      if (options.parametersJson) body.parametersJson = options.parametersJson;
      break;

    case TurnActionType.OptimizeField:
    case TurnActionType.AbandonField:
      if (!options.targetAssetId) throw new Error('Select a field for this action.');
      body.targetAssetId = options.targetAssetId;
      if (options.targetBlockId) body.targetBlockId = options.targetBlockId;
      break;

    case TurnActionType.TakeDebt:
      body.bidAmount = options.bidAmount ?? 100_000_000;
      break;

    case TurnActionType.RepayDebt:
      body.bidAmount = options.bidAmount ?? 50_000_000;
      break;

    case TurnActionType.HedgeProduction:
      body.bidAmount = options.bidAmount ?? 50;
      break;

    default:
      throw new Error(`Unsupported action: ${actionType}`);
  }

  return body;
}
