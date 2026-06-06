using Beep.OilGasSim.Domain.Common;

namespace Beep.OilGasSim.Domain.Blocks;

public sealed partial class LicenseBlock
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid BasinId { get; set; }
    public string BlockCode { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid? OwnerCompanyId { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public BlockPublicData PublicData { get; set; } = new();
    public HiddenGeology HiddenGeology { get; set; } = new();
    public AssetStage Stage { get; set; } = AssetStage.Unlicensed;
}

public sealed class BlockPublicData
{
    public string PublicGeologyHint { get; set; } = "";
    public double InfrastructureAccess { get; set; }
    public double SurfaceRisk { get; set; }
    public double EnvironmentalSensitivity { get; set; }
    public PublicRiskRating PublicRiskRating { get; set; }
}

public sealed partial class HiddenGeology
{
    public double SourceRockQuality { get; set; }
    public double ReservoirQuality { get; set; }
    public double TrapIntegrity { get; set; }
    public double SealQuality { get; set; }
    public double TimingMigration { get; set; }
    public FluidType FluidType { get; set; } = FluidType.Unknown;
    public double RecoverableVolumeMmboe { get; set; }
    public double DepthMeters { get; set; }
    public double DevelopmentComplexity { get; set; }
}
