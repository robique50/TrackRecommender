using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackRecommender.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Region",
                table: "Trails");

            migrationBuilder.AddColumn<int>(
                name: "RegionId",
                table: "Trails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinLat = table.Column<double>(type: "float", nullable: false),
                    MaxLat = table.Column<double>(type: "float", nullable: false),
                    MinLon = table.Column<double>(type: "float", nullable: false),
                    MaxLon = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trails_RegionId",
                table: "Trails",
                column: "RegionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Trails_Regions_RegionId",
                table: "Trails",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trails_Regions_RegionId",
                table: "Trails");

            migrationBuilder.DropTable(
                name: "Regions");

            migrationBuilder.DropIndex(
                name: "IX_Trails_RegionId",
                table: "Trails");

            migrationBuilder.DropColumn(
                name: "RegionId",
                table: "Trails");

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Trails",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
