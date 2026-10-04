using Microsoft.AspNetCore.SignalR;

namespace QULOOBUL__MOE_MIENEEN_Backend.Hubs
{
    /// <summary>
    /// The Observer subject (architecture doc, "Observer"). Each connection joins a group
    /// named after its user so a service can push to "user-{id}" without knowing which
    /// devices are listening. Services send via IHubContext&lt;NotificationHub&gt;.
    /// Reference: E. Gamma, R. Helm, R. E. Johnson, and J. Vlissides, Design Patterns: Elements of Reusable Object-Oriented Software. Boston, MA, USA: Addison-Wesley, 2016.
    /// Reference: Microsoft, "Use hubs in ASP.NET Core SignalR." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs?view=aspnetcore-10.0
    /// </summary>
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class NotificationHub : Hub
    {
        public static string GroupFor(int userId) => $"user-{userId}";

        public override async Task OnConnectedAsync()
        {
            var sub = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            // Reference: Microsoft, "Manage users and groups in SignalR." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/aspnet/core/signalr/groups
            if (int.TryParse(sub, out var userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));
            await base.OnConnectedAsync();
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//