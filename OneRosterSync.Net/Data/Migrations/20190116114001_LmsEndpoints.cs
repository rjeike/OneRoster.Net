using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class LmsEndpoints : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcademicSessionEndPoint",
                table: "Districts",
                nullable: false,
                defaultValue: "academicSession");

            migrationBuilder.AddColumn<string>(
                name: "ClassEndPoint",
                table: "Districts",
                nullable: false,
                defaultValue: "class");

            migrationBuilder.AddColumn<string>(
                name: "CourseEndPoint",
                table: "Districts",
                nullable: false,
                defaultValue: "course");

            migrationBuilder.AddColumn<string>(
                name: "EnrollmentEndPoint",
                table: "Districts",
                nullable: false,
                defaultValue: "enrollment");

            migrationBuilder.AddColumn<string>(
                name: "OrgEndPoint",
                table: "Districts",
                nullable: false,
                defaultValue: "org");

            migrationBuilder.AddColumn<string>(
                name: "UserEndPoint",
                table: "Districts",
                nullable: false,
                defaultValue: "user");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcademicSessionEndPoint",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "ClassEndPoint",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "CourseEndPoint",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "EnrollmentEndPoint",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "OrgEndPoint",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "UserEndPoint",
                table: "Districts");
        }
    }
}
