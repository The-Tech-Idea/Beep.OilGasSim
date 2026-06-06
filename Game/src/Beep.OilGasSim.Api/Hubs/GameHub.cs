using Microsoft.AspNetCore.SignalR;

namespace Beep.OilGasSim.Api.Hubs;

public sealed class GameHub : Hub
{
    public async Task JoinSession(Guid sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}");
    }

    public async Task LeaveSession(Guid sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session:{sessionId}");
    }
}
