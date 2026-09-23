using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    public class UsersController(StaffOverviewService staff, UserService users) : ApiControllerBase
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
        [HttpPost("{id:int}/reset-password"), Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> ResetPassword(int id)
        {
            await users.ResetPasswordAsync(id, UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/anonymise"), Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> Anonymise(int id)
        {
            await users.AnonymiseAsync(id, UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        [HttpGet("~/api/roles"), Authorize(Policy = Permissions.ManageUsers)]
        public async Task<ActionResult<List<RoleDto>>> Roles() => Ok(await users.RolesAsync());
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//