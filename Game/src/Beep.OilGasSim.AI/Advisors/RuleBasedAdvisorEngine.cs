using Beep.OilGasSim.AI.Context;

namespace Beep.OilGasSim.AI.Advisors;

public interface IAiAdvisorEngine
{
    AiAdvisorResponse Generate(AiGameContext context, AiAdvisorRequest request);
}

public sealed class RuleBasedAdvisorEngine : IAiAdvisorEngine
{
    public AiAdvisorResponse Generate(AiGameContext context, AiAdvisorRequest request)
    {
        return request.AdvisorType switch
        {
            AiAdvisorType.Geologist => GeologistAdvice(context, request),
            AiAdvisorType.Cfo => CfoAdvice(context, request),
            AiAdvisorType.Hse => HseAdvice(context, request),
            _ => StrategyAdvice(context, request)
        };
    }

    private static AiAdvisorResponse StrategyAdvice(AiGameContext context, AiAdvisorRequest request)
    {
        var response = new AiAdvisorResponse
        {
            AdvisorType = "Strategy",
            RecommendationType = "TurnPlan"
        };

        var turnsLeft = context.TotalTurns - context.CurrentTurn;
        var producing = context.Assets.Count(a => a.AssetType == "ProducingField");
        var discoveries = context.Assets.Count(a => a.AssetType == "Discovery");
        var licensed = context.Assets.Count(a => a.AssetType == "LicenseBlock");

        response.Message = $"Turn {context.CurrentTurn}/{context.TotalTurns}. Rank #{context.Company.CurrentRank}. ";
        response.Message += turnsLeft <= 4
            ? "Late game — prioritize cash flow and company value over risky exploration."
            : "Mid game — balance exploration upside with development and production.";

        if (context.Company.ActionSlotsRemaining <= 0)
        {
            response.Risks.Add("Action slots are full — commit the turn or remove an action.");
        }

        if (discoveries > 0 && context.Company.Cash > 200_000_000m)
        {
            response.SuggestedActions.Add("ApproveDevelopment");
            response.Message += " You have commercial discoveries ready to develop.";
        }
        else if (licensed > 0 && producing == 0)
        {
            response.SuggestedActions.Add("GeologicalStudy");
            response.SuggestedActions.Add("DrillExplorationWell");
            response.Message += " Focus on converting licensed blocks into discoveries.";
        }
        else if (producing > 0)
        {
            response.SuggestedActions.Add("OptimizeField");
            response.Message += " Maintain production and optimize producing fields.";
        }

        if (ContainsAny(request.Message, "risk", "ahead", "behind", "priority"))
        {
            response.Risks.Add($"Cash ${context.Company.Cash / 1_000_000m:F0}M vs debt ${context.Company.Debt / 1_000_000m:F0}M.");
            if (context.Company.AbandonmentLiability > 50_000_000m)
            {
                response.Risks.Add($"Abandonment liability ${context.Company.AbandonmentLiability / 1_000_000m:F0}M affects final score.");
            }
        }

        return response;
    }

    private static AiAdvisorResponse GeologistAdvice(AiGameContext context, AiAdvisorRequest request)
    {
        var response = new AiAdvisorResponse { AdvisorType = "Geologist", RecommendationType = "Exploration" };

        if (context.Selected is not null)
        {
            var cos = context.Selected.EstimatedChanceOfSuccess;
            response.Message =
                $"Block {context.Selected.BlockCode} is at stage {context.Selected.Stage}. Public hint: {context.Selected.PublicGeologyHint}";
            response.Risks.Add($"Surface risk rating: {context.Selected.PublicRiskRating}.");

            if (cos.HasValue)
            {
                response.Message += $" Estimated chance of success: {cos.Value * 100:F0}%.";
                if (cos >= 0.30)
                {
                    response.SuggestedActions.Add("DrillExplorationWell");
                    response.Message += " Upside is sufficient to consider drilling if you can absorb a dry hole.";
                }
                else if (context.Selected.Stage is "Licensed" or "Studied")
                {
                    response.SuggestedActions.Add("Acquire2DSeismic");
                    response.Message += " Improve confidence with seismic before drilling.";
                }
            }
            else if (context.Selected.Stage == "Licensed")
            {
                response.SuggestedActions.Add("GeologicalStudy");
                response.Message += " Run a geological study before committing to a well.";
            }

            return response;
        }

        var best = context.Assets
            .Where(a => a.AssetType == "LicenseBlock" && a.EstimatedChanceOfSuccess.HasValue)
            .OrderByDescending(a => a.EstimatedChanceOfSuccess)
            .FirstOrDefault();

        response.Message = best is not null
            ? $"Best known prospect: {best.Name} with ~{best.EstimatedChanceOfSuccess!.Value * 100:F0}% chance of success."
            : "No studied blocks yet — acquire licenses and run geological studies.";
        if (best is not null)
        {
            response.SuggestedActions.Add("DrillExplorationWell");
        }

        return response;
    }

    private static AiAdvisorResponse CfoAdvice(AiGameContext context, AiAdvisorRequest request)
    {
        var response = new AiAdvisorResponse { AdvisorType = "CFO", RecommendationType = "Finance" };
        var cash = context.Company.Cash;
        var debt = context.Company.Debt;

        response.Message =
            $"Cash ${cash / 1_000_000m:F0}M, debt ${debt / 1_000_000m:F0}M, company value ${context.Company.CompanyValue / 1_000_000m:F0}M. ";
        response.Message += $"Oil at ${context.Market.OilPrice:F0}/bbl ({context.Market.Trend}).";

        if (cash < 100_000_000m)
        {
            response.Risks.Add("Low cash — dry holes or development could force emergency debt.");
            response.SuggestedActions.Add("TakeDebt");
        }
        else if (debt > 0 && cash > debt)
        {
            response.SuggestedActions.Add("RepayDebt");
            response.Message += " Consider repaying debt to improve credit rating.";
        }

        if (context.EnableHedging && context.Company.ProductionBoePerDay > 0)
        {
            response.SuggestedActions.Add("HedgeProduction");
            response.Message += " Hedging can lock in revenue on producing volumes.";
        }

        var devCost = context.Assets
            .Where(a => a.AssetType == "Discovery")
            .Select(a => a.EstimatedCostToNextStep)
            .FirstOrDefault();

        if (ContainsAny(request.Message, "afford", "development", "drill", "cost") && devCost.HasValue)
        {
            response.Message += devCost <= cash
                ? $" Standard development (~${devCost / 1_000_000m:F0}M) appears affordable."
                : $" Standard development (~${devCost / 1_000_000m:F0}M) exceeds available cash.";
        }

        return response;
    }

    private static AiAdvisorResponse HseAdvice(AiGameContext context, AiAdvisorRequest request)
    {
        var response = new AiAdvisorResponse { AdvisorType = "HSE", RecommendationType = "Compliance" };
        var lateLife = context.Assets.Where(a =>
            a.AssetType == "ProducingField" && a.Stage is "LateLife" or "Producing" && a.MainRisk.Contains("Late-life")).ToList();

        response.Message =
            $"Abandonment liability on books: ${context.Company.AbandonmentLiability / 1_000_000m:F0}M. Reputation: {context.Company.Reputation}/100.";

        if (context.Company.AbandonmentLiability > 100_000_000m)
        {
            response.Risks.Add("High abandonment liability will reduce final score if not cleared.");
            response.SuggestedActions.Add("AbandonField");
        }

        if (lateLife.Count > 0)
        {
            response.Message += $" {lateLife.Count} field(s) approaching late-life — plan responsible abandonment.";
            response.SuggestedActions.Add("AbandonField");
        }
        else
        {
            response.Message += " No immediate late-life compliance issues.";
        }

        return response;
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
}
