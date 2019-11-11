using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class FtpCredentialsForDistrictCSVs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FTPPassword",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FTPPath",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FTPUsername",
                table: "Districts",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FTPPassword",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "FTPPath",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "FTPUsername",
                table: "Districts");
        }
    }
}
