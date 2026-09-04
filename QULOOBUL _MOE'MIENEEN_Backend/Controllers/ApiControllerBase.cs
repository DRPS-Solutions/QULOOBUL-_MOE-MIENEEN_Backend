using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}