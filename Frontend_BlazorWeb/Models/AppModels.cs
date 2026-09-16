// Models/AppModels.cs
//
// Shared in-memory data models for the prototype. These are plain
// POCOs held by AppDataService (a Singleton - see AppDataService.cs),
// so every page reads and writes the *same* objects instead of each
// page keeping its own private placeholder list. That's what lets,
// e.g., ticking a task complete on Home show up on the Daily Tasks
// page immediately, or a new Event show up in the Requests page's
// "link to an event" dropdown.
//
// No namespace declared here on purpose - same convention already
// used by NotificationService.cs/NewTaskResult.cs in this prototype -
// so every page sees these types without an extra @using.

using System;
using System.Collections.Generic;
using System.Linq;

// Permission level used for page/tab visibility (see MainLayout.razor's
// CanSeeTasks/CanSeeRequests/CanSeeMeetings/CanSeeUserManagement/
// CanSeeEvents) and for notification routing (see NotificationService).
// Separate from AppUser.JobTitle, which is just a display label (e.g.
// "Fieldwork Lead"). Declared low-tier to high-tier:
//   - GeneralStaff: Home page only. Notified only about Daily-type
//     tasks assigned to them.
//   - FacilityManager: Home, Daily Tasks, Resource Requests pages.
//     Can't approve/deny requests, and gets no resource-request
//     notifications at all. Notified about events/tasks only when
//     personally linked to them (staffed/assigned).
//   - SecondaryAdmin: everything Admin has except User Management.
//     Notified whenever an event or resource request is made
//     (role-wide), plus any task assigned to them.
//   - Admin: every page, full create/edit/delete everywhere, and the
//     only role that can manage users. Not selectable as an event/task
//     assignee. Notified whenever an event or resource request is made
//     (role-wide) - no personal task/event-assignment notifications
//     since Admins aren't assignable.
public enum SystemRole
{
    GeneralStaff,
    FacilityManager,
    SecondaryAdmin,
    Admin
}

// Whether a task is a standalone day-to-day item, or one that belongs
// to a specific Event/Project planned on the Events page.
public enum AppTaskType
{
    Daily,
    Event
}

public enum TaskPriority
{
    Low,
    Medium,
    High
}

public enum RequestStatus
{
    Pending,
    Approved,
    Denied
}

// What a resource request is tied to, if anything.
public enum RequestLinkType
{
    None,
    Event,
    Task
}

// A registered account. Doubles as both the login credential record
// (Username/Password/SystemRole, used by the sign-in screen) and the
// employee profile shown on the User Management page (JobTitle/
// Department/JoinDate).
public class AppUser
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;

    // Used as the login identifier - can look like an email or a plain
    // username, the login screen just calls it "Username or email".
    public string Username { get; set; } = string.Empty;

    // Plain text on purpose - this is a volatile, in-memory prototype
    // with no real auth/security behind it.
    public string Password { get; set; } = string.Empty;

    public SystemRole SystemRole { get; set; } = SystemRole.GeneralStaff;
    public string JobTitle { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime JoinDate { get; set; } = DateTime.Today;

    public string Initials =>
        string.Concat(FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                .Select(part => part[0]))
              .ToUpper();

    public string SystemRoleLabel => RoleLabel(SystemRole);

    public static string RoleLabel(SystemRole role) => role switch
    {
        SystemRole.GeneralStaff => "General Staff",
        SystemRole.FacilityManager => "Facility Manager",
        SystemRole.SecondaryAdmin => "Secondary Admin",
        SystemRole.Admin => "Admin",
        _ => role.ToString()
    };
}

// A single scheduled task - either a standalone Daily task, or one
// tied to an Event/Project (Type == Event, LinkedEventId set). Home
// and Daily Tasks both read/write this same collection (via
// AppDataService.Tasks), so completing a task on either page is
// immediately reflected on the other.
public class AppTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Date AND time this task needs to be performed.
    public DateTime ScheduledStart { get; set; } = DateTime.Today;
    public bool IsAllDay { get; set; } = false;

    public string Category { get; set; } = "Admin";
    public TaskPriority Priority { get; set; } = TaskPriority.Low;
    public AppTaskType Type { get; set; } = AppTaskType.Daily;

    // Only meaningful when Type == AppTaskType.Event.
    public Guid? LinkedEventId { get; set; }
    public string? LinkedEventTitle { get; set; }

    public string Assignee { get; set; } = string.Empty;

    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
}

// An Event/Project planned on the Events page.
public class AppEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? Date { get; set; } = DateTime.Today;
    public TimeSpan? Time { get; set; } = new TimeSpan(9, 0, 0);
    public string Location { get; set; } = string.Empty;
    public string Coordinator { get; set; } = string.Empty;
    public decimal? EstimatedBudget { get; set; }
    public int? EstimatedCapacity { get; set; }
    public List<string> StaffMembers { get; set; } = new();
    public List<string> RequiredResources { get; set; } = new();

    // Deep-enough copy for the edit form's working copy, same pattern
    // Events.razor already used before this only lived on a private
    // nested class - the two list properties get their own new List<>
    // so editing the clone never leaks back into the original until
    // CopyFrom is explicitly called on Save. Keeps the same Id so the
    // roster item being edited is easy to re-identify.
    public AppEvent Clone() => new AppEvent
    {
        Id = Id,
        Title = Title,
        Description = Description,
        Date = Date,
        Time = Time,
        Location = Location,
        Coordinator = Coordinator,
        EstimatedBudget = EstimatedBudget,
        EstimatedCapacity = EstimatedCapacity,
        StaffMembers = new List<string>(StaffMembers),
        RequiredResources = new List<string>(RequiredResources)
    };

    public void CopyFrom(AppEvent other)
    {
        Title = other.Title;
        Description = other.Description;
        Date = other.Date;
        Time = other.Time;
        Location = other.Location;
        Coordinator = other.Coordinator;
        EstimatedBudget = other.EstimatedBudget;
        EstimatedCapacity = other.EstimatedCapacity;
        StaffMembers = new List<string>(other.StaffMembers);
        RequiredResources = new List<string>(other.RequiredResources);
    }
}

// A submitted resource request - equipment, budget, personnel, or a
// venue - optionally tied to an Event or a Task.
public class ResourceRequestItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? Amount { get; set; }
    public string? Notes { get; set; }

    public RequestLinkType LinkType { get; set; } = RequestLinkType.None;
    public Guid? LinkedId { get; set; }
    public string? LinkedName { get; set; }

    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; } = DateTime.Now;

    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? DenyReason { get; set; }
}

// A single note or action item captured during a meeting.
public class MeetingNote
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public bool IsActionItem { get; set; } = false;
    public string Timestamp { get; set; } = string.Empty;
}

// A meeting's minutes/recording record.
public class Meeting
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;
    public string Duration { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public string MinutesText { get; set; } = string.Empty;
    public List<MeetingNote> Notes { get; set; } = new();

    // Placeholder transcript text - stands in for real speech-to-text
    // output until a transcription service is integrated. Empty until
    // "Generate Transcript" is used (or pre-seeded for demo purposes).
    public string Transcript { get; set; } = string.Empty;
}
