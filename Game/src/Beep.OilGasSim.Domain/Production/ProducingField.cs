using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Development;

namespace Beep.OilGasSim.Domain.Production;

public sealed partial class ProducingField
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BlockId { get; set; }
    public Guid DiscoveryId { get; set; }
    public string Name { get; set; } = "";
    public DevelopmentConceptType ConceptType { get; set; }

    public double OriginalRecoverableMmboe { get; set; }
    public double RemainingRecoverableMmboe { get; set; }
    public double PeakProductionBoePerDay { get; set; }
    public double CurrentProductionBoePerDay { get; set; }
    public double FacilityCapacityBoePerDay { get; set; }
    public double DeclineRatePerTurn { get; set; }
    public double Uptime { get; set; }
    public double RampUpFactor { get; set; } = 1.0;

    public decimal FixedOpexPerTurn { get; set; }
    public decimal VariableOpexPerBoe { get; set; }
    public decimal AbandonmentLiability { get; set; }

    public int ProductionTurnsActive { get; set; }
    public bool OptimizationBoostNextTurn { get; set; }
    public ProductionPhase ProductionPhase { get; set; } = ProductionPhase.RampUp;
    public AssetStage Stage { get; set; } = AssetStage.Producing;
}

public sealed partial class ProducingField
{
    public bool IsLateLife =>
        CurrentProductionBoePerDay < PeakProductionBoePerDay * 0.25
        || RemainingRecoverableMmboe < OriginalRecoverableMmboe * 0.20;
}
