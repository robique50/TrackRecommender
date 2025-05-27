using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace TrackRecommender.Server.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSpatialTypesAndTrailRegionRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeoJsonData",
                table: "Trails");

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "UserTrailRatings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<LineString>(
                name: "Coordinates",
                table: "Trails",
                type: "geometry",
                nullable: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdated",
                table: "Trails",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "OsmId",
                table: "Trails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Regions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Geometry>(
                name: "Boundary",
                table: "Regions",
                type: "geometry",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trails_OsmId",
                table: "Trails",
                column: "OsmId",
                unique: true,
                filter: "[OsmId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Regions_Name",
                table: "Regions",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trails_OsmId",
                table: "Trails");

            migrationBuilder.DropIndex(
                name: "IX_Regions_Name",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "Coordinates",
                table: "Trails");

            migrationBuilder.DropColumn(
                name: "LastUpdated",
                table: "Trails");

            migrationBuilder.DropColumn(
                name: "OsmId",
                table: "Trails");

            migrationBuilder.DropColumn(
                name: "Boundary",
                table: "Regions");

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "UserTrailRatings",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeoJsonData",
                table: "Trails",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Regions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
