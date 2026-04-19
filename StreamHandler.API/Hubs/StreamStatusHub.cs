using Microsoft.AspNetCore.SignalR;

namespace StreamHandler.API.Hubs;

/// <summary>
/// SignalR hub. Clients connect and receive real-time stream status updates.
/// The server pushes to all connected clients via IHubContext<StreamStatusHub>.
/// </summary>
public class StreamStatusHub : Hub
{
    // Clients subscribe simply by connecting — no join/group logic needed.
    // Pushed event name: "StreamStatusUpdated"
    // Pushed event name: "StatusSummaryUpdated"
}
