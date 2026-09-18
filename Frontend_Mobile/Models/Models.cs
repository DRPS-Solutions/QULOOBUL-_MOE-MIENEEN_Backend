using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Frontend_Mobile.Models;

/// <summary>
/// Minimal INotifyPropertyChanged base. The Kotlin port used immutable data
/// classes swapped into a MutableStateList; XAML bindings are cheaper to feed
/// from mutable properties that raise change notifications, so the models are
/// mutable here and the collections stay put.
/// </summary>
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
/// still show "Basic Staff" rather than "BasicStaff".
/// </summary>
public sealed record Option<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

// ----- Role (Role.kt) -----

public enum Role { BasicStaff, SecondaryStaff, ManagementStaff, Admin }

public static class RoleInfo
{
    public static string Label(this Role r) => r switch
    {
        Role.BasicStaff => "Basic Staff",
        Role.SecondaryStaff => "Secondary Staff",
        Role.ManagementStaff => "Management Staff",
        _ => "Admin"
    };

    public static string Hint(this Role r) => r switch
    {
        Role.BasicStaff => "Sees Home and Daily Tasks.",
        Role.SecondaryStaff => "Sees Home, Daily Tasks, and User Management.",
        Role.ManagementStaff => "Sees everything except User Management.",
        _ => "Sees every tab."
    };

    // Same gating rules as MainLayout.razor / Role.kt.
    public static bool CanSeeMeetings(this Role r) => r is Role.ManagementStaff or Role.Admin;
    public static bool CanSeeUserManagement(this Role r) => r is Role.SecondaryStaff or Role.Admin;
    public static bool CanSeeEvents(this Role r) => r is Role.ManagementStaff or Role.Admin;

    public static IReadOnlyList<Option<Role>> All { get; } =
        Enum.GetValues<Role>().Select(r => new Option<Role>(r, r.Label())).ToList();
}

// ----- Tasks (Task.kt) -----

public enum Priority { Low, Medium, High }

public enum Category { Admin, Meeting, Fieldwork, Other }

public static class TaskEnums
{
    public static string Label(this Category c) => c.ToString();

    public static string Label(this Priority p) => p.ToString();

    // GetCategoryIcon's Material icons, as glyphs (see README note on fonts).
    public static string Glyph(this Category c) => c switch
    {
        Category.Meeting => "\U0001F465",   // groups
        Category.Admin => "\U0001F4C4",     // description
        Category.Fieldwork => "\U0001F4CD", // location
        _ => "✅"                       // task
    };

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

    public static IReadOnlyList<Option<Category>> Categories { get; } =
        Enum.GetValues<Category>().Select(c => new Option<Category>(c, c.Label())).ToList();

    public static IReadOnlyList<Option<Priority>> Priorities { get; } =
        Enum.GetValues<Priority>().Select(p => new Option<Priority>(p, p.Label())).ToList();
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

/// <summary>A scheduled task on the Daily Tasks timeline (Tasks.razor's TaskItem).</summary>
public sealed class TaskItem : Observable
{
    private bool _isDone;
    private string? _completedBy;

    public string Id { get; } = Guid.NewGuid().ToString();
    public required string Title { get; init; }
    public required DateTime ScheduledStart { get; init; }
    public required Category Category { get; init; }
    public Priority Priority { get; init; } = Priority.Low;
    public bool IsAllDay { get; init; }

    public bool IsDone
    {
        get => _isDone;
        set { if (Set(ref _isDone, value)) RaiseDerived(); }
    }

    public string? CompletedBy
    {
        get => _completedBy;
        set { if (Set(ref _completedBy, value)) RaiseDerived(); }
    }

    private void RaiseDerived()
    {
        Raise(nameof(Decorations));
        Raise(nameof(ShowCompletedBy));
        Raise(nameof(CompletedByLabel));
    }

    public Color TintColor => Priority.Tint();
    public Color AccentColor => Priority.Accent();
    public string Glyph => Category.Glyph();
    public string MetaLabel => $"{ScheduledStart:h:mm tt} · {Category.Label()}";
    public TextDecorations Decorations => IsDone ? TextDecorations.Strikethrough : TextDecorations.None;
    public bool ShowCompletedBy => IsDone && !string.IsNullOrEmpty(CompletedBy);
    public string CompletedByLabel => $"Completed by {CompletedBy}";
}

/// <summary>A today-only task on the Home dashboard (Home.razor's DailyTask).</summary>
public sealed class DailyTask : Observable
{
    private bool _isCompleted;
    private string? _completedBy;

    public string Id { get; } = Guid.NewGuid().ToString();
    public required string Title { get; init; }
    public required string Category { get; init; }

    public bool IsCompleted
    {
        get => _isCompleted;
        set { if (Set(ref _isCompleted, value)) RaiseDerived(); }
    }

    public string? CompletedBy
    {
        get => _completedBy;
        set { if (Set(ref _completedBy, value)) RaiseDerived(); }
    }

    private void RaiseDerived()
    {
        Raise(nameof(Decorations));
        Raise(nameof(ShowCompletedBy));
        Raise(nameof(CompletedByLabel));
        Raise(nameof(RowBackground));
        Raise(nameof(RowBorder));
        Raise(nameof(Initials));
        Raise(nameof(SummaryMeta));
    }

    public TextDecorations Decorations => IsCompleted ? TextDecorations.Strikethrough : TextDecorations.None;
    public bool ShowCompletedBy => IsCompleted && !string.IsNullOrEmpty(CompletedBy);
    public string CompletedByLabel => $"✔ Completed by {CompletedBy}";
    public Color RowBackground => IsCompleted ? Colors.TintGreen : Microsoft.Maui.Graphics.Colors.Transparent;
    public Color RowBorder => IsCompleted ? Colors.TintGreenBorder : Color.FromArgb("#E7E4D8");
    public string SummaryMeta => $"{CompletedBy} · {Category}";
    public string Initials => InitialsOf(CompletedBy);

    public static string InitialsOf(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? "?"
            : string.Concat(name.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0])));
}

// ----- Resource requests (Request.kt) -----

public enum RequestStatus { Pending, Approved, Denied }

public enum ResourceType { Equipment, Budget, Personnel, Venue, Other }

public enum RequestLinkType { None, Event, Task }

public static class RequestEnums
{
    public static string Label(this ResourceType t) => t.ToString();

    public static string Label(this RequestLinkType t) => t switch
    {
        RequestLinkType.None => "Not linked",
        RequestLinkType.Event => "An event",
        _ => "A task"
    };

    public static string Glyph(this ResourceType t) => t switch
    {
        ResourceType.Equipment => "\U0001F527",  // build
        ResourceType.Budget => "\U0001F4B0",     // money
        ResourceType.Personnel => "\U0001F465",  // groups
        ResourceType.Venue => "\U0001F4CD",      // location
        _ => "\U0001F4E6"                        // inventory
    };

    public static IReadOnlyList<Option<ResourceType>> ResourceTypes { get; } =
        Enum.GetValues<ResourceType>().Select(t => new Option<ResourceType>(t, t.Label())).ToList();

    public static IReadOnlyList<Option<RequestLinkType>> LinkTypes { get; } =
        Enum.GetValues<RequestLinkType>().Select(t => new Option<RequestLinkType>(t, t.Label())).ToList();
}

public sealed class ResourceRequest : Observable
{
    private RequestStatus _status = RequestStatus.Pending;
    private string? _reviewedBy;
    private DateTime? _reviewedDate;
    private string? _denyReason;

    public string Id { get; } = Guid.NewGuid().ToString();
    public required string Title { get; init; }
    public required ResourceType ResourceType { get; init; }
    public string? Amount { get; init; }
    public string? Notes { get; init; }
    public RequestLinkType LinkType { get; init; } = RequestLinkType.None;
    public string? LinkedName { get; init; }
    public required string RequestedBy { get; init; }
    public DateTime RequestedDate { get; init; } = DateTime.Now;

    public RequestStatus Status
    {
        get => _status;
        set { if (Set(ref _status, value)) RaiseDerived(); }
    }

    public string? ReviewedBy
    {
        get => _reviewedBy;
        set { if (Set(ref _reviewedBy, value)) RaiseDerived(); }
    }

    public DateTime? ReviewedDate
    {
        get => _reviewedDate;
        set { if (Set(ref _reviewedDate, value)) RaiseDerived(); }
    }

    public string? DenyReason
    {
        get => _denyReason;
        set { if (Set(ref _denyReason, value)) RaiseDerived(); }
    }

    private void RaiseDerived()
    {
        Raise(nameof(StatusLabel));
        Raise(nameof(StatusBadge));
        Raise(nameof(StatusBackground));
        Raise(nameof(StatusForeground));
        Raise(nameof(ShowReview));
        Raise(nameof(ReviewLabel));
        Raise(nameof(ShowAdminActions));
    }

    public string StatusLabel => Status.ToString();
    public string StatusBadge => Status.ToString().ToUpperInvariant();

    public Color StatusBackground => Status switch
    {
        RequestStatus.Pending => Colors.TintAmber,
        RequestStatus.Approved => Colors.TintGreen,
        _ => Colors.TintRed
    };

    public Color StatusForeground => Status switch
    {
        RequestStatus.Pending => Colors.StatusPending,
        RequestStatus.Approved => Colors.StatusApproved,
        _ => Colors.StatusDenied
    };

    public string Glyph => ResourceType.Glyph();

    public string TypeLabel =>
        string.IsNullOrWhiteSpace(Amount) ? ResourceType.Label() : $"{ResourceType.Label()} · {Amount}";

    public string RequestedLabel => $"{RequestedBy}   \U0001F553 {RequestedDate:d MMM, h:mm tt}";

    public bool HasLink => LinkType != RequestLinkType.None && !string.IsNullOrWhiteSpace(LinkedName);
    public string LinkLabel => $"{(LinkType == RequestLinkType.Event ? "\U0001F4C5" : "✅")} {LinkType}: {LinkedName}";
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public bool ShowReview => Status != RequestStatus.Pending && !string.IsNullOrWhiteSpace(ReviewedBy);

    public string ReviewLabel
    {
        get
        {
            var reason = Status == RequestStatus.Denied && !string.IsNullOrWhiteSpace(DenyReason)
                ? $" — “{DenyReason}”"
                : string.Empty;
            return $"{StatusLabel} by {ReviewedBy} on {ReviewedDate:d MMM}{reason}";
        }
    }

    /// <summary>
    /// Approve/Deny render only for Admins on still-Pending requests, the same
    /// pair of checks Requests.razor made with IsAdmin. Role can only change by
    /// logging out (which rebuilds the page), so reading it here is safe.
    /// </summary>
    public bool ShowAdminActions => Status == RequestStatus.Pending && Data.AppState.Current.IsAdmin;
}

// ----- Events (Event.kt) -----

public sealed class EventModel : Observable
{
    private string _title = string.Empty;
    private string _description = string.Empty;
    private DateTime? _date = DateTime.Today;
    private TimeSpan? _time = new(9, 0, 0);
    private string _location = string.Empty;
    private string _coordinator = string.Empty;
    private double? _estimatedBudget;
    private int? _estimatedCapacity;
    private List<string> _staffMembers = [];
    private List<string> _requiredResources = [];

    public string Id { get; } = Guid.NewGuid().ToString();

    public string Title { get => _title; set { Set(ref _title, value); } }
    public string Description { get => _description; set { if (Set(ref _description, value)) Raise(nameof(HasDescription)); } }
    public DateTime? Date { get => _date; set { if (Set(ref _date, value)) Raise(nameof(DateTimeLabel)); } }
    public TimeSpan? Time { get => _time; set { if (Set(ref _time, value)) Raise(nameof(DateTimeLabel)); } }
    public string Location { get => _location; set { if (Set(ref _location, value)) Raise(nameof(LocationLabel)); } }
    public string Coordinator { get => _coordinator; set { if (Set(ref _coordinator, value)) Raise(nameof(CoordinatorLabel)); } }
    public double? EstimatedBudget { get => _estimatedBudget; set { if (Set(ref _estimatedBudget, value)) Raise(nameof(BudgetLabel)); } }
    public int? EstimatedCapacity { get => _estimatedCapacity; set { if (Set(ref _estimatedCapacity, value)) Raise(nameof(CapacityLabel)); } }

    public List<string> StaffMembers
    {
        get => _staffMembers;
        set
        {
            if (!Set(ref _staffMembers, value)) return;
            Raise(nameof(HasStaff));
            Raise(nameof(StaffSectionLabel));
        }
    }

    public List<string> RequiredResources
    {
        get => _requiredResources;
        set { if (Set(ref _requiredResources, value)) Raise(nameof(HasResources)); }
    }

    public string DateTimeLabel
    {
        get
        {
            var datePart = Date?.ToString("dd MMM yyyy") ?? "TBD";
            return Time is { } t ? $"{datePart} at {DateTime.Today.Add(t):h:mm tt}" : datePart;
        }
    }

    public string LocationLabel => string.IsNullOrWhiteSpace(Location) ? "No location set" : Location;
    public string CoordinatorLabel => $"Lead: {(string.IsNullOrWhiteSpace(Coordinator) ? "Unassigned" : Coordinator)}";
    public string BudgetLabel => EstimatedBudget is { } b ? $"R {b:N0}" : "—";
    public string CapacityLabel => EstimatedCapacity?.ToString() ?? "—";
    public string StaffSectionLabel => $"STAFF HELPING ({StaffMembers.Count})";
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasStaff => StaffMembers.Count > 0;
    public bool HasResources => RequiredResources.Count > 0;

    public EventModel Clone() => new()
    {
        Title = Title,
        Description = Description,
        Date = Date,
        Time = Time,
        Location = Location,
        Coordinator = Coordinator,
        EstimatedBudget = EstimatedBudget,
        EstimatedCapacity = EstimatedCapacity,
        StaffMembers = [.. StaffMembers],
        RequiredResources = [.. RequiredResources]
    };

    /// <summary>Copies the working form values back onto the roster entry on Save.</summary>
    public void CopyFrom(EventModel other)
    {
        Title = other.Title;
        Description = other.Description;
        Date = other.Date;
        Time = other.Time;
        Location = other.Location;
        Coordinator = other.Coordinator;
        EstimatedBudget = other.EstimatedBudget;
        EstimatedCapacity = other.EstimatedCapacity;
        StaffMembers = [.. other.StaffMembers];
        RequiredResources = [.. other.RequiredResources];
    }
}

/// <summary>One "is this person on the event" checkbox row in the event form.</summary>
public sealed class StaffPick : Observable
{
    private bool _isSelected;

    public required string Name { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}

// ----- Notifications (Notification.kt) -----

public enum NotificationType { ResourceRequest, EventAssignment }

public sealed class NotificationItem : Observable
{
    private bool _isRead;

    public string Id { get; } = Guid.NewGuid().ToString();
    public required NotificationType Type { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string? LinkedName { get; init; }

    public bool IsRead
    {
        get => _isRead;
        set
        {
            if (!Set(ref _isRead, value)) return;
            Raise(nameof(RowBackground));
            Raise(nameof(ShowUnreadDot));
        }
    }

    public bool IsRequest => Type == NotificationType.ResourceRequest;
    public string TypeLabel => IsRequest ? "Resource Request" : "Event Assignment";
    public string Glyph => IsRequest ? "\U0001F4E6" : "\U0001F4C5";
    public Color IconBackground => IsRequest ? Colors.TintAmber : Colors.TintGreen;
    public Color IconForeground => IsRequest ? Colors.StatusPending : Colors.StatusApproved;
    public Color RowBackground => IsRead ? Microsoft.Maui.Graphics.Colors.Transparent : Colors.TintBlue;
    public bool ShowUnreadDot => !IsRead;
    public bool HasLink => !string.IsNullOrWhiteSpace(LinkedName);

    public string RelativeTime
    {
        get
        {
            var elapsed = DateTime.Now - Timestamp;
            if (elapsed.TotalMinutes < 1) return "Just now";
            if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
            if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";
            return Timestamp.ToString("dd MMM");
        }
    }
}
