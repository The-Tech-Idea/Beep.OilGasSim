import { describe, expect, it } from 'vitest';
import type { GameSession } from '../api/types';
import { getCompetitionSummary } from './modeUi';

const makeSession = (overrides: Partial<GameSession> = {}): GameSession => ({
  id: 'session-1',
  name: 'Desert Frontier',
  scenarioId: 'desert-frontier',
  gameplayMode: 'Fun',
  state: 'Planning',
  currentTurnNumber: 1,
  totalTurns: 12,
  oilPrice: 75,
  actionSlotsPerTurn: 2,
  isMultiplayer: false,
  modeProfile: {
    uiComplexityLevel: 'Simple',
    aiAssistanceLevel: 'Guided',
    enableHedging: false,
    enableAdvancedFinance: false,
    startingCash: 700_000_000,
    maxDebt: 300_000_000,
  },
  companies: [
    {
      id: 'player-company',
      name: 'Beep Energy',
      colorHex: '#2563eb',
      cash: 700_000_000,
      debt: 0,
      companyValue: 700_000_000,
      rank: 1,
      productionBoePerDay: 0,
    },
  ],
  lobbyPlayers: [],
  discoveries: [],
  producingFields: [],
  pendingActions: [],
  ...overrides,
});

describe('getCompetitionSummary', () => {
  it('explains solo Fun Mode as a clock and company-value score chase', () => {
    const summary = getCompetitionSummary(makeSession(), 'player-company');

    expect(summary.title).toBe('Beat the 12-turn clock');
    expect(summary.primaryGoal).toContain('Finish with the highest company value you can');
    expect(summary.pressure).toContain('oil price');
    expect(summary.rivalCount).toBe(0);
  });

  it('explains multiplayer as a company-value race against other companies', () => {
    const summary = getCompetitionSummary(
      makeSession({
        isMultiplayer: true,
        companies: [
          {
            id: 'player-company',
            name: 'Beep Energy',
            colorHex: '#2563eb',
            cash: 650_000_000,
            debt: 0,
            companyValue: 650_000_000,
            rank: 2,
            productionBoePerDay: 0,
          },
          {
            id: 'rival-company',
            name: 'Delta Oil',
            colorHex: '#f97316',
            cash: 710_000_000,
            debt: 0,
            companyValue: 710_000_000,
            rank: 1,
            productionBoePerDay: 0,
          },
        ],
      }),
      'player-company',
    );

    expect(summary.title).toBe('Race 1 rival company');
    expect(summary.primaryGoal).toContain('Delta Oil');
    expect(summary.gapText).toContain('$60M');
    expect(summary.rivalCount).toBe(1);
  });
});
