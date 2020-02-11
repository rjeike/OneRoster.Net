using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class CronExpression : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CronExpression",
                table: "Districts",
                maxLength: 50,
                nullable: false,
                defaultValue: "0 1 * * *");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CronExpression",
                table: "Districts");
        }
    }
}
