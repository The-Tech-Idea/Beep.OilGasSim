import { useGame } from '../../store/GameContext';
import { getCompetitionSummary, getModeHint, isFunMode } from '../../mode/modeUi';
import { GameIcon } from '../ui/GameIcon';
import { getFinancialWarnings } from '../../utils/financialWarnings';

function fmtMoney(value: number): string {
  if (Math.abs(value) >= 1_000_000_000) return `$${(value / 1_000_000_000).toFixed(2)}B`;
  return `$${(value / 1_000_000).toFixed(0)}M`;
}

export function TopBar() {
  const { session, playerCompanyId, commitTurn, loading, actionQueue } = useGame();
  if (!session || !playerCompanyId) return null;

  const company = session.companies.find((c) => c.id === playerCompanyId)!;
  const competitionSummary = getCompetitionSummary(session, playerCompanyId);
  const projectedSpend = actionQueue.reduce((sum, a) => sum + a.estimatedCost, 0);
  const warnings = getFinancialWarnings(company, session, projectedSpend);
  const topWarning = warnings.sort((a, b) => {
    const rank = { danger: 0, warn: 1, info: 2 };
    return rank[a.level] - rank[b.level];
  })[0];

  return (
    <header className="top-bar">
      <div className="top-bar-left">
        <span className={`mode-pill ${isFunMode(session) ? 'fun' : 'balanced'}`}>
          {session.gameplayMode}
        </span>
        <span className="company-badge" style={{ borderColor: company.colorHex }}>
          {company.name}
        </span>
        <span>Cash {fmtMoney(company.cash)}</span>
        <span>Debt {fmtMoney(company.debt)}</span>
        <span className="top-stat">
          <GameIcon name="oilPrice" size={16} />
          Oil ${session.oilPrice.toFixed(0)}/bbl
        </span>
        <span>
          Turn {session.currentTurnNumber}/{session.totalTurns}
        </span>
        <span>{competitionSummary.rivalCount > 0 ? `Rank #${company.rank}` : 'Solo Score'}</span>
      </div>
      <div className="top-bar-right">
        {topWarning && (
          <span className={`finance-warning warning-${topWarning.level}`} title={topWarning.message}>
            {topWarning.message}
          </span>
        )}
        <span className="mode-hint muted">
          {isFunMode(session) ? competitionSummary.pressure : getModeHint(session)}
        </span>
        <button
          className="btn-commit"
          disabled={loading || session.state !== 'Planning' || company.turnCommitted === true}
          onClick={() => void commitTurn()}
        >
          Commit Turn ({actionQueue.length}/{session.actionSlotsPerTurn})
        </button>
      </div>
    </header>
  );
}
