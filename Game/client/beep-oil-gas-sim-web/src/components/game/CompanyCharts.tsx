import { useEffect, useState } from 'react';
import { api } from '../../api/ApiClient';
import type { SessionHistoryResponse } from '../../api/types';
import { TrendChart } from '../ui/TrendChart';

function fmtMoney(value: number): string {
  return `$${(value / 1_000_000).toFixed(0)}M`;
}

export function CompanyCharts({
  sessionId,
  companyId,
}: {
  sessionId: string;
  companyId: string;
}) {
  const [history, setHistory] = useState<SessionHistoryResponse | null>(null);

  useEffect(() => {
    void api.getHistory(sessionId, companyId).then(setHistory).catch(() => setHistory(null));
  }, [sessionId, companyId]);

  if (!history) return null;

  return (
    <section className="company-charts">
      <TrendChart
        title="Oil price"
        color="#fbbf24"
        points={history.oilPrice.map((p) => ({ turnNumber: p.turnNumber, value: Number(p.value) }))}
        formatValue={(v) => `$${v.toFixed(0)}/bbl`}
      />
      <TrendChart
        title="Production"
        color="#22c55e"
        points={history.productionBoePerDay.map((p) => ({
          turnNumber: p.turnNumber,
          value: Number(p.value),
        }))}
        formatValue={(v) => `${v.toLocaleString()} boe/d`}
      />
      <TrendChart
        title="Company value"
        color="#818cf8"
        points={history.companyValue.map((p) => ({ turnNumber: p.turnNumber, value: Number(p.value) }))}
        formatValue={fmtMoney}
      />
    </section>
  );
}
