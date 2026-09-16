// Plain data carrier for the fields collected by CreateTaskDialog.
// Tasks.razor maps this onto an AppTask (via AppDataService.AddTask)
// after the dialog is confirmed, so the two components don't need to
// share a private nested type.
//
// No namespace declared here on purpose - drop this file anywhere in
// the project (e.g. a Models/ folder) and it'll be visible to both
// CreateTaskDialog.razor and Tasks.razor without an extra @using.
public class NewTaskResult
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ScheduledStart { get; set; }
    public bool IsAllDay { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = "Low";
    public string Assignee { get; set; } = string.Empty;

    // "Daily" | "Event"
    public string Type { get; set; } = "Daily";

    // Only set when Type == "Event".
    public Guid? LinkedEventId { get; set; }
    public string? LinkedEventTitle { get; set; }
}
