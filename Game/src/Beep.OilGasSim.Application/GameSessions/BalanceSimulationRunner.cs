using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Domain.Blocks;
using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;

namespace Beep.OilGasSim.Application.GameSessions;

public sealed class BalanceSimulationReport
{
    public string ModeProfileId { get; set; } = "";
    public int GamesRun { get; set; }
    public double DiscoveryRate { get; set; }
    public double DryHoleRate { get; set; }
    public double ProductionReachRate { get; set; }
    public double FinancialDistressRate { get; set; }
    public decimal AverageEndingCash { get; set; }
    public decimal AverageCompanyValue { get; set; }
    public decimal AverageDebt { get; set; }
    public double AverageProducingFields { get; set; }
    public double AverageProductionBoePerDay { get; set; }
}

public sealed class BalanceSimulationRunner
{
    private readonly IGameSessionService _sessions;

    public BalanceSimulationRunner(IGameSessionService sessions) => _sessions = sessions;

    public async Task<BalanceSimulationReport> RunAsync(
        string modeProfileId,
        int gameCount,
        CancellationToken cancellationToken = default)
    {
        if (gameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gameCount));
        }

        var discoveries = 0;
        var dryHoles = 0;
        var successfulDrills = 0;
        var productionReached = 0;
        var distress = 0;
        decimal totalCash = 0;
        decimal totalValue = 0;
        decimal totalDebt = 0;
        double totalFields = 0;
        double totalProduction = 0;

        for (var i = 0; i < gameCount; i++)
        {
            var aggregate = await _sessions.CreateSessionAsync(new CreateGameSessionRequest
            {
                ScenarioId = "desert-frontier",
                GameplayModeProfileId = modeProfileId,
                CompanyName = $"Sim Co {i + 1}",
                PlayerDisplayName = "Bot"
            }, cancellationToken);

            await _sessions.StartSessionAsync(aggregate.Session.Id, cancellationToken);
            var sessionId = aggregate.Session.Id;
            var companyId = aggregate.Session.Companies[0].Id;

            while (true)
            {
                aggregate = (await _sessions.GetSessionAsync(sessionId, cancellationToken))!;
                if (aggregate.Session.State == GameSessionState.Completed)
                {
                    break;
                }

                await ApplyBotTurnAsync(sessionId, companyId, aggregate, cancellationToken);
                await _sessions.CommitTurnAsync(sessionId, companyId, cancellationToken);
            }

            aggregate = (await _sessions.GetSessionAsync(sessionId, cancellationToken))!;
            var company = aggregate.Session.Companies[0];

            if (aggregate.Discoveries.Any(d => d.CompanyId == companyId))
            {
                discoveries++;
            }

            foreach (var evt in aggregate.TurnResults.SelectMany(r => r.Events).Where(e => e.CompanyId == companyId))
            {
                if (evt.Category != "Exploration") continue;
                if (evt.Headline.Contains("Dry", StringComparison.OrdinalIgnoreCase))
                {
                    dryHoles++;
                }
                else if (evt.Headline.Contains("Discovery", StringComparison.OrdinalIgnoreCase))
                {
                    successfulDrills++;
                }
            }

            if (aggregate.ProducingFields.Any(f => f.CompanyId == companyId))
            {
                productionReached++;
            }

            var hadDistress = aggregate.TurnResults
                .SelectMany(r => r.CompanySummaries)
                .Any(s => s.CompanyId == companyId && s.EndingCash < 0);
            if (hadDistress)
            {
                distress++;
            }

            totalCash += company.Finance.Cash;
            totalValue += company.CompanyValue;
            totalDebt += company.Finance.Debt;
            totalFields += aggregate.ProducingFields.Count(f => f.CompanyId == companyId);
            totalProduction += company.TotalProductionBoePerDay;
        }

        var drillOutcomes = dryHoles + successfulDrills;
        return new BalanceSimulationReport
        {
            ModeProfileId = modeProfileId,
            GamesRun = gameCount,
            DiscoveryRate = discoveries / (double)gameCount,
            DryHoleRate = drillOutcomes > 0 ? dryHoles / (double)drillOutcomes : 0,
            ProductionReachRate = productionReached / (double)gameCount,
            FinancialDistressRate = distress / (double)gameCount,
            AverageEndingCash = totalCash / gameCount,
            AverageCompanyValue = totalValue / gameCount,
            AverageDebt = totalDebt / gameCount,
            AverageProducingFields = totalFields / gameCount,
            AverageProductionBoePerDay = totalProduction / gameCount
        };
    }

    private async Task ApplyBotTurnAsync(
        Guid sessionId,
        Guid companyId,
        GameSessionAggregate aggregate,
        CancellationToken cancellationToken)
    {
        var session = aggregate.Session;
        var slots = session.ModeProfile.ActionSlotsPerTurn;
        var queued = aggregate.PendingActions.Count(a =>
            a.CompanyId == companyId && a.TurnNumber == session.CurrentTurnNumber);

        var discovery = aggregate.Discoveries.FirstOrDefault(d => d.CompanyId == companyId &&
            d.Stage is not AssetStage.Producing and not AssetStage.Abandoned);
        var field = aggregate.ProducingFields.FirstOrDefault(f => f.CompanyId == companyId &&
            f.Stage is not AssetStage.Abandoned);

        if (discovery != null && field == null)
        {
            queued += await TryQueueAsync(sessionId, queued, slots, new SubmitActionRequest
            {
                CompanyId = companyId,
                ActionType = TurnActionType.ApproveDevelopment,
                TargetAssetId = discovery.Id,
                ParametersJson = "Standard"
            }, cancellationToken);

            if (queued < slots)
            {
                queued += await TryQueueAsync(sessionId, queued, slots, new SubmitActionRequest
                {
                    CompanyId = companyId,
                    ActionType = TurnActionType.DrillAppraisalWell,
                    TargetAssetId = discovery.Id
                }, cancellationToken);
            }
        }

        if (field != null && queued < slots)
        {
            queued += await TryQueueAsync(sessionId, queued, slots, new SubmitActionRequest
            {
                CompanyId = companyId,
                ActionType = TurnActionType.OptimizeField,
                TargetAssetId = field.Id
            }, cancellationToken);
        }

        var activeBlock = session.Basin.Blocks
            .Where(b => b.OwnerCompanyId == companyId && b.Stage != AssetStage.DryHole)
            .OrderBy(b => b.Stage)
            .FirstOrDefault(b => !aggregate.Discoveries.Any(d => d.BlockId == b.Id && d.Stage == AssetStage.DryHole));

        if (activeBlock != null && discovery == null && queued < slots)
        {
            var action = activeBlock.Stage switch
            {
                AssetStage.Licensed => new SubmitActionRequest
                {
                    CompanyId = companyId,
                    ActionType = TurnActionType.GeologicalStudy,
                    TargetBlockId = activeBlock.Id
                },
                AssetStage.Studied => new SubmitActionRequest
                {
                    CompanyId = companyId,
                    ActionType = TurnActionType.Acquire2DSeismic,
                    TargetBlockId = activeBlock.Id
                },
                AssetStage.SeismicEvaluated or AssetStage.ExplorationDrilling => new SubmitActionRequest
                {
                    CompanyId = companyId,
                    ActionType = TurnActionType.DrillExplorationWell,
                    TargetBlockId = activeBlock.Id
                },
                _ => null
            };

            if (action != null)
            {
                queued += await TryQueueAsync(sessionId, queued, slots, action, cancellationToken);
            }
        }

        var needsLicense = !session.Basin.Blocks.Any(b =>
            b.OwnerCompanyId == companyId &&
            b.Stage is not AssetStage.DryHole and not AssetStage.Abandoned);

        if (needsLicense && queued < slots)
        {
            var company = session.Companies.First(c => c.Id == companyId);
            if (company.Finance.Cash < 30_000_000m && company.Finance.Debt < session.ModeProfile.MaxDebt - 100_000_000m)
            {
                queued += await TryQueueAsync(sessionId, queued, slots, new SubmitActionRequest
                {
                    CompanyId = companyId,
                    ActionType = TurnActionType.TakeDebt,
                    BidAmount = 100_000_000m
                }, cancellationToken);
            }

            var target = session.Basin.Blocks
                .Where(b => b.Stage == AssetStage.Unlicensed)
                .OrderBy(b => b.PublicData.PublicRiskRating)
                .FirstOrDefault();

            if (target != null)
            {
                await TryQueueAsync(sessionId, queued, slots, new SubmitActionRequest
                {
                    CompanyId = companyId,
                    ActionType = TurnActionType.BidForLicense,
                    TargetBlockId = target.Id,
                    BidAmount = 15_000_000m
                }, cancellationToken);
            }
        }
    }

    private async Task<int> TryQueueAsync(
        Guid sessionId,
        int actionsQueued,
        int slots,
        SubmitActionRequest request,
        CancellationToken cancellationToken)
    {
        if (actionsQueued >= slots) return 0;
        try
        {
            await _sessions.SubmitActionAsync(sessionId, request, cancellationToken);
            return 1;
        }
        catch
        {
            return 0;
        }
    }
}
