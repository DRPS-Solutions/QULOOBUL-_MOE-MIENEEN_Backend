using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    public class UsersController(StaffOverviewService staff, UserService users, AuditService audit, AuthService auth) : ApiControllerBase
    {
        /// <summary>Staff directory: every signed-in user needs it for names and assignee pickers.</summary>
        [HttpGet]
        public async Task<ActionResult<List<UserSummaryDto>>> Directory() => Ok(await staff.StaffAsync());
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>UC-06 per-person workload counts.</summary>
        [HttpGet("overview"), Authorize(Policy = Permissions.ManageUsers)]
        public async Task<ActionResult<List<StaffOverviewRow>>> Overview() => Ok(await staff.RowsAsync());
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// UC-07 Create a new user. Only available to users with the ManageUsers permission.
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost, Authorize(Policy = Permissions.ManageUsers)]
        public async Task<ActionResult<UserDto>> Create(CreateUserRequest req) =>
            Ok(await users.CreateAsync(req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>The main admin's view of what a user has done (Audit_Log), newest first.</summary>
        [HttpGet("{id:int}/audit"), Authorize(Policy = Permissions.ManageUsers)]
        public async Task<ActionResult<List<AuditEntryDto>>> Audit(int id, int take = 100) =>
            Ok(await audit.ForUserAsync(id, take));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> Me() => Ok(await users.GetAsync(UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Emails the signed-in user a link to choose a new password.</summary>
        [HttpPost("me/password-link")]
        public async Task<IActionResult> PasswordLink()
        {
            await users.SendOwnPasswordLinkAsync(UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Replace the current password, e.g. an admin's temporary one. The one route a temporary
        /// password unlocks (Program.cs); rate-limited like login since it checks a password.</summary>
        [HttpPost("me/password"), EnableRateLimiting(AuthController.RateLimitPolicy)]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
        {
            await auth.ChangePasswordAsync(UserId(), req);
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-03 updateProfile: self, or an admin (who alone may change the role).</summary>
        [HttpPatch("{id:int}")]
        public async Task<ActionResult<UserDto>> Update(int id, UpdateUserRequest req) =>
            Ok(await users.UpdateProfileAsync(id, req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/deactivate"), Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> Deactivate(int id)
        {
            await users.DeactivateAsync(id, UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/reactivate"), Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> Reactivate(int id)
        {
            await users.ReactivateAsync(id, UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Returns a temporary password for the admin to hand over; the user must change it at sign-in.</summary>
        [HttpPost("{id:int}/reset-password"), Authorize(Policy = Permissions.ManageUsers)]
        public async Task<ActionResult<TemporaryPasswordDto>> ResetPassword(int id) =>
            Ok(new TemporaryPasswordDto(await users.ResetPasswordAsync(id, UserId())));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/anonymise"), Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> Anonymise(int id)
        {
            await users.AnonymiseAsync(id, UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        [HttpDelete("{id:int}"), Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> Delete(int id)
        {
            await users.DeleteAsync(id, UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        // Any signed-in user: clients need the levels to offer only legal assignees.
        [HttpGet("~/api/roles")]
        public async Task<ActionResult<List<RoleDto>>> Roles() => Ok(await users.RolesAsync());
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//