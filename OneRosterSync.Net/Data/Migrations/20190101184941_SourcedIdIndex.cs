using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class SourcedIdIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataSyncLines_DistrictId",
                table: "DataSyncLines");

            migrationBuilder.AlterColumn<string>(
                name: "Table",
                table: "DataSyncLines",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SourceId",
                table: "DataSyncLines",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataSyncLines_DistrictId_Table_SourceId",
                table: "DataSyncLines",
                columns: new[] { "DistrictId", "Table", "SourceId" },
                unique: true,
                filter: "[Table] IS NOT NULL AND [SourceId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataSyncLines_DistrictId_Table_SourceId",
                table: "DataSyncLines");

            migrationBuilder.AlterColumn<string>(
                name: "Table",
                table: "DataSyncLines",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SourceId",
                table: "DataSyncLines",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataSyncLines_DistrictId",
                table: "DataSyncLines",
                column: "DistrictId");
        }
    }
}
