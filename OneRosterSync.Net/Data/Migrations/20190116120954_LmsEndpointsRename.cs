using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class LmsEndpointsRename : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserEndPoint",
                table: "Districts",
                newName: "LmsUserEndPoint");

            migrationBuilder.RenameColumn(
                name: "OrgEndPoint",
                table: "Districts",
                newName: "LmsOrgEndPoint");

            migrationBuilder.RenameColumn(
                name: "EnrollmentEndPoint",
                table: "Districts",
                newName: "LmsEnrollmentEndPoint");

            migrationBuilder.RenameColumn(
                name: "CourseEndPoint",
                table: "Districts",
                newName: "LmsCourseEndPoint");

            migrationBuilder.RenameColumn(
                name: "ClassEndPoint",
                table: "Districts",
                newName: "LmsClassEndPoint");

            migrationBuilder.RenameColumn(
                name: "AcademicSessionEndPoint",
                table: "Districts",
                newName: "LmsAcademicSessionEndPoint");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LmsUserEndPoint",
                table: "Districts",
                newName: "UserEndPoint");

            migrationBuilder.RenameColumn(
                name: "LmsOrgEndPoint",
                table: "Districts",
                newName: "OrgEndPoint");

            migrationBuilder.RenameColumn(
                name: "LmsEnrollmentEndPoint",
                table: "Districts",
                newName: "EnrollmentEndPoint");

            migrationBuilder.RenameColumn(
                name: "LmsCourseEndPoint",
                table: "Districts",
                newName: "CourseEndPoint");

            migrationBuilder.RenameColumn(
                name: "LmsClassEndPoint",
                table: "Districts",
                newName: "ClassEndPoint");

            migrationBuilder.RenameColumn(
                name: "LmsAcademicSessionEndPoint",
                table: "Districts",
                newName: "AcademicSessionEndPoint");
        }
    }
}
