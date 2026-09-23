using QULOOBUL__MOE_MIENEEN_Shared;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Middleware
{
    /// <summary>Last link in the pipeline: turns service exceptions into a single ErrorResponse shape.</summary>
    public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
    {
        public async Task Invoke(HttpContext ctx)
        {
            try
            {
                await next(ctx);
            }
            catch (Exception ex)
            {
                var status = ex switch
                {
                    KeyNotFoundException => StatusCodes.Status404NotFound,
                    UnauthorizedAccessException => StatusCodes.Status403Forbidden,
                    InvalidOperationException or ArgumentException => StatusCodes.Status400BadRequest,
                    _ => StatusCodes.Status500InternalServerError,
                };
                if (status == 500) log.LogError(ex, "Unhandled exception");

                ctx.Response.StatusCode = status;
                await ctx.Response.WriteAsJsonAsync(new ErrorResponse(
                    status == 500 ? "An unexpected error occurred." : ex.Message));
            }
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//