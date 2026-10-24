// Plain data carrier for the fields collected by CreateTaskDialog and
// TaskDetailsDialog. AppDataService turns it into API calls, so the
// dialogs and Tasks.razor don't need to share a private nested type.
//
// Lives in the project's root namespace, so the dialogs and Tasks.razor
// see it without an extra @using.
namespace Frontend_BlazorWeb;

public class NewTaskResult
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Midnight means an all-day task (see TaskCardExtensions.IsAllDay).
    public DateTime ScheduledStart { get; set; }
    public string Priority { get; set; } = "Low";
    public int? AssigneeId { get; set; }

    // The linked event (project); null for a daily task.
    public int? ProjectId { get; set; }
}
