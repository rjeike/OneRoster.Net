using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class SourcedIdSpellingFix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataSyncLines_DistrictId_Table_SourceId",
                table: "DataSyncLines");

            migrationBuilder.RenameColumn(
                name: "SourceId",
                table: "DataSyncLines",
                newName: "SourcedId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSyncLines_DistrictId_Table_SourcedId",
                table: "DataSyncLines",
                columns: new[] { "DistrictId", "Table", "SourcedId" },
                unique: true,
                filter: "[Table] IS NOT NULL AND [SourcedId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataSyncLines_DistrictId_Table_SourcedId",
                table: "DataSyncLines");

            migrationBuilder.RenameColumn(
                name: "SourcedId",
                table: "DataSyncLines",
                newName: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSyncLines_DistrictId_Table_SourceId",
                table: "DataSyncLines",
                columns: new[] { "DistrictId", "Table", "SourceId" },
                unique: true,
                filter: "[Table] IS NOT NULL AND [SourceId] IS NOT NULL");
        }
    }
}
