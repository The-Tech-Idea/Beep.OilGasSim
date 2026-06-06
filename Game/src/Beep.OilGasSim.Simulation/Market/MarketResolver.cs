using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Simulation.Randomness;

namespace Beep.OilGasSim.Simulation.Market;

public interface IMarketResolver
{
    void UpdateMarket(TurnResolutionContext context, IGameRandom random);
}

public sealed class MarketResolver : IMarketResolver
{
    public void UpdateMarket(TurnResolutionContext context, IGameRandom random)
    {
        var market = context.Aggregate.Session.Market;
        var volatility = context.GameplayModeProfile.OilPriceVolatilityModifier;
        var min = context.BalanceProfile.Market.MinNormalPrice;
        var max = context.BalanceProfile.Market.MaxNormalPrice;

        var delta = market.Trend switch
        {
            MarketTrend.Bullish => random.NextInt(2, 9),
            MarketTrend.Bearish => random.NextInt(-8, -1),
            MarketTrend.Volatile => random.NextInt(-15, 16),
            _ => random.NextInt(-3, 4)
        };

        delta = (int)(delta * volatility);
        market.OilPrice = Math.Clamp(market.OilPrice + delta, min, max);
        market.TurnNumber = context.TurnNumber;

        if (random.NextDouble() < 0.08 * context.GameplayModeProfile.EventIntensityModifier)
        {
            market.Trend = random.NextDouble() < 0.5 ? MarketTrend.Bearish : MarketTrend.Bullish;
            context.Result.Events.Add(new Domain.Turns.TurnEventReport
            {
                Category = "Market",
                Headline = market.Trend == MarketTrend.Bullish ? "Oil price outlook improving." : "Oil price pressure building.",
                Detail = $"Oil price now ${market.OilPrice:F0}/bbl.",
                IsPublic = true
            });
        }
        else if (market.Trend == MarketTrend.Bearish && random.NextDouble() < 0.3)
        {
            market.Trend = MarketTrend.Stable;
        }
        else if (market.Trend == MarketTrend.Bullish && random.NextDouble() < 0.3)
        {
            market.Trend = MarketTrend.Stable;
        }

        market.MarketSummary = $"Oil ${market.OilPrice:F0}/bbl — {market.Trend}.";
    }
}
