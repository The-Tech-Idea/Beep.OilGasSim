using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.GameplayModes;
using Beep.OilGasSim.Domain.Scenarios;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Beep.OilGasSim.Infrastructure.Content;

public sealed class JsonContentLoader : IContentLoader
{
    private readonly string _contentRoot;
    private readonly ILogger<JsonContentLoader> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonContentLoader(IHostEnvironment env, ILogger<JsonContentLoader> logger)
    {
        _logger = logger;
        _contentRoot = Path.Combine(env.ContentRootPath, "..", "..", "content");
        if (!Directory.Exists(_contentRoot))
        {
            _contentRoot = Path.Combine(AppContext.BaseDirectory, "content");
        }
    }

    public async Task<IReadOnlyList<ScenarioDefinition>> GetScenariosAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_contentRoot, "scenarios", "desert-frontier.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Scenario file missing at {Path}, using embedded fallback.", path);
            return [CreateFallbackScenario()];
        }

        await using var stream = File.OpenRead(path);
        var scenario = await JsonSerializer.DeserializeAsync<ScenarioDefinition>(stream, _jsonOptions, cancellationToken);
        return scenario is null ? [CreateFallbackScenario()] : [scenario];
    }

    public async Task<ScenarioDefinition?> GetScenarioAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        var scenarios = await GetScenariosAsync(cancellationToken);
        return scenarios.FirstOrDefault(s => s.Id.Equals(scenarioId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<GameplayModeProfile>> GetGameplayModesAsync(CancellationToken cancellationToken = default)
    {
        var modes = new List<GameplayModeProfile>();
        foreach (var file in new[] { "fun-mode.json", "balanced-mode.json" })
        {
            var path = Path.Combine(_contentRoot, "gameplay-modes", file);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                await using var stream = File.OpenRead(path);
                var mode = await JsonSerializer.DeserializeAsync<GameplayModeProfile>(stream, _jsonOptions, cancellationToken);
                if (mode is not null)
                {
                    modes.Add(mode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load gameplay mode from {Path}, skipping.", path);
            }
        }

        return modes.Count > 0 ? modes : CreateFallbackModes();
    }

    public async Task<GameplayModeProfile?> GetGameplayModeAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var modes = await GetGameplayModesAsync(cancellationToken);
        return modes.FirstOrDefault(m => m.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<BalanceProfile> GetBalanceProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_contentRoot, "balance", "mvp-balance.json");
        if (File.Exists(path))
        {
            await using var stream = File.OpenRead(path);
            var balance = await JsonSerializer.DeserializeAsync<BalanceProfile>(stream, _jsonOptions, cancellationToken);
            if (balance is not null)
            {
                return balance;
            }
        }

        return new BalanceProfile { Id = profileId };
    }

    private static IReadOnlyList<GameplayModeProfile> CreateFallbackModes() =>
    [
        new GameplayModeProfile
        {
            Id = "fun",
            ModeType = GameplayModeType.Fun,
            Name = "Fun Mode",
            TotalTurns = 12,
            ActionSlotsPerTurn = 2,
            StartingCash = 700_000_000m,
            MaxDebt = 300_000_000m,
            ExplorationChanceModifier = 1.35,
            CostModifier = 0.85,
            DevelopmentTimeModifier = 0.6,
            AbandonmentPenaltyModifier = 0.5,
            AiAssistanceLevel = AiAssistanceLevel.Guided,
            UiComplexityLevel = UiComplexityLevel.Simple,
            EnableHedging = false
        },
        new GameplayModeProfile
        {
            Id = "balanced",
            ModeType = GameplayModeType.Balanced,
            Name = "Balanced Mode",
            TotalTurns = 20,
            ActionSlotsPerTurn = 3,
            StartingCash = 500_000_000m,
            MaxDebt = 500_000_000m,
            AiAssistanceLevel = AiAssistanceLevel.FullAdvisor,
            UiComplexityLevel = UiComplexityLevel.Standard,
            EnableHedging = true
        }
    ];

    private static ScenarioDefinition CreateFallbackScenario() => DesertFrontierGenerator.Create();
}
