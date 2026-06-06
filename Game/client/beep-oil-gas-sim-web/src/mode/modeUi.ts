import type {
  BlockMapDto,
  CompanyDto,
  DiscoveryDto,
  GameSession,
  ProducingFieldDto,
} from '../api/types';

const STANDARD_ACTION_LABELS: Record<string, string> = {
  BidForLicense: 'Bid for License',
  GeologicalStudy: 'Geological Study',
  Acquire2DSeismic: 'Acquire 2D Seismic',
  DrillExplorationWell: 'Drill Exploration Well',
  DrillAppraisalWell: 'Drill Appraisal Well',
  ApproveDevelopment: 'Approve Development',
  OptimizeField: 'Optimize Field',
  AbandonField: 'Abandon Field',
  TakeDebt: 'Take Debt',
  RepayDebt: 'Repay Debt',
  HedgeProduction: 'Hedge Production',
};

const FUN_ACTION_LABELS: Record<string, string> = {
  BidForLicense: 'Buy License',
  GeologicalStudy: 'Study Block',
  Acquire2DSeismic: 'Scan Area',
  DrillExplorationWell: 'Drill Well',
  DrillAppraisalWell: 'Check Discovery',
  ApproveDevelopment: 'Build Field',
  OptimizeField: 'Boost Production',
  AbandonField: 'Close Field',
  TakeDebt: 'Borrow Money',
  RepayDebt: 'Pay Back Loan',
  HedgeProduction: 'Protect Revenue',
};

export function isFunMode(session: GameSession): boolean {
  return session.gameplayMode === 'Fun';
}

export function isSimpleUi(session: GameSession): boolean {
  return session.modeProfile?.uiComplexityLevel === 'Simple';
}

export function getActionLabel(actionType: string, session: GameSession): string {
  const labels = isSimpleUi(session) ? FUN_ACTION_LABELS : STANDARD_ACTION_LABELS;
  return labels[actionType] ?? actionType.replace(/([A-Z])/g, ' $1').trim();
}

export function simplifyRisk(rating: string): 'Low' | 'Medium' | 'High' {
  const r = rating.toLowerCase();
  if (r.includes('low') || r.includes('unknown')) return 'Low';
  if (r.includes('very') || r.includes('high')) return 'High';
  return 'Medium';
}

export function simplifyPotential(chance?: number): 'Low' | 'Medium' | 'High' {
  if (chance == null) return 'Medium';
  if (chance >= 0.35) return 'High';
  if (chance >= 0.2) return 'Medium';
  return 'Low';
}

export function getStageLabel(stage: string, session: GameSession): string {
  if (!isSimpleUi(session)) return stage;
  const map: Record<string, string> = {
    Unlicensed: 'Available',
    Licensed: 'Owned',
    Studied: 'Studied',
    SeismicEvaluated: 'Scanned',
    ExplorationDrilling: 'Drilling',
    DryHole: 'Dry Hole',
    Discovery: 'Discovery!',
    Appraisal: 'Appraising',
    DevelopmentApproved: 'Building',
    UnderConstruction: 'Building',
    Producing: 'Producing',
    LateLife: 'Slowing Down',
    Abandoned: 'Closed',
  };
  return map[stage] ?? stage;
}

export interface RecommendedAction {
  actionType: string;
  reason: string;
}

export function getRecommendedAction(
  block: BlockMapDto,
  discovery: DiscoveryDto | undefined,
  field: ProducingFieldDto | undefined,
  isOwned: boolean,
  session: GameSession,
): RecommendedAction | null {
  if (!isSimpleUi(session)) return null;

  if (block.stage === 'Unlicensed') {
    return { actionType: 'BidForLicense', reason: 'Good blocks are going fast — grab a license.' };
  }
  if (isOwned && !discovery && !field) {
    if (block.stage === 'Licensed' || block.stage === 'Studied') {
      return { actionType: 'GeologicalStudy', reason: 'Study the block before drilling.' };
    }
    if (block.estimatedChanceOfSuccess != null && block.estimatedChanceOfSuccess >= 0.25) {
      return { actionType: 'DrillExplorationWell', reason: 'Potential looks promising — time to drill.' };
    }
    return { actionType: 'Acquire2DSeismic', reason: 'Scan the area to improve your odds.' };
  }
  if (discovery && !field) {
    if (discovery.stage === 'Discovery' || discovery.stage === 'Appraisal') {
      return { actionType: 'ApproveDevelopment', reason: 'Turn your discovery into a producing field.' };
    }
  }
  if (field && field.stage === 'Producing') {
    return { actionType: 'OptimizeField', reason: 'Boost output while prices are favorable.' };
  }
  return null;
}

export function getModeHint(session: GameSession): string {
  if (isFunMode(session)) {
    const summary = getCompetitionSummary(session);
    return `Fun Mode — ${summary.title}. ${summary.scoreboardLabel}.`;
  }
  return 'Balanced Mode — full strategy rules, hedging, and deeper decisions.';
}

export interface CompetitionSummary {
  title: string;
  primaryGoal: string;
  pressure: string;
  scoreboardLabel: string;
  rivalCount: number;
  gapText: string;
}

export function getCompetitionSummary(
  session: GameSession,
  playerCompanyId?: string | null,
): CompetitionSummary {
  const player = findPlayerCompany(session, playerCompanyId);
  const rivals = session.companies.filter((company) => company.id !== player?.id);
  const totalTurns = Math.max(1, session.totalTurns);
  const currentTurn = Math.min(Math.max(1, session.currentTurnNumber), totalTurns);

  if (!player || rivals.length === 0) {
    return {
      title: `Beat the ${totalTurns}-turn clock`,
      primaryGoal: `Finish with the highest company value you can before turn ${totalTurns}.`,
      pressure:
        'You are playing against time, cash burn, drilling risk, license costs, and oil price swings.',
      scoreboardLabel: 'Solo score chase',
      rivalCount: 0,
      gapText: `Turn ${currentTurn}/${totalTurns}`,
    };
  }

  const ranked = [...session.companies].sort((a, b) => b.companyValue - a.companyValue);
  const leader = ranked[0];
  const topRival = ranked.find((company) => company.id !== player.id) ?? rivals[0];
  const target = leader.id === player.id ? topRival : leader;
  const gap = Math.abs(player.companyValue - target.companyValue);
  const rivalLabel = rivals.length === 1 ? '1 rival company' : `${rivals.length} rival companies`;
  const direction =
    player.companyValue >= target.companyValue
      ? `You are ${formatBriefMoney(gap)} ahead of ${target.name}.`
      : `You are ${formatBriefMoney(gap)} behind ${target.name}.`;

  return {
    title: `Race ${rivalLabel}`,
    primaryGoal: `${direction} Finish above ${target.name} by company value.`,
    pressure:
      'Other companies can win blocks, build fields, earn production, and outrank you before the last turn.',
    scoreboardLabel: leader.id === player.id ? 'You lead the table' : `${leader.name} leads`,
    rivalCount: rivals.length,
    gapText: direction,
  };
}

function findPlayerCompany(session: GameSession, playerCompanyId?: string | null): CompanyDto | null {
  if (playerCompanyId) {
    return session.companies.find((company) => company.id === playerCompanyId) ?? null;
  }
  return session.companies[0] ?? null;
}

function formatBriefMoney(value: number): string {
  const abs = Math.abs(value);
  if (abs >= 1_000_000_000) return `$${(abs / 1_000_000_000).toFixed(1)}B`;
  return `$${Math.round(abs / 1_000_000)}M`;
}
