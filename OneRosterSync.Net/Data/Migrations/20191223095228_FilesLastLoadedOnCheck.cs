using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class FilesLastLoadedOnCheck : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FTPFilesLastLoadedOn",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReadyForNightlySync",
                table: "Districts",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FTPFilesLastLoadedOn",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "ReadyForNightlySync",
                table: "Districts");
        }
    }
}
