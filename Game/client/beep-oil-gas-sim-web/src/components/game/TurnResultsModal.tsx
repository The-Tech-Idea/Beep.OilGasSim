import { useGame } from '../../store/GameContext';

export function TurnResultsModal() {
  const { lastTurnResult, dismissTurnResult, session, playerCompanyId } = useGame();

  if (!lastTurnResult?.events?.length) return null;

  const summary = lastTurnResult.companySummaries?.find((s) => s.companyId === playerCompanyId);

  return (
    <div className="modal-overlay" role="dialog" aria-modal="true">
      <div className="turn-results-modal">
        <header>
          <h2>Turn {lastTurnResult.turnNumber} Results</h2>
          <button type="button" className="btn-close" onClick={dismissTurnResult}>×</button>
        </header>

        <div className="result-cards">
          {lastTurnResult.events.map((event, i) => (
            <article
              key={`${event.headline}-${i}`}
              className={`result-card category-${event.category.toLowerCase()}`}
            >
              <span className="result-category">{event.category}</span>
              <h3>{event.headline}</h3>
              <p>{event.detail}</p>
            </article>
          ))}
        </div>

        {summary && (
          <section className="financial-summary">
            <h3>Financial Summary</h3>
            <p>Ending cash {formatMoney(summary.endingCash)} · CAPEX {formatMoney(summary.capex)} · Value {formatMoney(summary.companyValue)}</p>
          </section>
        )}

        {session?.state === 'Completed' && (
          <p className="game-complete">Match complete! Check the leaderboard for final standings.</p>
        )}

        <footer>
          <button type="button" className="btn-primary" onClick={dismissTurnResult}>
            Continue
          </button>
        </footer>
      </div>
    </div>
  );
}

function formatMoney(value: number): string {
  return `$${(value / 1_000_000).toFixed(0)}M`;
}
