import { useCallback, useMemo, useState } from 'react';
import { BasinMapCanvas } from '../map/BasinMapCanvas';
import type { IsoWorldType } from '../map/isoWorldGenerator';
import { BottomBar } from './BottomBar';
import { LeftSidebar } from './LeftSidebar';
import { RightPanel } from './RightPanel';
import { TopBar } from './TopBar';
import { TurnResultsModal } from './TurnResultsModal';
import { TutorialOverlay } from '../../tutorial/TutorialOverlay';
import { useGame } from '../../store/GameContext';
import { getCompetitionSummary, isFunMode } from '../../mode/modeUi';

const WORLD_TYPES: Array<{ id: IsoWorldType; label: string }> = [
  { id: 'desert-frontier', label: 'Desert' },
  { id: 'coastal-delta', label: 'Delta' },
  { id: 'fold-belt', label: 'Fold belt' },
  { id: 'offshore-shelf', label: 'Offshore' },
];

export function GameShell() {
  const {
    session,
    mapBlocks,
    selectedBlockId,
    selectBlock,
    playerCompanyId,
    loading,
    error,
    sidebarView,
  } = useGame();
  const [worldType, setWorldType] = useState<IsoWorldType>('desert-frontier');

  const handleSelectBlock = useCallback(
    (id: string) => {
      selectBlock(id);
    },
    [selectBlock],
  );

  const companyColors = useMemo(() => {
    const map = new Map<string, string>();
    for (const c of session?.companies ?? []) {
      map.set(c.id, c.colorHex);
    }
    return map;
  }, [session?.companies]);

  const competitionSummary = useMemo(
    () => (session ? getCompetitionSummary(session, playerCompanyId) : null),
    [playerCompanyId, session],
  );

  if (loading && !session) {
    return <div className="game-loading">Loading game…</div>;
  }

  if (error && !session) {
    return <div className="game-error">Error: {error}</div>;
  }

  return (
    <div className="game-shell">
      <TopBar />
      <div className="game-main">
        <LeftSidebar />
        <main className="map-area">
          {sidebarView === 'map' ? (
            <>
              <div className="map-hud">
                <div className="map-hud-title">
                  <span className="map-hud-icon">ISO</span>
                  Isometric Basin World
                </div>
                <div className="map-world-switch" role="tablist" aria-label="World type">
                  {WORLD_TYPES.map((type) => (
                    <button
                      key={type.id}
                      type="button"
                      className={`map-world-button${
                        type.id === worldType ? ' map-world-button--active' : ''
                      }`}
                      aria-pressed={type.id === worldType}
                      onClick={() => setWorldType(type.id)}
                    >
                      {type.label}
                    </button>
                  ))}
                </div>
                <div className="map-hud-legend">
                  <span className="map-legend-chip map-legend-chip--producing">Production sites</span>
                  <span className="map-legend-chip map-legend-chip--drilling">Drill rigs</span>
                  <span className="map-legend-chip map-legend-chip--pipeline">Export terminal</span>
                </div>
                {session && isFunMode(session) && competitionSummary && (
                  <div className="map-objective-strip">
                    <strong>{competitionSummary.title}</strong>
                    <span>{competitionSummary.primaryGoal}</span>
                  </div>
                )}
                <p className="map-hud-hint">Kenney isometric assets · Drag to pan · Wheel to zoom</p>
              </div>
              <BasinMapCanvas
                blocks={mapBlocks}
                playerCompanyId={playerCompanyId}
                selectedBlockId={selectedBlockId}
                companyColors={companyColors}
                worldType={worldType}
                onSelectBlock={handleSelectBlock}
              />
            </>
          ) : (
            <div className="map-placeholder">
              <p>Switch to <strong>Map</strong> in the sidebar to view the basin.</p>
            </div>
          )}
          {error && <div className="toast-error">{error}</div>}
        </main>
        <RightPanel />
      </div>
      <BottomBar />
      <TurnResultsModal />
      <TutorialOverlay />
    </div>
  );
}
