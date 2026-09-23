using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.File;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// UC-07 / SD-04: upload a file and attach it to a task, a meeting, or a reporting period.
    /// The API only ever writes the File_Upload metadata row and hands back a single-blob SAS;
    /// the bytes go straight from the client to Blob Storage.
    /// </summary>
    public class FileUploadService(
        AppDbContext db, BlobStorageService blob, AuditService audit, IConfiguration config)
    {
        public const string Container = "attachments";

        /// <summary>SD-04: validate(file) [ size &lt;= 25 MB AND fileType is permitted ].</summary>
        public const long DefaultMaxBytes = 25L * 1024 * 1024;

        private static readonly string[] DefaultExtensions =
            [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
             ".txt", ".csv", ".png", ".jpg", ".jpeg", ".gif", ".webp"];

        private long MaxBytes => config.GetValue<long?>("Files:MaxBytes") ?? DefaultMaxBytes;

        private string[] AllowedExtensions =>
            config.GetSection("Files:AllowedExtensions").Get<string[]>() ?? DefaultExtensions;
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>At most one attachment context may be set (architecture doc / CK_FileUpload_OneContext).</summary>
        public record AttachmentContext(int? TaskId = null, int? MeetingId = null, DateTime? PeriodMonth = null)
        {
            public int Count => (TaskId is null ? 0 : 1) + (MeetingId is null ? 0 : 1) + (PeriodMonth is null ? 0 : 1);
        }
//----------------------------------------------------------------------------------------------------------------------//
        public record UploadTicket(FileUpload File, Uri UploadUri);
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Records the file metadata and returns a SAS the caller uploads to. FilePath is the
        /// blob path (container/blobName), not a public URL.
        /// </summary>
        public async Task<UploadTicket> CreateUploadAsync(
            int uploadedBy, int categoryId, string fileName, string fileType, long sizeBytes,
            AttachmentContext context)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is required.", nameof(fileName));

            // SD-04 validates before a SAS is issued: once the client holds one, the bytes go
            // straight to Blob Storage and the API never sees them again.
            if (sizeBytes <= 0 || sizeBytes > MaxBytes)
                throw new ArgumentException(
                    $"Files must be between 1 byte and {MaxBytes / (1024 * 1024)} MB.", nameof(sizeBytes));

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension)
                || !AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"\"{extension}\" is not a permitted file type.", nameof(fileName));

            if (context.Count > 1)
                throw new InvalidOperationException("A file may be attached to only one of: task, meeting, period.");
            if (!await db.FileCategories.AnyAsync(c => c.CategoryId == categoryId))
                throw new InvalidOperationException($"Unknown file category {categoryId}.");
            if (context.TaskId is int t && !await db.Tasks.AnyAsync(x => x.TaskId == t))
                throw new InvalidOperationException($"Unknown task {t}.");
            if (context.MeetingId is int m && !await db.Meetings.AnyAsync(x => x.MeetingId == m))
                throw new InvalidOperationException($"Unknown meeting {m}.");

            var blobName = $"{Guid.NewGuid():N}{extension}";

            var file = new FileUpload
            {
                CategoryId = categoryId,
                UploadedBy = uploadedBy,
                FileName = fileName,
                FileType = fileType,
                FilePath = $"{Container}/{blobName}",
                TaskId = context.TaskId,
                MeetingId = context.MeetingId,
                PeriodMonth = context.PeriodMonth,
            };
            db.FileUploads.Add(file);
            await db.SaveChangesAsync();               // assigns file.FileId
            await audit.RecordAndSaveAsync(uploadedBy, "file.upload", nameof(FileUpload), file.FileId);

            var uploadUri = await blob.CreateUploadSasAsync(Container, blobName);
            return new UploadTicket(file, uploadUri);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>A short-lived read SAS for downloading an existing file.</summary>
        public async Task<Uri> GetDownloadUriAsync(int fileId)
        {
            var file = await db.FileUploads.FindAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");
            return await blob.CreateReadSasAsync(Container, BlobName(file));
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Removes the blob; the caller deletes the row. The container's soft delete is the undo.</summary>
        public Task DeleteBlobAsync(FileUpload file) => blob.DeleteAsync(Container, BlobName(file));

        private static string BlobName(FileUpload file) => file.FilePath.Split('/', 2)[^1];
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//