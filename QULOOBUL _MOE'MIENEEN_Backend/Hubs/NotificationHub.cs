using Microsoft.AspNetCore.SignalR;

namespace QULOOBUL__MOE_MIENEEN_Backend.Hubs
{
    /// <summary>
    /// The Observer subject (architecture doc, "Observer"). Each connection joins a group
    /// named after its user so a service can push to "user-{id}" without knowing which
    /// devices are listening. Services send via IHubContext&lt;NotificationHub&gt;.
    /// </summary>
    public class NotificationHub : Hub
    {
        public static string GroupFor(int userId) => $"user-{userId}";

        public override async Task OnConnectedAsync()
        {
            // ponytail: trusts the "userId" query arg. Replace with Context.UserIdentifier
            // once JWT auth is wired (architecture doc, "Dependency injection" / middleware chain).
            var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
            if (!string.IsNullOrEmpty(userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(int.Parse(userId)));
            await base.OnConnectedAsync();
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//