using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace QULOOBUL__MOE_MIENEEN_Shared.Reports
{
    public record ReportRequest
    {
        [Required]
        public DateTime PeriodStart { get; init; }

        [Required] 
        public DateTime PeriodEnd { get; init; }

        [Required, RegularExpression("TaskCompletion|Workload|ProjectProgress")]
        public string ReportType { get; init; } = "TaskCompletion";
        public int? SubjectUserId { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record TaskCompletionStat(string Bucket, int Completed, int Outstanding);
    public record WorkloadStat(int UserId, string FullName, int Assigned, int Completed, int Overdue);
    public record ProjectProgressStat(int ProjectId, string ProjectName, int Total, int Done, double PercentComplete);
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>UC-09 "progress over time": tasks completed in each bucket, plus the running total.</summary>
    public record ProgressPoint(DateTime BucketStart, int Completed, int CumulativeCompleted);

    public record ReportDto(
        string ReportType, DateTime PeriodStart, DateTime PeriodEnd,
        IReadOnlyList<TaskCompletionStat> Completion,
        IReadOnlyList<WorkloadStat> Workload,
        IReadOnlyList<ProjectProgressStat> Progress,
        IReadOnlyList<ProgressPoint> Trend);
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>The exported CSV lives in Blob Storage; this is a short-lived read SAS to it.</summary>
    public record ReportExportResponse(int ReportId, string DownloadUrl, DateTime ExpiresAtUtc);
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//