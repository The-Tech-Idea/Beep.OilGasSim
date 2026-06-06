using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Turns;

namespace Beep.OilGasSim.Tests.Simulation;

public class MultiplayerFlowTests
{
    [Fact]
    public async Task CreateMultiplayerLobby_HasJoinCodeAndHostNotReady()
    {
        var service = TestServiceFactory.CreateService();

        var aggregate = await service.CreateSessionAsync(new CreateGameSessionRequest
        {
            ScenarioId = "desert-frontier",
            GameplayModeProfileId = "balanced",
            CompanyName = "Host Energy",
            IsMultiplayer = true
        });

        Assert.True(aggregate.Session.IsMultiplayer);
        Assert.Equal(6, aggregate.Session.MaxPlayers);
        Assert.Equal(2, aggregate.Session.MinPlayers);
        Assert.False(string.IsNullOrWhiteSpace(aggregate.Session.JoinCode));
        Assert.False(aggregate.Session.Companies[0].Players[0].IsReady);
    }

    [Fact]
    public async Task JoinSetReadyStart_RequiresMinPlayersAndAllReady()
    {
        var service = TestServiceFactory.CreateService();
        var host = await service.CreateSessionAsync(new CreateGameSessionRequest
        {
            ScenarioId = "desert-frontier",
            GameplayModeProfileId = "balanced",
            CompanyName = "Host",
            PlayerDisplayName = "Host Player",
            IsMultiplayer = true
        });

        var joinCode = host.Session.JoinCode;
        var hostCompanyId = host.Session.Companies[0].Id;
        var hostPlayerId = host.Session.Companies[0].Players[0].PlayerId;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartSessionAsync(host.Session.Id));

        var guest = await service.JoinSessionAsync(new JoinGameSessionRequest
        {
            JoinCode = joinCode,
            CompanyName = "Guest Corp",
            PlayerDisplayName = "Guest"
        });

        await service.SetPlayerReadyAsync(guest.SessionId, hostCompanyId, hostPlayerId, true);
        await service.SetPlayerReadyAsync(guest.SessionId, guest.CompanyId, guest.PlayerId, true);

        var started = await service.StartSessionAsync(host.Session.Id);
        Assert.Equal(GameSessionState.Planning, started.Session.State);
        Assert.Equal(1, started.Session.CurrentTurnNumber);
    }

    [Fact]
    public async Task TwoCompaniesCommit_ResolvesTurnOnce()
    {
        var service = TestServiceFactory.CreateService();
        var host = await service.CreateSessionAsync(new CreateGameSessionRequest
        {
            ScenarioId = "desert-frontier",
            GameplayModeProfileId = "balanced",
            CompanyName = "Alpha",
            IsMultiplayer = true
        });

        var guest = await service.JoinSessionAsync(new JoinGameSessionRequest
        {
            JoinCode = host.Session.JoinCode,
            CompanyName = "Beta"
        });

        var hostCompany = host.Session.Companies[0];
        await service.SetPlayerReadyAsync(host.Session.Id, hostCompany.Id, hostCompany.Players[0].PlayerId, true);
        await service.SetPlayerReadyAsync(host.Session.Id, guest.CompanyId, guest.PlayerId, true);
        await service.StartSessionAsync(host.Session.Id);

        var block = host.Session.Basin.Blocks[0];
        await service.SubmitActionAsync(host.Session.Id, new SubmitActionRequest
        {
            CompanyId = hostCompany.Id,
            ActionType = TurnActionType.BidForLicense,
            TargetBlockId = block.Id,
            BidAmount = 20_000_000m
        });

        var partial = await service.CommitTurnAsync(host.Session.Id, hostCompany.Id);
        Assert.Empty(partial.Events);

        var resolved = await service.CommitTurnAsync(host.Session.Id, guest.CompanyId);
        Assert.NotEmpty(resolved.Events);

        var updated = await service.GetSessionAsync(host.Session.Id);
        Assert.NotNull(updated);
        Assert.Equal(2, updated!.Session.CurrentTurnNumber);
    }

    [Fact]
    public async Task GetSessionByJoinCode_FindsLobby()
    {
        var service = TestServiceFactory.CreateService();
        var created = await service.CreateSessionAsync(new CreateGameSessionRequest
        {
            ScenarioId = "desert-frontier",
            GameplayModeProfileId = "balanced",
            CompanyName = "Lookup Co",
            IsMultiplayer = true
        });

        var found = await service.GetSessionByJoinCodeAsync(created.Session.JoinCode);
        Assert.NotNull(found);
        Assert.Equal(created.Session.Id, found!.Session.Id);
    }
}
