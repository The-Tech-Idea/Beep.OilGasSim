namespace Beep.OilGasSim.Domain.Economy;

public sealed class HedgePosition
{
    public Guid CompanyId { get; set; }
    public int ForTurnNumber { get; set; }
    public double HedgePercent { get; set; }
    public decimal HedgePrice { get; set; }
}
