// NotificationService.cs
//
// In-memory notification store for the prototype. No namespace is
// declared on purpose so this drops into any project without needing
// a matching @using - everything here lives in the global namespace,
// same as the rest of this prototype's code-behind classes.
//
// Register in Program.cs:
//     builder.Services.AddSingleton<NotificationService>();
//
// Singleton (not Scoped) is intentional now that notifications are
// actually role/user-targeted instead of a single shared feed: every
// signed-in circuit needs to see the *same* pool of notifications, each
// one filtered down to what that particular signed-in user/role should
// see (see NotificationsFor below), the same way AppDataService already
// shares its Tasks/Events/Requests/Users across every session.

using System;
using System.Collections.Generic;
using System.Linq;

public enum NotificationType
{
    ResourceRequest,
    EventAssignment,
    TaskAssignment
}

public class NotificationItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // Optional name of the event/request/task this notification refers
    // to - shown as a small chip and used to decide which page "View"
    // opens.
    public string? LinkedName { get; set; }

    // --- Audience -----------------------------------------------------
    // A notification is visible to a signed-in user if EITHER their
    // user Id is in TargetUserIds (a personal "this is about you"
    // notification - e.g. "you were assigned to this task") OR their
    // SystemRole is in TargetRoles (a role-wide broadcast - e.g. "a new
    // event was created", which every Admin/Secondary Admin should see
    // regardless of who created it). A notification can use either,
    // both, or neither field - one with neither is effectively
    // invisible, so every call site should set at least one.
    public HashSet<Guid> TargetUserIds { get; } = new();
    public HashSet<SystemRole> TargetRoles { get; } = new();

    // --- Per-viewer state ----------------------------------------------
    // Read/dismissed state used to be single shared bools, but now that
    // one NotificationItem can be visible to several different signed-in
    // users at once (a role-wide broadcast, or two people with the same
    // role), "read" and "cleared" have to be tracked per-viewer - marking
    // your own bell as read must never clear it for someone else who can
    // also see that same notification.
    private readonly HashSet<Guid> _readBy = new();
    private readonly HashSet<Guid> _dismissedBy = new();

    public bool IsVisibleTo(Guid userId, SystemRole role) =>
        !_dismissedBy.Contains(userId) &&
        (TargetUserIds.Contains(userId) || TargetRoles.Contains(role));

    public bool IsReadBy(Guid userId) => _readBy.Contains(userId);

    public void MarkReadBy(Guid userId) => _readBy.Add(userId);

    public void DismissFor(Guid userId) => _dismissedBy.Add(userId);

    // Seed-only helper so SeedDemoData below can attach target roles
    // fluently without an extra constructor overload.
    public NotificationItem WithRoles(params SystemRole[] roles)
    {
        foreach (var role in roles) TargetRoles.Add(role);
        return this;
    }
}

public class NotificationService
{
    // Components (NavMenu's bell badge, the Notifications page) subscribe
    // to this and call StateHasChanged so the UI reacts immediately to
    // Add/MarkAsRead/etc. without any polling.
    public event Action? OnChange;

    private readonly List<NotificationItem> _notifications = new();

    public NotificationService()
    {
        SeedDemoData();
    }

    // targetUserIds drives personal "this is about you" notifications
    // (e.g. task/event assignment); targetRoles drives role-wide
    // broadcasts (e.g. "a new event was made", seen by every Admin and
    // Secondary Admin). Pass either, both, or neither - a call with
    // neither produces a notification nobody will ever see, so callers
    // should always supply at least one.
    public void Add(NotificationType type, string title, string message,
                     IEnumerable<Guid>? targetUserIds = null,
                     IEnumerable<SystemRole>? targetRoles = null,
                     string? linkedName = null)
    {
        var item = new NotificationItem
        {
            Type = type,
            Title = title,
            Message = message,
            LinkedName = linkedName
        };

        if (targetUserIds is not null)
        {
            foreach (var id in targetUserIds) item.TargetUserIds.Add(id);
        }
        if (targetRoles is not null)
        {
            foreach (var role in targetRoles) item.TargetRoles.Add(role);
        }

        _notifications.Add(item);
        NotifyStateChanged();
    }

    // Every notification visible to this user/role, newest first - what
    // the bell badge count and the Notifications page are both built on.
    public List<NotificationItem> NotificationsFor(Guid userId, SystemRole role) =>
        _notifications
            .Where(n => n.IsVisibleTo(userId, role))
            .OrderByDescending(n => n.Timestamp)
            .ToList();

    public int UnreadCountFor(Guid userId, SystemRole role) =>
        NotificationsFor(userId, role).Count(n => !n.IsReadBy(userId));

    public void MarkAsRead(NotificationItem item, Guid userId)
    {
        if (item.IsReadBy(userId)) return;
        item.MarkReadBy(userId);
        NotifyStateChanged();
    }

    public void MarkAllAsRead(Guid userId, SystemRole role)
    {
        var changed = false;
        foreach (var n in NotificationsFor(userId, role))
        {
            if (n.IsReadBy(userId)) continue;
            n.MarkReadBy(userId);
            changed = true;
        }
        if (changed) NotifyStateChanged();
    }

    // Dismissing/clearing only hides the item for the viewer who did it -
    // it can't delete the underlying NotificationItem outright, since a
    // role-wide broadcast is likely still visible to other signed-in
    // users who share that role.
    public void Remove(NotificationItem item, Guid userId)
    {
        item.DismissFor(userId);
        NotifyStateChanged();
    }

    public void ClearAll(Guid userId, SystemRole role)
    {
        var visible = NotificationsFor(userId, role);
        if (visible.Count == 0) return;
        foreach (var n in visible) n.DismissFor(userId);
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    // A few seeded notifications so the bell/page aren't empty on first
    // load - mirrors the placeholder seed data already used in
    // Requests.razor/Events.razor. Targeted by role so every demo login
    // (see AppDataService.SeedUsers) has something to look at.
    private void SeedDemoData()
    {
        _notifications.Add(new NotificationItem
        {
            Type = NotificationType.EventAssignment,
            Title = "Added to Annual Donor Gala",
            Message = "You've been added to the staff list for \"Annual Donor Gala\".",
            LinkedName = "Annual Donor Gala",
            Timestamp = DateTime.Now.AddHours(-3)
        }.WithRoles(SystemRole.SecondaryAdmin, SystemRole.FacilityManager));

        _notifications.Add(new NotificationItem
        {
            Type = NotificationType.ResourceRequest,
            Title = "New resource request",
            Message = "Aisha Patel requested a projector for \"Annual Donor Gala\".",
            LinkedName = "Annual Donor Gala",
            Timestamp = DateTime.Now.AddDays(-2)
        }.WithRoles(SystemRole.Admin, SystemRole.SecondaryAdmin));

        _notifications.Add(new NotificationItem
        {
            Type = NotificationType.ResourceRequest,
            Title = "Request approved",
            Message = "Naledi Dube approved your \"Printing budget\" request.",
            LinkedName = "Community Workshop",
            Timestamp = DateTime.Now.AddDays(-4)
        }.WithRoles(SystemRole.Admin, SystemRole.SecondaryAdmin));

        _notifications.Add(new NotificationItem
        {
            Type = NotificationType.TaskAssignment,
            Title = "New task assigned",
            Message = "You were assigned \"Site visit — Alexandra shelter\".",
            LinkedName = "Site visit — Alexandra shelter",
            Timestamp = DateTime.Now.AddHours(-6)
        }.WithRoles(SystemRole.GeneralStaff, SystemRole.FacilityManager));
    }
}
