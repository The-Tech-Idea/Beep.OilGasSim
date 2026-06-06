using Beep.OilGasSim.Domain.Common;

namespace Beep.OilGasSim.Domain.Development;

public sealed class DevelopmentConceptTemplate
{
    public DevelopmentConceptType ConceptType { get; set; }
    public string Name { get; set; } = "";
    public decimal Capex { get; set; }
    public int ConstructionTurns { get; set; }
    public double FacilityCapacityBoePerDay { get; set; }
    public decimal FixedOpexPerTurn { get; set; }
    public decimal VariableOpexPerBoe { get; set; }
    public double BaseUptime { get; set; }
    public decimal AbandonmentLiability { get; set; }
    public double NormalDeclineRatePerTurn { get; set; } = 0.08;
}

public sealed partial class DevelopmentProject
{
    public Guid Id { get; set; }
    public Guid DiscoveryId { get; set; }
    public Guid BlockId { get; set; }
    public Guid CompanyId { get; set; }
    public string FieldName { get; set; } = "";
    public DevelopmentConceptType ConceptType { get; set; }
    public int ConstructionTurnsRequired { get; set; }
    public int ConstructionTurnsCompleted { get; set; }
    public decimal CapexCommitted { get; set; }
    public double TargetRecoverableMmboe { get; set; }
    public AssetStage Stage { get; set; } = AssetStage.UnderConstruction;
}

public sealed partial class DevelopmentProject
{
    public bool IsComplete => ConstructionTurnsCompleted >= ConstructionTurnsRequired;
    public double ProgressPercent => ConstructionTurnsRequired == 0
        ? 100
        : (double)ConstructionTurnsCompleted / ConstructionTurnsRequired * 100;
}
