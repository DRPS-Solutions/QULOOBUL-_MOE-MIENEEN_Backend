using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;
using QULOOBUL__MOE_MIENEEN_Backend.Models.File;
using QULOOBUL__MOE_MIENEEN_Shared.Files;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    public class FileManagementService(
        AppDbContext db, FileUploadService uploads, AuditService audit, AccessGuard guard,
        ILogger<FileManagementService> log)
    {
        /// <summary>
        /// UC-07's "one place to search all documents", narrowed to the documents the caller may see.
        /// An explicit task/meeting filter is checked outright so a guessed id returns 403, not an
        /// empty list that would confirm the record exists. Archived files are left out unless asked for.
        /// </summary>
        public async Task<List<FileMetadataDto>> ListAsync(
            int? taskId, int? meetingId, int? categoryId, string? search, bool includeArchived, int userId)
        {
            if (taskId is int t) await guard.EnsureTaskAsync(userId, t);
            if (meetingId is int m) await guard.EnsureMeetingAsync(userId, m);

            var q = db.FileUploads
                .Where(f => (taskId == null || f.TaskId == taskId)
                         && (meetingId == null || f.MeetingId == meetingId)
                         && (categoryId == null || f.CategoryId == categoryId)
                         && (search == null || f.FileName.Contains(search))
                         && (includeArchived || !f.IsArchived));

            return await guard.VisibleFiles(q, userId, await guard.HasPermissionAsync(userId, Permissions.ManageFiles))
                .OrderBy(f => f.FileName)
                .Select(f => new FileMetadataDto(
                    f.FileId, f.FileName, f.FileType, f.CategoryId, f.Category!.CategoryName,
                    f.UploadedBy, f.TaskId, f.MeetingId, f.PeriodMonth, f.IsArchived))
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
            var f = await FindManagedAsync(fileId, actorId);
            if (!await db.FileCategories.AnyAsync(c => c.CategoryId == categoryId))
                throw new InvalidOperationException($"Unknown category {categoryId}.");
            f.CategoryId = categoryId;
            audit.Record(actorId, "file.recategorise", nameof(FileUpload), fileId);
            await db.SaveChangesAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>UC-07 alternate flow, admin only: "archive files". Reversible; nothing is removed.</summary>
        public async Task SetArchivedAsync(int fileId, bool archived, int actorId)
        {
            var f = await FindManagedAsync(fileId, actorId);
            f.IsArchived = archived;
            audit.Record(actorId, archived ? "file.archive" : "file.unarchive", nameof(FileUpload), fileId);
            await db.SaveChangesAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Admin only. Deletes the row, then the blob: if the blob delete fails the file is merely
        /// orphaned in private storage (and soft-deleted there after 30 days), never a row pointing at nothing.
        /// </summary>
        public async Task DeleteAsync(int fileId, int actorId)
        {
            var f = await FindManagedAsync(fileId, actorId);
            db.FileUploads.Remove(f);
            audit.Record(actorId, "file.delete", nameof(FileUpload), fileId);
            await db.SaveChangesAsync();
            // The row is gone, so the delete succeeded: a blob failure must not surface as a 500.
            try { await uploads.DeleteBlobAsync(f); }
            catch (Exception ex) { log.LogError(ex, "Deleting blob for file {Id} failed; left orphaned", fileId); }
        }
//----------------------------------------------------------------------------------------------------------------------//
        private async Task<FileUpload> FindManagedAsync(int fileId, int actorId)
        {
            await guard.EnsureFileAsync(actorId, fileId);
            return await db.FileUploads.FindAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
