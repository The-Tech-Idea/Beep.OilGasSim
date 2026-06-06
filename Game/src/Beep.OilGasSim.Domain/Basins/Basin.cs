using Beep.OilGasSim.Domain.Blocks;

namespace Beep.OilGasSim.Domain.Basins;

public sealed partial class Basin
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public string Name { get; set; } = "";
    public string BasinType { get; set; } = "DesertOnshore";
    public double GeologicalPotential { get; set; }
    public double InfrastructureMaturity { get; set; }
    public double ServiceCostIndex { get; set; } = 1.0;
    public List<LicenseBlock> Blocks { get; set; } = [];
}
