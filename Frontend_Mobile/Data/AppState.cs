using System.Collections.ObjectModel;
using Frontend_Mobile.Models;

namespace Frontend_Mobile.Data;

/// <summary>
/// Single in-memory source of truth, the direct port of the Android app's
/// AppViewModel (itself standing in for MainLayout's session state plus each
/// Razor page's demo lists). Everything resets on app restart, matching the
/// prototype's "no real backend yet" behaviour.
/// </summary>
public sealed class AppState : Observable
{
    public static AppState Current { get; } = new();

    private AppState() { }

    // ----- Session (MainLayout.razor's currentRole / currentUserName) -----

    private Role? _currentRole;
    private string _currentUserName = string.Empty;

    public Role? CurrentRole
    {
        get => _currentRole;
        private set
        {
            if (!Set(ref _currentRole, value)) return;
            Raise(nameof(IsLoggedIn));
            Raise(nameof(IsAdmin));
            Raise(nameof(CanSeeMeetings));
            Raise(nameof(CanSeeUserManagement));
            Raise(nameof(CanSeeEvents));
            Raise(nameof(RoleLabel));
        }
    }

    public string CurrentUserName
    {
        get => _currentUserName;
        private set => Set(ref _currentUserName, value);
    }

    public bool IsLoggedIn => CurrentRole is not null;
    public bool IsAdmin => CurrentRole == Role.Admin;
    public bool CanSeeMeetings => CurrentRole?.CanSeeMeetings() == true;
    public bool CanSeeUserManagement => CurrentRole?.CanSeeUserManagement() == true;
    public bool CanSeeEvents => CurrentRole?.CanSeeEvents() == true;
    public string RoleLabel => CurrentRole?.Label() ?? string.Empty;

    public void Login(string name, Role role)
    {
        CurrentRole = role;
        CurrentUserName = string.IsNullOrWhiteSpace(name) ? role.Label() : name.Trim();
    }

    public void Logout()
    {
        CurrentRole = null;
        CurrentUserName = string.Empty;
    }

    // ----- Shared staff list (both "completed by" dialogs and the event form) -----

    public IReadOnlyList<string> StaffMembers { get; } =
    [
        "Naledi Dube",
        "Sipho Mokoena",
        "Aisha Patel",
        "Liam Fourie",
        "Grace Adeyemi"
    ];

    // ----- Home dashboard (Home.razor's todayTasks) -----

    public ObservableCollection<DailyTask> TodayTasks { get; } =
    [
        new() { Title = "Review Morning Dispatch Logs", Category = "Operations" },
        new() { Title = "Submit Weekly Safety Checklist", Category = "Compliance" },
        new() { Title = "Confirm Driver Schedules", Category = "Logistics" },
        new() { Title = "Client Status Update Call", Category = "Management" }
    ];

    public void SetDailyTaskCompletion(DailyTask task, bool isChecked, string? completedBy)
    {
        task.IsCompleted = isChecked;
        task.CompletedBy = isChecked ? completedBy : null;
    }

    // ----- Daily Tasks timeline (Tasks.razor's tasks) -----

    public ObservableCollection<TaskItem> Tasks { get; } = BuildSeedTasks();

    private static ObservableCollection<TaskItem> BuildSeedTasks()
    {
        var today = DateTime.Today;
        return
        [
            new()
            {
                Title = "Volunteer check-in call",
                ScheduledStart = today.AddHours(8),
                Category = Category.Admin,
                Priority = Priority.Low,
                IsDone = true,
                CompletedBy = "Naledi Dube"
            },
            new() { Title = "Prepare donor report", ScheduledStart = today.AddHours(9).AddMinutes(30), Category = Category.Admin, Priority = Priority.Medium },
            new() { Title = "Site visit — Alexandra shelter", ScheduledStart = today.AddHours(11), Category = Category.Fieldwork, Priority = Priority.High },
            new() { Title = "Team stand-up", ScheduledStart = today.AddHours(13), Category = Category.Meeting, Priority = Priority.Low },
            new() { Title = "Grant proposal review", ScheduledStart = today.AddHours(15).AddMinutes(30), Category = Category.Admin, Priority = Priority.High },
            new() { Title = "Community workshop setup", ScheduledStart = today.AddDays(1).AddHours(9), Category = Category.Fieldwork, Priority = Priority.Medium },
            new() { Title = "Board meeting", ScheduledStart = today.AddDays(1).AddHours(14), Category = Category.Meeting, Priority = Priority.High },
            new() { Title = "Inventory count", ScheduledStart = today.AddDays(2).AddHours(10), Category = Category.Fieldwork, Priority = Priority.Low },
            new() { Title = "Submit grant report", ScheduledStart = today, Category = Category.Admin, Priority = Priority.High, IsAllDay = true },
            new() { Title = "Renew office lease paperwork", ScheduledStart = today.AddDays(1), Category = Category.Admin, Priority = Priority.Medium, IsAllDay = true }
        ];
    }

    public void SetTaskCompletion(TaskItem task, bool isChecked, string? completedBy)
    {
        task.IsDone = isChecked;
        task.CompletedBy = isChecked ? completedBy : null;
    }

    public TaskItem AddTask(string title, DateTime date, TimeSpan? time, bool isAllDay, Category category, Priority priority)
    {
        var start = isAllDay || time is null ? date.Date : date.Date.Add(time.Value);
        var task = new TaskItem
        {
            Title = title.Trim(),
            ScheduledStart = start,
            Category = category,
            Priority = priority,
            IsAllDay = isAllDay
        };
        Tasks.Add(task);
        return task;
    }

    // ----- Notifications (NotificationService.cs) -----

    public ObservableCollection<NotificationItem> Notifications { get; } =
    [
        new()
        {
            Type = NotificationType.EventAssignment,
            Title = "Added to Annual Donor Gala",
            Message = "You've been added to the staff list for \"Annual Donor Gala\".",
            LinkedName = "Annual Donor Gala",
            Timestamp = DateTime.Now.AddHours(-3)
        },
        new()
        {
            Type = NotificationType.ResourceRequest,
            Title = "New resource request",
            Message = "Aisha Patel requested a projector for \"Annual Donor Gala\".",
            LinkedName = "Annual Donor Gala",
            Timestamp = DateTime.Now.AddDays(-2)
        },
        new()
        {
            Type = NotificationType.ResourceRequest,
            Title = "Request approved",
            Message = "Naledi Dube approved your \"Printing budget\" request.",
            LinkedName = "Community Workshop",
            Timestamp = DateTime.Now.AddDays(-4),
            IsRead = true
        }
    ];

    public int UnreadNotificationCount => Notifications.Count(n => !n.IsRead);

    public string UnreadBadge => UnreadNotificationCount switch
    {
        0 => string.Empty,
        > 9 => "9+",
        var n => n.ToString()
    };

    public bool HasUnread => UnreadNotificationCount > 0;

    /// <summary>Re-reads the badge after anything touches read state.</summary>
    public void RefreshNotificationCounts()
    {
        Raise(nameof(UnreadNotificationCount));
        Raise(nameof(UnreadBadge));
        Raise(nameof(HasUnread));
    }

    public void AddNotification(NotificationType type, string title, string message, string? linkedName = null)
    {
        Notifications.Insert(0, new NotificationItem
        {
            Type = type,
            Title = title,
            Message = message,
            LinkedName = linkedName
        });
        RefreshNotificationCounts();
    }

    public void MarkNotificationAsRead(NotificationItem item)
    {
        if (item.IsRead) return;
        item.IsRead = true;
        RefreshNotificationCounts();
    }

    public void MarkAllNotificationsAsRead()
    {
        foreach (var n in Notifications) n.IsRead = true;
        RefreshNotificationCounts();
    }

    public void ClearAllNotifications()
    {
        Notifications.Clear();
        RefreshNotificationCounts();
    }

    // ----- Resource requests (Requests.razor) -----

    // Placeholder options for the "Link to" dropdowns; in a real app these
    // would come from wherever Events/Tasks persist rather than being
    // duplicated here.
    public IReadOnlyList<string> EventOptions { get; } =
        ["Annual Donor Gala", "Community Workshop", "Board Meeting"];

    public IReadOnlyList<string> TaskOptions { get; } =
    [
        "Site visit — Alexandra shelter",
        "Grant proposal review",
        "Community workshop setup",
        "Inventory count"
    ];

    // Seeded across all three statuses so the filter chips and the Admin-only
    // controls are visible without submitting anything first.
    public ObservableCollection<ResourceRequest> Requests { get; } =
    [
        new()
        {
            Title = "Projector for donor gala",
            ResourceType = ResourceType.Equipment,
            Amount = "1 projector",
            Notes = "Needed for the sponsor highlight reel.",
            LinkType = RequestLinkType.Event,
            LinkedName = "Annual Donor Gala",
            RequestedBy = "Aisha Patel",
            RequestedDate = DateTime.Now.AddDays(-2)
        },
        new()
        {
            Title = "Additional volunteers",
            ResourceType = ResourceType.Personnel,
            Amount = "4 volunteers",
            Notes = "Site visit needs extra hands for the day.",
            LinkType = RequestLinkType.Task,
            LinkedName = "Site visit — Alexandra shelter",
            RequestedBy = "Sipho Mokoena",
            RequestedDate = DateTime.Now.AddDays(-1)
        },
        new()
        {
            Title = "Printing budget",
            ResourceType = ResourceType.Budget,
            Amount = "R 1,200",
            Notes = "Workshop handouts and signage.",
            LinkType = RequestLinkType.Event,
            LinkedName = "Community Workshop",
            RequestedBy = "Grace Adeyemi",
            RequestedDate = DateTime.Now.AddDays(-5),
            Status = RequestStatus.Approved,
            ReviewedBy = "Naledi Dube",
            ReviewedDate = DateTime.Now.AddDays(-4)
        },
        new()
        {
            Title = "New laptop",
            ResourceType = ResourceType.Equipment,
            Amount = "1 laptop",
            Notes = "Current one won't hold a charge.",
            RequestedBy = "Liam Fourie",
            RequestedDate = DateTime.Now.AddDays(-7),
            Status = RequestStatus.Denied,
            ReviewedBy = "Naledi Dube",
            ReviewedDate = DateTime.Now.AddDays(-6),
            DenyReason = "Budget's earmarked for Q3 - resubmit then."
        }
    ];

    public ResourceRequest SubmitRequest(
        string title,
        ResourceType resourceType,
        string? amount,
        string? notes,
        RequestLinkType linkType,
        string? linkedName)
    {
        var requestedBy = string.IsNullOrWhiteSpace(CurrentUserName) ? "You" : CurrentUserName;
        var trimmedTitle = title.Trim();
        var effectiveLinkedName = linkType == RequestLinkType.None ? null : linkedName;

        var request = new ResourceRequest
        {
            Title = trimmedTitle,
            ResourceType = resourceType,
            Amount = amount,
            Notes = notes,
            LinkType = linkType,
            LinkedName = effectiveLinkedName,
            RequestedBy = requestedBy,
            RequestedDate = DateTime.Now
        };
        Requests.Add(request);

        // Notifications aren't scoped to a recipient in this prototype; Admins
        // would be the real audience for "new request awaiting review".
        var suffix = effectiveLinkedName is null ? "." : $" for {effectiveLinkedName}.";
        AddNotification(
            NotificationType.ResourceRequest,
            "New resource request",
            $"{requestedBy} requested \"{trimmedTitle}\"{suffix}",
            effectiveLinkedName);

        return request;
    }

    // Approve needs no extra input, so it applies immediately.
    public void ApproveRequest(ResourceRequest request)
    {
        var reviewedBy = string.IsNullOrWhiteSpace(CurrentUserName) ? "Admin" : CurrentUserName;
        request.ReviewedBy = reviewedBy;
        request.ReviewedDate = DateTime.Now;
        request.Status = RequestStatus.Approved;

        AddNotification(
            NotificationType.ResourceRequest,
            "Request approved",
            $"{reviewedBy} approved your \"{request.Title}\" request.",
            request.LinkedName);
    }

    // Deny runs behind a confirmation prompt so the admin can leave a reason;
    // cancelling leaves the request Pending, so this only runs once a reason
    // (possibly blank) is confirmed.
    public void DenyRequest(ResourceRequest request, string? reason)
    {
        var reviewedBy = string.IsNullOrWhiteSpace(CurrentUserName) ? "Admin" : CurrentUserName;
        request.ReviewedBy = reviewedBy;
        request.ReviewedDate = DateTime.Now;
        request.DenyReason = reason;
        request.Status = RequestStatus.Denied;

        var reasonSuffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Reason: {reason}";
        AddNotification(
            NotificationType.ResourceRequest,
            "Request denied",
            $"{reviewedBy} denied your \"{request.Title}\" request.{reasonSuffix}",
            request.LinkedName);
    }

    // ----- Event planning (Events.razor) -----

    public ObservableCollection<EventModel> PlannedEvents { get; } =
    [
        new()
        {
            Title = "Annual Donor Gala",
            Description = "Formal evening event to thank major donors and showcase this year's impact numbers.",
            Date = DateTime.Today.AddDays(14),
            Time = new TimeSpan(18, 0, 0),
            Location = "Cape Town Convention Centre",
            Coordinator = "Aisha Patel",
            EstimatedBudget = 85000,
            EstimatedCapacity = 220,
            StaffMembers = ["Aisha Patel", "Grace Adeyemi", "Naledi Dube"],
            RequiredResources = ["Stage & PA system", "Catering (220 covers)", "Photographer"]
        },
        new()
        {
            Title = "Community Workshop",
            Description = "Half-day skills workshop for local youth, run out of the Alexandra community hall.",
            Date = DateTime.Today.AddDays(21),
            Time = new TimeSpan(9, 30, 0),
            Location = "Alexandra Community Hall",
            Coordinator = "Sipho Mokoena",
            EstimatedBudget = 12000,
            EstimatedCapacity = 60,
            StaffMembers = ["Sipho Mokoena", "Liam Fourie"],
            RequiredResources = ["Projector", "Folding chairs (60)", "Printed handouts"]
        }
    ];

    // "+ New" -> Save. Everyone on a brand-new event is newly assigned.
    public void AddEvent(EventModel evt)
    {
        PlannedEvents.Add(evt);
        NotifyAssignedStaff(evt, evt.StaffMembers);
    }

    // "Edit" -> Save. Diffs the staff lists so only newly-added people get a
    // notification, same as SaveEvent's isEditing branch.
    public void UpdateEvent(EventModel target, EventModel edited)
    {
        var newlyAdded = edited.StaffMembers.Except(target.StaffMembers).ToList();
        target.CopyFrom(edited);
        NotifyAssignedStaff(target, newlyAdded);
    }

    private void NotifyAssignedStaff(EventModel evt, IEnumerable<string> staff)
    {
        var dateLabel = evt.Date?.ToString("dd MMM") ?? "TBD";
        foreach (var name in staff)
        {
            AddNotification(
                NotificationType.EventAssignment,
                $"Assigned to {evt.Title}",
                $"{name} was added to the staff list for \"{evt.Title}\" on {dateLabel}.",
                evt.Title);
        }
    }
}
