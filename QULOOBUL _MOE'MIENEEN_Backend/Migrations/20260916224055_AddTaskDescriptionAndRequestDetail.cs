using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QULOOBUL__MOE_MIENEEN_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDescriptionAndRequestDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Task",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Detail",
                table: "Resource_Request",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Task");

            migrationBuilder.DropColumn(
                name: "Detail",
                table: "Resource_Request");
        }
    }
}
