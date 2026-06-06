using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Scenarios;

namespace Beep.OilGasSim.Infrastructure.Content;

public static class DesertFrontierGenerator
{
    public static ScenarioDefinition Create()
    {
        var scenario = new ScenarioDefinition
        {
            Id = "desert-frontier",
            Name = "Desert Frontier",
            Description = "Fictional onshore desert basin with moderate infrastructure and mixed geology.",
            DefaultGameplayMode = GameplayModeType.Balanced,
            SupportedGameplayModes = [GameplayModeType.Fun, GameplayModeType.Balanced],
            BalanceProfileId = "mvp-balance",
            StartingOilPrice = 75m,
            BlockCount = 20,
            Basin = new BasinDefinition
            {
                Id = "desert-basin",
                Name = "Desert Frontier Basin",
                BasinType = "DesertOnshore",
                GeologicalPotential = 0.72,
                InfrastructureMaturity = 0.58,
                ServiceCostIndex = 1.0
            }
        };

        var hints = new[]
        {
            "Regional structural trend with moderate source potential.",
            "Near mature source kitchen; trap quality uncertain.",
            "Isolated closure with limited public data.",
            "Proximity to export corridor; shallow targets.",
            "Deep section with higher drilling risk.",
            "Historical seeps reported nearby.",
            "Mixed clastic play; seal risk elevated.",
            "Platform area with moderate reservoir quality."
        };

        var rng = new Random(42);
        for (var i = 0; i < 20; i++)
        {
            var row = i / 5;
            var col = i % 5;
            var code = $"D-{i + 1:D2}";

            scenario.Blocks.Add(new BlockDefinition
            {
                BlockId = code,
                Name = $"Block {code}",
                GridX = col,
                GridY = row,
                PublicData = new BlockPublicDefinition
                {
                    PublicGeologyHint = hints[i % hints.Length],
                    InfrastructureAccess = 0.4 + rng.NextDouble() * 0.5,
                    SurfaceRisk = rng.NextDouble() * 0.4,
                    EnvironmentalSensitivity = 0.2 + rng.NextDouble() * 0.5,
                    PublicRiskRating = (PublicRiskRating)(1 + i % 4)
                },
                HiddenGeology = new HiddenGeologyDefinition
                {
                    SourceRockQuality = 0.45 + rng.NextDouble() * 0.45,
                    ReservoirQuality = 0.40 + rng.NextDouble() * 0.50,
                    TrapIntegrity = 0.35 + rng.NextDouble() * 0.55,
                    SealQuality = 0.40 + rng.NextDouble() * 0.50,
                    TimingMigration = 0.45 + rng.NextDouble() * 0.45,
                    FluidType = i % 7 == 0 ? "Dry" : "Oil",
                    RecoverableVolumeMmboe = 20 + rng.NextDouble() * 180,
                    DepthMeters = 2000 + rng.Next(0, 3500),
                    DevelopmentComplexity = 0.3 + rng.NextDouble() * 0.5
                }
            });
        }

        return scenario;
    }
}
