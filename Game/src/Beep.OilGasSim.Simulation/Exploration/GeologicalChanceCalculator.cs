using Beep.OilGasSim.Domain.Blocks;
using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Exploration;
using Beep.OilGasSim.Domain.GameplayModes;

namespace Beep.OilGasSim.Simulation.Exploration;

public static class GeologicalChanceCalculator
{
    public static double EstimateChanceFromKnowledge(
        HiddenGeology hidden,
        KnowledgeLevel level,
        double modeModifier)
    {
        var trueChance = hidden.CalculateTrueChanceOfSuccess() * modeModifier;
        var error = level switch
        {
            KnowledgeLevel.None or KnowledgeLevel.PublicHint => 0.15,
            KnowledgeLevel.GeologicalStudy => 0.10,
            KnowledgeLevel.TwoDSeismic => 0.06,
            KnowledgeLevel.ThreeDSeismic => 0.03,
            _ => 0.02
        };

        return Math.Clamp(trueChance + (Random.Shared.NextDouble() - 0.5) * error, 0.05, 0.60);
    }

    public static void ApplyStudyKnowledge(
        BlockKnowledge knowledge,
        LicenseBlock block,
        KnowledgeLevel newLevel,
        double modeModifier)
    {
        knowledge.KnowledgeLevel = newLevel;
        knowledge.EstimatedChanceOfSuccess = EstimateChanceFromKnowledge(
            block.HiddenGeology, newLevel, modeModifier);

        knowledge.Confidence = newLevel switch
        {
            KnowledgeLevel.GeologicalStudy => 30,
            KnowledgeLevel.TwoDSeismic => 48,
            KnowledgeLevel.ThreeDSeismic => 68,
            _ => knowledge.Confidence
        };

        var volume = block.HiddenGeology.RecoverableVolumeMmboe;
        var spread = Math.Max(20, volume * (1 - knowledge.Confidence / 100.0));
        knowledge.EstimatedMidVolumeMmboe = volume;
        knowledge.EstimatedLowVolumeMmboe = Math.Max(0, volume - spread);
        knowledge.EstimatedHighVolumeMmboe = volume + spread;
        knowledge.MainRisk = IdentifyMainRisk(block.HiddenGeology);
        knowledge.InterpretationSummary = newLevel switch
        {
            KnowledgeLevel.GeologicalStudy =>
                $"{block.Name}: moderate source potential; structural uncertainty remains.",
            KnowledgeLevel.TwoDSeismic =>
                $"{block.Name}: possible structural trap identified; trap confidence improved.",
            _ => knowledge.InterpretationSummary
        };
    }

    public static string IdentifyMainRisk(HiddenGeology hidden)
    {
        var factors = new (string Name, double Value)[]
        {
            ("Source", hidden.SourceRockQuality),
            ("Reservoir", hidden.ReservoirQuality),
            ("Trap", hidden.TrapIntegrity),
            ("Seal", hidden.SealQuality),
            ("Timing", hidden.TimingMigration)
        };

        return factors.MinBy(f => f.Value).Name;
    }
}
