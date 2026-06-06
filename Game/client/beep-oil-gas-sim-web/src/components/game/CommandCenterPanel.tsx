import { useState } from 'react';
import { api } from '../../api/ApiClient';
import type { AiAdvisorResponse } from '../../api/types';
import { useGame } from '../../store/GameContext';
import { getActionLabel } from '../../mode/modeUi';
import { GameIcon } from '../ui/GameIcon';

const ADVISORS = [
  { id: 'Strategy', label: 'Strategy', icon: 'hedge' as const },
  { id: 'Geologist', label: 'Geologist', icon: 'seismic' as const },
  { id: 'Cfo', label: 'CFO', icon: 'finance' as const },
  { id: 'Hse', label: 'HSE', icon: 'pipeline' as const },
];

export function CommandCenterPanel() {
  const { session, playerCompanyId, selectedBlockId, lastTurnResult } = useGame();
  const [advisor, setAdvisor] = useState('Strategy');
  const [message, setMessage] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [history, setHistory] = useState<AiAdvisorResponse[]>([]);
  const [turnReport, setTurnReport] = useState<string | null>(null);

  if (!session || !playerCompanyId) {
    return <aside className="right-panel"><p>Loading…</p></aside>;
  }

  const ask = async () => {
    if (!message.trim()) return;
    setLoading(true);
    setError(null);
    try {
      const response = await api.askAdvisor(session.id, {
        companyId: playerCompanyId,
        advisorType: advisor,
        message: message.trim(),
        selectedBlockId: selectedBlockId ?? undefined,
      });
      setHistory((prev) => [...prev, response]);
      setMessage('');
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  };

  const loadTurnReport = async () => {
    setLoading(true);
    setError(null);
    try {
      const report = await api.getTurnReport(session.id, playerCompanyId);
      setTurnReport(report.summary);
      if (report.highlights.length) {
        setHistory((prev) => [
          ...prev,
          {
            advisorType: 'Turn Report',
            message: report.summary,
            recommendationType: 'Summary',
            suggestedActions: report.recommendations,
            risks: report.highlights,
          },
        ]);
      }
    } catch {
      setTurnReport('No turn report available yet.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <aside className="right-panel command-center">
      <h2>Command Center</h2>
      <p className="muted">AI advisors use only data your company has discovered.</p>

      <div className="advisor-tabs">
        {ADVISORS.map((a) => (
          <button
            key={a.id}
            type="button"
            className={`advisor-tab ${advisor === a.id ? 'active' : ''}`}
            onClick={() => setAdvisor(a.id)}
          >
            <GameIcon name={a.icon} size={16} />
            {a.label}
          </button>
        ))}
      </div>

      <div className="command-chat">
        {history.length === 0 && (
          <p className="muted chat-empty">
            Ask about the selected block, finances, or turn strategy.
            {selectedBlockId ? ' Block context is attached.' : ' Select a block on the map for geological advice.'}
          </p>
        )}
        {history.map((item, i) => (
          <article key={i} className="chat-bubble advisor">
            <header>{item.advisorType}</header>
            <p>{item.message}</p>
            {item.suggestedActions.length > 0 && (
              <ul className="suggested-actions">
                {item.suggestedActions.map((action) => (
                  <li key={action}>{getActionLabel(action, session)}</li>
                ))}
              </ul>
            )}
            {item.risks.length > 0 && (
              <ul className="risk-list">
                {item.risks.map((r) => (
                  <li key={r}>{r}</li>
                ))}
              </ul>
            )}
          </article>
        ))}
      </div>

      <div className="command-input">
        <textarea
          value={message}
          rows={2}
          placeholder={`Ask ${advisor}…`}
          onChange={(e) => setMessage(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault();
              void ask();
            }
          }}
        />
        <div className="command-actions">
          <button type="button" disabled={loading || !message.trim()} onClick={() => void ask()}>
            Send
          </button>
          <button type="button" disabled={loading} onClick={() => void loadTurnReport()}>
            Turn Report
          </button>
        </div>
      </div>

      {lastTurnResult && !turnReport && (
        <button type="button" className="link-btn" onClick={() => void loadTurnReport()}>
          Generate report for turn {lastTurnResult.turnNumber}
        </button>
      )}

      {error && <p className="error-text">{error}</p>}
    </aside>
  );
}
