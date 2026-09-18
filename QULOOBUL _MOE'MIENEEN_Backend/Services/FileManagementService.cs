using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Shared.Files;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    public class FileManagementService(
        AppDbContext db, FileUploadService uploads, AuditService audit, AccessGuard guard)
    {
        /// <summary>
        /// UC-07's "one place to search all documents", narrowed to the documents the caller may see.
        /// An explicit task/meeting filter is checked outright so a guessed id returns 403, not an
        /// empty list that would confirm the record exists.
        /// </summary>
        public async Task<List<FileMetadataDto>> ListAsync(int? taskId, int? meetingId, int? categoryId, int userId)
        {
            if (taskId is int t) await guard.EnsureTaskAsync(userId, t);
            if (meetingId is int m) await guard.EnsureMeetingAsync(userId, m);

            var q = db.FileUploads
                .Where(f => (taskId == null || f.TaskId == taskId)
                         && (meetingId == null || f.MeetingId == meetingId)
                         && (categoryId == null || f.CategoryId == categoryId));

            return await guard.VisibleFiles(q, userId, await guard.IsAdminAsync(userId))
                .Select(f => new FileMetadataDto(
                    f.FileId, f.FileName, f.FileType, f.CategoryId, f.Category!.CategoryName,
                    f.UploadedBy, f.TaskId, f.MeetingId, f.PeriodMonth))
                .ToListAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        public Task<List<FileCategoryDto>> CategoriesAsync() =>
            db.FileCategories.OrderBy(c => c.CategoryName)
                .Select(c => new FileCategoryDto(c.CategoryId, c.CategoryName)).ToListAsync();
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<FileDownloadResponse> DownloadAsync(int fileId, int userId)
        {
            await guard.EnsureFileAsync(userId, fileId);
            var f = await db.FileUploads.FindAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");
            var uri = await uploads.GetDownloadUriAsync(fileId);
            return new FileDownloadResponse(fileId, f.FileName, uri.ToString(), DateTime.UtcNow.AddMinutes(15));
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>UC-07 alternate flow, admin only: reorganise files across categories.
        /// The ManageFiles policy on the route is the first gate; this re-checks the file itself.</summary>
        public async Task RecategoriseAsync(int fileId, int categoryId, int actorId)
        {
            await guard.EnsureFileAsync(actorId, fileId);
            var f = await db.FileUploads.FindAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");
            if (!await db.FileCategories.AnyAsync(c => c.CategoryId == categoryId))
                throw new InvalidOperationException($"Unknown category {categoryId}.");
            f.CategoryId = categoryId;
            audit.Record(actorId, "file.recategorise", nameof(QULOOBUL__MOE_MIENEEN_Backend.Models.File.FileUpload), fileId);
            await db.SaveChangesAsync();
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
