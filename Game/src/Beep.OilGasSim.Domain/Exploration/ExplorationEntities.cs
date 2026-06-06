using Beep.OilGasSim.Domain.Common;

namespace Beep.OilGasSim.Domain.Exploration;

public sealed class BlockKnowledge
{
    public Guid CompanyId { get; set; }
    public Guid BlockId { get; set; }
    public KnowledgeLevel KnowledgeLevel { get; set; } = KnowledgeLevel.None;
    public double EstimatedChanceOfSuccess { get; set; }
    public double Confidence { get; set; }
    public double EstimatedLowVolumeMmboe { get; set; }
    public double EstimatedMidVolumeMmboe { get; set; }
    public double EstimatedHighVolumeMmboe { get; set; }
    public string MainRisk { get; set; } = "";
    public string InterpretationSummary { get; set; } = "";
}

public sealed class Prospect
{
    public Guid Id { get; set; }
    public Guid BlockId { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = "";
    public double EstimatedChanceOfSuccess { get; set; }
    public double Confidence { get; set; }
    public string MainRisk { get; set; } = "";
    public bool IsDrillReady { get; set; }
}

public sealed partial class Discovery
{
    public Guid Id { get; set; }
    public Guid BlockId { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = "";
    public FluidType FluidType { get; set; }
    public double EstimatedLowVolumeMmboe { get; set; }
    public double EstimatedMidVolumeMmboe { get; set; }
    public double EstimatedHighVolumeMmboe { get; set; }
    public double Confidence { get; set; }
    public double CommercialityScore { get; set; }
    public string MainRisk { get; set; } = "";
    public DiscoverySizeClass SizeClass { get; set; }
    public AssetStage Stage { get; set; } = AssetStage.Discovery;
}
