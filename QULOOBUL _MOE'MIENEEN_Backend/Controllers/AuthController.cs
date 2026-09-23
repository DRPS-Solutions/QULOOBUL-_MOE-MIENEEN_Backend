using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;

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
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest req) => Ok(await auth.LoginAsync(req));

        /// <summary>SM-03 setPassword: redeem the activation / reset link's token.</summary>
        [HttpPost("set-password"), AllowAnonymous]
        public async Task<IActionResult> SetPassword(SetPasswordRequest req)
        {
            await auth.SetPasswordAsync(req);
            return NoContent();
        }
    }
}