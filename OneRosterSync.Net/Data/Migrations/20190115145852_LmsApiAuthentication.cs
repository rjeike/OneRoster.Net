using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class LmsApiAuthentication : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LmsApiAuthenticationJsonData",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LmsApiAuthenticatorType",
                table: "Districts",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LmsApiAuthenticationJsonData",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "LmsApiAuthenticatorType",
                table: "Districts");
        }
    }
}
