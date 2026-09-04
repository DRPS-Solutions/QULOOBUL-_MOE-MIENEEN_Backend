using Microsoft.AspNetCore.SignalR;

namespace QULOOBUL__MOE_MIENEEN_Backend.Hubs
{
    /// <summary>
    /// The Observer subject (architecture doc, "Observer"). Each connection joins a group
    /// named after its user so a service can push to "user-{id}" without knowing which
    /// devices are listening. Services send via IHubContext&lt;NotificationHub&gt;.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class NotificationHub : Hub
    {
        public static string GroupFor(int userId) => $"user-{userId}";

        public override async Task OnConnectedAsync()
        {
            var sub = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(sub, out var userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));
            await base.OnConnectedAsync();
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//