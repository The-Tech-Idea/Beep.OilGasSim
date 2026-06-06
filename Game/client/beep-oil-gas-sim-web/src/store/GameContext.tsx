import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { api } from '../api/ApiClient';
import type {
  BlockMapDto,
  GameSession,
  QueuedAction,
  SidebarView,
  SubmitActionRequest,
  TurnResultResponse,
} from '../api/types';
import {
  connectGameHub,
  disconnectGameHub,
  isTransientRealtimeError,
  loadPlayerIdentity,
} from '../realtime/GameHubClient';

interface GameContextValue {
  session: GameSession | null;
  mapBlocks: BlockMapDto[];
  selectedBlockId: string | null;
  sidebarView: SidebarView;
  actionQueue: QueuedAction[];
  lastTurnResult: TurnResultResponse | null;
  loading: boolean;
  error: string | null;
  playerCompanyId: string | null;
  commitStatus: { committed: number; total: number } | null;
  selectBlock: (blockId: string | null) => void;
  setSidebarView: (view: SidebarView) => void;
  refreshSession: () => Promise<void>;
  refreshMap: () => Promise<void>;
  submitAction: (request: SubmitActionRequest, label: string) => Promise<void>;
  removeQueuedAction: (actionId: string) => void;
  commitTurn: () => Promise<TurnResultResponse | null>;
  dismissTurnResult: () => void;
}

const GameContext = createContext<GameContextValue | null>(null);

export function GameProvider({
  sessionId,
  companyId: companyIdProp,
  children,
}: {
  sessionId: string;
  companyId?: string;
  children: ReactNode;
}) {
  const [session, setSession] = useState<GameSession | null>(null);
  const [mapBlocks, setMapBlocks] = useState<BlockMapDto[]>([]);
  const [selectedBlockId, setSelectedBlockId] = useState<string | null>(null);
  const [sidebarView, setSidebarView] = useState<SidebarView>('map');
  const [actionQueue, setActionQueue] = useState<QueuedAction[]>([]);
  const [lastTurnResult, setLastTurnResult] = useState<TurnResultResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [commitStatus, setCommitStatus] = useState<{ committed: number; total: number } | null>(
    null,
  );

  const storedIdentity = loadPlayerIdentity(sessionId);
  const playerCompanyId =
    companyIdProp || storedIdentity?.companyId || session?.companies[0]?.id || null;

  const refreshSession = useCallback(async () => {
    const data = await api.getSession(sessionId);
    setSession(data);
    if (data.isMultiplayer) {
      const committed = data.companies.filter((c) => c.turnCommitted).length;
      setCommitStatus({ committed, total: data.companies.length });
    } else {
      setCommitStatus(null);
    }
    if (data.pendingActions?.length) {
      const mine = playerCompanyId
        ? data.pendingActions.filter((a) => !a.companyId || a.companyId === playerCompanyId)
        : data.pendingActions;
      setActionQueue(
        mine.map((a) => ({
          id: a.id,
          actionType: a.actionType,
          label: formatActionLabel(a.actionType),
          estimatedCost: a.estimatedCost,
          targetBlockId: a.targetBlockId,
          targetAssetId: a.targetAssetId,
        })),
      );
    } else {
      setActionQueue([]);
    }
  }, [sessionId, playerCompanyId]);

  const refreshMap = useCallback(async () => {
    const data = await api.getMap(sessionId, playerCompanyId ?? undefined);
    setMapBlocks(data.blocks);
  }, [sessionId, playerCompanyId]);

  const loadAll = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      await Promise.all([refreshSession(), refreshMap()]);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }, [refreshSession, refreshMap]);

  useEffect(() => {
    void loadAll();
  }, [loadAll]);

  useEffect(() => {
    let disposed = false;
    void connectGameHub(sessionId, {
      onTurnCommitted: (payload) => {
        setCommitStatus({
          committed: payload.committedCount,
          total: payload.totalCompanies,
        });
        void refreshSession();
      },
      onTurnResolved: () => {
        setCommitStatus(null);
        void Promise.all([refreshSession(), refreshMap()]).then(async () => {
          const data = await api.getSession(sessionId);
          const me = playerCompanyId
            ? data.companies.find((c) => c.id === playerCompanyId)
            : null;
          if (me?.turnCommitted === false) {
            setActionQueue([]);
          }
        });
      },
    }).catch((e: unknown) => {
      if (!disposed && !isTransientRealtimeError(e)) {
        setError(e instanceof Error ? e.message : String(e));
      }
    });
    return () => {
      disposed = true;
      void disconnectGameHub();
    };
  }, [sessionId, refreshSession, refreshMap, playerCompanyId]);

  const submitAction = useCallback(
    async (request: SubmitActionRequest, label: string) => {
      setError(null);
      const response = await api.submitAction(sessionId, request);
      setActionQueue((prev) => [
        ...prev,
        {
          id: response.id,
          actionType: response.actionType,
          label,
          estimatedCost: response.estimatedCost,
          targetBlockId: response.targetBlockId,
          targetAssetId: response.targetAssetId,
        },
      ]);
      await refreshSession();
    },
    [sessionId, refreshSession],
  );

  const removeQueuedAction = useCallback((actionId: string) => {
    setActionQueue((prev) => prev.filter((a) => a.id !== actionId));
  }, []);

  const commitTurn = useCallback(async () => {
    if (!playerCompanyId) return null;
    setLoading(true);
    setError(null);
    try {
      const result = await api.commitTurn(sessionId, playerCompanyId);
      setActionQueue([]);
      await Promise.all([refreshSession(), refreshMap()]);
      if (result.events?.length) {
        setLastTurnResult(result);
        setCommitStatus(null);
      }
      return result;
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      return null;
    } finally {
      setLoading(false);
    }
  }, [sessionId, playerCompanyId, refreshSession, refreshMap]);

  const value = useMemo<GameContextValue>(
    () => ({
      session,
      mapBlocks,
      selectedBlockId,
      sidebarView,
      actionQueue,
      lastTurnResult,
      loading,
      error,
      playerCompanyId,
      commitStatus,
      selectBlock: setSelectedBlockId,
      setSidebarView,
      refreshSession,
      refreshMap,
      submitAction,
      removeQueuedAction,
      commitTurn,
      dismissTurnResult: () => setLastTurnResult(null),
    }),
    [
      session,
      mapBlocks,
      selectedBlockId,
      sidebarView,
      actionQueue,
      lastTurnResult,
      loading,
      error,
      playerCompanyId,
      commitStatus,
      refreshSession,
      refreshMap,
      submitAction,
      removeQueuedAction,
      commitTurn,
    ],
  );

  return <GameContext.Provider value={value}>{children}</GameContext.Provider>;
}

export function useGame() {
  const ctx = useContext(GameContext);
  if (!ctx) throw new Error('useGame must be used within GameProvider');
  return ctx;
}

function formatActionLabel(actionType: string): string {
  return actionType.replace(/([A-Z])/g, ' $1').trim();
}
