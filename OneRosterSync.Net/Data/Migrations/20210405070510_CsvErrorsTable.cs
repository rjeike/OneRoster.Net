using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class CsvErrorsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DistrictCsvErrors",
                columns: table => new
                {
                    Version = table.Column<int>(nullable: false),
                    Created = table.Column<DateTime>(nullable: false),
                    Modified = table.Column<DateTime>(nullable: false),
                    ID = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DistrictId = table.Column<int>(nullable: false),
                    RawData = table.Column<string>(nullable: true),
                    Error = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistrictCsvErrors", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DistrictCsvErrors_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "DistrictId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistrictFilters_DistrictId",
                table: "DistrictFilters",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_DistrictCsvErrors_DistrictId",
                table: "DistrictCsvErrors",
                column: "DistrictId");

            migrationBuilder.AddForeignKey(
                name: "FK_DistrictFilters_Districts_DistrictId",
                table: "DistrictFilters",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "DistrictId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DistrictFilters_Districts_DistrictId",
                table: "DistrictFilters");

            migrationBuilder.DropTable(
                name: "DistrictCsvErrors");

            migrationBuilder.DropIndex(
                name: "IX_DistrictFilters_DistrictId",
                table: "DistrictFilters");
        }
    }
}
