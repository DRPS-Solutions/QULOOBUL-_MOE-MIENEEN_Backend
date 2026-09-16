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

    public record TaskDetailDto(
        int TaskId, int ProjectId, string Title, string Status, string Priority,
        int PercentComplete, DateTime Deadline, int CreatedBy, int? CompletedBy,
        IReadOnlyList<TaskCommentDto> Comments,
        IReadOnlyList<TaskStatusHistoryDto> History,
        string? Description);

    public record CreateTaskRequest
    {
        [Required]
        public int ProjectId { get; init; }
        [Required, MaxLength(200)]
        public string Title { get; init; } = string.Empty;
        [MaxLength(2000)]
        public string? Description { get; init; }
        [MaxLength(20)]
        public string Priority { get; init; } = "Normal";
        [Required]
        public DateTime Deadline { get; init; }
    }

    public record UpdateTaskRequest
    {
        [MaxLength(200)]
        public string? Title { get; init; }
        [MaxLength(2000)]
        public string? Description { get; init; }
        [MaxLength(20)]
        public string? Priority { get; init; }
        public DateTime? Deadline { get; init; }
        [Range(0, 100)]
        public int? PercentComplete { get; init; }
        /// <summary>Moves the task to another project (the web client's "linked event").</summary>
        public int? ProjectId { get; init; }
    }

    /// <summary>Kanban column move — SM-01 guarded server-side.</summary>
    public record MoveTaskRequest
    {
        [Required, MaxLength(30)] 
        public string NewStatus { get; init; } = string.Empty;
    }

    public record AssignTaskRequest
    {
        [Required] 
        public int AssigneeId { get; init; }
    }

    public record AddTaskCommentRequest
    {
        [Required, MaxLength(2000)] 
        public string Comment { get; init; } = string.Empty;
    }

    public record TaskCommentDto(int CommentId, int UserId, string Comment, DateTime TimeCreated);
    public record TaskStatusHistoryDto(string NewStatus, int ChangedBy, DateTime TimeCreated);
}
