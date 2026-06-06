using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Application.GameSessions;
using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Infrastructure.Content;

namespace Beep.OilGasSim.Tests.Simulation;

public class GameplayModeTests
{
    [Fact]
    public async Task FunMode_SessionUsesTwelveTurnsAndTwoSlots()
    {
        var service = TestServiceFactory.CreateService(includeFunMode: true);

        var aggregate = await service.CreateSessionAsync(new CreateGameSessionRequest
        {
            GameplayModeProfileId = "fun",
            CompanyName = "Fun Energy"
        });

        Assert.Equal(GameplayModeType.Fun, aggregate.Session.GameplayMode);
        Assert.Equal(12, aggregate.Session.TotalTurns);
        Assert.Equal(2, aggregate.Session.ModeProfile.ActionSlotsPerTurn);
        Assert.Equal(700_000_000m, aggregate.Session.Companies[0].Finance.Cash);
        Assert.Equal(300_000_000m, aggregate.Session.ModeProfile.MaxDebt);
        Assert.False(aggregate.Session.ModeProfile.EnableHedging);
        Assert.Equal(UiComplexityLevel.Simple, aggregate.Session.ModeProfile.UiComplexityLevel);
        Assert.Equal(1.35, aggregate.Session.ModeProfile.ExplorationChanceModifier);
    }

    [Fact]
    public async Task BalancedMode_SessionUsesTwentyTurnsAndThreeSlots()
    {
        var service = TestServiceFactory.CreateService();

        var aggregate = await service.CreateSessionAsync(new CreateGameSessionRequest
        {
            GameplayModeProfileId = "balanced",
            CompanyName = "Balanced Energy"
        });

        Assert.Equal(GameplayModeType.Balanced, aggregate.Session.GameplayMode);
        Assert.Equal(20, aggregate.Session.TotalTurns);
        Assert.Equal(3, aggregate.Session.ModeProfile.ActionSlotsPerTurn);
        Assert.Equal(500_000_000m, aggregate.Session.Companies[0].Finance.Cash);
        Assert.True(aggregate.Session.ModeProfile.EnableHedging);
        Assert.Equal(UiComplexityLevel.Standard, aggregate.Session.ModeProfile.UiComplexityLevel);
    }

    [Fact]
    public async Task FunMode_RejectsHedgingAction()
    {
        var service = TestServiceFactory.CreateService(includeFunMode: true);
        var aggregate = await service.CreateSessionAsync(new CreateGameSessionRequest
        {
            GameplayModeProfileId = "fun",
            CompanyName = "Fun Energy"
        });
        await service.StartSessionAsync(aggregate.Session.Id);
        var companyId = aggregate.Session.Companies[0].Id;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitActionAsync(aggregate.Session.Id, new SubmitActionRequest
            {
                CompanyId = companyId,
                ActionType = TurnActionType.HedgeProduction,
                BidAmount = 50
            }));
    }
}
