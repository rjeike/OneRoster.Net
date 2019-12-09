using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class StopCurrentAction : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "StopCurrentAction",
                table: "Districts",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StopCurrentAction",
                table: "Districts");
        }
    }
}
