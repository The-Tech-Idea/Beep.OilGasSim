export type TutorialStepId =
  | 'welcome'
  | 'pick_block'
  | 'buy_license'
  | 'commit_turn'
  | 'study_block'
  | 'drill_well'
  | 'build_field'
  | 'view_progress'
  | 'complete';

export interface TutorialStep {
  id: TutorialStepId;
  title: string;
  body: string;
  highlight?: 'map' | 'actions' | 'commit' | 'company';
}

export const TUTORIAL_STORAGE_KEY = 'ogs_fun_tutorial_done';

export const FUN_TUTORIAL_STEPS: TutorialStep[] = [
  {
    id: 'welcome',
    title: 'Welcome to Desert Frontier',
    body: 'Solo Fun Mode is a 12-turn score chase. You are playing against time, cash burn, drilling risk, and oil price swings. Find oil, build fields, and finish with the highest company value you can.',
    highlight: 'map',
  },
  {
    id: 'pick_block',
    title: 'Pick a promising block',
    body: 'Click a highlighted block on the map. Lower-risk blocks are good first picks in Fun Mode.',
    highlight: 'map',
  },
  {
    id: 'buy_license',
    title: 'Buy a license',
    body: 'Use Buy License in the actions panel. This gives you the right to explore that block.',
    highlight: 'actions',
  },
  {
    id: 'commit_turn',
    title: 'Commit your turn',
    body: 'When your action queue is ready, press Commit Turn at the bottom. The server resolves everyone’s actions.',
    highlight: 'commit',
  },
  {
    id: 'study_block',
    title: 'Study your block',
    body: 'After you own a block, run Study Block to improve your chance estimate before drilling.',
    highlight: 'actions',
  },
  {
    id: 'drill_well',
    title: 'Drill for oil',
    body: 'Queue Drill Well on a block you own. Fun Mode gives you friendlier odds — keep exploring!',
    highlight: 'actions',
  },
  {
    id: 'build_field',
    title: 'Build your field',
    body: 'When you find a discovery, choose Build Field to start development and future production.',
    highlight: 'actions',
  },
  {
    id: 'view_progress',
    title: 'Track your progress',
    body: 'Open Company or Rankings to see cash, production, and your company-value score.',
    highlight: 'company',
  },
  {
    id: 'complete',
    title: 'You are ready',
    body: 'Use Advisors anytime for hints. Have fun — and may your wells flow!',
  },
];

export function isTutorialComplete(): boolean {
  return localStorage.getItem(TUTORIAL_STORAGE_KEY) === '1';
}

export function markTutorialComplete(): void {
  localStorage.setItem(TUTORIAL_STORAGE_KEY, '1');
}
