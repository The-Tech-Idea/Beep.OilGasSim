namespace Beep.OilGasSim.Domain.Collaboration;

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameSessionId { get; set; }
    public Guid? CompanyId { get; set; }
    public string SenderName { get; set; } = "";
    public string Channel { get; set; } = "public";
    public string Text { get; set; } = "";
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}
