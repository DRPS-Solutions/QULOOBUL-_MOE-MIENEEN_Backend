using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Shared.Files;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    public class FileManagementService(AppDbContext db, FileUploadService uploads, AuditService audit)
    {
        public Task<List<FileMetadataDto>> ListAsync(int? taskId, int? meetingId, int? categoryId) =>
            db.FileUploads
                .Where(f => (taskId == null || f.TaskId == taskId)
                         && (meetingId == null || f.MeetingId == meetingId)
                         && (categoryId == null || f.CategoryId == categoryId))
                .Select(f => new FileMetadataDto(
                    f.FileId, f.FileName, f.FileType, f.CategoryId, f.Category!.CategoryName,
                    f.UploadedBy, f.TaskId, f.MeetingId, f.PeriodMonth))
                .ToListAsync();

        public async Task<FileDownloadResponse> DownloadAsync(int fileId)
        {
            var f = await db.FileUploads.FindAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");
            var uri = await uploads.GetDownloadUriAsync(fileId);
            return new FileDownloadResponse(fileId, f.FileName, uri.ToString(), DateTime.UtcNow.AddMinutes(15));
        }

        public async Task RecategoriseAsync(int fileId, int categoryId, int actorId)
        {
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