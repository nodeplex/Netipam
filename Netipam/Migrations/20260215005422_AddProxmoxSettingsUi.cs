using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class AddProxmoxSettingsUi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProxmoxApiTokenId",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProxmoxApiTokenSecretProtected",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProxmoxBaseUrl",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProxmoxEnabled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProxmoxHostDeviceMac",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProxmoxIntervalSeconds",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 300);

            migrationBuilder.AddColumn<bool>(
                name: "ProxmoxUpdateExistingHostAssignments",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProxmoxApiTokenId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ProxmoxApiTokenSecretProtected",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ProxmoxBaseUrl",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ProxmoxEnabled",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ProxmoxHostDeviceMac",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ProxmoxIntervalSeconds",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ProxmoxUpdateExistingHostAssignments",
                table: "AppSettings");
        }
    }
}
