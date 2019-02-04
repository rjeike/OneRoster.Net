using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class SyncEntityFlags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SyncAcademicSessions",
                table: "Districts",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SyncClasses",
                table: "Districts",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SyncCourses",
                table: "Districts",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SyncEnrollment",
                table: "Districts",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SyncOrgs",
                table: "Districts",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SyncUsers",
                table: "Districts",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SyncAcademicSessions",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "SyncClasses",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "SyncCourses",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "SyncEnrollment",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "SyncOrgs",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "SyncUsers",
                table: "Districts");
        }
    }
}
