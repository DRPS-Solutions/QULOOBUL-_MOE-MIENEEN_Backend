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
        public async Task<ActionResult<ReportDto>> Build(ReportRequest req) =>
            Ok(await reports.BuildAsync(req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Same figures as a CSV in Blob Storage; returns a 15-minute download link.</summary>
        [HttpPost("export")]
        public async Task<ActionResult<ReportExportResponse>> Export(ReportRequest req) =>
            Ok(await reports.ExportAsync(req, UserId()));
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
