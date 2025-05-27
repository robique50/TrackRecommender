using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackRecommender.Server.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureSridForGeometry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [Trails] 
                SET [Coordinates] = geometry::STGeomFromWKB([Coordinates].STAsBinary(), 4326)
                WHERE [Coordinates] IS NOT NULL AND ([Coordinates].STSrid IS NULL OR [Coordinates].STSrid != 4326);

                UPDATE [Regions] 
                SET [Boundary] = geometry::STGeomFromWKB([Boundary].STAsBinary(), 4326)
                WHERE [Boundary] IS NOT NULL AND ([Boundary].STSrid IS NULL OR [Boundary].STSrid != 4326);
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE [Trails] 
                ADD CONSTRAINT [CK_Trails_Coordinates_SRID] 
                CHECK ([Coordinates] IS NULL OR [Coordinates].STSrid = 4326);

                ALTER TABLE [Regions] 
                ADD CONSTRAINT [CK_Regions_Boundary_SRID] 
                CHECK ([Boundary] IS NULL OR [Boundary].STSrid = 4326);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Trails_Coordinates_SRID')
                    ALTER TABLE [Trails] DROP CONSTRAINT [CK_Trails_Coordinates_SRID];

                IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Regions_Boundary_SRID')
                    ALTER TABLE [Regions] DROP CONSTRAINT [CK_Regions_Boundary_SRID];
            ");

        }
    }
}