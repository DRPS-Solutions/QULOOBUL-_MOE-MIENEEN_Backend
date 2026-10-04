using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    /// <summary>
    /// Base for every API controller: resource-oriented routes, authenticated by default, no logic
    /// (each action delegates to its use-case service).
    /// Reference: Fielding, R.T. (2000) Architectural styles and the design of network-based software architectures. PhD thesis. University of California, Irvine.
    /// Reference: Microsoft (2026) Create web APIs with ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/web-api/ (Accessed: 4 October 2026).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}