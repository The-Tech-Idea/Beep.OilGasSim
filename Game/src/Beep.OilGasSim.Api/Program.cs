using Beep.OilGasSim.AI.Advisors;
using Beep.OilGasSim.AI.Context;
using Beep.OilGasSim.AI.Services;
using Beep.OilGasSim.Api.Hubs;
using Beep.OilGasSim.Api.Realtime;
using Beep.OilGasSim.Api.Setup;
using Beep.OilGasSim.Application.GameSessions;
using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Infrastructure.Content;
using Beep.OilGasSim.Infrastructure.Persistence;
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
using Beep.OilGasSim.Simulation.TurnEngine;
using System.Text.Json;
using System.Text.Json.Serialization;

if (args is ["setup"] or ["--setup"])
{
    return await PersistenceSetupWizard.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<Beep.OilGasSim.Api.Filters.GameApiExceptionFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddSingleton<IContentLoader, JsonContentLoader>();
builder.Services.AddGamePersistence(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<IGameRandomFactory, GameRandomFactory>();
builder.Services.AddSingleton<IActionValidator, ActionValidator>();
builder.Services.AddSingleton<IAuctionResolver, AuctionResolver>();
builder.Services.AddSingleton<IExplorationResolver, ExplorationResolver>();
builder.Services.AddSingleton<IAppraisalResolver, AppraisalResolver>();
builder.Services.AddSingleton<IDevelopmentResolver, DevelopmentResolver>();
builder.Services.AddSingleton<IProductionResolver, ProductionResolver>();
builder.Services.AddSingleton<IMarketResolver, MarketResolver>();
builder.Services.AddSingleton<IEconomyResolver, EconomyResolver>();
builder.Services.AddSingleton<IAbandonmentResolver, AbandonmentResolver>();
builder.Services.AddSingleton<IScoringService, ScoringService>();
builder.Services.AddSingleton<IAiContextBuilder, AiContextBuilder>();
builder.Services.AddSingleton<IAiAdvisorEngine, RuleBasedAdvisorEngine>();
builder.Services.AddSingleton<IAiAdvisorService, AiAdvisorService>();
builder.Services.AddSingleton<IAiTurnReportService, AiTurnReportService>();
builder.Services.AddSingleton<IGameRealtimeNotifier, SignalRGameNotifier>();
builder.Services.AddSingleton<ITurnEngine, Beep.OilGasSim.Simulation.TurnEngine.TurnEngine>();
builder.Services.AddSingleton<IGameSessionService, GameSessionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

app.Run();

return 0;

public partial class Program;
