using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackRecommender.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarkingPreferencesToUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarkingPreferences",
                table: "UserPreferences",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkingPreferences",
                table: "UserPreferences");
        }
    }
}
