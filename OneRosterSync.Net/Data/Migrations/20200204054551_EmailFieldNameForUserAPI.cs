using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class EmailFieldNameForUserAPI : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NCESDistrictID",
                table: "Districts",
                maxLength: 7,
                nullable: false,
                oldClrType: typeof(string));

            migrationBuilder.AddColumn<string>(
                name: "EmailFieldNameForUserAPI",
                table: "Districts",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailFieldNameForUserAPI",
                table: "Districts");

            migrationBuilder.AlterColumn<string>(
                name: "NCESDistrictID",
                table: "Districts",
                nullable: false,
                oldClrType: typeof(string),
                oldMaxLength: 7);
        }
    }
}
