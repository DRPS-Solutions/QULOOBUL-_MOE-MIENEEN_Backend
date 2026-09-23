using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QULOOBUL__MOE_MIENEEN_Backend.Migrations
{
    /// <inheritdoc />
    public partial class UserLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                table: "User",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordTokenExpires",
                table: "User",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordTokenHash",
                table: "User",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "User",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Active");

            // Existing deactivated rows (incl. the System account) become SM-03 Inactive. Their real
            // deactivation date is unknown, so the POPIA retention clock starts now.
            migrationBuilder.Sql(
                "UPDATE [User] SET [Status] = 'Inactive', [DeactivatedAt] = SYSUTCDATETIME() WHERE [IsActive] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "User");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "User");

            migrationBuilder.DropColumn(
                name: "PasswordTokenExpires",
                table: "User");

            migrationBuilder.DropColumn(
                name: "PasswordTokenHash",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "User");
        }
    }
}
