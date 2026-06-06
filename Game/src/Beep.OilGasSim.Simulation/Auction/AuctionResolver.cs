using Beep.OilGasSim.Domain.Blocks;
using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.GameSessions;
using Beep.OilGasSim.Domain.Turns;
using Beep.OilGasSim.Simulation.Randomness;

namespace Beep.OilGasSim.Simulation.Auction;

public interface IAuctionResolver
{
    void Resolve(TurnResolutionContext context, IGameRandom random);
}

public sealed class AuctionResolver : IAuctionResolver
{
    public void Resolve(TurnResolutionContext context, IGameRandom random)
    {
        var bids = context.Actions
            .Where(a => a.ActionType == TurnActionType.BidForLicense && a.TargetBlockId.HasValue)
            .GroupBy(a => a.TargetBlockId!.Value)
            .ToList();

        foreach (var group in bids)
        {
            var block = context.Aggregate.Session.Basin.Blocks.First(b => b.Id == group.Key);
            if (block.OwnerCompanyId.HasValue)
            {
                continue;
            }

            var winningBid = group
                .OrderByDescending(b => b.BidAmount)
                .ThenBy(_ => random.NextInt(0, 1000))
                .First();

            var winner = context.Aggregate.Session.Companies.First(c => c.Id == winningBid.CompanyId);
            if (winner.Finance.Cash < winningBid.BidAmount)
            {
                context.Result.Events.Add(new TurnEventReport
                {
                    CompanyId = winner.Id,
                    Category = "License",
                    Headline = $"Bid for {block.Name} failed — insufficient cash.",
                    Detail = $"Required: ${winningBid.BidAmount:N0}.",
                    IsPublic = false
                });
                continue;
            }

            winner.Finance.Cash -= winningBid.BidAmount;
            winner.Finance.CapexThisTurn += winningBid.BidAmount;
            block.OwnerCompanyId = winner.Id;
            block.Stage = AssetStage.Licensed;

            context.Result.Events.Add(new TurnEventReport
            {
                CompanyId = winner.Id,
                Category = "License",
                Headline = $"{winner.Name} won {block.Name} for ${winningBid.BidAmount:N0}.",
                Detail = "License acquired.",
                IsPublic = true
            });

            foreach (var loser in group.Where(b => b.CompanyId != winner.Id))
            {
                context.Result.Events.Add(new TurnEventReport
                {
                    CompanyId = loser.CompanyId,
                    Category = "License",
                    Headline = $"Lost auction for {block.Name}.",
                    Detail = $"Your bid: ${loser.BidAmount:N0}. Winning bid: ${winningBid.BidAmount:N0}.",
                    IsPublic = false
                });
            }
        }
    }
}
