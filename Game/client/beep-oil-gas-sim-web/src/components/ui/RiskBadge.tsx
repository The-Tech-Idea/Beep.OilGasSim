import { simplifyRisk } from '../../mode/modeUi';

type RiskLevel = 'Low' | 'Medium' | 'High';

interface RiskBadgeProps {
  rating: string;
  simple?: boolean;
}

function levelFromRating(rating: string): RiskLevel {
  return simplifyRisk(rating);
}

export function RiskBadge({ rating, simple = false }: RiskBadgeProps) {
  const level = levelFromRating(rating);
  const label = simple ? level : rating;

  return (
    <span className={`risk-badge risk-${level.toLowerCase()}`} title={`Surface risk: ${rating}`}>
      {label}
    </span>
  );
}

export function PotentialBadge({ level }: { level: 'Low' | 'Medium' | 'High' }) {
  return (
    <span className={`risk-badge potential-${level.toLowerCase()}`} title={`Prospect potential: ${level}`}>
      {level}
    </span>
  );
}
