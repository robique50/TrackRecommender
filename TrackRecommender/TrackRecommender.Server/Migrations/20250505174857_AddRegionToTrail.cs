using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackRecommender.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionToTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Trails",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Region",
                table: "Trails");
        }
    }
}
