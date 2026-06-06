import { GameIcon, getActionIcon } from './GameIcon';

interface ActionButtonProps {
  actionType: string;
  label: string;
  disabled?: boolean;
  onClick: () => void;
}

export function ActionButton({ actionType, label, disabled, onClick }: ActionButtonProps) {
  return (
    <button type="button" className="action-btn" disabled={disabled} onClick={onClick}>
      <GameIcon name={getActionIcon(actionType)} size={18} />
      <span>{label}</span>
    </button>
  );
}
