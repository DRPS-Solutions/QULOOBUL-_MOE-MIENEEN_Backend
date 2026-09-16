// AppDataService.cs
//
// Central in-memory data store for the prototype. Every page reads
// and writes through this one Singleton instead of keeping its own
// private placeholder list, so the pages actually interact with each
// other: completing a task on Home updates the Daily Tasks page,
// creating an Event makes it selectable from the Requests page's
// "link to an event" dropdown and from the New Task dialog's "link to
// an event" field, and so on. There's still no database - everything
// here lives in memory for the lifetime of the running app, exactly
// as much "full function" as a prototype needs, and resets the moment
// the app restarts.
//
// Singleton (not Scoped, unlike NotificationService) is intentional:
// it lets every signed-in user see the same shared pool of tasks,
// events, requests and meetings, the way a real multi-user system
// would look once wired to a database.
//
// Register in Program.cs:
//     builder.Services.AddSingleton<AppDataService>();
//
// No namespace declared here on purpose - same convention as
// NotificationService.cs/Models/AppModels.cs - so every page sees this
// without an extra @using.

using System;
using System.Collections.Generic;
using System.Linq;

public class AppDataService
{
    // Pages (NavMenu's bell aside - that's NotificationService) that
    // want to react live to data changing elsewhere (e.g. Home should
    // update the moment a task's completion is toggled from the Daily
    // Tasks page in another open tab of the same session) can subscribe
    // to this and call StateHasChanged.
    public event Action? OnChange;
    private void NotifyStateChanged() => OnChange?.Invoke();

    private readonly List<AppUser> _users = new();
    private readonly List<AppTask> _tasks = new();
    private readonly List<AppEvent> _events = new();
    private readonly List<ResourceRequestItem> _requests = new();
    private readonly List<Meeting> _meetings = new();

    public IReadOnlyList<AppUser> Users => _users;
    public IReadOnlyList<AppTask> Tasks => _tasks;
    public IReadOnlyList<AppEvent> Events => _events;
    public IReadOnlyList<ResourceRequestItem> Requests => _requests;
    public IReadOnlyList<Meeting> Meetings => _meetings;

    public AppDataService()
    {
        SeedUsers();
        SeedEvents();
        SeedTasks();
        SeedRequests();
        SeedMeetings();
    }

    // ------------------------------------------------------------------
    // Users / login
    // ------------------------------------------------------------------

    // Case-insensitive match on Username, exact match on Password -
    // plenty for a prototype with no real auth behind it.
    public AppUser? Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        return _users.FirstOrDefault(u =>
            string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase) &&
            u.Password == password);
    }

    public AppUser AddUser(AppUser user)
    {
        _users.Add(user);
        NotifyStateChanged();
        return user;
    }

    // Employee names for dropdowns/checkbox lists (assignees, event
    // staff, meeting organizer, etc.) - every page used to keep its
    // own hard-coded copy of this; now it's derived from the one
    // shared user list.
    public List<string> EmployeeNames => _users.Select(u => u.FullName).OrderBy(n => n).ToList();

    // Same as EmployeeNames but excludes Admin-tier accounts - Admins
    // (chairman/secretary/treasurer) aren't line staff, so they should
    // never show up as a pickable assignee/coordinator/staff member on
    // the Daily Tasks or Events pages. Every other role stays pickable.
    public List<string> SelectableEmployeeNames =>
        _users.Where(u => u.SystemRole != SystemRole.Admin)
              .Select(u => u.FullName)
              .OrderBy(n => n)
              .ToList();

    // Resolves an employee's Id/role by their display name - used when
    // routing a notification to a specific person (e.g. a task's
    // assignee, an event's newly-added staff, a request's original
    // requester) since AppTask/AppEvent/ResourceRequestItem only store
    // names, not Ids.
    public AppUser? UserByName(string fullName) =>
        _users.FirstOrDefault(u => u.FullName == fullName);

    // ------------------------------------------------------------------
    // Tasks
    // ------------------------------------------------------------------

    public AppTask AddTask(AppTask task)
    {
        _tasks.Add(task);
        NotifyStateChanged();
        return task;
    }

    // Marks a task complete and attributes it to whoever completed it -
    // used by both Home and Tasks.razor's CompletedByDialog flow.
    public void CompleteTask(AppTask task, string completedBy)
    {
        task.IsCompleted = true;
        task.CompletedBy = completedBy;
        task.CompletedAt = DateTime.Now;
        NotifyStateChanged();
    }

    public void ReopenTask(AppTask task)
    {
        task.IsCompleted = false;
        task.CompletedBy = null;
        task.CompletedAt = null;
        NotifyStateChanged();
    }

    // Applies edited field values (from TaskDetailsDialog) onto the
    // existing AppTask instance - kept as one shared object (same Id)
    // rather than replaced, so it stays the same reference other pages
    // (Home, Events) already hold onto.
    public void UpdateTask(AppTask task, AppTask edited)
    {
        task.Title = edited.Title;
        task.Description = edited.Description;
        task.ScheduledStart = edited.ScheduledStart;
        task.IsAllDay = edited.IsAllDay;
        task.Category = edited.Category;
        task.Priority = edited.Priority;
        task.Assignee = edited.Assignee;
        task.Type = edited.Type;
        task.LinkedEventId = edited.LinkedEventId;
        task.LinkedEventTitle = edited.LinkedEventTitle;
        NotifyStateChanged();
    }

    public void DeleteTask(AppTask task)
    {
        _tasks.Remove(task);
        NotifyStateChanged();
    }

    public IEnumerable<AppTask> TasksForDate(DateTime date) =>
        _tasks.Where(t => t.ScheduledStart.Date == date.Date);

    public IEnumerable<AppTask> TodayTasks => TasksForDate(DateTime.Today);

    public IEnumerable<AppTask> TasksForEvent(Guid eventId) =>
        _tasks.Where(t => t.LinkedEventId == eventId);

    // ------------------------------------------------------------------
    // Events
    // ------------------------------------------------------------------

    public AppEvent AddEvent(AppEvent evt)
    {
        _events.Add(evt);
        NotifyStateChanged();
        return evt;
    }

    // Renaming/deleting an event would strand any tasks or requests
    // that reference it by name, so instead of overwriting in place we
    // just notify - callers (Events.razor) mutate the existing roster
    // item directly (see AppEvent.CopyFrom) then call this.
    public void NotifyEventChanged() => NotifyStateChanged();

    // ------------------------------------------------------------------
    // Resource requests
    // ------------------------------------------------------------------

    public ResourceRequestItem AddRequest(ResourceRequestItem request)
    {
        _requests.Add(request);
        NotifyStateChanged();
        return request;
    }

    public void ApproveRequest(ResourceRequestItem request, string reviewedBy)
    {
        request.Status = RequestStatus.Approved;
        request.ReviewedBy = reviewedBy;
        request.ReviewedDate = DateTime.Now;
        NotifyStateChanged();
    }

    public void DenyRequest(ResourceRequestItem request, string reviewedBy, string? reason)
    {
        request.Status = RequestStatus.Denied;
        request.ReviewedBy = reviewedBy;
        request.ReviewedDate = DateTime.Now;
        request.DenyReason = reason;
        NotifyStateChanged();
    }

    // ------------------------------------------------------------------
    // Meetings
    // ------------------------------------------------------------------

    public Meeting AddMeeting(Meeting meeting)
    {
        _meetings.Insert(0, meeting);
        NotifyStateChanged();
        return meeting;
    }

    // ------------------------------------------------------------------
    // Seed data - placeholder-but-realistic starting content so every
    // page/role has something to look at (and to log in as) the moment
    // the app starts, without needing a database.
    // ------------------------------------------------------------------

    private void SeedUsers()
    {
        _users.AddRange(new[]
        {
            new AppUser
            {
                FullName = "Naledi Dube",
                Username = "naledi.dube@drpssolutions.org",
                Email = "naledi.dube@drpssolutions.org",
                Password = "Naledi123",
                SystemRole = SystemRole.Admin,
                JobTitle = "Operations Coordinator",
                Department = "Admin",
                JoinDate = new DateTime(2024, 3, 1)
            },
            new AppUser
            {
                FullName = "Sipho Mokoena",
                Username = "sipho.mokoena@drpssolutions.org",
                Email = "sipho.mokoena@drpssolutions.org",
                Password = "Sipho123",
                SystemRole = SystemRole.GeneralStaff,
                JobTitle = "Fieldwork Lead",
                Department = "Programs",
                JoinDate = new DateTime(2023, 6, 15)
            },
            new AppUser
            {
                FullName = "Aisha Patel",
                Username = "aisha.patel@drpssolutions.org",
                Email = "aisha.patel@drpssolutions.org",
                Password = "Aisha123",
                SystemRole = SystemRole.FacilityManager,
                JobTitle = "Programs Manager",
                Department = "Programs",
                JoinDate = new DateTime(2022, 1, 10)
            },
            new AppUser
            {
                FullName = "Liam Fourie",
                Username = "liam.fourie@drpssolutions.org",
                Email = "liam.fourie@drpssolutions.org",
                Password = "Liam123",
                SystemRole = SystemRole.SecondaryAdmin,
                JobTitle = "IT & Systems Support",
                Department = "Operations",
                JoinDate = new DateTime(2024, 9, 2)
            },
            new AppUser
            {
                FullName = "Grace Adeyemi",
                Username = "grace.adeyemi@drpssolutions.org",
                Email = "grace.adeyemi@drpssolutions.org",
                Password = "Grace123",
                SystemRole = SystemRole.GeneralStaff,
                JobTitle = "Communications Officer",
                Department = "Marketing",
                JoinDate = new DateTime(2023, 11, 20)
            }
        });
    }

    private void SeedEvents()
    {
        _events.AddRange(new[]
        {
            new AppEvent
            {
                Title = "Annual Donor Gala",
                Description = "Formal evening event to thank major donors and showcase this year's impact numbers.",
                Date = DateTime.Today.AddDays(14),
                Time = new TimeSpan(18, 0, 0),
                Location = "Cape Town Convention Centre",
                Coordinator = "Aisha Patel",
                EstimatedBudget = 85000,
                EstimatedCapacity = 220,
                StaffMembers = new() { "Aisha Patel", "Grace Adeyemi", "Naledi Dube" },
                RequiredResources = new() { "Stage & PA system", "Catering (220 covers)", "Photographer" }
            },
            new AppEvent
            {
                Title = "Community Workshop",
                Description = "Half-day skills workshop for local youth, run out of the Alexandra community hall.",
                Date = DateTime.Today.AddDays(21),
                Time = new TimeSpan(9, 30, 0),
                Location = "Alexandra Community Hall",
                Coordinator = "Sipho Mokoena",
                EstimatedBudget = 12000,
                EstimatedCapacity = 60,
                StaffMembers = new() { "Sipho Mokoena", "Liam Fourie" },
                RequiredResources = new() { "Projector", "Folding chairs (60)", "Printed handouts" }
            },
            new AppEvent
            {
                Title = "Board Meeting",
                Description = "Quarterly board strategy session covering the Q3 numbers and upcoming grant cycle.",
                Date = DateTime.Today.AddDays(1),
                Time = new TimeSpan(14, 0, 0),
                Location = "Head Office Boardroom",
                Coordinator = "Naledi Dube",
                EstimatedBudget = 1500,
                EstimatedCapacity = 12,
                StaffMembers = new() { "Naledi Dube", "Aisha Patel" },
                RequiredResources = new() { "Printed board pack" }
            }
        });
    }

    private void SeedTasks()
    {
        var gala = _events.First(e => e.Title == "Annual Donor Gala");
        var workshop = _events.First(e => e.Title == "Community Workshop");
        var board = _events.First(e => e.Title == "Board Meeting");

        _tasks.AddRange(new[]
        {
            new AppTask
            {
                Title = "Volunteer check-in call",
                Description = "Quick call with this week's volunteer roster to confirm availability.",
                ScheduledStart = DateTime.Today.AddHours(8),
                Category = "Admin",
                Priority = TaskPriority.Low,
                Type = AppTaskType.Daily,
                Assignee = "Naledi Dube",
                IsCompleted = true,
                CompletedBy = "Naledi Dube",
                CompletedAt = DateTime.Today.AddHours(8).AddMinutes(20)
            },
            new AppTask
            {
                Title = "Prepare donor report",
                Description = "Draft the monthly donor impact report for review.",
                ScheduledStart = DateTime.Today.AddHours(9).AddMinutes(30),
                Category = "Admin",
                Priority = TaskPriority.Medium,
                Type = AppTaskType.Daily,
                Assignee = "Naledi Dube"
            },
            new AppTask
            {
                Title = "Site visit — Alexandra shelter",
                Description = "On-site check of the shelter ahead of the community workshop.",
                ScheduledStart = DateTime.Today.AddHours(11),
                Category = "Fieldwork",
                Priority = TaskPriority.High,
                Type = AppTaskType.Daily,
                Assignee = "Sipho Mokoena"
            },
            new AppTask
            {
                Title = "Team stand-up",
                Description = "Daily sync on outstanding fieldwork and admin items.",
                ScheduledStart = DateTime.Today.AddHours(13),
                Category = "Meeting",
                Priority = TaskPriority.Low,
                Type = AppTaskType.Daily,
                Assignee = "Aisha Patel"
            },
            new AppTask
            {
                Title = "Grant proposal review",
                Description = "Review the sponsorship proposal ahead of the donor gala.",
                ScheduledStart = DateTime.Today.AddHours(15).AddMinutes(30),
                Category = "Admin",
                Priority = TaskPriority.High,
                Type = AppTaskType.Event,
                LinkedEventId = gala.Id,
                LinkedEventTitle = gala.Title,
                Assignee = "Aisha Patel"
            },
            new AppTask
            {
                Title = "Community workshop setup",
                Description = "Set up chairs, projector and handouts at the community hall.",
                ScheduledStart = DateTime.Today.AddDays(1).AddHours(9),
                Category = "Fieldwork",
                Priority = TaskPriority.Medium,
                Type = AppTaskType.Event,
                LinkedEventId = workshop.Id,
                LinkedEventTitle = workshop.Title,
                Assignee = "Sipho Mokoena"
            },
            new AppTask
            {
                Title = "Board meeting",
                Description = "Present Q3 numbers and the upcoming grant cycle plan.",
                ScheduledStart = DateTime.Today.AddDays(1).AddHours(14),
                Category = "Meeting",
                Priority = TaskPriority.High,
                Type = AppTaskType.Event,
                LinkedEventId = board.Id,
                LinkedEventTitle = board.Title,
                Assignee = "Naledi Dube"
            },
            new AppTask
            {
                Title = "Inventory count",
                Description = "Count and log current warehouse stock levels.",
                ScheduledStart = DateTime.Today.AddDays(2).AddHours(10),
                Category = "Fieldwork",
                Priority = TaskPriority.Low,
                Type = AppTaskType.Daily,
                Assignee = "Sipho Mokoena"
            },
            new AppTask
            {
                Title = "Submit grant report",
                Description = "Submit the finalised grant report before end of day.",
                ScheduledStart = DateTime.Today,
                Category = "Admin",
                Priority = TaskPriority.High,
                IsAllDay = true,
                Type = AppTaskType.Daily,
                Assignee = "Naledi Dube"
            },
            new AppTask
            {
                Title = "Renew office lease paperwork",
                Description = "Sign and return the renewed office lease documents.",
                ScheduledStart = DateTime.Today.AddDays(1),
                Category = "Admin",
                Priority = TaskPriority.Medium,
                IsAllDay = true,
                Type = AppTaskType.Daily,
                Assignee = "Liam Fourie"
            }
        });
    }

    private void SeedRequests()
    {
        var gala = _events.First(e => e.Title == "Annual Donor Gala");
        var workshop = _events.First(e => e.Title == "Community Workshop");
        var siteVisit = _tasks.First(t => t.Title == "Site visit — Alexandra shelter");

        _requests.AddRange(new[]
        {
            new ResourceRequestItem
            {
                Title = "Projector for donor gala",
                ResourceType = "Equipment",
                Amount = "1 projector",
                Notes = "Needed for the sponsor highlight reel.",
                LinkType = RequestLinkType.Event,
                LinkedId = gala.Id,
                LinkedName = gala.Title,
                RequestedBy = "Aisha Patel",
                RequestedDate = DateTime.Now.AddDays(-2),
                Status = RequestStatus.Pending
            },
            new ResourceRequestItem
            {
                Title = "Additional volunteers",
                ResourceType = "Personnel",
                Amount = "4 volunteers",
                Notes = "Site visit needs extra hands for the day.",
                LinkType = RequestLinkType.Task,
                LinkedId = siteVisit.Id,
                LinkedName = siteVisit.Title,
                RequestedBy = "Sipho Mokoena",
                RequestedDate = DateTime.Now.AddDays(-1),
                Status = RequestStatus.Pending
            },
            new ResourceRequestItem
            {
                Title = "Printing budget",
                ResourceType = "Budget",
                Amount = "R 1,200",
                Notes = "Workshop handouts and signage.",
                LinkType = RequestLinkType.Event,
                LinkedId = workshop.Id,
                LinkedName = workshop.Title,
                RequestedBy = "Grace Adeyemi",
                RequestedDate = DateTime.Now.AddDays(-5),
                Status = RequestStatus.Approved,
                ReviewedBy = "Naledi Dube",
                ReviewedDate = DateTime.Now.AddDays(-4)
            },
            new ResourceRequestItem
            {
                Title = "New laptop",
                ResourceType = "Equipment",
                Amount = "1 laptop",
                Notes = "Current one won't hold a charge.",
                LinkType = RequestLinkType.None,
                RequestedBy = "Liam Fourie",
                RequestedDate = DateTime.Now.AddDays(-7),
                Status = RequestStatus.Denied,
                ReviewedBy = "Naledi Dube",
                ReviewedDate = DateTime.Now.AddDays(-6),
                DenyReason = "Budget's earmarked for Q3 - resubmit then."
            }
        });
    }

    private void SeedMeetings()
    {
        _meetings.AddRange(new[]
        {
            new Meeting
            {
                Title = "Donor Operations Review",
                Date = DateTime.Today.AddDays(-1),
                Duration = "45 mins",
                Organizer = "Aisha Patel",
                MinutesText = "Reviewed donor pipeline health and Q3 gala prep timeline.",
                Notes = new()
                {
                    new MeetingNote { Text = "Gala budget tracking on target", IsActionItem = false, Timestamp = "00:04" },
                    new MeetingNote { Text = "Aisha to confirm caterer by Friday", IsActionItem = true, Timestamp = "00:12" }
                },
                // Placeholder transcript text, pre-generated for demo
                // purposes - stands in for real speech-to-text output.
                Transcript =
                    "Transcript for \"Donor Operations Review\" (45:00 recorded)\n\n" +
                    "[Speaker 1] Placeholder line of dialogue reconstructed from the recorded audio.\n" +
                    "[Speaker 2] Placeholder response continuing the discussion.\n" +
                    "[Speaker 1] Placeholder wrap-up remark closing out this section.\n\n" +
                    "This is placeholder text standing in for real speech-to-text output, " +
                    "to be replaced once a transcription service is integrated."
            },
            new Meeting
            {
                Title = "Fieldwork Logistics & Planning",
                Date = DateTime.Today,
                Duration = "30 mins",
                Organizer = "Sipho Mokoena",
                MinutesText = "Walked through the community workshop setup plan and staffing.",
                Notes = new()
                {
                    new MeetingNote { Text = "Need 4 extra volunteers for site visit", IsActionItem = true, Timestamp = "00:07" }
                }
            },
            new Meeting
            {
                Title = "Q3 Board Strategy Session",
                Date = DateTime.Today.AddDays(2),
                Duration = "60 mins",
                Organizer = "Naledi Dube"
            }
        });
    }
}
