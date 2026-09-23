// Plain data carrier for the fields collected by CreateTaskDialog and
// TaskDetailsDialog. AppDataService turns it into API calls, so the
// dialogs and Tasks.razor don't need to share a private nested type.
//
// No namespace declared here on purpose - drop this file anywhere in
// the project and it'll be visible to the dialogs and Tasks.razor
// without an extra @using.
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
