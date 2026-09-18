using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(AuthService auth) : ControllerBase
    {
        /// <summary>
        /// Login user and return JWT token
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost("login"), AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest req) => Ok(await auth.LoginAsync(req));
    }
}