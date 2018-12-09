using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class Tweaks02 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "IncludeInSync",
                table: "DataSyncLines",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsApprovalRequired",
                table: "DataSyncLines",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Table",
                table: "DataSyncHistoryDetails",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "IncludeInSync",
                table: "DataSyncLines");

            migrationBuilder.DropColumn(
                name: "IsApprovalRequired",
                table: "DataSyncLines");

            migrationBuilder.DropColumn(
                name: "Table",
                table: "DataSyncHistoryDetails");
        }
    }
}
