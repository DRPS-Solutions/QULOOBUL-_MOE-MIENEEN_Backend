using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Shared;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Middleware
{
    /// <summary>
    /// Last link in the pipeline: turns service exceptions into a single ErrorResponse shape.
    /// Reference: Microsoft (2026) Handle errors in ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling (Accessed: 4 October 2026).
    /// Reference: Microsoft (2026) ASP.NET Core middleware. Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-10.0 (Accessed: 8 August 2026).
    /// </summary>
    public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
    {
        public async Task Invoke(HttpContext ctx)
        {
            try
            {
                await next(ctx);
            }
            catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
            {
                // The client went away; nobody is left to tell.
            }
            catch (Exception ex)
            {
                var (status, message) = ex switch
                {
                    KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                    UnauthorizedAccessException => (StatusCodes.Status403Forbidden, ex.Message),
                    InvalidOperationException or ArgumentException => (StatusCodes.Status400BadRequest, ex.Message),
                    BadHttpRequestException or System.Text.Json.JsonException =>
                        (StatusCodes.Status400BadRequest, "The request was not in the expected format."),
                    // Reference: Microsoft (2026) Handling concurrency conflicts - EF Core. Available at: https://learn.microsoft.com/en-us/ef/core/saving/concurrency (Accessed: 4 October 2026).
                    DbUpdateConcurrencyException =>
                        (StatusCodes.Status409Conflict, "Someone else changed this at the same time. Refresh and try again."),
                    DbUpdateException =>
                        (StatusCodes.Status409Conflict, "That change conflicts with existing data (for example a duplicate)."),
                    _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
                };

                // Expected rejections are warnings; anything that is our fault (500) or a database failure keeps its stack trace.
                if (status == 500 || ex is DbUpdateException)
                    log.LogError(ex, "{Method} {Path} failed ({Trace})", ctx.Request.Method, ctx.Request.Path, ctx.TraceIdentifier);
                else
                    log.LogWarning("{Method} {Path} rejected with {Status}: {Message}", ctx.Request.Method, ctx.Request.Path, status, ex.Message);

                // Let support find the log line from what the user reports.
                if (status == 500) message += $" Reference: {ctx.TraceIdentifier}";

                if (ctx.Response.HasStarted) throw; // too late to change the response; let the server abort it
                ctx.Response.Clear();
                ctx.Response.StatusCode = status;
                await ctx.Response.WriteAsJsonAsync(new ErrorResponse(message));
            }
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
