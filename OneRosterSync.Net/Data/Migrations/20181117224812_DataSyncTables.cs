using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class DataSyncTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Districts",
                columns: table => new
                {
                    DistrictId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(nullable: true),
                    DailyProcessingTime = table.Column<TimeSpan>(nullable: true),
                    NextProcessingTime = table.Column<DateTime>(nullable: true),
                    ProcessingStatus = table.Column<int>(nullable: false),
                    Version = table.Column<int>(nullable: false),
                    Created = table.Column<DateTime>(nullable: false),
                    Modified = table.Column<DateTime>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.DistrictId);
                });

            migrationBuilder.CreateTable(
                name: "DataSyncHistories",
                columns: table => new
                {
                    DataSyncHistoryId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DistrictId = table.Column<int>(nullable: false),
                    Started = table.Column<DateTime>(nullable: false),
                    Completed = table.Column<DateTime>(nullable: false),
                    NumRows = table.Column<int>(nullable: false),
                    NumAdded = table.Column<int>(nullable: false),
                    NumModified = table.Column<int>(nullable: false),
                    NumDeleted = table.Column<int>(nullable: false),
                    Version = table.Column<int>(nullable: false),
                    Created = table.Column<DateTime>(nullable: false),
                    Modified = table.Column<DateTime>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSyncHistories", x => x.DataSyncHistoryId);
                    table.ForeignKey(
                        name: "FK_DataSyncHistories_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "DistrictId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DataSyncLines",
                columns: table => new
                {
                    DataSyncLineId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DistrictId = table.Column<int>(nullable: false),
                    Table = table.Column<string>(nullable: true),
                    SourceId = table.Column<string>(nullable: true),
                    TargetId = table.Column<string>(nullable: true),
                    Data = table.Column<string>(nullable: true),
                    RawData = table.Column<string>(nullable: true),
                    LoadStatus = table.Column<int>(nullable: false),
                    SyncStatus = table.Column<int>(nullable: false),
                    Error = table.Column<string>(nullable: true),
                    LastSeen = table.Column<DateTime>(nullable: false),
                    Version = table.Column<int>(nullable: false),
                    Created = table.Column<DateTime>(nullable: false),
                    Modified = table.Column<DateTime>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSyncLines", x => x.DataSyncLineId);
                    table.ForeignKey(
                        name: "FK_DataSyncLines_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "DistrictId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DataSyncHistoryDetails",
                columns: table => new
                {
                    DataSyncHistoryDetailId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DataSyncHistoryId = table.Column<int>(nullable: false),
                    DataSyncLineId = table.Column<int>(nullable: false),
                    DataOrig = table.Column<string>(nullable: true),
                    DataNew = table.Column<string>(nullable: true),
                    Version = table.Column<int>(nullable: false),
                    Created = table.Column<DateTime>(nullable: false),
                    Modified = table.Column<DateTime>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSyncHistoryDetails", x => x.DataSyncHistoryDetailId);
                    table.ForeignKey(
                        name: "FK_DataSyncHistoryDetails_DataSyncHistories_DataSyncHistoryId",
                        column: x => x.DataSyncHistoryId,
                        principalTable: "DataSyncHistories",
                        principalColumn: "DataSyncHistoryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DataSyncHistoryDetails_DataSyncLines_DataSyncLineId",
                        column: x => x.DataSyncLineId,
                        principalTable: "DataSyncLines",
                        principalColumn: "DataSyncLineId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataSyncHistories_DistrictId",
                table: "DataSyncHistories",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSyncHistoryDetails_DataSyncHistoryId",
                table: "DataSyncHistoryDetails",
                column: "DataSyncHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSyncHistoryDetails_DataSyncLineId",
                table: "DataSyncHistoryDetails",
                column: "DataSyncLineId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSyncLines_DistrictId",
                table: "DataSyncLines",
                column: "DistrictId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataSyncHistoryDetails");

            migrationBuilder.DropTable(
                name: "DataSyncHistories");

            migrationBuilder.DropTable(
                name: "DataSyncLines");

            migrationBuilder.DropTable(
                name: "Districts");
        }
    }
}
