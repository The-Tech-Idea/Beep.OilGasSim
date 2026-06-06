using Beep.OilGasSim.Domain.GameSessions;

namespace Beep.OilGasSim.Simulation.Scoring;

public interface IScoringService
{
    void CalculateFinalScores(GameSessionAggregate aggregate);
}

public sealed class ScoringService : IScoringService
{
    public void CalculateFinalScores(GameSessionAggregate aggregate)
    {
        var penaltyMultiplier = 1.5m * (decimal)aggregate.Session.ModeProfile.AbandonmentPenaltyModifier;

        foreach (var company in aggregate.Session.Companies)
        {
            var reputationBonus = (company.Reputation.Overall - 50) * 2_000_000m;
            var abandonmentPenalty = company.Finance.AbandonmentLiability * penaltyMultiplier;

            var finalScore = company.Finance.Cash
                             - company.Finance.Debt
                             + company.Finance.AssetValue
                             + reputationBonus
                             - abandonmentPenalty;

            aggregate.FinalScores[company.Id] = finalScore;
            company.CompanyValue = finalScore;
        }
    }
}
