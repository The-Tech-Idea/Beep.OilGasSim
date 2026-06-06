import { parseApiError } from './parseApiError';
import type {
  AiAskRequest,
  AiAdvisorResponse,
  AiTurnReport,
  ChatMessageDto,
  CreateGameSessionRequest,
  GameMode,
  GameSession,
  HealthResponse,
  JoinGameSessionRequest,
  JoinSessionResponse,
  LeaderboardEntryDto,
  MapResponse,
  SendChatRequest,
  SessionHistoryResponse,
  SubmitActionRequest,
  TurnActionResponse,
  TurnResultResponse,
} from './types';

const API = '';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(`${API}${path}`, {
      headers: { 'Content-Type': 'application/json', ...init?.headers },
      ...init,
    });
  } catch {
    throw new Error(
      'Cannot reach the API. Start it first: cd Game && dotnet run --project src/Beep.OilGasSim.Api',
    );
  }
  if (!res.ok) {
    const text = await res.text();
    throw new Error(parseApiError(res.status, text));
  }
  return res.json() as Promise<T>;
}

export const api = {
  health: () => request<HealthResponse>('/health'),

  getGameModes: () => request<GameMode[]>('/api/game-modes'),

  createSession: (body: CreateGameSessionRequest) =>
    request<GameSession>('/api/game-sessions', { method: 'POST', body: JSON.stringify(body) }),

  getSession: (id: string) => request<GameSession>(`/api/game-sessions/${id}`),

  getSessionByJoinCode: (code: string) =>
    request<GameSession>(`/api/game-sessions/by-code/${encodeURIComponent(code)}`),

  joinSession: (body: JoinGameSessionRequest) =>
    request<JoinSessionResponse>('/api/game-sessions/join', {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  setReady: (sessionId: string, companyId: string, playerId: string, isReady: boolean) =>
    request<GameSession>(
      `/api/game-sessions/${sessionId}/companies/${companyId}/players/${playerId}/ready`,
      { method: 'POST', body: JSON.stringify({ isReady }) },
    ),

  sendChat: (sessionId: string, body: SendChatRequest) =>
    request<ChatMessageDto>(`/api/game-sessions/${sessionId}/chat`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  startSession: (id: string) =>
    request<GameSession>(`/api/game-sessions/${id}/start`, { method: 'POST' }),

  getMap: (id: string, companyId?: string) =>
    request<MapResponse>(
      `/api/game-sessions/${id}/map${companyId ? `?companyId=${companyId}` : ''}`,
    ),

  getHistory: (id: string, companyId?: string) =>
    request<SessionHistoryResponse>(
      `/api/game-sessions/${id}/history${companyId ? `?companyId=${companyId}` : ''}`,
    ),

  submitAction: (id: string, body: SubmitActionRequest) =>
    request<TurnActionResponse>(`/api/game-sessions/${id}/actions`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  commitTurn: (id: string, companyId: string) =>
    request<TurnResultResponse>(`/api/game-sessions/${id}/companies/${companyId}/commit`, {
      method: 'POST',
    }),

  getLeaderboard: (id: string) =>
    request<LeaderboardEntryDto[]>(`/api/game-sessions/${id}/leaderboard`),

  askAdvisor: (sessionId: string, body: AiAskRequest) =>
    request<AiAdvisorResponse>(`/api/game-sessions/${sessionId}/ai/ask`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  getTurnReport: (sessionId: string, companyId: string) =>
    request<AiTurnReport>(
      `/api/game-sessions/${sessionId}/ai/turn-report?companyId=${companyId}`,
    ),
};
