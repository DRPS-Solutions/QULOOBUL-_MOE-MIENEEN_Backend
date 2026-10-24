using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QULOOBUL__MOE_MIENEEN_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddNotBlankCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_User_EmailFormat",
                table: "User",
                sql: "[Email] LIKE '%_@_%._%'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_FirstNameNotBlank",
                table: "User",
                sql: "LEN(LTRIM(RTRIM([FirstName]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_SurnameNotBlank",
                table: "User",
                sql: "LEN(LTRIM(RTRIM([Surname]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Task_TitleNotBlank",
                table: "Task",
                sql: "LEN(LTRIM(RTRIM([Title]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Project_NameNotBlank",
                table: "Project",
                sql: "LEN(LTRIM(RTRIM([ProjectName]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Meeting_TitleNotBlank",
                table: "Meeting",
                sql: "LEN(LTRIM(RTRIM([Title]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileUpload_FileNameNotBlank",
                table: "File_Upload",
                sql: "LEN(LTRIM(RTRIM([FileName]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileUpload_FilePathNotBlank",
                table: "File_Upload",
                sql: "LEN(LTRIM(RTRIM([FilePath]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_TitleNotBlank",
                table: "Event",
                sql: "LEN(LTRIM(RTRIM([Title]))) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_User_EmailFormat",
                table: "User");

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_FirstNameNotBlank",
                table: "User");

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_SurnameNotBlank",
                table: "User");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Task_TitleNotBlank",
                table: "Task");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Project_NameNotBlank",
                table: "Project");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Meeting_TitleNotBlank",
                table: "Meeting");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FileUpload_FileNameNotBlank",
                table: "File_Upload");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FileUpload_FilePathNotBlank",
                table: "File_Upload");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_TitleNotBlank",
                table: "Event");
        }
    }
}
