using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Exploration;
using Beep.OilGasSim.Domain.Production;
using Beep.OilGasSim.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Beep.OilGasSim.Tests.Api;

public sealed class TurnActionApiTests : IClassFixture<ActionApiFactory>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ActionApiFactory _factory;

    public TurnActionApiTests(ActionApiFactory factory) => _factory = factory;

    [Fact]
    public async Task BidForLicense_AcceptsStringActionType()
    {
        var ctx = await CreateStartedSessionAsync("balanced");
        var blockId = ctx.Blocks.First(b => b.Stage == "Unlicensed").Id;

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "BidForLicense",
            TargetBlockId = blockId,
            BidAmount = 20_000_000
        });

        var errorBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, errorBody);
        var body = await ReadActionAsync(response);
        Assert.Equal("BidForLicense", body.ActionType);
        Assert.Equal("Pending", body.Status);
    }

    [Fact]
    public async Task GeologicalStudy_AcceptsStringActionType()
    {
        var ctx = await CreateStartedSessionAsync("balanced");
        var blockId = await OwnBlockAsync(ctx);

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "GeologicalStudy",
            TargetBlockId = blockId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("GeologicalStudy", (await ReadActionAsync(response)).ActionType);
    }

    [Fact]
    public async Task Acquire2DSeismic_AcceptsStringActionType()
    {
        var ctx = await CreateStartedSessionAsync("balanced");
        var blockId = await OwnBlockAsync(ctx);

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "Acquire2DSeismic",
            TargetBlockId = blockId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Acquire2DSeismic", (await ReadActionAsync(response)).ActionType);
    }

    [Fact]
    public async Task DrillExplorationWell_AcceptsStringActionType()
    {
        var ctx = await CreateStartedSessionAsync("balanced");
        var blockId = await OwnBlockAsync(ctx);

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "DrillExplorationWell",
            TargetBlockId = blockId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("DrillExplorationWell", (await ReadActionAsync(response)).ActionType);
    }

    [Fact]
    public async Task DrillAppraisalWell_AcceptsStringActionType()
    {
        var ctx = await CreateStartedSessionAsync("balanced");
        var discoveryId = await SeedDiscoveryAsync(ctx);

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "DrillAppraisalWell",
            TargetAssetId = discoveryId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("DrillAppraisalWell", (await ReadActionAsync(response)).ActionType);
    }

    [Fact]
    public async Task ApproveDevelopment_AcceptsStringActionType()
    {
        var ctx = await CreateStartedSessionAsync("balanced");
        var discoveryId = await SeedDiscoveryAsync(ctx);

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "ApproveDevelopment",
            TargetAssetId = discoveryId,
            ParametersJson = "Standard"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ApproveDevelopment", (await ReadActionAsync(response)).ActionType);
    }

    [Fact]
    public async Task OptimizeField_AcceptsStringActionType()
    {
        var ctx = await CreateStartedSessionAsync("balanced");
        var fieldId = await SeedProducingFieldAsync(ctx);

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "OptimizeField",
            TargetAssetId = fieldId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("OptimizeField", (await ReadActionAsync(response)).ActionType);
    }

    [Fact]
    public async Task AbandonField_AcceptsStringActionType()
    {
        var ctx = await CreateStartedSessionAsync("balanced");
        var fieldId = await SeedProducingFieldAsync(ctx);

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "AbandonField",
            TargetAssetId = fieldId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("AbandonField", (await ReadActionAsync(response)).ActionType);
    }

    [Fact]
    public async Task TakeDebt_AcceptsStringActionType()
    {
        var ctx = await CreateStartedSessionAsync("balanced");

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "TakeDebt",
            BidAmount = 100_000_000
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("TakeDebt", (await ReadActionAsync(response)).ActionType);
    }

    [Fact]
    public async Task RepayDebt_AcceptsStringActionType()
    {
        var ctx = await CreateStartedSessionAsync("balanced");
        await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "TakeDebt",
            BidAmount = 100_000_000
        });
        await CommitTurnAsync(ctx);

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "RepayDebt",
            BidAmount = 50_000_000
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("RepayDebt", (await ReadActionAsync(response)).ActionType);
    }

    [Fact]
    public async Task HedgeProduction_AcceptsStringActionType_InBalancedMode()
    {
        var ctx = await CreateStartedSessionAsync("balanced");

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "HedgeProduction",
            BidAmount = 50
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("HedgeProduction", (await ReadActionAsync(response)).ActionType);
    }

    [Fact]
    public async Task HedgeProduction_ReturnsBadRequest_InFunMode()
    {
        var ctx = await CreateStartedSessionAsync("fun");

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "HedgeProduction",
            BidAmount = 50
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadAsStringAsync();
        Assert.Contains("Hedging is disabled", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidActionType_ReturnsValidationError()
    {
        var ctx = await CreateStartedSessionAsync("balanced");

        var response = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "NotARealAction",
            BidAmount = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<SessionContext> CreateStartedSessionAsync(string mode)
    {
        var client = _factory.CreateClient();
        var create = await client.PostAsJsonAsync("/api/game-sessions", new
        {
            scenarioId = "desert-frontier",
            gameplayModeProfileId = mode,
            companyName = "API Test Co",
            playerDisplayName = "Tester"
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<SessionResponse>(Json)
                      ?? throw new InvalidOperationException("Create session failed.");

        var started = await client.PostAsync($"/api/game-sessions/{created.Id}/start", null);
        started.EnsureSuccessStatusCode();
        var session = await started.Content.ReadFromJsonAsync<SessionResponse>(Json)
                      ?? throw new InvalidOperationException("Start session failed.");

        var map = await client.GetFromJsonAsync<MapApiResponse>(
            $"/api/game-sessions/{session.Id}/map?companyId={session.Companies[0].Id}", Json)
                  ?? throw new InvalidOperationException("Map failed.");

        return new SessionContext(client, session.Id, session.Companies[0].Id, map.Blocks);
    }

    private static async Task<Guid> OwnBlockAsync(SessionContext ctx)
    {
        var blockId = ctx.Blocks.First(b => b.Stage == "Unlicensed").Id;
        var bid = await PostActionAsync(ctx, new ActionPayload
        {
            CompanyId = ctx.CompanyId,
            ActionType = "BidForLicense",
            TargetBlockId = blockId,
            BidAmount = 20_000_000
        });
        bid.EnsureSuccessStatusCode();
        await CommitTurnAsync(ctx);
        return blockId;
    }

    private async Task<Guid> SeedDiscoveryAsync(SessionContext ctx)
    {
        var blockId = await OwnBlockAsync(ctx);
        var aggregate = await _factory.Store.GetAsync(ctx.SessionId)
                        ?? throw new InvalidOperationException("Session missing.");
        var discovery = new Discovery
        {
            Id = Guid.NewGuid(),
            BlockId = blockId,
            CompanyId = ctx.CompanyId,
            Name = "Test Discovery",
            SizeClass = DiscoverySizeClass.Commercial,
            EstimatedMidVolumeMmboe = 120,
            EstimatedLowVolumeMmboe = 80,
            EstimatedHighVolumeMmboe = 160,
            Confidence = 55,
            Stage = AssetStage.Discovery
        };
        aggregate.Discoveries.Add(discovery);
        await _factory.Store.SaveAsync(aggregate);
        return discovery.Id;
    }

    private async Task<Guid> SeedProducingFieldAsync(SessionContext ctx)
    {
        var blockId = await OwnBlockAsync(ctx);
        var aggregate = await _factory.Store.GetAsync(ctx.SessionId)
                        ?? throw new InvalidOperationException("Session missing.");
        var field = new ProducingField
        {
            Id = Guid.NewGuid(),
            BlockId = blockId,
            CompanyId = ctx.CompanyId,
            Name = "Test Field",
            Stage = AssetStage.Producing,
            CurrentProductionBoePerDay = 12_000,
            RemainingRecoverableMmboe = 40,
            ProductionPhase = ProductionPhase.Plateau
        };
        aggregate.ProducingFields.Add(field);
        await _factory.Store.SaveAsync(aggregate);
        return field.Id;
    }

    private static Task<HttpResponseMessage> PostActionAsync(SessionContext ctx, ActionPayload payload) =>
        ctx.Client.PostAsJsonAsync($"/api/game-sessions/{ctx.SessionId}/actions", payload);

    private static async Task CommitTurnAsync(SessionContext ctx)
    {
        var response = await ctx.Client.PostAsync(
            $"/api/game-sessions/{ctx.SessionId}/companies/{ctx.CompanyId}/commit",
            null);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ActionResponse> ReadActionAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ActionResponse>(json, Json)
               ?? throw new InvalidOperationException("Action response missing.");
    }

    private sealed record SessionContext(
        HttpClient Client,
        Guid SessionId,
        Guid CompanyId,
        List<MapBlockDto> Blocks);

    private sealed class ActionPayload
    {
        public Guid CompanyId { get; set; }
        public string ActionType { get; set; } = "";
        public Guid? TargetBlockId { get; set; }
        public Guid? TargetAssetId { get; set; }
        public decimal BidAmount { get; set; }
        public string? ParametersJson { get; set; }
    }

    private sealed class ActionResponse
    {
        public Guid Id { get; set; }
        public string ActionType { get; set; } = "";
        public string Status { get; set; } = "";
    }

    private sealed class SessionResponse
    {
        public Guid Id { get; set; }
        public List<CompanyResponse> Companies { get; set; } = [];
    }

    private sealed class CompanyResponse
    {
        public Guid Id { get; set; }
    }

    private sealed class MapApiResponse
    {
        public List<MapBlockDto> Blocks { get; set; } = [];
    }

    private sealed class MapBlockDto
    {
        public Guid Id { get; set; }
        public string Stage { get; set; } = "";
    }
}

public sealed class ActionApiFactory : WebApplicationFactory<Program>
{
    public InMemoryGameSessionStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "InMemory"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGameSessionStore>();
            services.AddSingleton<IGameSessionStore>(Store);
        });
    }
}
