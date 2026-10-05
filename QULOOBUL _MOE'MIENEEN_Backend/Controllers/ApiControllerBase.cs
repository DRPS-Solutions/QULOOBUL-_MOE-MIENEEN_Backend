using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    /// <summary>
    /// Base for every API controller: resource-oriented routes, authenticated by default, no logic
    /// (each action delegates to its use-case service).
    /// Reference: R. T. Fielding, "Architectural styles and the design of network-based software architectures," Ph.D. dissertation, Univ. of California, Irvine, CA, USA, 2000.
    /// Reference: Microsoft, "Create web APIs with ASP.NET Core." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/aspnet/core/web-api/
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}