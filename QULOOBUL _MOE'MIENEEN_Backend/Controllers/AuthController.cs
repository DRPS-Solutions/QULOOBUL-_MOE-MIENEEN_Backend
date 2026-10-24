using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting(RateLimitPolicy)]
    public class AuthController(AuthService auth) : ControllerBase
    {
        /// <summary>Program.cs registers this limiter: a small per-IP budget for the anonymous routes.</summary>
        public const string RateLimitPolicy = "auth";

        /// <summary>
        /// Login user and return JWT token
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost("login"), AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
        {
            // A wrong password is routine, so it is answered here rather than left to the middleware
            // (an exception escaping user code makes the debugger stop on every typo).
            try { return Ok(await auth.LoginAsync(req)); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new ErrorResponse(ex.Message)); }
        }

        /// <summary>Mobile "stay signed in": a refresh token for a new access token and refresh token.</summary>
        [HttpPost("refresh"), AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req)
        {
            try { return Ok(await auth.RefreshAsync(req)); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new ErrorResponse(ex.Message)); }
        }

        /// <summary>Mobile sign-out: revokes the refresh token. Always 204, so it reveals nothing.</summary>
        [HttpPost("logout"), AllowAnonymous]
        public async Task<IActionResult> Logout(RefreshRequest req)
        {
            await auth.RevokeAsync(req);
            return NoContent();
        }

        /// <summary>SM-03 setPassword: redeem the activation / reset link's token.</summary>
        [HttpPost("set-password"), AllowAnonymous]
        public async Task<IActionResult> SetPassword(SetPasswordRequest req)
        {
            // 403, not 401: the client treats a 401 as an expired session and would hide the message.
            try { await auth.SetPasswordAsync(req); }
            catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse(ex.Message)); }
            return NoContent();
        }

        /// <summary>"Forgot password?": notifies the admins, who issue a temporary password.</summary>
        [HttpPost("forgot-password"), AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
        {
            await auth.ForgotPasswordAsync(req);
            return NoContent();
        }
    }
}