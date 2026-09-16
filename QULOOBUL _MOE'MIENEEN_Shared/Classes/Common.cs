using System;
using System.Collections.Generic;
using System.Text;

namespace QULOOBUL__MOE_MIENEEN_Shared.Classes
{
    public class Common
    {
        public Common()
        {
        }

        /// <summary>Standard page envelope for list endpoints.</summary>
        public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

        /// <summary>The only error shape clients ever parse (see global exception handler).</summary>
        public record ErrorResponse(string Message, string? Detail = null);

        /// <summary>Every task belongs to a project; standalone daily tasks live in this seeded one.</summary>
        public const string DailyTasksProject = "Daily Tasks";

        public static class Roles
        {
            public const string Admin = "Admin";
            public const string SecondaryAdmin = "Secondary Admin";
            public const string FacilityManager = "Facility Manager";
            public const string Staff = "Staff";
            public const string Imaamat = "Imaamat";

            public const string outSider = "Outsider";
        }

        public static class Permissions
        {
            public const string ReviewRequests = "ReviewRequests";
            public const string ManageUsers = "ManageUsers";
            public const string GenerateReports = "GenerateReports";
            public const string ManageFiles = "ManageFiles";
        }
    }
}
