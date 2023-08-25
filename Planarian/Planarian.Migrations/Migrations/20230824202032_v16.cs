using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v16 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "Entrances",
                type: "geography",
                nullable: true);
            
            migrationBuilder.Sql(@"
                UPDATE ""Entrances""
                SET ""Location"" = geography::STPointFromText('POINT(' + CAST(""Longitude"" AS VARCHAR(20)) + ' ' + CAST(""Latitude"" AS VARCHAR(20)) + ' ' + CAST(""ElevationFeet"" AS VARCHAR(20)) + ')', 4326);
            ");

            migrationBuilder.AlterColumn<Point>(
                name: "Location",
                table: "Entrances",
                type: "geography",
                nullable: false);
            
            migrationBuilder.Sql("CREATE SPATIAL INDEX IX_Entrances_Location ON Entrances(Location)");

            migrationBuilder.DropColumn(
                name: "ElevationFeet",
                table: "Entrances");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Entrances");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Entrances");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add columns back
            migrationBuilder.AddColumn<double>(
                name: "ElevationFeet",
                table: "Entrances",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Entrances",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Entrances",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            // Extract data from Location and fill the above columns
            migrationBuilder.Sql(@"
            UPDATE ""Entrances""
            SET 
                ""Longitude"" = Location.Long,
                ""Latitude"" = Location.Lat,
                ""ElevationFeet"" = Location.Z;
            ");

            // Now drop the Location column and its index
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Entrances_Location ON Entrances");
            migrationBuilder.DropColumn(
                name: "Location",
                table: "Entrances");
        }
    }
}
