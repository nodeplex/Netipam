using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreSettingsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DateFormat",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SiteTitle",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UnifiBaseUrl",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnifiPasswordProtected",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnifiSiteName",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UnifiUpdaterEnabled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UnifiUsername",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateFormat",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SiteTitle",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UnifiBaseUrl",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UnifiPasswordProtected",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UnifiSiteName",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UnifiUpdaterEnabled",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UnifiUsername",
                table: "AppSettings");
        }
    }
}
