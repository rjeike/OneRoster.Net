using Microsoft.EntityFrameworkCore.Migrations;

namespace OneRosterSync.Net.Data.Migrations
{
    public partial class ClassLinkApiColsInDistrict : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<string>(
                name: "ApiError",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassLinkConsumerKey",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassLinkConsumerSecret",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassLinkOrgsApiUrl",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassLinkUsersApiUrl",
                table: "Districts",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApiValidated",
                table: "Districts",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCsvBased",
                table: "Districts",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UsersLastDateModified",
                table: "Districts",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiError",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "ClassLinkConsumerKey",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "ClassLinkConsumerSecret",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "ClassLinkOrgsApiUrl",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "ClassLinkUsersApiUrl",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "IsApiValidated",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "IsCsvBased",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "UsersLastDateModified",
                table: "Districts");

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
        }
    }
}
