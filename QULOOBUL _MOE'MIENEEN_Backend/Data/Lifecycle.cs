namespace QULOOBUL__MOE_MIENEEN_Backend.Data
{
    /// <summary>
    /// SM-01 / SM-02 guarded transition tables. One table per workflow, checked server-side
    /// inside the owning use-case service (architecture doc, "State machine as guarded transitions").
    /// A class per state would add six types to express a two-column rule, so it is a table.
    /// </summary>
    public static class Lifecycle
    {
        public const string TaskStart = "To Do";
        public const string RequestStart = "Pending";

        /// <summary>SM-02: reached by the 48-hour guard in MaintenanceJobs, never by a user action.</summary>
        public const string RequestEscalated = "Escalated";
//----------------------------------------------------------------------------------------------------------------------//
        private static readonly Dictionary<string, string[]> Task = new(StringComparer.OrdinalIgnoreCase)
        {
            ["To Do"] = ["In Progress", "Blocked"],
            ["In Progress"] = ["Blocked", "Done", "To Do"],
            ["Blocked"] = ["In Progress", "To Do"],
            ["Done"] = ["In Progress"], // reopen
        };
//----------------------------------------------------------------------------------------------------------------------//
        private static readonly Dictionary<string, string[]> Request = new(StringComparer.OrdinalIgnoreCase)
        {
            // ponytail: SM-02's "Under Review" and the 30-day resubmit window are still missing; this
            // table covers the escalation path only, because that is what the 48-hour job needs.
            ["Pending"] = ["Approved", "Declined", RequestEscalated],
            [RequestEscalated] = ["Approved", "Declined"], // escalation must not dead-end the request
            ["Approved"] = ["Completed"],
        };
//----------------------------------------------------------------------------------------------------------------------//
        public static bool TaskCanMove(string? current, string next) =>
            CanMove(Task, current, TaskStart, next);
//----------------------------------------------------------------------------------------------------------------------//
        public const string UserStart = "Active";
//----------------------------------------------------------------------------------------------------------------------//
        private static readonly Dictionary<string, string[]> UserSm = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Active"] = ["Deactivated"],
            ["Deactivated"] = ["Active"], // reactivation
        };
//----------------------------------------------------------------------------------------------------------------------//
        public static bool UserCanMove(string? current, string next) =>
            CanMove(UserSm, current, UserStart, next);
//----------------------------------------------------------------------------------------------------------------------//
        public static void EnsureUserMove(string? current, string next)
        {
            if (!UserCanMove(current, next))
                throw new InvalidOperationException($"User cannot move from \"{Norm(current, UserStart)}\" to \"{next}\".");
        }
//----------------------------------------------------------------------------------------------------------------------//
        public static string UserState(bool isActive) => isActive ? "Active" : "Deactivated";
//----------------------------------------------------------------------------------------------------------------------//
        public static bool RequestCanMove(string? current, string next) =>
            CanMove(Request, current, RequestStart, next);
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