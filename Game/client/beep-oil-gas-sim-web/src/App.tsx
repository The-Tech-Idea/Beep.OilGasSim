import { useState } from 'react';
import { GameProvider } from './store/GameContext';
import { GameShell } from './components/game/GameShell';
import { LobbyScreen } from './components/lobby/LobbyScreen';
import { MultiplayerLobby } from './components/lobby/MultiplayerLobby';
import { loadPlayerIdentity } from './realtime/GameHubClient';

type LaunchState =
  | { phase: 'lobby' }
  | {
      phase: 'multiplayer-lobby';
      sessionId: string;
      companyId: string;
      playerId: string;
      displayName: string;
    }
  | { phase: 'game'; sessionId: string; companyId: string };

export default function App() {
  const [launch, setLaunch] = useState<LaunchState>({ phase: 'lobby' });

  if (launch.phase === 'lobby') {
    return (
      <LobbyScreen
        onSoloStarted={(sessionId) => {
          const identity = loadPlayerIdentity(sessionId);
          setLaunch({
            phase: 'game',
            sessionId,
            companyId: identity?.companyId ?? '',
          });
        }}
        onMultiplayerLobby={(payload) =>
          setLaunch({ phase: 'multiplayer-lobby', ...payload })
        }
      />
    );
  }

  if (launch.phase === 'multiplayer-lobby') {
    return (
      <MultiplayerLobby
        sessionId={launch.sessionId}
        companyId={launch.companyId}
        playerId={launch.playerId}
        displayName={launch.displayName}
        onLeave={() => setLaunch({ phase: 'lobby' })}
        onGameStarted={() =>
          setLaunch({
            phase: 'game',
            sessionId: launch.sessionId,
            companyId: launch.companyId,
          })
        }
      />
    );
  }

  return (
    <GameProvider sessionId={launch.sessionId} companyId={launch.companyId}>
      <GameShell />
    </GameProvider>
  );
}
