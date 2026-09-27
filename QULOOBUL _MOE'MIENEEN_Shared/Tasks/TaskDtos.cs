using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace QULOOBUL__MOE_MIENEEN_Shared.Tasks
{
    public record TaskCardDto(
    int TaskId, int ProjectId, string Title, string Status, string Priority,
    int PercentComplete, DateTime Deadline, IReadOnlyList<int> AssigneeIds,
    string? Description, int? CompletedBy);
//----------------------------------------------------------------------------------------------------------------------//
    public record TaskDetailDto(
        int TaskId, int ProjectId, string Title, string Status, string Priority,
        int PercentComplete, DateTime Deadline, int CreatedBy, int? CompletedBy,
        IReadOnlyList<TaskCommentDto> Comments,
        IReadOnlyList<TaskStatusHistoryDto> History,
        string? Description);
//----------------------------------------------------------------------------------------------------------------------//
    public record CreateTaskRequest
    {
        [Required, Range(1, int.MaxValue)]
        public int ProjectId { get; init; }
        [Required, MaxLength(200)]
        public string Title { get; init; } = string.Empty;
        [MaxLength(2000)]
        public string? Description { get; init; }
        [MaxLength(20), RegularExpression("Low|Normal|Medium|High")]
        public string Priority { get; init; } = "Normal";
        /// <summary>The Range rejects the unset default (0001-01-01), which [Required] alone lets through.</summary>
        [Required, Range(typeof(DateTime), "2000-01-01", "2100-12-31")]
        public DateTime Deadline { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record UpdateTaskRequest
    {
        [MaxLength(200)]
        public string? Title { get; init; }
        [MaxLength(2000)]
        public string? Description { get; init; }
        [MaxLength(20), RegularExpression("Low|Normal|Medium|High")]
        public string? Priority { get; init; }
        [Range(typeof(DateTime), "2000-01-01", "2100-12-31")]
        public DateTime? Deadline { get; init; }
        [Range(0, 100)]
        public int? PercentComplete { get; init; }
        /// <summary>Moves the task to another project (the web client's "linked event").</summary>
        [Range(1, int.MaxValue)]
        public int? ProjectId { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>Kanban column move — SM-01 guarded server-side.</summary>
    public record MoveTaskRequest
    {
        [Required, MaxLength(30)] 
        public string NewStatus { get; init; } = string.Empty;
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record AssignTaskRequest
    {
        [Required, Range(1, int.MaxValue)]
        public int AssigneeId { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record AddTaskCommentRequest
    {
        [Required, MaxLength(2000)] 
        public string Comment { get; init; } = string.Empty;
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>A recurring weekly template (Task_Schedule). IsActive = false pauses it.</summary>
    public record TaskScheduleDto(
        int ScheduleId, string Title, string DayOfWeek, int AssignedTo, int CreatedBy, bool IsActive);

    public record CreateTaskScheduleRequest
    {
        [Required, MaxLength(200)]
        public string Title { get; init; } = string.Empty;
        /// <summary>English day name, e.g. "Saturday".</summary>
        [Required, MaxLength(10)]
        public string DayOfWeek { get; init; } = string.Empty;
        [Required, Range(1, int.MaxValue)]
        public int AssignedTo { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record UpdateTaskScheduleRequest
    {
        [MaxLength(200)]
        public string? Title { get; init; }
        [MaxLength(10)]
        public string? DayOfWeek { get; init; }
        [Range(1, int.MaxValue)]
        public int? AssignedTo { get; init; }
        public bool? IsActive { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record TaskCommentDto(int CommentId, int UserId, string Comment, DateTime TimeCreated);
    public record TaskStatusHistoryDto(string NewStatus, int ChangedBy, DateTime TimeCreated);
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//