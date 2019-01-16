using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class RenameLmsApiEndpoint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LmsApiEndpoint",
                table: "Districts",
                newName: "LmsApiBaseUrl");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LmsApiBaseUrl",
                table: "Districts",
                newName: "LmsApiEndpoint");
        }
    }
}
