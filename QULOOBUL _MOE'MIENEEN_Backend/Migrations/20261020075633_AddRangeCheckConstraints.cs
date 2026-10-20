using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QULOOBUL__MOE_MIENEEN_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRangeCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_User_FailedLoginCount",
                table: "User",
                sql: "[FailedLoginCount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Task_PercentComplete",
                table: "Task",
                sql: "[PercentComplete] BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Report_PeriodOrder",
                table: "Report",
                sql: "[PeriodEnd] >= [PeriodStart]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_User_FailedLoginCount",
                table: "User");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Task_PercentComplete",
                table: "Task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Report_PeriodOrder",
                table: "Report");
        }
    }
}
