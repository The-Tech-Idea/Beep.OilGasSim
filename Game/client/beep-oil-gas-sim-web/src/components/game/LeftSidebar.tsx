import type { SidebarView } from '../../api/types';
import { useGame } from '../../store/GameContext';
import { isSimpleUi } from '../../mode/modeUi';
import { GameIcon, type GameIconName } from '../ui/GameIcon';

const ITEMS: { id: SidebarView; label: string; funLabel: string; icon: GameIconName }[] = [
  { id: 'map', label: 'Map', funLabel: 'Map', icon: 'map' },
  { id: 'company', label: 'Company', funLabel: 'Company', icon: 'company' },
  { id: 'assets', label: 'Assets', funLabel: 'Assets', icon: 'assets' },
  { id: 'finance', label: 'Finance', funLabel: 'Money', icon: 'finance' },
  { id: 'leaderboard', label: 'Leaderboard', funLabel: 'Rankings', icon: 'leaderboard' },
  { id: 'command', label: 'Command Center', funLabel: 'Advisors', icon: 'study' },
];

export function LeftSidebar() {
  const { sidebarView, setSidebarView, session } = useGame();
  const simple = session ? isSimpleUi(session) : false;

  return (
    <nav className="left-sidebar">
      {ITEMS.map((item) => (
        <button
          key={item.id}
          type="button"
          className={`sidebar-item ${sidebarView === item.id ? 'active' : ''}`}
          onClick={() => setSidebarView(item.id)}
          title={simple ? item.funLabel : item.label}
        >
          <GameIcon name={item.icon} size={22} className="sidebar-icon-img" />
          <span className="sidebar-label">{simple ? item.funLabel : item.label}</span>
        </button>
      ))}
    </nav>
  );
}
