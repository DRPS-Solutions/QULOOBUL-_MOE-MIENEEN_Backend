using System.Security.Claims;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;

namespace QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Middleware
{
    /// <summary>
    /// Writes an Audit_Log row for every successful state-changing request by a signed-in user.
    /// Reference: Microsoft, "Write custom ASP.NET Core middleware." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write
    /// Reference: OWASP Foundation, "OWASP Top 10:2025." Accessed: Aug. 08, 2026. [Online]. Available: https://owasp.org/Top10/2025/
    /// </summary>
    public class AuditMiddleware(RequestDelegate next)
    {
        private static readonly string[] Mutating = ["POST", "PUT", "PATCH", "DELETE"];
//----------------------------------------------------------------------------------------------------------------------//
        public async Task Invoke(HttpContext ctx, AppDbContext db)
        {
            await next(ctx);

            if (!Mutating.Contains(ctx.Request.Method) || ctx.Response.StatusCode >= 400) return;
            var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(sub, out var userId)) return;

            db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = $"{ctx.Request.Method} {ctx.Request.Path}",
                EntityType = "http",
                EntityId = 0,
                Timestamp = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//