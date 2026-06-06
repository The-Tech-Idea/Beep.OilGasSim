import { useCallback, useEffect, useState } from 'react';
import { api } from '../../api/ApiClient';
import type { GameMode, HealthResponse } from '../../api/types';
import { GameIcon } from '../ui/GameIcon';
import { savePlayerIdentity } from '../../realtime/GameHubClient';

interface LobbyScreenProps {
  onSoloStarted: (sessionId: string) => void;
  onMultiplayerLobby: (payload: {
    sessionId: string;
    companyId: string;
    playerId: string;
    displayName: string;
  }) => void;
}

function fmtMoney(value: number): string {
  return `$${(value / 1_000_000).toFixed(0)}M`;
}

export function LobbyScreen({ onSoloStarted, onMultiplayerLobby }: LobbyScreenProps) {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [healthError, setHealthError] = useState<string | null>(null);
  const [modes, setModes] = useState<GameMode[]>([]);
  const [selectedModeId, setSelectedModeId] = useState<string>('fun');
  const [loading, setLoading] = useState(false);
  const [companyName, setCompanyName] = useState('Beep Energy');
  const [playerName, setPlayerName] = useState('Player');
  const [joinCode, setJoinCode] = useState('');
  const [playMode, setPlayMode] = useState<'solo' | 'multi-create' | 'multi-join'>('solo');

  useEffect(() => {
    void api.health().then(setHealth).catch((e: unknown) => setHealthError(String(e)));
    void api.getGameModes().then((m) => {
      setModes(m);
      if (m.length > 0) setSelectedModeId(m[0].id);
    }).catch(() => {});
  }, []);

  const selectedMode = modes.find((m) => m.id === selectedModeId);

  const startSoloGame = useCallback(async () => {
    if (!selectedModeId) return;
    setLoading(true);
    setHealthError(null);
    try {
      const created = await api.createSession({
        scenarioId: 'desert-frontier',
        gameplayModeProfileId: selectedModeId,
        companyName,
        playerDisplayName: playerName,
        isMultiplayer: false,
      });
      const host = created.lobbyPlayers[0];
      savePlayerIdentity(created.id, host.companyId, host.playerId, playerName);
      await api.startSession(created.id);
      onSoloStarted(created.id);
    } catch (e) {
      setHealthError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }, [selectedModeId, companyName, playerName, onSoloStarted]);

  const createMultiplayerLobby = useCallback(async () => {
    if (!selectedModeId) return;
    setLoading(true);
    setHealthError(null);
    try {
      const created = await api.createSession({
        scenarioId: 'desert-frontier',
        gameplayModeProfileId: selectedModeId,
        companyName,
        playerDisplayName: playerName,
        isMultiplayer: true,
      });
      const host = created.lobbyPlayers[0];
      onMultiplayerLobby({
        sessionId: created.id,
        companyId: host.companyId,
        playerId: host.playerId,
        displayName: playerName,
      });
    } catch (e) {
      setHealthError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }, [selectedModeId, companyName, playerName, onMultiplayerLobby]);

  const joinMultiplayerLobby = useCallback(async () => {
    if (!joinCode.trim()) return;
    setLoading(true);
    setHealthError(null);
    try {
      const joined = await api.joinSession({
        joinCode: joinCode.trim(),
        companyName,
        playerDisplayName: playerName,
      });
      savePlayerIdentity(joined.sessionId, joined.companyId, joined.playerId, playerName);
      onMultiplayerLobby({
        sessionId: joined.sessionId,
        companyId: joined.companyId,
        playerId: joined.playerId,
        displayName: playerName,
      });
    } catch (e) {
      setHealthError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }, [joinCode, companyName, playerName, onMultiplayerLobby]);

  const handlePrimaryAction = () => {
    if (playMode === 'solo') void startSoloGame();
    else if (playMode === 'multi-create') void createMultiplayerLobby();
    else void joinMultiplayerLobby();
  };

  const primaryLabel =
    playMode === 'solo'
      ? `Start ${selectedMode?.name ?? 'Game'}`
      : playMode === 'multi-create'
        ? 'Create multiplayer lobby'
        : 'Join lobby';

  return (
    <div className="lobby">
      <header className="lobby-header">
        <div className="lobby-brand">
          <GameIcon name="logo" size={48} className="lobby-logo" />
          <div>
            <h1>Beep Oil and Gas Sim</h1>
            <p>Desert Frontier · Solo or multiplayer (2–6 players)</p>
          </div>
        </div>
      </header>

      {health && <p className="status-ok lobby-status">API connected</p>}
      {healthError && !modes.length && <p className="status-error">{healthError}</p>}

      <div className="play-mode-tabs">
        <button
          type="button"
          className={playMode === 'solo' ? 'selected' : ''}
          onClick={() => setPlayMode('solo')}
        >
          Solo
        </button>
        <button
          type="button"
          className={playMode === 'multi-create' ? 'selected' : ''}
          onClick={() => setPlayMode('multi-create')}
        >
          Host multiplayer
        </button>
        <button
          type="button"
          className={playMode === 'multi-join' ? 'selected' : ''}
          onClick={() => setPlayMode('multi-join')}
        >
          Join with code
        </button>
      </div>

      <label className="field lobby-company">
        Company name
        <input value={companyName} onChange={(e) => setCompanyName(e.target.value)} />
      </label>

      <label className="field lobby-company">
        Your name
        <input value={playerName} onChange={(e) => setPlayerName(e.target.value)} />
      </label>

      {playMode === 'multi-join' && (
        <label className="field lobby-company">
          Join code
          <input
            value={joinCode}
            onChange={(e) => setJoinCode(e.target.value.toUpperCase())}
            placeholder="ABC123"
          />
        </label>
      )}

      {playMode !== 'multi-join' && (
        <div className="mode-cards">
          {modes.map((m) => (
            <button
              key={m.id}
              type="button"
              className={`mode-card ${selectedModeId === m.id ? 'selected' : ''} mode-${m.modeType.toLowerCase()}`}
              onClick={() => setSelectedModeId(m.id)}
            >
              <h2>{m.name}</h2>
              <div className="mode-card-icon">
                <GameIcon name={m.modeType === 'Fun' ? 'oil' : 'hedge'} size={32} />
              </div>
              <p>{m.description}</p>
              <ul className="mode-stats">
                <li>{m.totalTurns} turns</li>
                <li>{m.actionSlotsPerTurn} actions/turn</li>
                <li>{fmtMoney(m.startingCash)} start</li>
                <li>{fmtMoney(m.maxDebt)} max debt</li>
              </ul>
              <span className="mode-tag">{m.uiComplexityLevel} UI</span>
              {m.enableHedging && <span className="mode-tag">Hedging</span>}
            </button>
          ))}
        </div>
      )}

      {selectedMode && playMode !== 'multi-join' && (
        <p className="mode-summary muted">
          {selectedMode.modeType === 'Fun'
            ? 'Solo Fun Mode is a 12-turn score chase: beat the clock, manage cash, find oil, and finish with the highest company value you can.'
            : 'Standard competitive rules with hedging, extra action slot, and full finance tools.'}
        </p>
      )}

      <button
        className="btn-start-game"
        data-testid="start-game-button"
        disabled={
          loading ||
          !health ||
          (playMode !== 'multi-join' && !selectedModeId) ||
          (playMode === 'multi-join' && !joinCode.trim())
        }
        onClick={handlePrimaryAction}
      >
        {loading ? 'Working…' : primaryLabel}
      </button>
      {healthError && modes.length > 0 && <p className="status-error">{healthError}</p>}
    </div>
  );
}
