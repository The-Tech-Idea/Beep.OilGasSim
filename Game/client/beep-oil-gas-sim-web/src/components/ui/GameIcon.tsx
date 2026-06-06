import type { CSSProperties } from 'react';

export type GameIconName =
  | 'logo'
  | 'map'
  | 'company'
  | 'assets'
  | 'finance'
  | 'leaderboard'
  | 'oilPrice'
  | 'drill'
  | 'seismic'
  | 'license'
  | 'development'
  | 'production'
  | 'oil'
  | 'study'
  | 'pipeline'
  | 'debt'
  | 'hedge'
  | 'discovery';

const ICON_PATHS: Record<GameIconName, string> = {
  logo: '/assets/icons/logo-drilling-rig.svg',
  map: '/assets/icons/map-basin.svg',
  company: '/assets/icons/company-factory.svg',
  assets: '/assets/icons/assets-barrel.svg',
  finance: '/assets/icons/finance-currency.svg',
  leaderboard: '/assets/icons/leaderboard-meter.svg',
  oilPrice: '/assets/icons/oil-price.svg',
  drill: '/assets/icons/action-drill.svg',
  seismic: '/assets/icons/action-seismic.svg',
  license: '/assets/icons/action-license.svg',
  development: '/assets/icons/action-development.svg',
  production: '/assets/icons/action-production.svg',
  oil: '/assets/icons/action-oil.svg',
  study: '/assets/icons/action-study.svg',
  pipeline: '/assets/icons/action-pipeline.svg',
  debt: '/assets/icons/action-debt.svg',
  hedge: '/assets/icons/action-hedge.svg',
  discovery: '/assets/icons/stage-discovery.svg',
};

const ACTION_ICON_MAP: Record<string, GameIconName> = {
  BidForLicense: 'license',
  GeologicalStudy: 'study',
  Acquire2DSeismic: 'seismic',
  DrillExplorationWell: 'drill',
  DrillAppraisalWell: 'drill',
  ApproveDevelopment: 'development',
  OptimizeField: 'production',
  AbandonField: 'pipeline',
  TakeDebt: 'debt',
  RepayDebt: 'finance',
  HedgeProduction: 'hedge',
};

interface GameIconProps {
  name: GameIconName;
  size?: number;
  className?: string;
  title?: string;
  style?: CSSProperties;
}

export function GameIcon({ name, size = 20, className = '', title, style }: GameIconProps) {
  return (
    <img
      src={ICON_PATHS[name]}
      alt=""
      aria-hidden={title ? undefined : true}
      title={title}
      className={`game-icon ${className}`.trim()}
      style={{ width: size, height: size, ...style }}
      draggable={false}
    />
  );
}

export function getActionIcon(actionType: string): GameIconName {
  return ACTION_ICON_MAP[actionType] ?? 'oil';
}
