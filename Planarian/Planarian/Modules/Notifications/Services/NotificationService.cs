using Microsoft.AspNetCore.SignalR;
using Planarian.Modules.Notifications.Hubs;

namespace Planarian.Modules.Notifications.Services;

public class NotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendNotificationToGroupAsync(string groupName, string message)
    {
        await _hubContext.Clients.Group(groupName).SendAsync("ReceiveNotification", message);
    }
}