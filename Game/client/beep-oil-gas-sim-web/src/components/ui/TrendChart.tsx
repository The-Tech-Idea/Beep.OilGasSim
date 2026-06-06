interface TrendChartProps {
  title: string;
  points: Array<{ turnNumber: number; value: number }>;
  formatValue?: (value: number) => string;
  color?: string;
  height?: number;
}

export function TrendChart({
  title,
  points,
  formatValue = (v) => v.toLocaleString(undefined, { maximumFractionDigits: 0 }),
  color = '#2563eb',
  height = 72,
}: TrendChartProps) {
  if (points.length === 0) {
    return (
      <div className="trend-chart empty">
        <h4>{title}</h4>
        <p className="muted">No history yet.</p>
      </div>
    );
  }

  const width = 280;
  const padding = 8;
  const values = points.map((p) => p.value);
  const min = Math.min(...values);
  const max = Math.max(...values);
  const range = max - min || 1;

  const coords = points.map((p, i) => {
    const x = padding + (i / Math.max(points.length - 1, 1)) * (width - padding * 2);
    const y = padding + (1 - (p.value - min) / range) * (height - padding * 2);
    return { x, y, ...p };
  });

  const polyline = coords.map((c) => `${c.x},${c.y}`).join(' ');
  const latest = points[points.length - 1];

  return (
    <div className="trend-chart">
      <div className="trend-chart-header">
        <h4>{title}</h4>
        <span>{formatValue(latest.value)}</span>
      </div>
      <svg viewBox={`0 0 ${width} ${height}`} className="trend-chart-svg" aria-hidden>
        <polyline fill="none" stroke={color} strokeWidth="2" points={polyline} />
        {coords.map((c) => (
          <circle key={c.turnNumber} cx={c.x} cy={c.y} r="2.5" fill={color} />
        ))}
      </svg>
    </div>
  );
}
