import { useEffect, useMemo, useState } from 'react';
import { useGame } from '../store/GameContext';
import { isFunMode } from '../mode/modeUi';
import {
  FUN_TUTORIAL_STEPS,
  isTutorialComplete,
  markTutorialComplete,
  type TutorialStepId,
} from './tutorialSteps';

function stepIndex(id: TutorialStepId): number {
  return FUN_TUTORIAL_STEPS.findIndex((s) => s.id === id);
}

export function TutorialOverlay() {
  const { session, selectedBlockId, actionQueue, sidebarView, lastTurnResult, playerCompanyId } = useGame();
  const [active, setActive] = useState(false);
  const [stepIdx, setStepIdx] = useState(0);

  useEffect(() => {
    if (session && isFunMode(session) && !isTutorialComplete()) {
      setActive(true);
    }
  }, [session]);

  const step = FUN_TUTORIAL_STEPS[stepIdx];

  useEffect(() => {
    if (!active || !session) return;

    const turn = session.currentTurnNumber;
    const hasProgress =
      session.discoveries.length > 0 ||
      session.producingFields.some((f) => f.companyId === playerCompanyId);

    if (stepIdx <= stepIndex('pick_block') && selectedBlockId) {
      setStepIdx(Math.max(stepIdx, stepIndex('buy_license')));
    }
    if (stepIdx <= stepIndex('buy_license') && actionQueue.some((a) => a.actionType === 'BidForLicense')) {
      setStepIdx(Math.max(stepIdx, stepIndex('commit_turn')));
    }
    if (stepIdx <= stepIndex('commit_turn') && turn > 1) {
      setStepIdx(Math.max(stepIdx, stepIndex('study_block')));
    }
    if (stepIdx <= stepIndex('study_block') && actionQueue.some((a) => a.actionType === 'GeologicalStudy')) {
      setStepIdx(Math.max(stepIdx, stepIndex('drill_well')));
    }
    if (stepIdx <= stepIndex('drill_well') && session.discoveries.length > 0) {
      setStepIdx(Math.max(stepIdx, stepIndex('build_field')));
    }
    if (stepIdx <= stepIndex('build_field') && session.producingFields.length > 0) {
      setStepIdx(Math.max(stepIdx, stepIndex('view_progress')));
    }
    if (stepIdx <= stepIndex('view_progress') && sidebarView === 'company' && session.producingFields.length > 0) {
      setStepIdx(stepIndex('complete'));
    }
    if (lastTurnResult && hasProgress && stepIdx < stepIndex('view_progress')) {
      setStepIdx(Math.max(stepIdx, stepIndex('view_progress')));
    }
  }, [
    active,
    session,
    selectedBlockId,
    actionQueue,
    sidebarView,
    lastTurnResult,
    stepIdx,
    playerCompanyId,
  ]);

  const highlightClass = useMemo(() => {
    if (!step?.highlight) return '';
    return `tutorial-highlight-${step.highlight}`;
  }, [step]);

  if (!active || !session || !step) return null;

  const finish = () => {
    markTutorialComplete();
    setActive(false);
  };

  const next = () => {
    if (stepIdx >= FUN_TUTORIAL_STEPS.length - 1) {
      finish();
      return;
    }
    setStepIdx(stepIdx + 1);
  };

  return (
    <div className={`tutorial-overlay ${highlightClass}`}>
      <div className="tutorial-card">
        <p className="tutorial-kicker">
          Tutorial · Step {stepIdx + 1}/{FUN_TUTORIAL_STEPS.length}
        </p>
        <h3>{step.title}</h3>
        <p>{step.body}</p>
        <div className="tutorial-actions">
          <button type="button" className="btn-secondary" onClick={finish}>
            Skip
          </button>
          <button type="button" className="btn-primary" onClick={next}>
            {stepIdx >= FUN_TUTORIAL_STEPS.length - 1 ? 'Got it' : 'Next'}
          </button>
        </div>
      </div>
    </div>
  );
}
