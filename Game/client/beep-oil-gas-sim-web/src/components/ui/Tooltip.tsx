import type { ReactNode } from 'react';

interface TooltipProps {
  label: string;
  children: ReactNode;
}

export function Tooltip({ label, children }: TooltipProps) {
  return (
    <span className="tooltip-wrap" title={label} aria-label={label}>
      {children}
      <span className="tooltip-bubble" role="tooltip">
        {label}
      </span>
    </span>
  );
}
