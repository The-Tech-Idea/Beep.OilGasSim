namespace Beep.OilGasSim.Domain.Blocks;

public sealed partial class HiddenGeology
{
    public double CalculateTrueChanceOfSuccess(double balanceModifier = 1.15)
    {
        var chance = SourceRockQuality
                     * ReservoirQuality
                     * TrapIntegrity
                     * SealQuality
                     * TimingMigration
                     * balanceModifier;

        if (DepthMeters > 4500)
        {
            chance -= 0.03;
        }

        return Math.Clamp(chance, 0.05, 0.60);
    }
}
