using System.Security.Claims;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;

namespace QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Middleware
{
    /// <summary>
    /// Writes an Audit_Log row for every successful state-changing request by a signed-in user.
    /// Reference: Microsoft (2026) Write custom ASP.NET Core middleware. Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write (Accessed: 4 October 2026).
    /// Reference: OWASP Foundation (2025) OWASP Top 10:2025. Available at: https://owasp.org/Top10/2025/ (Accessed: 8 August 2026).
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