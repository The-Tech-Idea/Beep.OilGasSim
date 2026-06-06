namespace Beep.OilGasSim.Domain.Companies;

public sealed partial class Company
{
    public decimal CompanyValue { get; set; }
    public int Rank { get; set; } = 1;
    public double TotalProductionBoePerDay { get; set; }
    public double TotalReservesMmboe { get; set; }
    public bool TurnCommitted { get; set; }
}
