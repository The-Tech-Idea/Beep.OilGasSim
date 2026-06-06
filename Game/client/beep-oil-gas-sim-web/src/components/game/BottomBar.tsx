import { useGame } from '../../store/GameContext';

function fmtMoney(value: number): string {
  return `$${(value / 1_000_000).toFixed(0)}M`;
}

export function BottomBar() {
  const { session, actionQueue, playerCompanyId, removeQueuedAction, commitTurn, loading, commitStatus } =
    useGame();

  if (!session || !playerCompanyId) return null;

  const company = session.companies.find((c) => c.id === playerCompanyId)!;
  const totalSpend = actionQueue.reduce((sum, a) => sum + a.estimatedCost, 0);
  const cashAfter = company.cash - totalSpend;
  const alreadyCommitted = company.turnCommitted === true;
  const waitingOnOthers =
    session.isMultiplayer &&
    alreadyCommitted &&
    commitStatus &&
    commitStatus.committed < commitStatus.total;

  return (
    <footer className="bottom-bar">
      <div className="action-queue">
        <strong>
          Actions {actionQueue.length}/{session.actionSlotsPerTurn}
        </strong>
        {actionQueue.length === 0 && !alreadyCommitted && (
          <span className="muted">No actions queued — select a block and choose an action.</span>
        )}
        <ul>
          {actionQueue.map((a, i) => (
            <li key={a.id}>
              <span>{i + 1}. {a.label} — {fmtMoney(a.estimatedCost)}</span>
              <button type="button" className="btn-remove" onClick={() => removeQueuedAction(a.id)}>
                ×
              </button>
            </li>
          ))}
        </ul>
      </div>
      <div className="bottom-summary">
        <span>Est. spend {fmtMoney(totalSpend)}</span>
        <span>Cash after {fmtMoney(cashAfter)}</span>
        {session.isMultiplayer && commitStatus && (
          <span className="commit-status">
            Commits {commitStatus.committed}/{commitStatus.total}
          </span>
        )}
        {waitingOnOthers ? (
          <span className="muted">Waiting for other companies…</span>
        ) : (
          <button
            className="btn-commit"
            disabled={loading || session.state !== 'Planning' || alreadyCommitted}
            onClick={() => void commitTurn()}
          >
            {alreadyCommitted ? 'Turn committed' : 'Commit Turn'}
          </button>
        )}
      </div>
    </footer>
  );
}
