using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;
using Beep.OilGasSim.Simulation.Abandonment;
using Beep.OilGasSim.Simulation.Appraisal;
using Beep.OilGasSim.Simulation.Auction;
using Beep.OilGasSim.Simulation.Development;
using Beep.OilGasSim.Simulation.Economy;
using Beep.OilGasSim.Simulation.Exploration;
using Beep.OilGasSim.Simulation.Market;
using Beep.OilGasSim.Simulation.Production;
using Beep.OilGasSim.Simulation.Randomness;
using Beep.OilGasSim.Simulation.Scoring;

namespace Beep.OilGasSim.Simulation.TurnEngine;

public interface ITurnEngine
{
    TurnResult ResolveTurn(GameSessionAggregate aggregate, int turnNumber);
}

public sealed class TurnEngine : ITurnEngine
{
    private readonly IActionValidator _actionValidator;
    private readonly IAuctionResolver _auctionResolver;
    private readonly IExplorationResolver _explorationResolver;
    private readonly IAppraisalResolver _appraisalResolver;
    private readonly IDevelopmentResolver _developmentResolver;
    private readonly IProductionResolver _productionResolver;
    private readonly IMarketResolver _marketResolver;
    private readonly IEconomyResolver _economyResolver;
    private readonly IAbandonmentResolver _abandonmentResolver;
    private readonly IScoringService _scoringService;
    private readonly IGameRandomFactory _randomFactory;

    public TurnEngine(
        IActionValidator actionValidator,
        IAuctionResolver auctionResolver,
        IExplorationResolver explorationResolver,
        IAppraisalResolver appraisalResolver,
        IDevelopmentResolver developmentResolver,
        IProductionResolver productionResolver,
        IMarketResolver marketResolver,
        IEconomyResolver economyResolver,
        IAbandonmentResolver abandonmentResolver,
        IScoringService scoringService,
        IGameRandomFactory randomFactory)
    {
        _actionValidator = actionValidator;
        _auctionResolver = auctionResolver;
        _explorationResolver = explorationResolver;
        _appraisalResolver = appraisalResolver;
        _developmentResolver = developmentResolver;
        _productionResolver = productionResolver;
        _marketResolver = marketResolver;
        _economyResolver = economyResolver;
        _abandonmentResolver = abandonmentResolver;
        _scoringService = scoringService;
        _randomFactory = randomFactory;
    }

    public TurnResult ResolveTurn(GameSessionAggregate aggregate, int turnNumber)
    {
        var session = aggregate.Session;

        var context = new TurnResolutionContext
        {
            Aggregate = aggregate,
            TurnNumber = turnNumber,
            GameplayModeProfile = session.ModeProfile,
            BalanceProfile = session.BalanceProfile,
            Actions = aggregate.PendingActions.Where(a => a.TurnNumber == turnNumber).ToList(),
            Result = new TurnResult
            {
                Id = Guid.NewGuid(),
                GameSessionId = session.Id,
                TurnNumber = turnNumber
            }
        };

        foreach (var action in context.Actions)
        {
            var validation = _actionValidator.Validate(action, aggregate);
            if (!validation.IsValid)
            {
                action.Status = TurnActionStatus.Failed;
                context.Result.Events.Add(new TurnEventReport
                {
                    CompanyId = action.CompanyId,
                    Category = "Validation",
                    Headline = $"Action {action.ActionType} failed validation.",
                    Detail = string.Join("; ", validation.Errors),
                    IsPublic = false
                });
            }
        }

        session.State = GameSessionState.Resolving;

        var validActions = context.Actions.Where(a => a.Status != TurnActionStatus.Failed).ToList();
        context.Actions = validActions;

        var seed = session.GameSeed;

        _auctionResolver.Resolve(context, _randomFactory.CreateForTurn(seed, turnNumber, "auction"));
        ApplyLicenseFees(context);
        _explorationResolver.Resolve(context, _randomFactory.CreateForTurn(seed, turnNumber, "exploration"));
        _appraisalResolver.ResolveActions(context, _randomFactory.CreateForTurn(seed, turnNumber, "appraisal"));

        _economyResolver.ResolveActions(context);
        _developmentResolver.ResolveActions(context);
        _developmentResolver.AdvanceConstruction(context);

        _productionResolver.ResolveActions(context);
        _productionResolver.RunProduction(context);

        _marketResolver.UpdateMarket(context, _randomFactory.CreateForTurn(seed, turnNumber, "market"));
        _economyResolver.ApplyTurnFinancials(context);
        _abandonmentResolver.ResolveActions(context);
        _economyResolver.UpdateAssetValues(context);

        UpdateCompanySummaries(context);
        UpdateLeaderboard(context);

        foreach (var company in session.Companies)
        {
            company.TurnCommitted = false;
            company.Finance.RevenueThisTurn = 0;
            company.Finance.OpexThisTurn = 0;
            company.Finance.CapexThisTurn = 0;
            company.Finance.InterestThisTurn = 0;
            company.Finance.NetIncomeThisTurn = 0;
            company.Finance.FreeCashFlowThisTurn = 0;
        }

        aggregate.PendingActions.RemoveAll(a => a.TurnNumber == turnNumber);
        context.Result.ResolvedAtUtc = DateTime.UtcNow;
        context.Result.OilPrice = session.Market.OilPrice;
        aggregate.TurnResults.Add(context.Result);

        session.CurrentTurnNumber = turnNumber;
        session.State = turnNumber >= session.TotalTurns
            ? GameSessionState.Completed
            : GameSessionState.Planning;

        if (session.State == GameSessionState.Completed)
        {
            session.CompletedAtUtc = DateTime.UtcNow;
            _scoringService.CalculateFinalScores(aggregate);
        }

        return context.Result;
    }

    private static void ApplyLicenseFees(TurnResolutionContext context)
    {
        var fee = context.BalanceProfile.Costs.LicenseFeePerBlockPerTurn;
        foreach (var company in context.Aggregate.Session.Companies)
        {
            var ownedCount = context.Aggregate.Session.Basin.Blocks
                .Count(b => b.OwnerCompanyId == company.Id);
            var totalFee = fee * ownedCount;
            company.Finance.Cash -= totalFee;
            company.Finance.OpexThisTurn += totalFee;
        }
    }

    private static void UpdateCompanySummaries(TurnResolutionContext context)
    {
        foreach (var company in context.Aggregate.Session.Companies)
        {
            company.CompanyValue = company.Finance.Cash - company.Finance.Debt + company.Finance.AssetValue
                                   - company.Finance.AbandonmentLiability
                                   + (company.Reputation.Overall - 50) * 2_000_000m;

            context.Result.CompanySummaries.Add(new CompanyTurnSummary
            {
                CompanyId = company.Id,
                EndingCash = company.Finance.Cash,
                Capex = company.Finance.CapexThisTurn,
                Revenue = company.Finance.RevenueThisTurn,
                CompanyValue = company.CompanyValue,
                ProductionBoePerDay = company.TotalProductionBoePerDay,
                Rank = company.Rank
            });
        }
    }

    private static void UpdateLeaderboard(TurnResolutionContext context)
    {
        var ranked = context.Aggregate.Session.Companies
            .OrderByDescending(c => c.CompanyValue)
            .ToList();

        for (var i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
        }
    }
}
