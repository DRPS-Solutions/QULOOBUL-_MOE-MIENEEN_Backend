using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration;
using QULOOBUL__MOE_MIENEEN_Backend.Models.File;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Request;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Task;

namespace QULOOBUL__MOE_MIENEEN_Backend.Data
{
    /// <summary>
    /// The single Unit of Work for the platform. Every DbSet is a Repository and
    /// SaveChanges() commits every tracked change in one transaction (see architecture doc,
    /// "Repository and Unit of Work"). Registered request-scoped in Program.cs.
    /// Reference: M. Fowler, Patterns of Enterprise Application Architecture. Boston, MA, USA: Addison-Wesley, 2003.
    /// Reference: Microsoft, "Implementing the infrastructure persistence layer with Entity Framework Core." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core
    /// </summary>
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        // Identity and access
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
//----------------------------------------------------------------------------------------------------------------------//
        // Projects and tasks
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
        public DbSet<ProjectTask> Tasks => Set<ProjectTask>();
        public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
        public DbSet<TaskComment> TaskComments => Set<TaskComment>();
        public DbSet<TaskSchedule> TaskSchedules => Set<TaskSchedule>();
        public DbSet<TaskStatusHistory> TaskStatusHistory => Set<TaskStatusHistory>();
//----------------------------------------------------------------------------------------------------------------------//
        // Meetings and events
        public DbSet<Meeting> Meetings => Set<Meeting>();
        public DbSet<MeetingAttendee> MeetingAttendees => Set<MeetingAttendee>();
        public DbSet<MeetingMinutes> MeetingMinutes => Set<MeetingMinutes>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<EventAttendee> EventAttendees => Set<EventAttendee>();
//----------------------------------------------------------------------------------------------------------------------//
        // Files and reporting
        public DbSet<FileCategory> FileCategories => Set<FileCategory>();
        public DbSet<FileUpload> FileUploads => Set<FileUpload>();
        public DbSet<Report> Reports => Set<Report>();
//----------------------------------------------------------------------------------------------------------------------//
        // Resource requests
        public DbSet<RequestCategory> RequestCategories => Set<RequestCategory>();
        public DbSet<ResourceRequest> ResourceRequests => Set<ResourceRequest>();
        public DbSet<RequestStatusHistory> RequestStatusHistory => Set<RequestStatusHistory>();
//----------------------------------------------------------------------------------------------------------------------//
        // Notifications and audit
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<NotificationType> NotificationTypes => Set<NotificationType>();
        public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<UserDirectoryEntry> UserDirectory => Set<UserDirectoryEntry>();
//----------------------------------------------------------------------------------------------------------------------//

        /// <summary>
        /// Performs additional configuration of the model that was discovered by convention from the entity types
        /// Protected override method called by EF Core during model creation. Sets delete behaviors, unique constraints, and check constraints according to the architecture requirements.
        /// </summary>
        /// <param name="b"></param>
        protected override void OnModelCreating(ModelBuilder b)
        {
            // Every FK is ON DELETE NO ACTION: the audit trail and history tables must never
            // be cascaded away, and 22 relationships point at User (architecture doc,
            // "Deactivation rather than deletion of user"). Also avoids SQL Server's
            // multiple-cascade-path error without hand-writing every relationship.
            // Reference: Microsoft, "Cascade delete - EF Core." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete
            foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
                fk.DeleteBehavior = DeleteBehavior.Restrict;

            // ...then the per-relationship exceptions (architecture doc, "Delete behaviour is set per
            // relationship"). Cascade: rows meaningless without their parent. Set null: optional links.
            // Every FK to User stays NO ACTION, and so do the history tables.
            b.Entity<TaskComment>().HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<TaskAssignment>().HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<ProjectMember>().HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<MeetingAttendee>().HasOne(x => x.Meeting).WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<EventAttendee>().HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<FileUpload>().HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.SetNull);
            b.Entity<FileUpload>().HasOne(x => x.Meeting).WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.SetNull);

            // The access paths the use cases hit directly (architecture doc, "Indexing"); User.Email is below.
            // Reference: Microsoft, "Indexes - EF Core." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/ef/core/modeling/indexes
            // Reference: T. M. Connolly and C. E. Begg, Database Systems: A Practical Approach to Design, Implementation, and Management. Boston, MA, USA: Addison-Wesley, 2010.
            b.Entity<ProjectTask>().HasIndex(x => new { x.ProjectId, x.Status });   // the task board
            b.Entity<Notification>().HasIndex(x => new { x.UserId, x.IsRead });     // the unread badge
            b.Entity<AuditLog>().HasIndex(x => x.Timestamp);                        // time-ranged audit queries

            // Staff directory reads a view that has no credential columns (defence in depth); created in
            // the SecurityAndIndexes migration alongside the Notification row-level security policy.
            // Reference: Microsoft, "Keyless entity types - EF Core." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/ef/core/modeling/keyless-entity-types
            b.Entity<UserDirectoryEntry>().HasNoKey().ToView("vw_UserDirectory");

            // Junction tables reject duplicate pairs (architecture doc, "Junction tables carry
            // their own surrogate key ... with a unique constraint on the pair").
            // Reference: P. P.-S. Chen, "The entity-relationship model - toward a unified view of data," ACM Trans. Database Syst., vol. 1, no. 1, pp. 9-36, Mar. 1976, doi: 10.1145/320434.320440.
            b.Entity<TaskAssignment>().HasIndex(x => new { x.TaskId, x.UserId }).IsUnique();
            b.Entity<ProjectMember>().HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique();
            b.Entity<MeetingAttendee>().HasIndex(x => new { x.MeetingId, x.UserId }).IsUnique();
            b.Entity<EventAttendee>().HasIndex(x => new { x.EventId, x.UserId }).IsUnique();
            b.Entity<RolePermission>().HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
            b.Entity<User>().HasIndex(x => x.Email).IsUnique();
            b.Entity<NotificationPreference>().HasIndex(x => new { x.UserId, x.TypeId }).IsUnique();

            // "Booking / scheduling integrity": one event per venue per slot, held by the database as
            // well as EventService, so two concurrent bookings cannot both slip past the service check.
            b.Entity<Event>().HasIndex(x => new { x.Location, x.EventDate }).IsUnique()
                .HasFilter("[Location] <> ''");

            // ponytail: at-most-one attachment context on FileUpload is enforced here as a
            // CHECK constraint; the service also guards it so the error is friendly.
            // Reference: E. F. Codd, "A relational model of data for large shared data banks," Commun. ACM, vol. 13, no. 6, pp. 377-387, Jun. 1970, doi: 10.1145/362384.362685.
            b.Entity<FileUpload>().ToTable(t => t.HasCheckConstraint(
                "CK_FileUpload_OneContext",
                "((CASE WHEN [TaskId] IS NULL THEN 0 ELSE 1 END) + " +
                "(CASE WHEN [MeetingId] IS NULL THEN 0 ELSE 1 END) + " +
                "(CASE WHEN [PeriodMonth] IS NULL THEN 0 ELSE 1 END)) <= 1"));

            // Domain (range) rules held by the database as well as the services, so a bad write from
            // any path (a future endpoint, a script, a manual UPDATE) is rejected rather than stored.
            // Reference: T. M. Connolly and C. E. Begg, Database Systems: A Practical Approach to Design, Implementation, and Management. Boston, MA, USA: Addison-Wesley, 2010.
            b.Entity<ProjectTask>().ToTable(t => t.HasCheckConstraint(
                "CK_Task_PercentComplete", "[PercentComplete] BETWEEN 0 AND 100"));
            b.Entity<Report>().ToTable(t => t.HasCheckConstraint(
                "CK_Report_PeriodOrder", "[PeriodEnd] >= [PeriodStart]"));
            b.Entity<User>().ToTable(t => t.HasCheckConstraint(
                "CK_User_FailedLoginCount", "[FailedLoginCount] >= 0"));

            // Allowed-value rules: the SM-01/02/03 states in Lifecycle, so an unknown status cannot be
            // stored even if a service guard is bypassed. Priority and project status are free text in the
            // services, so these lists are the values the clients and seeder use today.
            // Reference: Microsoft, "Check constraints - EF Core." Accessed: Oct. 20, 2026. [Online]. Available: https://learn.microsoft.com/en-us/ef/core/modeling/constraints#check-constraints
            string[] taskStatuses = ["To Do", "In Progress", "Blocked", "Done", Lifecycle.TaskCancelled, Lifecycle.TaskArchived];
            string[] requestStatuses = [Lifecycle.RequestStart, Lifecycle.RequestUnderReview, Lifecycle.RequestEscalated, "Approved", Lifecycle.RequestDeclined, "Completed"];
            string[] userStatuses = [Lifecycle.UserPending, Lifecycle.UserActive, Lifecycle.UserLocked, Lifecycle.UserInactive, Lifecycle.UserAnonymised];
            string[] priorities = ["Low", "Normal", "Medium", "High"];
            string[] projectStatuses = ["Active", Lifecycle.ProjectClosed, Lifecycle.ProjectCancelled];
            string[] days = Enum.GetNames<DayOfWeek>();

            b.Entity<ProjectTask>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_Task_Status", In("Status", taskStatuses));
                t.HasCheckConstraint("CK_Task_Priority", In("Priority", priorities));
            });
            b.Entity<TaskStatusHistory>().ToTable(t => t.HasCheckConstraint("CK_TaskStatusHistory_NewStatus", In("NewStatus", taskStatuses)));
            b.Entity<ResourceRequest>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_ResourceRequest_Status", In("Status", requestStatuses));
                t.HasCheckConstraint("CK_ResourceRequest_Priority", In("Priority", priorities));
            });
            b.Entity<RequestStatusHistory>().ToTable(t => t.HasCheckConstraint("CK_RequestStatusHistory_NewStatus", In("NewStatus", requestStatuses)));
            b.Entity<User>().ToTable(t => t.HasCheckConstraint("CK_User_Status", In("Status", userStatuses)));
            b.Entity<Project>().ToTable(t => t.HasCheckConstraint("CK_Project_Status", In("Status", projectStatuses)));
            b.Entity<TaskSchedule>().ToTable(t => t.HasCheckConstraint("CK_TaskSchedule_DayOfWeek", In("DayOfWeek", days)));

            // Cross-column rules: two columns that must agree. Lifecycle.MoveUser and the services keep
            // them in step today; these stop any other write path from splitting them.
            b.Entity<User>().ToTable(t =>
            {
                // IsActive mirrors "Status is Active" (Lifecycle.MoveUser).
                t.HasCheckConstraint("CK_User_ActiveMatchesStatus",
                    "([IsActive] = 1 AND [Status] = 'Active') OR ([IsActive] = 0 AND [Status] <> 'Active')");
                t.HasCheckConstraint("CK_User_InactiveHasDate",
                    "[Status] <> 'Inactive' OR [DeactivatedAt] IS NOT NULL");
                // The emailed set-password token and its expiry are issued and cleared together.
                t.HasCheckConstraint("CK_User_TokenPair",
                    "([PasswordTokenHash] IS NULL AND [PasswordTokenExpires] IS NULL) OR ([PasswordTokenHash] IS NOT NULL AND [PasswordTokenExpires] IS NOT NULL)");
            });
            // A decision needs a decider; a resubmission clears ReviewedBy only on the move back to Pending.
            b.Entity<ResourceRequest>().ToTable(t => t.HasCheckConstraint("CK_ResourceRequest_DecisionHasReviewer",
                "[Status] NOT IN ('Approved', 'Declined') OR [ReviewedBy] IS NOT NULL"));
            // TaskAssignmentService.MoveStatus records who finished it whenever it sets Done.
            b.Entity<ProjectTask>().ToTable(t => t.HasCheckConstraint("CK_Task_DoneHasCompletedBy",
                "[Status] <> 'Done' OR [CompletedBy] IS NOT NULL"));

            // Required text must hold something: [MaxLength] allows '' and the DTO [Required] checks stop
            // at the API, so these stop an empty or all-space value from any other write path.
            // (Event.Location and the other optional text columns are deliberately left blank-able.)
            b.Entity<Project>().ToTable(t => t.HasCheckConstraint("CK_Project_NameNotBlank", NotBlank("ProjectName")));
            b.Entity<ProjectTask>().ToTable(t => t.HasCheckConstraint("CK_Task_TitleNotBlank", NotBlank("Title")));
            b.Entity<Meeting>().ToTable(t => t.HasCheckConstraint("CK_Meeting_TitleNotBlank", NotBlank("Title")));
            b.Entity<Event>().ToTable(t => t.HasCheckConstraint("CK_Event_TitleNotBlank", NotBlank("Title")));
            b.Entity<FileUpload>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_FileUpload_FileNameNotBlank", NotBlank("FileName"));
                t.HasCheckConstraint("CK_FileUpload_FilePathNotBlank", NotBlank("FilePath"));
            });
            b.Entity<User>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_User_FirstNameNotBlank", NotBlank("FirstName"));
                t.HasCheckConstraint("CK_User_SurnameNotBlank", NotBlank("Surname"));
                // Basic shape only (something@something.something); full validation stays in the DTO's [EmailAddress].
                t.HasCheckConstraint("CK_User_EmailFormat", "[Email] LIKE '%_@_%._%'");
            });

            static string NotBlank(string column) => $"LEN(LTRIM(RTRIM([{column}]))) > 0";

            static string In(string column, string[] values) =>
                $"[{column}] IN ({string.Join(", ", values.Select(v => $"'{v}'"))})";
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//