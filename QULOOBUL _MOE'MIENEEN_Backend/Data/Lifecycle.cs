using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;

namespace QULOOBUL__MOE_MIENEEN_Backend.Data
{
    /// <summary>
    /// SM-01 / SM-02 / SM-03 guarded transition tables. One table per workflow, checked server-side
    /// inside the owning use-case service (architecture doc, "State machine as guarded transitions").
    /// A class per state would add six types to express a two-column rule, so it is a table.
    /// Guards that need data (assignee present, permission held, 30-day window) live in the services.
    /// </summary>
    public static class Lifecycle
    {
        public const string TaskStart = "To Do";
        public const string TaskCancelled = "Cancelled";
        public const string TaskArchived = "Archived";
        public const string RequestStart = "Pending";
        public const string RequestUnderReview = "Under Review";
        public const string RequestDeclined = "Declined";

        /// <summary>SM-02: reached by the 48-hour guard in MaintenanceJobs, never by a user action.</summary>
        public const string RequestEscalated = "Escalated";

        /// <summary>SM-01 archive() guard: a project with this status has closed.</summary>
        public const string ProjectClosed = "Closed";

        /// <summary>Event Planning's cancel: the event won't happen, so its open tasks are cancelled with it.</summary>
        public const string ProjectCancelled = "Cancelled";

        /// <summary>Task statuses that no longer count as open work.</summary>
        public static readonly string[] TaskFinished = ["Done", TaskCancelled, TaskArchived];
//----------------------------------------------------------------------------------------------------------------------//
        // In Progress/Blocked → To Do are not drawn in SM-01, but the kanban drags cards back, so they stay.
        private static readonly Dictionary<string, string[]> Task = new(StringComparer.OrdinalIgnoreCase)
        {
            ["To Do"] = ["In Progress", TaskCancelled],
            ["In Progress"] = ["Blocked", "Done", "To Do", TaskCancelled],
            ["Blocked"] = ["In Progress", "To Do"],
            ["Done"] = ["In Progress", TaskArchived], // reopen, archive
        };
//----------------------------------------------------------------------------------------------------------------------//
        private static readonly Dictionary<string, string[]> Request = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Pending"] = [RequestUnderReview, RequestEscalated],
            [RequestEscalated] = [RequestUnderReview],    // the facility manager picks it up
            [RequestUnderReview] = ["Approved", RequestDeclined],
            [RequestDeclined] = [RequestStart],           // resubmit, guarded by the 30-day window
            ["Approved"] = ["Completed"],
        };
//----------------------------------------------------------------------------------------------------------------------//
        public const string UserPending = "Pending Activation";
        public const string UserActive = "Active";
        public const string UserLocked = "Locked";
        public const string UserInactive = "Inactive";
        public const string UserAnonymised = "Anonymised";

        private static readonly Dictionary<string, string[]> UserSm = new(StringComparer.OrdinalIgnoreCase)
        {
            [UserPending] = [UserActive],                  // setPassword
            [UserActive] = [UserInactive, UserLocked],     // deactivate, 3 failed logins
            [UserLocked] = [UserActive],                   // admin resetPassword
            [UserInactive] = [UserActive, UserAnonymised], // reactivate, anonymise
        };
//----------------------------------------------------------------------------------------------------------------------//
        public static bool TaskCanMove(string? current, string next) =>
            CanMove(Task, current, TaskStart, next);
//----------------------------------------------------------------------------------------------------------------------//
        public static bool RequestCanMove(string? current, string next) =>
            CanMove(Request, current, RequestStart, next);
//----------------------------------------------------------------------------------------------------------------------//
        public static bool UserCanMove(string? current, string next) =>
            CanMove(UserSm, current, UserActive, next);
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Throws <see cref="InvalidOperationException"/> if the move is not allowed.</summary>
        public static void EnsureTaskMove(string? current, string next)
        {
            if (!TaskCanMove(current, next))
                throw new InvalidOperationException($"Task cannot move from \"{Norm(current, TaskStart)}\" to \"{next}\".");
        }
//----------------------------------------------------------------------------------------------------------------------//
        public static void EnsureRequestMove(string? current, string next)
        {
            if (!RequestCanMove(current, next))
                throw new InvalidOperationException($"Request cannot move from \"{Norm(current, RequestStart)}\" to \"{next}\".");
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>The one way a user's SM-03 state changes. IsActive mirrors "Status is Active", so
        /// every existing "can log in / can be assigned work" check keeps working unchanged.</summary>
        public static void MoveUser(User user, string next)
        {
            if (!UserCanMove(user.Status, next))
                throw new InvalidOperationException($"User cannot move from \"{Norm(user.Status, UserActive)}\" to \"{next}\".");
            user.Status = next;
            user.IsActive = next == UserActive;
        }
//----------------------------------------------------------------------------------------------------------------------//
        private static bool CanMove(Dictionary<string, string[]> table, string? current, string start, string next)
        {
            var from = Norm(current, start);
            if (from.Equals(next, StringComparison.OrdinalIgnoreCase)) return true; // idempotent no-op
            return table.TryGetValue(from, out var allowed)
                   && allowed.Contains(next, StringComparer.OrdinalIgnoreCase);
        }
//----------------------------------------------------------------------------------------------------------------------//
        private static string Norm(string? current, string start) =>
            string.IsNullOrEmpty(current) ? start : current;
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
