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
            b.Entity<ProjectTask>().HasIndex(x => new { x.ProjectId, x.Status });   // the task board
            b.Entity<Notification>().HasIndex(x => new { x.UserId, x.IsRead });     // the unread badge
            b.Entity<AuditLog>().HasIndex(x => x.Timestamp);                        // time-ranged audit queries

            // Staff directory reads a view that has no credential columns (defence in depth); created in
            // the SecurityAndIndexes migration alongside the Notification row-level security policy.
            b.Entity<UserDirectoryEntry>().HasNoKey().ToView("vw_UserDirectory");

            // Junction tables reject duplicate pairs (architecture doc, "Junction tables carry
            // their own surrogate key ... with a unique constraint on the pair").
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
            b.Entity<FileUpload>().ToTable(t => t.HasCheckConstraint(
                "CK_FileUpload_OneContext",
                "((CASE WHEN [TaskId] IS NULL THEN 0 ELSE 1 END) + " +
                "(CASE WHEN [MeetingId] IS NULL THEN 0 ELSE 1 END) + " +
                "(CASE WHEN [PeriodMonth] IS NULL THEN 0 ELSE 1 END)) <= 1"));
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//