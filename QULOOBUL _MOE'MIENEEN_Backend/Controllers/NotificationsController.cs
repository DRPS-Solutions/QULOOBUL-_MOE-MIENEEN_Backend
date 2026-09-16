using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Notifications;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    public class NotificationsController(NotificationQueryService notifications) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<NotificationDto>>> List() =>
            Ok(await notifications.ForUserAsync(UserId(), unreadOnly: false));

        [HttpPost("read")]
        public async Task<IActionResult> MarkRead(MarkReadRequest req)
        {
            await notifications.MarkReadAsync(UserId(), req.NotificationIds);
            return NoContent();
        }
    }
}
