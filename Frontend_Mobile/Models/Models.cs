using System.ComponentModel;
using System.Runtime.CompilerServices;
using Frontend_Mobile.Data;
using QULOOBUL__MOE_MIENEEN_Shared.Notifications;
using QULOOBUL__MOE_MIENEEN_Shared.Requests;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;

namespace Frontend_Mobile.Models;

/// <summary>Minimal INotifyPropertyChanged base, used by AppState for the Shell's bindings.</summary>
public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}

/// <summary>
/// Value + display text, so a Picker can bind straight to an enum list and
/// still show "Not linked" rather than "None".
/// </summary>
public sealed record Option<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

// The view models below are read-only wrappers over the Shared DTOs. Pages
// rebuild them whenever AppState reloads a list, so nothing here is mutable:
// the API is the only source of truth.

// ----- Tasks -----

public enum Priority { Low, Medium, High }

public static class TaskEnums
{
    public static Color Tint(this Priority p) => p switch
    {
        Priority.High => Colors.TintRed,
        Priority.Medium => Colors.TintAmber,
        _ => Colors.TintBlue
    };

    public static Color Accent(this Priority p) => p switch
    {
        Priority.High => Colors.PriorityHigh,
        Priority.Medium => Colors.PriorityMedium,
        _ => Colors.PriorityLow
    };

    // The API's default priority is "Normal"; anything unrecognised sits in the middle.
    public static Priority Parse(string priority) =>
        Enum.TryParse<Priority>(priority, true, out var p) ? p : Priority.Medium;

    public static IReadOnlyList<Option<Priority>> Priorities { get; } =
        Enum.GetValues<Priority>().Select(p => new Option<Priority>(p, p.ToString())).ToList();
}

/// <summary>Hex constants that XAML resources also declare, for code-side use.</summary>
public static class Colors
{
    public static readonly Color TintGreen = Color.FromArgb("#E7F3EB");
    public static readonly Color TintGreenBorder = Color.FromArgb("#A9CDB8");
    public static readonly Color TintBlue = Color.FromArgb("#EAF2ED");
    public static readonly Color TintAmber = Color.FromArgb("#F6EBD3");
    public static readonly Color TintRed = Color.FromArgb("#FCEEF0");
    public static readonly Color PriorityHigh = Color.FromArgb("#E0435C");
    public static readonly Color PriorityMedium = Color.FromArgb("#B9862E");
    public static readonly Color PriorityLow = Color.FromArgb("#3C7A63");
    public static readonly Color StatusPending = Color.FromArgb("#B9862E");
    public static readonly Color StatusApproved = Color.FromArgb("#2E9E5B");
    public static readonly Color StatusDenied = Color.FromArgb("#E0435C");
    public static readonly Color AccentBlue = Color.FromArgb("#2A5CD1");
    public static readonly Color TintAccentBlue = Color.FromArgb("#EEF2FC");
    public static readonly Color Transparent = Microsoft.Maui.Graphics.Colors.Transparent;
}

/// <summary>One task, as both the Home checklist and the Daily Tasks timeline show it.</summary>
public sealed class TaskItem(TaskCardDto card, string? eventName, string? completedBy)
{
    public TaskCardDto Card => card;
    public string Title => card.Title;
    public DateTime ScheduledStart => card.Deadline;

    // ponytail: the ERD has no all-day flag, so a deadline at exactly midnight
    // means "any time that day" (same rule as the web client).
    public bool IsAllDay => card.Deadline.TimeOfDay == TimeSpan.Zero;

    public bool IsDone => string.Equals(card.Status, "Done", StringComparison.OrdinalIgnoreCase);
    public Priority Priority => TaskEnums.Parse(card.Priority);

    /// <summary>The linked event, or "Daily task" for one in the seeded daily-tasks project.</summary>
    public string EventLabel => eventName ?? "Daily task";

    public Color TintColor => Priority.Tint();
    public Color AccentColor => Priority.Accent();
    public string Glyph => eventName is null ? "✅" : "\U0001F4C5";
    public string MetaLabel => $"{ScheduledStart:h:mm tt} · {EventLabel}";
    public TextDecorations Decorations => IsDone ? TextDecorations.Strikethrough : TextDecorations.None;

    public string? CompletedBy => completedBy;
    public bool ShowCompletedBy => IsDone && !string.IsNullOrEmpty(CompletedBy);
    public string CompletedByLabel => $"✔ Completed by {CompletedBy}";

    // Home's row styling and "Completed Today" summary.
    public Color RowBackground => IsDone ? Colors.TintGreen : Microsoft.Maui.Graphics.Colors.Transparent;
    public Color RowBorder => IsDone ? Colors.TintGreenBorder : Color.FromArgb("#E7E4D8");
    public string SummaryMeta => $"{CompletedBy} · {EventLabel}";

    public string Initials =>
        string.IsNullOrWhiteSpace(CompletedBy)
            ? "?"
            : string.Concat(CompletedBy.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0])));
}

/// <summary>One row as MettingsPage list shows it</summary>
public sealed class MeetingRow(MeetingDto meetingDto, string nameOfOrganiser)
{
    public MeetingDto MeetingDto => meetingDto;
    public string MeetingTitle => meetingDto.Title;
    public string MeetingDateLabel => meetingDto.MeetingDate.ToString("dd MM yyyy");
    public string MeetingOrganiser => nameOfOrganiser;
    public string MeetingMinutesStatus => meetingDto.Minutes is null ? "Not Recorded" : 
        meetingDto.Minutes.ApprovedBy is null ? "Awaiting Approval" : "Approved";
}

/// <summary>One search hit with the meeting title resolved from the loaded list</summary>
public sealed class MinutesSearch(MeetingMinutesDto meetingMinutesDto, string? titleOfMeeting)
{
    public MeetingMinutesDto MeetingMinutesDto => meetingMinutesDto;
    public string MeetingTitle => titleOfMeeting ?? $"Meeting #{meetingMinutesDto.MeetingId}";
    public string SnippetOfMeetingMinutes => meetingMinutesDto.MinutesText.Length <= 120 ? meetingMinutesDto.MinutesText : 
        meetingMinutesDto.MinutesText[..120] + "...";
}

// ----- Resource requests (SM-02 statuses) -----

public enum RequestStatus { Pending, Escalated, Approved, Declined, Completed }

public enum RequestLinkType { None, Event, Task }

public static class RequestEnums
{
    public static string Label(this RequestLinkType t) => t switch
    {
        RequestLinkType.None => "Not linked",
        RequestLinkType.Event => "An event",
        _ => "A task"
    };

    public static IReadOnlyList<Option<RequestLinkType>> LinkTypes { get; } =
        Enum.GetValues<RequestLinkType>().Select(t => new Option<RequestLinkType>(t, t.Label())).ToList();
}

public sealed class ResourceRequest
{
    public ResourceRequest(ResourceRequestDto dto, string requestedBy, string? reviewedBy, bool canReview)
    {
        Dto = dto;
        RequestedBy = requestedBy;
        ReviewedBy = reviewedBy;
        ShowAdminActions = canReview;

        // A request stores one Detail text; its first line is the title (same as the web client).
        var parts = (dto.Detail ?? dto.CategoryName).Split('\n', 2);
        Title = parts[0];
        Notes = parts.Length > 1 ? parts[1] : null;
    }

    public ResourceRequestDto Dto { get; }
    public string Title { get; }
    public string? Notes { get; }
    public string RequestedBy { get; }
    public string? ReviewedBy { get; }
    public bool ShowAdminActions { get; }

    public RequestStatus Status =>
        Enum.TryParse<RequestStatus>(Dto.Status, true, out var s) ? s : RequestStatus.Pending;

    public string StatusBadge => Status.ToString().ToUpperInvariant();

    public Color StatusBackground => Status switch
    {
        RequestStatus.Pending or RequestStatus.Escalated => Colors.TintAmber,
        RequestStatus.Declined => Colors.TintRed,
        _ => Colors.TintGreen
    };

    public Color StatusForeground => Status switch
    {
        RequestStatus.Pending or RequestStatus.Escalated => Colors.StatusPending,
        RequestStatus.Declined => Colors.StatusDenied,
        _ => Colors.StatusApproved
    };

    public string Glyph => "\U0001F4E6";
    public string TypeLabel => Dto.CategoryName;
    public string RequestedLabel => $"{RequestedBy}   \U0001F553 {AppState.Local(Dto.DateSubmitted):d MMM, h:mm tt}";
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public bool ShowReview => Status is not (RequestStatus.Pending or RequestStatus.Escalated) && ReviewedBy is not null;

    // The latest history row is the review (or completion) itself.
    public string ReviewLabel =>
        Dto.History.LastOrDefault(h => h.NewStatus == Dto.Status) is { } review
            ? $"{Status} by {ReviewedBy} on {AppState.Local(review.TimeCreated):d MMM}"
            : $"{Status} by {ReviewedBy}";
}

// ----- Events (projects, as on the web client) -----

public sealed record EventRow(int ProjectId, string Title, string Meta);

/// <summary>One "is this person on the event" checkbox row in the event form.</summary>
public sealed class StaffPick : Observable
{
    private bool _isSelected;

    public required int UserId { get; init; }
    public required string Name { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}

// ----- Notifications -----

public enum NotificationKind { ResourceRequest, EventAssignment, TaskAssignment, Other }

public sealed class NotificationItem(NotificationDto dto)
{
    public NotificationDto Dto => dto;

    // Notification types are open-ended names in the API (TaskAssigned,
    // RequestReviewed, ProjectAssigned, Announcement...), grouped by prefix.
    public NotificationKind Kind =>
        dto.Type.StartsWith("Request") ? NotificationKind.ResourceRequest
        : dto.Type.StartsWith("Project") || dto.Type.StartsWith("Event") ? NotificationKind.EventAssignment
        : dto.Type.StartsWith("Task") ? NotificationKind.TaskAssignment
        : NotificationKind.Other;

    // "TaskAssigned" -> "Task Assigned"
    public string Title => string.Concat(dto.Type.Select((c, i) => i > 0 && char.IsUpper(c) ? $" {c}" : c.ToString()));
    public string Message => dto.Message;
    public bool IsRead => dto.IsRead;

    public string TypeLabel => Kind switch
    {
        NotificationKind.ResourceRequest => "Resource Request",
        NotificationKind.EventAssignment => "Event Assignment",
        NotificationKind.TaskAssignment => "Task Assignment",
        _ => "Announcement"
    };

    public string Glyph => Kind switch
    {
        NotificationKind.ResourceRequest => "\U0001F4E6",
        NotificationKind.EventAssignment => "\U0001F4C5",
        NotificationKind.TaskAssignment => "✅",
        _ => "\U0001F4E2"
    };

    public Color IconBackground => Kind == NotificationKind.ResourceRequest ? Colors.TintAmber : Colors.TintGreen;
    public Color IconForeground => Kind == NotificationKind.ResourceRequest ? Colors.StatusPending : Colors.StatusApproved;
    public Color RowBackground => IsRead ? Microsoft.Maui.Graphics.Colors.Transparent : Colors.TintBlue;
    public bool ShowUnreadDot => !IsRead;
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//