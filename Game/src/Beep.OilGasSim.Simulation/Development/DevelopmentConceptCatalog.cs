using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Development;
using Beep.OilGasSim.Domain.GameplayModes;

namespace Beep.OilGasSim.Simulation.Development;

public static class DevelopmentConceptCatalog
{
    public static DevelopmentConceptTemplate Get(DevelopmentConceptType type, BalanceProfile balance, GameplayModeProfile mode)
    {
        var costMod = (decimal)mode.CostModifier;
        var timeMod = mode.DevelopmentTimeModifier;

        return type switch
        {
            DevelopmentConceptType.Small => new DevelopmentConceptTemplate
            {
                ConceptType = DevelopmentConceptType.Small,
                Name = "Small Development",
                Capex = balance.Costs.SmallDevelopment * costMod,
                ConstructionTurns = Math.Max(1, (int)Math.Round(2 * timeMod)),
                FacilityCapacityBoePerDay = 12_000,
                FixedOpexPerTurn = 8_000_000m,
                VariableOpexPerBoe = 10m,
                BaseUptime = 0.92,
                AbandonmentLiability = 25_000_000m * (decimal)mode.AbandonmentPenaltyModifier
            },
            DevelopmentConceptType.Large => new DevelopmentConceptTemplate
            {
                ConceptType = DevelopmentConceptType.Large,
                Name = "Large Development",
                Capex = balance.Costs.LargeDevelopment * costMod,
                ConstructionTurns = Math.Max(1, (int)Math.Round(4 * timeMod)),
                FacilityCapacityBoePerDay = 45_000,
                FixedOpexPerTurn = 25_000_000m,
                VariableOpexPerBoe = 8m,
                BaseUptime = 0.93,
                AbandonmentLiability = 80_000_000m * (decimal)mode.AbandonmentPenaltyModifier
            },
            _ => new DevelopmentConceptTemplate
            {
                ConceptType = DevelopmentConceptType.Standard,
                Name = "Standard Development",
                Capex = balance.Costs.StandardDevelopment * costMod,
                ConstructionTurns = Math.Max(1, (int)Math.Round(3 * timeMod)),
                FacilityCapacityBoePerDay = 25_000,
                FixedOpexPerTurn = 14_000_000m,
                VariableOpexPerBoe = 9m,
                BaseUptime = 0.94,
                AbandonmentLiability = 45_000_000m * (decimal)mode.AbandonmentPenaltyModifier
            }
        };
    }
}
