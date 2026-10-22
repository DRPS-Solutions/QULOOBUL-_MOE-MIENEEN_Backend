using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QULOOBUL__MOE_MIENEEN_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowedValueCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_User_Status",
                table: "User",
                sql: "[Status] IN ('Pending Activation', 'Active', 'Locked', 'Inactive', 'Anonymised')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaskStatusHistory_NewStatus",
                table: "Task_Status_History",
                sql: "[NewStatus] IN ('To Do', 'In Progress', 'Blocked', 'Done', 'Cancelled', 'Archived')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaskSchedule_DayOfWeek",
                table: "Task_Schedule",
                sql: "[DayOfWeek] IN ('Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Task_Priority",
                table: "Task",
                sql: "[Priority] IN ('Low', 'Normal', 'Medium', 'High')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Task_Status",
                table: "Task",
                sql: "[Status] IN ('To Do', 'In Progress', 'Blocked', 'Done', 'Cancelled', 'Archived')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ResourceRequest_Priority",
                table: "Resource_Request",
                sql: "[Priority] IN ('Low', 'Normal', 'Medium', 'High')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ResourceRequest_Status",
                table: "Resource_Request",
                sql: "[Status] IN ('Pending', 'Under Review', 'Escalated', 'Approved', 'Declined', 'Completed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RequestStatusHistory_NewStatus",
                table: "Request_Status_History",
                sql: "[NewStatus] IN ('Pending', 'Under Review', 'Escalated', 'Approved', 'Declined', 'Completed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Project_Status",
                table: "Project",
                sql: "[Status] IN ('Active', 'Closed', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_User_Status",
                table: "User");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaskStatusHistory_NewStatus",
                table: "Task_Status_History");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaskSchedule_DayOfWeek",
                table: "Task_Schedule");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Task_Priority",
                table: "Task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Task_Status",
                table: "Task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ResourceRequest_Priority",
                table: "Resource_Request");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ResourceRequest_Status",
                table: "Resource_Request");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RequestStatusHistory_NewStatus",
                table: "Request_Status_History");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Project_Status",
                table: "Project");
        }
    }
}
