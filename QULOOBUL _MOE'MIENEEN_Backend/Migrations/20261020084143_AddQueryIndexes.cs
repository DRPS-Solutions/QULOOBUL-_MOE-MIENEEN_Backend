using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QULOOBUL__MOE_MIENEEN_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Audit_Log_UserId",
                table: "Audit_Log");

            migrationBuilder.CreateIndex(
                name: "IX_User_PasswordTokenHash",
                table: "User",
                column: "PasswordTokenHash",
                unique: true,
                filter: "[PasswordTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Task_Status_History_NewStatus_TimeCreated",
                table: "Task_Status_History",
                columns: new[] { "NewStatus", "TimeCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_Task_Deadline",
                table: "Task",
                column: "Deadline");

            migrationBuilder.CreateIndex(
                name: "IX_Task_Status_Deadline",
                table: "Task",
                columns: new[] { "Status", "Deadline" });

            migrationBuilder.CreateIndex(
                name: "IX_Resource_Request_Status_DateSubmitted",
                table: "Resource_Request",
                columns: new[] { "Status", "DateSubmitted" });

            migrationBuilder.CreateIndex(
                name: "IX_Meeting_MeetingDate",
                table: "Meeting",
                column: "MeetingDate");

            migrationBuilder.CreateIndex(
                name: "IX_Event_EventDate",
                table: "Event",
                column: "EventDate");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_Log_UserId_Timestamp",
                table: "Audit_Log",
                columns: new[] { "UserId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_PasswordTokenHash",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_Task_Status_History_NewStatus_TimeCreated",
                table: "Task_Status_History");

            migrationBuilder.DropIndex(
                name: "IX_Task_Deadline",
                table: "Task");

            migrationBuilder.DropIndex(
                name: "IX_Task_Status_Deadline",
                table: "Task");

            migrationBuilder.DropIndex(
                name: "IX_Resource_Request_Status_DateSubmitted",
                table: "Resource_Request");

            migrationBuilder.DropIndex(
                name: "IX_Meeting_MeetingDate",
                table: "Meeting");

            migrationBuilder.DropIndex(
                name: "IX_Event_EventDate",
                table: "Event");

            migrationBuilder.DropIndex(
                name: "IX_Audit_Log_UserId_Timestamp",
                table: "Audit_Log");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_Log_UserId",
                table: "Audit_Log",
                column: "UserId");
        }
    }
}
