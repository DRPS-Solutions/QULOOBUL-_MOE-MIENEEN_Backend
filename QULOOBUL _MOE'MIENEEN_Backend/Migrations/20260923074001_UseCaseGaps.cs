using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QULOOBUL__MOE_MIENEEN_Backend.Migrations
{
    /// <inheritdoc />
    public partial class UseCaseGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "File_Upload",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Notification_Preference",
                columns: table => new
                {
                    PreferenceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TypeId = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification_Preference", x => x.PreferenceId);
                    table.ForeignKey(
                        name: "FK_Notification_Preference_Notification_Type_TypeId",
                        column: x => x.TypeId,
                        principalTable: "Notification_Type",
                        principalColumn: "TypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notification_Preference_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Event_Location_EventDate",
                table: "Event",
                columns: new[] { "Location", "EventDate" },
                unique: true,
                filter: "[Location] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Preference_TypeId",
                table: "Notification_Preference",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Preference_UserId_TypeId",
                table: "Notification_Preference",
                columns: new[] { "UserId", "TypeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notification_Preference");

            migrationBuilder.DropIndex(
                name: "IX_Event_Location_EventDate",
                table: "Event");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "File_Upload");
        }
    }
}
