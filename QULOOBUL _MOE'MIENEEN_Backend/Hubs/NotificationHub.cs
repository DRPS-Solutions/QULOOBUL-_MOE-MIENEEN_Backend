using Microsoft.AspNetCore.SignalR;

namespace QULOOBUL__MOE_MIENEEN_Backend.Hubs
{
    /// <summary>
    /// The Observer subject (architecture doc, "Observer"). Each connection joins a group
    /// named after its user so a service can push to "user-{id}" without knowing which
    /// devices are listening. Services send via IHubContext&lt;NotificationHub&gt;.
    /// Reference: Gamma, E., Helm, R., Johnson, R. and Vlissides, J. (2016) Design patterns: elements of reusable object-oriented software. Boston: Addison-Wesley.
    /// Reference: Microsoft (2026) Use hubs in ASP.NET Core SignalR. Available at: https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs?view=aspnetcore-10.0 (Accessed: 8 August 2026).
    /// </summary>
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class NotificationHub : Hub
    {
        public static string GroupFor(int userId) => $"user-{userId}";

        public override async Task OnConnectedAsync()
        {
            var sub = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            // Reference: Microsoft (2026) Manage users and groups in SignalR. Available at: https://learn.microsoft.com/en-us/aspnet/core/signalr/groups (Accessed: 4 October 2026).
            if (int.TryParse(sub, out var userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));
            await base.OnConnectedAsync();
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//