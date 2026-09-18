using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Files;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    /// <summary>
    /// UC-07 / SD-04. The API records metadata and hands back a single-blob SAS; the bytes go
    /// straight between the client and Blob Storage and never pass through this process.
    /// </summary>
    public class FilesController(FileUploadService uploads, FileManagementService files) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<FileMetadataDto>>> List(int? taskId, int? meetingId, int? categoryId) =>
            Ok(await files.ListAsync(taskId, meetingId, categoryId, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpGet("categories")]
        public async Task<ActionResult<List<FileCategoryDto>>> Categories() => Ok(await files.CategoriesAsync());
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Validates the file, records its metadata row and returns the upload SAS.</summary>
        [HttpPost]
        public async Task<ActionResult<FileUploadResponse>> Create(FileUploadInitRequest req)
        {
            var ticket = await uploads.CreateUploadAsync(
                UserId(), req.CategoryId, req.FileName, req.FileType, req.SizeBytes,
                new FileUploadService.AttachmentContext(req.TaskId, req.MeetingId, req.PeriodMonth));

            return Ok(new FileUploadResponse(
                ticket.File.FileId, ticket.UploadUri.ToString(), DateTime.UtcNow.AddMinutes(15)));
        }
//----------------------------------------------------------------------------------------------------------------------//
        [HttpGet("{id:int}/download")]
        public async Task<ActionResult<FileDownloadResponse>> Download(int id) =>
            Ok(await files.DownloadAsync(id, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>UC-07 alternate flow: "Admin only: reorganise or archive files across categories".</summary>
        [HttpPatch("{id:int}/category"), Authorize(Policy = Permissions.ManageFiles)]
        public async Task<IActionResult> Recategorise(int id, RecategoriseFileRequest req)
        {
            await files.RecategoriseAsync(id, req.CategoryId, UserId());
            return NoContent();
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
