using Beep.OilGasSim.Domain.Common;

namespace Beep.OilGasSim.Domain.Scenarios;

public sealed class ScenarioDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public GameplayModeType DefaultGameplayMode { get; set; }
    public List<GameplayModeType> SupportedGameplayModes { get; set; } = [];
    public string BalanceProfileId { get; set; } = "mvp-balance";
    public int TurnLengthMonths { get; set; } = 6;
    public decimal StartingOilPrice { get; set; } = 75m;
    public int BlockCount { get; set; } = 20;
    public int MaxPlayers { get; set; } = 6;
    public string PrimaryCommodity { get; set; } = "Oil";
    public BasinDefinition Basin { get; set; } = new();
    public List<BlockDefinition> Blocks { get; set; } = [];
}

public sealed class BasinDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string BasinType { get; set; } = "DesertOnshore";
    public double GeologicalPotential { get; set; } = 0.7;
    public double InfrastructureMaturity { get; set; } = 0.6;
    public double ServiceCostIndex { get; set; } = 1.0;
}

public sealed class BlockDefinition
{
    public string BlockId { get; set; } = "";
    public string Name { get; set; } = "";
    public int GridX { get; set; }
    public int GridY { get; set; }
    public BlockPublicDefinition PublicData { get; set; } = new();
    public HiddenGeologyDefinition HiddenGeology { get; set; } = new();
}

public sealed class BlockPublicDefinition
{
    public string PublicGeologyHint { get; set; } = "";
    public double InfrastructureAccess { get; set; }
    public double SurfaceRisk { get; set; }
    public double EnvironmentalSensitivity { get; set; }
    public PublicRiskRating PublicRiskRating { get; set; }
}

public sealed class HiddenGeologyDefinition
{
    public double SourceRockQuality { get; set; }
    public double ReservoirQuality { get; set; }
    public double TrapIntegrity { get; set; }
    public double SealQuality { get; set; }
    public double TimingMigration { get; set; }
    public string FluidType { get; set; } = "Oil";
    public double RecoverableVolumeMmboe { get; set; }
    public double DepthMeters { get; set; }
    public double DevelopmentComplexity { get; set; }
}
