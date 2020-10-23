using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class CleverRosteringApiCols : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CleverOAuthToken",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RosteringApiSource",
                table: "Districts",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CleverOAuthToken",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "RosteringApiSource",
                table: "Districts");
        }
    }
}
