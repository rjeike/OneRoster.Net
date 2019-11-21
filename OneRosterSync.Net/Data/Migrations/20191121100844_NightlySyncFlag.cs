using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class NightlySyncFlag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FTPUsername",
                table: "Districts",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FTPPath",
                table: "Districts",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FTPPassword",
                table: "Districts",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedOn",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NightlySyncEnabled",
                table: "Districts",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSyncedOn",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "NightlySyncEnabled",
                table: "Districts");

            migrationBuilder.AlterColumn<string>(
                name: "FTPUsername",
                table: "Districts",
                nullable: true,
                oldClrType: typeof(string));

            migrationBuilder.AlterColumn<string>(
                name: "FTPPath",
                table: "Districts",
                nullable: true,
                oldClrType: typeof(string));

            migrationBuilder.AlterColumn<string>(
                name: "FTPPassword",
                table: "Districts",
                nullable: true,
                oldClrType: typeof(string));
        }
    }
}
