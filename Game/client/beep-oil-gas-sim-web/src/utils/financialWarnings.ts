import type { CompanyDto, GameSession } from '../api/types';

export type FinancialWarningLevel = 'info' | 'warn' | 'danger';

export interface FinancialWarning {
  level: FinancialWarningLevel;
  message: string;
}

export function getFinancialWarnings(
  company: CompanyDto,
  session: GameSession,
  projectedSpend = 0,
): FinancialWarning[] {
  const warnings: FinancialWarning[] = [];
  const maxDebt = session.modeProfile?.maxDebt ?? 500_000_000;
  const cashAfter = company.cash - projectedSpend;

  if (cashAfter < 0) {
    warnings.push({
      level: 'danger',
      message: 'Not enough cash for queued actions — borrow or remove actions.',
    });
  } else if (cashAfter < 50_000_000) {
    warnings.push({
      level: 'warn',
      message: 'Cash is getting tight after this turn.',
    });
  }

  if (company.debt >= maxDebt * 0.9) {
    warnings.push({
      level: 'danger',
      message: 'Near maximum debt limit.',
    });
  } else if (company.debt >= maxDebt * 0.65) {
    warnings.push({
      level: 'warn',
      message: 'Debt is elevated — interest will eat into profits.',
    });
  }

  if (company.debt > company.cash && company.productionBoePerDay === 0) {
    warnings.push({
      level: 'warn',
      message: 'No production yet while carrying debt — prioritize a discovery.',
    });
  }

  return warnings;
}
