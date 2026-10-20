using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QULOOBUL__MOE_MIENEEN_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCrossColumnCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The seeded System Automation account was inactive with no DeactivatedAt; backfill it
            // so CK_User_InactiveHasDate can be added to a database that already has it.
            migrationBuilder.Sql("UPDATE [User] SET DeactivatedAt = SYSUTCDATETIME() WHERE Status = 'Inactive' AND DeactivatedAt IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_ActiveMatchesStatus",
                table: "User",
                sql: "([IsActive] = 1 AND [Status] = 'Active') OR ([IsActive] = 0 AND [Status] <> 'Active')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_InactiveHasDate",
                table: "User",
                sql: "[Status] <> 'Inactive' OR [DeactivatedAt] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_TokenPair",
                table: "User",
                sql: "([PasswordTokenHash] IS NULL AND [PasswordTokenExpires] IS NULL) OR ([PasswordTokenHash] IS NOT NULL AND [PasswordTokenExpires] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Task_DoneHasCompletedBy",
                table: "Task",
                sql: "[Status] <> 'Done' OR [CompletedBy] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ResourceRequest_DecisionHasReviewer",
                table: "Resource_Request",
                sql: "[Status] NOT IN ('Approved', 'Declined') OR [ReviewedBy] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_User_ActiveMatchesStatus",
                table: "User");

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_InactiveHasDate",
                table: "User");

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_TokenPair",
                table: "User");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Task_DoneHasCompletedBy",
                table: "Task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ResourceRequest_DecisionHasReviewer",
                table: "Resource_Request");
        }
    }
}
