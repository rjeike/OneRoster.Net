using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class DistrictErrors : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalyzeError",
                table: "DataSyncHistories",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplyError",
                table: "DataSyncHistories",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoadError",
                table: "DataSyncHistories",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalyzeError",
                table: "DataSyncHistories");

            migrationBuilder.DropColumn(
                name: "ApplyError",
                table: "DataSyncHistories");

            migrationBuilder.DropColumn(
                name: "LoadError",
                table: "DataSyncHistories");
        }
    }
}
