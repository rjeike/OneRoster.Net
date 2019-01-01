using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class DistrictUpdate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Completed",
                table: "DataSyncHistories",
                newName: "LoadStarted");

            migrationBuilder.AddColumn<string>(
                name: "LmsApiEndpoint",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnalyzeCompleted",
                table: "DataSyncHistories",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnalyzeStarted",
                table: "DataSyncHistories",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApplyCompleted",
                table: "DataSyncHistories",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApplyStarted",
                table: "DataSyncHistories",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LoadCompleted",
                table: "DataSyncHistories",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LmsApiEndpoint",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "AnalyzeCompleted",
                table: "DataSyncHistories");

            migrationBuilder.DropColumn(
                name: "AnalyzeStarted",
                table: "DataSyncHistories");

            migrationBuilder.DropColumn(
                name: "ApplyCompleted",
                table: "DataSyncHistories");

            migrationBuilder.DropColumn(
                name: "ApplyStarted",
                table: "DataSyncHistories");

            migrationBuilder.DropColumn(
                name: "LoadCompleted",
                table: "DataSyncHistories");

            migrationBuilder.RenameColumn(
                name: "LoadStarted",
                table: "DataSyncHistories",
                newName: "Completed");
        }
    }
}
