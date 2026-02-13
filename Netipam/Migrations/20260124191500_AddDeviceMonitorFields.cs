using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Netipam.Data;

#nullable disable

namespace Netipam.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260124191500_AddDeviceMonitorFields")]
    public partial class AddDeviceMonitorFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MonitorMode",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MonitorPort",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MonitorUseHttps",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MonitorHttpPath",
                table: "Devices",
                type: "TEXT",
                maxLength: 512,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonitorMode",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MonitorPort",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MonitorUseHttps",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MonitorHttpPath",
                table: "Devices");
        }
    }
}
