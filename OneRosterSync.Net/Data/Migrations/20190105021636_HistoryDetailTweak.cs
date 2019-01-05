using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class HistoryDetailTweak : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataOrig",
                table: "DataSyncHistoryDetails");

            migrationBuilder.AddColumn<int>(
                name: "SyncStatus",
                table: "DataSyncHistoryDetails",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "DataSyncHistoryDetails");

            migrationBuilder.AddColumn<string>(
                name: "DataOrig",
                table: "DataSyncHistoryDetails",
                nullable: true);
        }
    }
}
