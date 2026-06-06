import { useCallback, useEffect, useMemo, useState } from 'react';
import { api } from '../../api/ApiClient';
import type { GameSession } from '../../api/types';
import { GameIcon } from '../ui/GameIcon';
import {
  connectGameHub,
  disconnectGameHub,
  loadPlayerIdentity,
  savePlayerIdentity,
} from '../../realtime/GameHubClient';

interface MultiplayerLobbyProps {
  sessionId: string;
  companyId: string;
  playerId: string;
  displayName: string;
  onGameStarted: () => void;
  onLeave: () => void;
}

export function MultiplayerLobby({
  sessionId,
  companyId,
  playerId,
  displayName,
  onGameStarted,
  onLeave,
}: MultiplayerLobbyProps) {
  const [session, setSession] = useState<GameSession | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [chatText, setChatText] = useState('');
  const [ready, setReady] = useState(false);

  const refresh = useCallback(async () => {
    const data = await api.getSession(sessionId);
    setSession(data);
    const me = data.lobbyPlayers.find((p) => p.playerId === playerId);
    setReady(me?.isReady ?? false);
  }, [sessionId, playerId]);

  useEffect(() => {
    savePlayerIdentity(sessionId, companyId, playerId, displayName);
    void refresh().catch((e: unknown) => setError(String(e)));

    void connectGameHub(sessionId, {
      onLobbyUpdated: () => void refresh(),
      onGameStarted: () => {
        void refresh().then(onGameStarted);
      },
      onChatMessage: () => void refresh(),
    });

    return () => {
      void disconnectGameHub();
    };
  }, [sessionId, companyId, playerId, displayName, refresh, onGameStarted]);

  const isHost = session?.hostCompanyId === companyId;
  const canStart =
    isHost &&
    (session?.lobbyPlayers.length ?? 0) >= (session?.minPlayers ?? 2) &&
    session?.lobbyPlayers.every((p) => p.isReady);

  const toggleReady = async () => {
    setLoading(true);
    setError(null);
    try {
      const next = !ready;
      const updated = await api.setReady(sessionId, companyId, playerId, next);
      setSession(updated);
      setReady(next);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  };

  const startGame = async () => {
    setLoading(true);
    setError(null);
    try {
      await api.startSession(sessionId);
      onGameStarted();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  };

  const sendChat = async () => {
    if (!chatText.trim()) return;
    setError(null);
    try {
      await api.sendChat(sessionId, {
        companyId,
        senderName: displayName,
        channel: 'public',
        text: chatText.trim(),
      });
      setChatText('');
      await refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const players = useMemo(() => session?.lobbyPlayers ?? [], [session]);

  return (
    <div className="lobby multiplayer-lobby">
      <header className="lobby-header">
        <div className="lobby-brand">
          <GameIcon name="logo" size={48} className="lobby-logo" />
          <div>
            <h1>Multiplayer Lobby</h1>
            <p>
              Join code: <strong className="join-code">{session?.joinCode ?? '…'}</strong>
              {' · '}
              {players.length}/{session?.maxPlayers ?? 6} players
            </p>
          </div>
        </div>
        <button type="button" className="btn-secondary" onClick={onLeave}>
          Leave
        </button>
      </header>

      {error && <p className="status-error">{error}</p>}

      <section className="lobby-players">
        <h2>Players</h2>
        <ul>
          {players.map((p) => (
            <li key={p.playerId} style={{ borderLeftColor: p.colorHex }}>
              <span>{p.companyName}</span>
              <span className="muted">{p.displayName}</span>
              {p.isHost && <span className="mode-tag">Host</span>}
              <span className={p.isReady ? 'status-ok' : 'muted'}>
                {p.isReady ? 'Ready' : 'Not ready'}
              </span>
            </li>
          ))}
        </ul>
      </section>

      <div className="lobby-actions">
        <button type="button" className="btn-primary" disabled={loading} onClick={() => void toggleReady()}>
          {ready ? 'Unready' : 'Ready up'}
        </button>
        {isHost && (
          <button
            type="button"
            className="btn-commit"
            disabled={loading || !canStart}
            onClick={() => void startGame()}
          >
            Start game
          </button>
        )}
      </div>

      <section className="lobby-chat">
        <h2>Chat</h2>
        <div className="chat-log">
          {(session?.chatMessages ?? []).map((m) => (
            <p key={m.id}>
              <strong>{m.senderName}:</strong> {m.text}
            </p>
          ))}
        </div>
        <div className="chat-compose">
          <input
            value={chatText}
            placeholder="Say something…"
            onChange={(e) => setChatText(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && void sendChat()}
          />
          <button type="button" onClick={() => void sendChat()}>
            Send
          </button>
        </div>
      </section>
    </div>
  );
}

export function resolveMultiplayerIdentity(sessionId: string) {
  return loadPlayerIdentity(sessionId);
}
