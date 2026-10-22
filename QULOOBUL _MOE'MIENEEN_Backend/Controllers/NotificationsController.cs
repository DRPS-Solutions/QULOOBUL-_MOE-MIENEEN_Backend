using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Notifications;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    public class NotificationsController(NotificationQueryService notifications, PushHubService pushHub) : ApiControllerBase
    {
        /// <summary>
        /// Returns a list of notifications for the current user.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult<List<NotificationDto>>> List() =>
            Ok(await notifications.ForUserAsync(UserId(), unreadOnly: false));
        
        //----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Marks the specified notifications as read for the current user.
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost("read")]
        public async Task<IActionResult> MarkRead(MarkReadRequest req)
        {
            await notifications.MarkReadAsync(UserId(), req.NotificationIds);
            return NoContent();
        }
        
        //----------------------------------------------------------------------------------------------------------------------//
        /// <summary>The caller's per-type on/off settings (UC-08).</summary>
        [HttpGet("preferences")]
        public async Task<ActionResult<List<NotificationPreferenceDto>>> Preferences() =>
            Ok(await notifications.PreferencesAsync(UserId()));
        
        //----------------------------------------------------------------------------------------------------------------------//
        [HttpPut("preferences")]
        public async Task<IActionResult> SetPreferences(UpdateNotificationPreferencesRequest req)
        {
            await notifications.SetPreferencesAsync(UserId(), req.Preferences);
            return NoContent();
        }

        /// <summary>Mobile push: register the phone with the Notification Hub for the signed-in user</summary>
        //----------------------------------------------------------------------------------------------------------------------//
        [HttpPut("installations")]
        public async Task<IActionResult> RegisterInstallation(PushInstallationRequest request, CancellationToken cancellationToken)
        {
            await pushHub.RegisterAsync(UserId(), request, cancellationToken);
            return NoContent();
        }

        /// <summary>Mobile push: forget the phone (called on sign-out)</summary>
        //----------------------------------------------------------------------------------------------------------------------//
        [HttpDelete("installations/{installationId}")]
        public async Task<IActionResult> RemoveInstallation(string installationId, CancellationToken cancellationToken)
        {
            await pushHub.RemoveAsync(UserId(), installationId, cancellationToken);
            return NoContent();
        }

        //----------------------------------------------------------------------------------------------------------------------//
        /// <summary>UC-08 alternate flow: an admin messages chosen users directly, past their preferences.</summary>
        [HttpPost("send"), Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> Send(SendNotificationRequest req)
        {
            await notifications.SendAsync(req, UserId());
            return NoContent();
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//