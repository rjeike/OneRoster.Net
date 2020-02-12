using Microsoft.EntityFrameworkCore.Migrations;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class PasswordFieldNameForUserAPI : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordFieldNameForUserAPI",
                table: "Districts",
                nullable: false,
                defaultValue: nameof(CsvUser.password));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordFieldNameForUserAPI",
                table: "Districts");
        }
    }
}
