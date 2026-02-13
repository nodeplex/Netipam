using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Netipam.Data;

#nullable disable

namespace Netipam.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260114193000_AddUnifiUpdaterSyncSettings")]
    public partial class AddUnifiUpdaterSyncSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UnifiSyncHostname",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UnifiSyncIpAddress",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "UnifiSyncManufacturer",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UnifiSyncModel",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UnifiSyncName",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UnifiSyncOnlineStatus",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnifiSyncHostname",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UnifiSyncIpAddress",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UnifiSyncManufacturer",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UnifiSyncModel",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UnifiSyncName",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UnifiSyncOnlineStatus",
                table: "AppSettings");
        }
    }
}
