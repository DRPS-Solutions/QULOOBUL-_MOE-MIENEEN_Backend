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

        private static readonly Dictionary<string, string[]> Task = new(StringComparer.OrdinalIgnoreCase)
        {
            ["To Do"] = ["In Progress", "Blocked"],
            ["In Progress"] = ["Blocked", "Done", "To Do"],
            ["Blocked"] = ["In Progress", "To Do"],
            ["Done"] = ["In Progress"], // reopen
        };

        private static readonly Dictionary<string, string[]> Request = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Pending"] = ["Approved", "Declined"],
            ["Approved"] = ["Completed"],
        };

        public static bool TaskCanMove(string? current, string next) =>
            CanMove(Task, current, TaskStart, next);

        public static bool RequestCanMove(string? current, string next) =>
            CanMove(Request, current, RequestStart, next);

        /// <summary>Throws <see cref="InvalidOperationException"/> if the move is not allowed.</summary>
        public static void EnsureTaskMove(string? current, string next)
        {
            if (!TaskCanMove(current, next))
                throw new InvalidOperationException($"Task cannot move from \"{Norm(current, TaskStart)}\" to \"{next}\".");
        }

        public static void EnsureRequestMove(string? current, string next)
        {
            if (!RequestCanMove(current, next))
                throw new InvalidOperationException($"Request cannot move from \"{Norm(current, RequestStart)}\" to \"{next}\".");
        }

        private static bool CanMove(Dictionary<string, string[]> table, string? current, string start, string next)
        {
            var from = Norm(current, start);
            if (from.Equals(next, StringComparison.OrdinalIgnoreCase)) return true; // idempotent no-op
            return table.TryGetValue(from, out var allowed)
                   && allowed.Contains(next, StringComparer.OrdinalIgnoreCase);
        }

        private static string Norm(string? current, string start) =>
            string.IsNullOrEmpty(current) ? start : current;
    }
}
