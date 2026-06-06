import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

export type LobbyUpdatedPayload = {
  sessionId: string;
  joinCode: string;
  playerCount: number;
  maxPlayers: number;
  minPlayers: number;
  hostCompanyId: string;
  players: Array<{
    companyId: string;
    playerId: string;
    companyName: string;
    displayName: string;
    colorHex: string;
    isHost: boolean;
    isReady: boolean;
  }>;
};

export type TurnCommittedPayload = {
  companyId: string;
  committedCount: number;
  totalCompanies: number;
};

export type TurnResolvedPayload = {
  turnNumber: number;
  eventCount: number;
};

export type ChatMessagePayload = {
  id: string;
  companyId?: string;
  senderName: string;
  channel: string;
  text: string;
  sentAtUtc: string;
};

export type GameStartedPayload = {
  sessionId: string;
  state: string;
  currentTurnNumber: number;
};

type Handlers = {
  onLobbyUpdated?: (payload: LobbyUpdatedPayload) => void;
  onTurnCommitted?: (payload: TurnCommittedPayload) => void;
  onTurnResolved?: (payload: TurnResolvedPayload) => void;
  onChatMessage?: (payload: ChatMessagePayload) => void;
  onGameStarted?: (payload: GameStartedPayload) => void;
};

let connection: HubConnection | null = null;
let activeSessionId: string | null = null;

export async function connectGameHub(sessionId: string, handlers: Handlers): Promise<void> {
  if (connection?.state === HubConnectionState.Connected && activeSessionId === sessionId) {
    return;
  }

  if (connection) {
    await disconnectGameHub();
  }

  connection = new HubConnectionBuilder()
    .withUrl('/hubs/game')
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build();

  connection.on('LobbyUpdated', (payload: LobbyUpdatedPayload) => handlers.onLobbyUpdated?.(payload));
  connection.on('TurnCommitted', (payload: TurnCommittedPayload) => handlers.onTurnCommitted?.(payload));
  connection.on('TurnResolved', (payload: TurnResolvedPayload) => handlers.onTurnResolved?.(payload));
  connection.on('ChatMessage', (payload: ChatMessagePayload) => handlers.onChatMessage?.(payload));
  connection.on('GameStarted', (payload: GameStartedPayload) => handlers.onGameStarted?.(payload));

  await connection.start();
  await connection.invoke('JoinSession', sessionId);
  activeSessionId = sessionId;
}

export function isTransientRealtimeError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error);
  return message.includes('connection was stopped during negotiation');
}

export async function disconnectGameHub(): Promise<void> {
  if (!connection) return;
  if (activeSessionId && connection.state === HubConnectionState.Connected) {
    try {
      await connection.invoke('LeaveSession', activeSessionId);
    } catch {
      // ignore disconnect errors
    }
  }
  await connection.stop();
  connection = null;
  activeSessionId = null;
}

export function playerIdentityKey(sessionId: string) {
  return `ogs_player_${sessionId}`;
}

export function savePlayerIdentity(sessionId: string, companyId: string, playerId: string, displayName: string) {
  sessionStorage.setItem(
    playerIdentityKey(sessionId),
    JSON.stringify({ companyId, playerId, displayName }),
  );
}

export function loadPlayerIdentity(sessionId: string): {
  companyId: string;
  playerId: string;
  displayName: string;
} | null {
  const raw = sessionStorage.getItem(playerIdentityKey(sessionId));
  if (!raw) return null;
  try {
    return JSON.parse(raw) as { companyId: string; playerId: string; displayName: string };
  } catch {
    return null;
  }
}
