using Beep.OilGasSim.Domain.Common;
using Beep.OilGasSim.Domain.Economy;

namespace Beep.OilGasSim.Domain.Companies;

public sealed partial class Company
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "#2563eb";
    public CompanyFinance Finance { get; set; } = new();
    public CompanyReputation Reputation { get; set; } = new();
    public List<CompanyPlayer> Players { get; set; } = [];
}

public sealed class CompanyPlayer
{
    public Guid PlayerId { get; set; }
    public Guid CompanyId { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsHost { get; set; }
    public bool IsReady { get; set; }
}

public sealed class CompanyReputation
{
    public int Safety { get; set; } = 50;
    public int Environmental { get; set; } = 50;
    public int Overall { get; set; } = 50;
}
