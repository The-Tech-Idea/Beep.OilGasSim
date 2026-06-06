using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Economy;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;

namespace Beep.OilGasSim.Simulation.Economy;

public interface IEconomyResolver
{
    void ResolveActions(TurnResolutionContext context);
    void ApplyTurnFinancials(TurnResolutionContext context);
    void UpdateAssetValues(TurnResolutionContext context);
}

public sealed class EconomyResolver : IEconomyResolver
{
    public void ResolveActions(TurnResolutionContext context)
    {
        foreach (var action in context.Actions)
        {
            var company = context.Aggregate.Session.Companies.First(c => c.Id == action.CompanyId);

            switch (action.ActionType)
            {
                case TurnActionType.TakeDebt:
                    ApplyTakeDebt(context, company, action);
                    break;
                case TurnActionType.RepayDebt:
                    ApplyRepayDebt(company, action);
                    break;
                case TurnActionType.HedgeProduction:
                    ApplyHedge(context, company, action);
                    break;
            }
        }
    }

    public void ApplyTurnFinancials(TurnResolutionContext context)
    {
        foreach (var company in context.Aggregate.Session.Companies)
        {
            ApplyHedgedRevenue(context, company);
            ApplyInterest(company);
            ApplyNetCashFlow(company);
            ApplyEmergencyDebt(context, company);
        }
    }

    public void UpdateAssetValues(TurnResolutionContext context)
    {
        var oilPrice = context.Aggregate.Session.Market.OilPrice;

        foreach (var company in context.Aggregate.Session.Companies)
        {
            decimal fieldValue = 0;
            foreach (var field in context.Aggregate.ProducingFields.Where(f => f.CompanyId == company.Id))
            {
                var netback = oilPrice - field.VariableOpexPerBoe - oilPrice * context.BalanceProfile.Economy.RoyaltyRate;
                fieldValue += (decimal)field.RemainingRecoverableMmboe * 1_000_000m * netback * 0.25m;
            }

            decimal discoveryValue = 0;
            foreach (var d in context.Aggregate.Discoveries.Where(d =>
                         d.CompanyId == company.Id && d.Stage is not AssetStage.Producing and not AssetStage.Abandoned))
            {
                discoveryValue += (decimal)d.EstimatedMidVolumeMmboe * 1_000_000m * 4m * 0.6m;
            }

            company.Finance.AssetValue = fieldValue + discoveryValue;
        }
    }

    private static void ApplyTakeDebt(TurnResolutionContext context, Domain.Companies.Company company, TurnAction action)
    {
        var amount = action.BidAmount > 0 ? action.BidAmount : 100_000_000m;
        var maxDebt = context.GameplayModeProfile.MaxDebt;

        if (company.Finance.Debt + amount > maxDebt)
        {
            return;
        }

        company.Finance.Cash += amount;
        company.Finance.Debt += amount;
        company.Finance.CreditRating = Math.Max(0, company.Finance.CreditRating - 3);
    }

    private static void ApplyRepayDebt(Domain.Companies.Company company, TurnAction action)
    {
        var amount = action.BidAmount > 0 ? action.BidAmount : Math.Min(company.Finance.Cash, company.Finance.Debt);
        amount = Math.Min(amount, company.Finance.Debt);
        company.Finance.Cash -= amount;
        company.Finance.Debt -= amount;
        company.Finance.CreditRating = Math.Min(100, company.Finance.CreditRating + 2);
    }

    private static void ApplyHedge(TurnResolutionContext context, Domain.Companies.Company company, TurnAction action)
    {
        if (!context.GameplayModeProfile.EnableHedging)
        {
            return;
        }

        var percent = action.BidAmount switch
        {
            >= 75 => 0.75,
            >= 50 => 0.50,
            _ => 0.25
        };

        var hedgePrice = context.Aggregate.Session.Market.OilPrice - context.BalanceProfile.Market.HedgePriceDiscount;
        context.Aggregate.HedgePositions.Add(new HedgePosition
        {
            CompanyId = company.Id,
            ForTurnNumber = context.TurnNumber + 1,
            HedgePercent = percent,
            HedgePrice = hedgePrice
        });
    }

    private static void ApplyHedgedRevenue(TurnResolutionContext context, Domain.Companies.Company company)
    {
        var hedge = context.Aggregate.HedgePositions
            .FirstOrDefault(h => h.CompanyId == company.Id && h.ForTurnNumber == context.TurnNumber);

        if (hedge is null || company.Finance.RevenueThisTurn <= 0)
        {
            return;
        }

        var hedgedRevenue = company.Finance.RevenueThisTurn * (decimal)hedge.HedgePercent;
        var marketRevenue = company.Finance.RevenueThisTurn * (decimal)(1 - hedge.HedgePercent);
        var adjustedMarket = marketRevenue / context.Aggregate.Session.Market.OilPrice
                             * context.Aggregate.Session.Market.OilPrice;

        company.Finance.RevenueThisTurn = hedgedRevenue / context.Aggregate.Session.Market.OilPrice * hedge.HedgePrice
                                          + adjustedMarket;
    }

    private static void ApplyInterest(Domain.Companies.Company company)
    {
        if (company.Finance.Debt <= 0)
        {
            return;
        }

        var annualRate = company.Finance.CreditRating switch
        {
            >= 80 => 0.05m,
            >= 60 => 0.08m,
            >= 40 => 0.12m,
            >= 20 => 0.18m,
            _ => 0.25m
        };

        company.Finance.InterestThisTurn = company.Finance.Debt * annualRate * 0.5m;
        company.Finance.OpexThisTurn += company.Finance.InterestThisTurn;
    }

    private static void ApplyNetCashFlow(Domain.Companies.Company company)
    {
        company.Finance.NetIncomeThisTurn = company.Finance.RevenueThisTurn
                                            - company.Finance.OpexThisTurn
                                            - company.Finance.CapexThisTurn;
        company.Finance.FreeCashFlowThisTurn = company.Finance.NetIncomeThisTurn;
        company.Finance.Cash += company.Finance.NetIncomeThisTurn;
    }

    private static void ApplyEmergencyDebt(TurnResolutionContext context, Domain.Companies.Company company)
    {
        if (company.Finance.Cash >= 0)
        {
            return;
        }

        var needed = Math.Abs(company.Finance.Cash) + 50_000_000m;
        var maxDebt = context.GameplayModeProfile.MaxDebt;
        var borrow = Math.Min(needed, maxDebt - company.Finance.Debt);

        if (borrow <= 0)
        {
            return;
        }

        company.Finance.Debt += borrow;
        company.Finance.Cash += borrow;
        company.Finance.CreditRating = Math.Max(0, company.Finance.CreditRating - 10);
    }
}
