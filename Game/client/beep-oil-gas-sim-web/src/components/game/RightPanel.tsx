import { useState } from 'react';
import { buildSubmitActionRequest, TurnActionType } from '../../api/actionTypes';
import { useGame } from '../../store/GameContext';
import {
  getActionLabel,
  getCompetitionSummary,
  getRecommendedAction,
  getStageLabel,
  isFunMode,
  isSimpleUi,
  simplifyPotential,
} from '../../mode/modeUi';
import { ActionButton } from '../ui/ActionButton';
import { GameIcon, getActionIcon } from '../ui/GameIcon';
import { PotentialBadge, RiskBadge } from '../ui/RiskBadge';
import { Tooltip } from '../ui/Tooltip';
import { CommandCenterPanel } from './CommandCenterPanel';
import { CompanyCharts } from './CompanyCharts';

function fmtMoney(value: number): string {
  return `$${(value / 1_000_000).toFixed(0)}M`;
}

export function RightPanel() {
  const {
    session,
    mapBlocks,
    selectedBlockId,
    playerCompanyId,
    submitAction,
    loading,
    error,
    sidebarView,
  } = useGame();

  const [bidAmount, setBidAmount] = useState(20_000_000);
  const [actionError, setActionError] = useState<string | null>(null);

  if (!session || !playerCompanyId) {
    return <aside className="right-panel"><p>Loading…</p></aside>;
  }

  const simple = isSimpleUi(session);
  const competitionSummary = getCompetitionSummary(session, playerCompanyId);
  const company = session.companies.find((c) => c.id === playerCompanyId)!;
  const block = selectedBlockId ? mapBlocks.find((b) => b.id === selectedBlockId) : null;
  const discovery = block
    ? session.discoveries.find(
        (d) => d.blockId === block.id && d.companyId === playerCompanyId,
      )
    : null;
  const field = block
    ? session.producingFields.find((f) => f.blockId === block.id && f.companyId === playerCompanyId)
    : null;

  const label = (actionType: string) => getActionLabel(actionType, session);

  const runAction = async (
    actionType: keyof typeof TurnActionType,
    options?: {
      targetAssetId?: string;
      bidAmount?: number;
      parametersJson?: string;
    },
    customLabel?: string,
  ) => {
    setActionError(null);
    try {
      const actionLabel =
        customLabel ?? `${label(actionType)}${block ? ` — ${block.blockCode}` : ''}`;
      const request = buildSubmitActionRequest(playerCompanyId, TurnActionType[actionType], {
        targetBlockId: block?.id,
        targetAssetId: options?.targetAssetId,
        bidAmount: options?.bidAmount,
        parametersJson: options?.parametersJson,
      });
      await submitAction(request, actionLabel);
    } catch (e) {
      setActionError(e instanceof Error ? e.message : String(e));
    }
  };

  if (sidebarView === 'company') {
    return (
      <aside className="right-panel">
        <h2>{simple ? 'Your Company' : 'Company Dashboard'}</h2>
        <dl className="detail-list">
          <dt>Cash</dt><dd>{fmtMoney(company.cash)}</dd>
          <dt>Debt</dt><dd>{fmtMoney(company.debt)}</dd>
          <dt>{simple ? 'Score' : 'Company Value'}</dt><dd>{fmtMoney(company.companyValue)}</dd>
          {!simple && (
            <>
              <dt>Production</dt><dd>{company.productionBoePerDay.toLocaleString()} boe/d</dd>
            </>
          )}
          {company.productionBoePerDay > 0 && simple && (
            <>
              <dt>Production</dt><dd>{company.productionBoePerDay.toLocaleString()} boe/d</dd>
            </>
          )}
          <dt>Rank</dt><dd>#{company.rank}</dd>
        </dl>
        <CompanyCharts sessionId={session.id} companyId={playerCompanyId} />
      </aside>
    );
  }

  if (sidebarView === 'assets') {
    return (
      <aside className="right-panel">
        <h2>Assets</h2>
        <section>
          <h3>Discoveries ({session.discoveries.length})</h3>
          {session.discoveries.length === 0 && <p className="muted">No discoveries yet.</p>}
          {session.discoveries.map((d) => (
            <div key={d.id} className="asset-card">
              <strong>{d.name}</strong>
              <span>
                {simple ? simplifyPotential(d.estimatedMidVolumeMmboe / 100) : d.sizeClass}
                {' · '}
                {d.estimatedMidVolumeMmboe.toFixed(0)} MMbbl
              </span>
            </div>
          ))}
        </section>
        <section>
          <h3>{simple ? 'Fields' : 'Producing Fields'} ({session.producingFields.length})</h3>
          {session.producingFields.length === 0 && <p className="muted">No fields on production.</p>}
          {session.producingFields.map((f) => (
            <div key={f.id} className="asset-card">
              <strong>{f.name}</strong>
              <span>{f.currentProductionBoePerDay.toLocaleString()} boe/d</span>
            </div>
          ))}
        </section>
      </aside>
    );
  }

  if (sidebarView === 'finance') {
    const showHedge = session.modeProfile?.enableHedging ?? false;
    const showAdvanced = session.modeProfile?.enableAdvancedFinance ?? false;

    return (
      <aside className="right-panel">
        <h2>{simple ? 'Money' : 'Finance'}</h2>
        <p className="muted">Cash: {fmtMoney(company.cash)} · Debt: {fmtMoney(company.debt)}</p>
        <div className="action-buttons">
          <ActionButton
            actionType="TakeDebt"
            label={`${label('TakeDebt')} ($100M)`}
            disabled={loading}
            onClick={() => void runAction('TakeDebt', { bidAmount: 100_000_000 }, `${label('TakeDebt')} $100M`)}
          />
          <ActionButton
            actionType="RepayDebt"
            label={`${label('RepayDebt')} ($50M)`}
            disabled={loading}
            onClick={() => void runAction('RepayDebt', { bidAmount: 50_000_000 }, `${label('RepayDebt')} $50M`)}
          />
          {showHedge && (
            <ActionButton
              actionType="HedgeProduction"
              label={`${label('HedgeProduction')} (50%)`}
              disabled={loading}
              onClick={() => void runAction('HedgeProduction', { bidAmount: 50 }, `${label('HedgeProduction')} 50%`)}
            />
          )}
        </div>
        {simple && !showAdvanced && (
          <p className="muted hint">Fun Mode keeps finance simple — focus on finding oil!</p>
        )}
        {(actionError || error) && <p className="error-text">{actionError ?? error}</p>}
      </aside>
    );
  }

  if (sidebarView === 'command') {
    return <CommandCenterPanel />;
  }

  if (sidebarView === 'leaderboard') {
    return (
      <aside className="right-panel">
        <h2>{competitionSummary.rivalCount > 0 ? 'Leaderboard' : 'Score Chase'}</h2>
        {isFunMode(session) && (
          <section className="objective-panel">
            <p className="objective-kicker">{competitionSummary.scoreboardLabel}</p>
            <strong>{competitionSummary.title}</strong>
            <span>{competitionSummary.primaryGoal}</span>
          </section>
        )}
        {[...session.companies].sort((a, b) => a.rank - b.rank).map((c) => (
          <div key={c.id} className={`leader-row ${c.id === playerCompanyId ? 'you' : ''}`}>
            <span>#{c.rank}</span>
            <strong>{c.name}</strong>
            <span>{fmtMoney(c.companyValue)}</span>
          </div>
        ))}
      </aside>
    );
  }

  if (!block) {
    return (
      <aside className="right-panel">
        <h2>Basin Map</h2>
        {isFunMode(session) && (
          <section className="objective-panel">
            <p className="objective-kicker">What you are playing against</p>
            <strong>{competitionSummary.title}</strong>
            <span>{competitionSummary.pressure}</span>
          </section>
        )}
        <p className="muted">Select a license block on the map to view details and take actions.</p>
        <div className="legend">
          <h3>Legend</h3>
          <span className="legend-item unlicensed"><GameIcon name="license" size={14} /> Available</span>
          <span className="legend-item licensed"><GameIcon name="study" size={14} /> Licensed</span>
          <span className="legend-item discovery"><GameIcon name="discovery" size={14} /> Discovery</span>
          <span className="legend-item producing"><GameIcon name="production" size={14} /> Producing</span>
        </div>
      </aside>
    );
  }

  const isOwned = block.ownerCompanyId === playerCompanyId;
  const isUnlicensed = block.stage === 'Unlicensed';
  const recommended = getRecommendedAction(block, discovery ?? undefined, field ?? undefined, isOwned, session);

  return (
    <aside className="right-panel">
      <h2>{block.name}</h2>
      <p className="block-code">{block.blockCode}</p>
      <dl className="detail-list">
        <dt>{simple ? 'Status' : 'Stage'}</dt>
        <dd>{getStageLabel(block.stage, session)}</dd>
        <dt>Owner</dt>
        <dd>{isOwned ? 'You' : block.ownerCompanyId ? 'Competitor' : 'Open'}</dd>
        <dt>Risk</dt>
        <dd>
          <RiskBadge rating={block.publicRiskRating} simple={simple} />
        </dd>
        {block.estimatedChanceOfSuccess != null && (
          <>
            <dt>{simple ? 'Potential' : 'Chance of Success'}</dt>
            <dd>
              {simple ? (
                <PotentialBadge level={simplifyPotential(block.estimatedChanceOfSuccess)} />
              ) : (
                <Tooltip label="Estimated probability of a commercial discovery based on your studies.">
                  <span>{(block.estimatedChanceOfSuccess * 100).toFixed(0)}%</span>
                </Tooltip>
              )}
            </dd>
          </>
        )}
        {!simple && (
          <>
            <dt>Geology Hint</dt>
            <dd className="hint">{block.publicGeologyHint}</dd>
          </>
        )}
      </dl>

      {recommended && (
        <section className="sub-panel recommended">
          <h3>Recommended</h3>
          <p className="recommended-row">
            <GameIcon name={getActionIcon(recommended.actionType)} size={18} />
            <strong>{label(recommended.actionType)}</strong>
          </p>
          <p className="muted">{recommended.reason}</p>
        </section>
      )}

      {discovery && (
        <section className="sub-panel">
          <h3>{discovery.name}</h3>
          <p>
            {simple ? 'Commercial find' : discovery.sizeClass}
            {' · '}
            {discovery.estimatedMidVolumeMmboe.toFixed(0)} MMbbl
            {!simple && ` · ${discovery.confidence.toFixed(0)}% conf.`}
          </p>
        </section>
      )}

      {field && (
        <section className="sub-panel">
          <h3>{field.name}</h3>
          <p>{field.currentProductionBoePerDay.toLocaleString()} boe/d · {getStageLabel(field.stage, session)}</p>
        </section>
      )}

      <h3>Actions</h3>
      <div className="action-buttons">
        {isUnlicensed && (
          <>
            <label>
              {simple ? 'Offer' : 'Bid amount'}
              <input
                type="number"
                value={bidAmount}
                step={5_000_000}
                onChange={(e) => setBidAmount(Number(e.target.value))}
              />
            </label>
            <ActionButton
              actionType="BidForLicense"
              label={label('BidForLicense')}
              disabled={loading}
              onClick={() => void runAction('BidForLicense', { bidAmount }, `${label('BidForLicense')} ${block.blockCode} ${fmtMoney(bidAmount)}`)}
            />
          </>
        )}
        {isOwned && !discovery && !field && (
          <>
            <ActionButton actionType="GeologicalStudy" label={label('GeologicalStudy')} disabled={loading} onClick={() => void runAction('GeologicalStudy')} />
            <ActionButton actionType="Acquire2DSeismic" label={label('Acquire2DSeismic')} disabled={loading} onClick={() => void runAction('Acquire2DSeismic')} />
            <ActionButton actionType="DrillExplorationWell" label={label('DrillExplorationWell')} disabled={loading} onClick={() => void runAction('DrillExplorationWell')} />
          </>
        )}
        {discovery && !field && (
          <>
            <ActionButton
              actionType="DrillAppraisalWell"
              label={label('DrillAppraisalWell')}
              disabled={loading}
              onClick={() => void runAction('DrillAppraisalWell', { targetAssetId: discovery.id })}
            />
            <ActionButton
              actionType="ApproveDevelopment"
              label={simple ? label('ApproveDevelopment') : 'Approve Standard Development'}
              disabled={loading}
              onClick={() => void runAction('ApproveDevelopment', { targetAssetId: discovery.id, parametersJson: 'Standard' })}
            />
          </>
        )}
        {field && field.stage !== 'Abandoned' && (
          <>
            <ActionButton actionType="OptimizeField" label={label('OptimizeField')} disabled={loading} onClick={() => void runAction('OptimizeField', { targetAssetId: field.id })} />
            <ActionButton actionType="AbandonField" label={label('AbandonField')} disabled={loading} onClick={() => void runAction('AbandonField', { targetAssetId: field.id })} />
          </>
        )}
      </div>
      {(actionError || error) && <p className="error-text">{actionError ?? error}</p>}
    </aside>
  );
}
