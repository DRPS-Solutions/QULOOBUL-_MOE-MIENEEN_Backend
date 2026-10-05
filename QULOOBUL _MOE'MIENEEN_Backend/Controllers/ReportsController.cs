using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Reports;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    /// <summary>UC-09: admin-only reporting dashboard.</summary>
    [Authorize(Policy = Permissions.GenerateReports)]
    public class ReportsController(ReportingService reports) : ApiControllerBase
    {
        [HttpPost]
        public Task<ActionResult<ReportDto>> Build(ReportRequest req) =>
            Refusable(() => reports.BuildAsync(req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Same figures as a CSV in Blob Storage; returns a 15-minute download link.</summary>
        [HttpPost("export")]
        public Task<ActionResult<ReportExportResponse>> Export(ReportRequest req) =>
            Refusable(() => reports.ExportAsync(req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// "Bad period" and "not enough data" are expected outcomes (UC-09), so they become a 400 here, in our own
        /// code, rather than an exception unwinding through the framework (which also trips the debugger).
        /// </summary>
        private async Task<ActionResult<T>> Refusable<T>(Func<Task<T>> run)
        {
            try { return Ok(await run()); }
            catch (ArgumentException ex) { return BadRequest(new ErrorResponse(ex.Message)); }
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
