using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QULOOBUL__MOE_MIENEEN_Backend.Migrations
{
    /// <inheritdoc />
    public partial class SecurityAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Event_Attendee_Event_EventId",
                table: "Event_Attendee");

            migrationBuilder.DropForeignKey(
                name: "FK_File_Upload_Meeting_MeetingId",
                table: "File_Upload");

            migrationBuilder.DropForeignKey(
                name: "FK_File_Upload_Task_TaskId",
                table: "File_Upload");

            migrationBuilder.DropForeignKey(
                name: "FK_Meeting_Attendee_Meeting_MeetingId",
                table: "Meeting_Attendee");

            migrationBuilder.DropForeignKey(
                name: "FK_Project_Member_Project_ProjectId",
                table: "Project_Member");

            migrationBuilder.DropForeignKey(
                name: "FK_Task_Assignment_Task_TaskId",
                table: "Task_Assignment");

            migrationBuilder.DropForeignKey(
                name: "FK_Task_Comment_Task_TaskId",
                table: "Task_Comment");

            migrationBuilder.DropIndex(
                name: "IX_Task_ProjectId",
                table: "Task");

            migrationBuilder.DropIndex(
                name: "IX_Notification_UserId",
                table: "Notification");

            migrationBuilder.CreateIndex(
                name: "IX_Task_ProjectId_Status",
                table: "Task",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_UserId_IsRead",
                table: "Notification",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_Audit_Log_Timestamp",
                table: "Audit_Log",
                column: "Timestamp");

            migrationBuilder.AddForeignKey(
                name: "FK_Event_Attendee_Event_EventId",
                table: "Event_Attendee",
                column: "EventId",
                principalTable: "Event",
                principalColumn: "EventId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_File_Upload_Meeting_MeetingId",
                table: "File_Upload",
                column: "MeetingId",
                principalTable: "Meeting",
                principalColumn: "MeetingId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_File_Upload_Task_TaskId",
                table: "File_Upload",
                column: "TaskId",
                principalTable: "Task",
                principalColumn: "TaskId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Meeting_Attendee_Meeting_MeetingId",
                table: "Meeting_Attendee",
                column: "MeetingId",
                principalTable: "Meeting",
                principalColumn: "MeetingId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Project_Member_Project_ProjectId",
                table: "Project_Member",
                column: "ProjectId",
                principalTable: "Project",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Task_Assignment_Task_TaskId",
                table: "Task_Assignment",
                column: "TaskId",
                principalTable: "Task",
                principalColumn: "TaskId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Task_Comment_Task_TaskId",
                table: "Task_Comment",
                column: "TaskId",
                principalTable: "Task",
                principalColumn: "TaskId",
                onDelete: ReferentialAction.Cascade);

            // Staff directory without credential columns (AppDbContext maps UserDirectoryEntry to it).
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_UserDirectory AS
                SELECT u.UserId, u.FirstName + ' ' + u.Surname AS FullName, u.Email, r.RoleName, u.IsActive, u.Status
                FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleId = u.RoleId
                """);

            // Row-level security: a request only ever sees its own user's notifications, whatever the
            // query says. SessionContextInterceptor sets SESSION_CONTEXT('UserId') per connection; with
            // none set (login, background jobs, migrations) the predicate lets the row through.
            migrationBuilder.Sql("CREATE SCHEMA Security");
            migrationBuilder.Sql("""
                CREATE FUNCTION Security.fn_OwnRowsOnly(@UserId int)
                RETURNS TABLE WITH SCHEMABINDING AS
                RETURN SELECT 1 AS Allowed
                WHERE SESSION_CONTEXT(N'UserId') IS NULL OR @UserId = CAST(SESSION_CONTEXT(N'UserId') AS int)
                """);
            migrationBuilder.Sql("""
                CREATE SECURITY POLICY Security.NotificationOwnerPolicy
                ADD FILTER PREDICATE Security.fn_OwnRowsOnly(UserId) ON dbo.Notification
                WITH (STATE = ON)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SECURITY POLICY Security.NotificationOwnerPolicy");
            migrationBuilder.Sql("DROP FUNCTION Security.fn_OwnRowsOnly");
            migrationBuilder.Sql("DROP SCHEMA Security");
            migrationBuilder.Sql("DROP VIEW dbo.vw_UserDirectory");

            migrationBuilder.DropForeignKey(
                name: "FK_Event_Attendee_Event_EventId",
                table: "Event_Attendee");

            migrationBuilder.DropForeignKey(
                name: "FK_File_Upload_Meeting_MeetingId",
                table: "File_Upload");

            migrationBuilder.DropForeignKey(
                name: "FK_File_Upload_Task_TaskId",
                table: "File_Upload");

            migrationBuilder.DropForeignKey(
                name: "FK_Meeting_Attendee_Meeting_MeetingId",
                table: "Meeting_Attendee");

            migrationBuilder.DropForeignKey(
                name: "FK_Project_Member_Project_ProjectId",
                table: "Project_Member");

            migrationBuilder.DropForeignKey(
                name: "FK_Task_Assignment_Task_TaskId",
                table: "Task_Assignment");

            migrationBuilder.DropForeignKey(
                name: "FK_Task_Comment_Task_TaskId",
                table: "Task_Comment");

            migrationBuilder.DropIndex(
                name: "IX_Task_ProjectId_Status",
                table: "Task");

            migrationBuilder.DropIndex(
                name: "IX_Notification_UserId_IsRead",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "IX_Audit_Log_Timestamp",
                table: "Audit_Log");

            migrationBuilder.CreateIndex(
                name: "IX_Task_ProjectId",
                table: "Task",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_UserId",
                table: "Notification",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Event_Attendee_Event_EventId",
                table: "Event_Attendee",
                column: "EventId",
                principalTable: "Event",
                principalColumn: "EventId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_File_Upload_Meeting_MeetingId",
                table: "File_Upload",
                column: "MeetingId",
                principalTable: "Meeting",
                principalColumn: "MeetingId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_File_Upload_Task_TaskId",
                table: "File_Upload",
                column: "TaskId",
                principalTable: "Task",
                principalColumn: "TaskId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Meeting_Attendee_Meeting_MeetingId",
                table: "Meeting_Attendee",
                column: "MeetingId",
                principalTable: "Meeting",
                principalColumn: "MeetingId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Project_Member_Project_ProjectId",
                table: "Project_Member",
                column: "ProjectId",
                principalTable: "Project",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Task_Assignment_Task_TaskId",
                table: "Task_Assignment",
                column: "TaskId",
                principalTable: "Task",
                principalColumn: "TaskId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Task_Comment_Task_TaskId",
                table: "Task_Comment",
                column: "TaskId",
                principalTable: "Task",
                principalColumn: "TaskId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
