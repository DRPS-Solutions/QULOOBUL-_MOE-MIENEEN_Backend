using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace QULOOBUL__MOE_MIENEEN_Shared.Files
{
    /// <summary>Ask the API to record metadata and hand back an upload SAS.</summary>
    public record FileUploadInitRequest
    {
        [Required, MaxLength(260)] 
        public string FileName { get; init; } = string.Empty;

        [Required, MaxLength(100)]
        public string FileType { get; init; } = string.Empty;

        /// <summary>Checked against the SD-04 upload limit before a SAS is issued.</summary>
        [Required, Range(1, long.MaxValue)]
        public long SizeBytes { get; init; }

        [Required]
        public int CategoryId { get; init; }
        public int? TaskId { get; init; }
        public int? MeetingId { get; init; }
        public DateTime? PeriodMonth { get; init; }
    }

    public record FileUploadResponse(int FileId, string UploadUrl, DateTime ExpiresAtUtc);
    public record FileDownloadResponse(int FileId, string FileName, string DownloadUrl, DateTime ExpiresAtUtc);

    public record FileMetadataDto(
        int FileId, string FileName, string FileType, int CategoryId, string CategoryName,
        int UploadedBy, int? TaskId, int? MeetingId, DateTime? PeriodMonth, bool IsArchived);

    public record FileCategoryDto(int CategoryId, string CategoryName);
    public record RecategoriseFileRequest { [Required] public int CategoryId { get; init; } }
}
