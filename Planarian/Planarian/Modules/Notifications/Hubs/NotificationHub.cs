using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Planarian.Modules.Notifications.Hubs;

public static class NotificationHubContract
{
    public const string Route = "/api/notificationHub";
    public const string ReceiveNotification = "ReceiveNotification";
}

[Authorize]
public class NotificationHub : Hub
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
