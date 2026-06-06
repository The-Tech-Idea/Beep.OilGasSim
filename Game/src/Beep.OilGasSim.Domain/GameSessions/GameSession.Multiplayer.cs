namespace Beep.OilGasSim.Domain.GameSessions;

public sealed partial class GameSession
{
    public bool IsMultiplayer { get; set; }
    public string JoinCode { get; set; } = "";
    public int MaxPlayers { get; set; } = 6;
    public int MinPlayers { get; set; } = 2;
    public Guid? HostCompanyId { get; set; }
}
