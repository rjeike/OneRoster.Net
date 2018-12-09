using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class Tweaks03 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasePath",
                table: "DataSyncLines");

            migrationBuilder.DropColumn(
                name: "EmailsEachProcess",
                table: "DataSyncLines");

            migrationBuilder.DropColumn(
                name: "EmailsOnChanges",
                table: "DataSyncLines");

            migrationBuilder.DropColumn(
                name: "IsApprovalRequired",
                table: "DataSyncLines");

            migrationBuilder.AddColumn<string>(
                name: "BasePath",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailsEachProcess",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailsOnChanges",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApprovalRequired",
                table: "Districts",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasePath",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "EmailsEachProcess",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "EmailsOnChanges",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "IsApprovalRequired",
                table: "Districts");

            migrationBuilder.AddColumn<string>(
                name: "BasePath",
                table: "DataSyncLines",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailsEachProcess",
                table: "DataSyncLines",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailsOnChanges",
                table: "DataSyncLines",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApprovalRequired",
                table: "DataSyncLines",
                nullable: false,
                defaultValue: false);
        }
    }
}
