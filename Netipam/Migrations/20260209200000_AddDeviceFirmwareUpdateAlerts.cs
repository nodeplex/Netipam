using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Netipam.Data;

#nullable disable

namespace Netipam.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260209200000_AddDeviceFirmwareUpdateAlerts")]
    public partial class AddDeviceFirmwareUpdateAlerts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceFirmwareUpdateAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    NameAtTime = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    MacAtTime = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ModelAtTime = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CurrentVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    TargetVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceFirmwareUpdateAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceFirmwareUpdateAlerts_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFirmwareUpdateAlerts_DeviceId",
                table: "DeviceFirmwareUpdateAlerts",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFirmwareUpdateAlerts_DetectedAtUtc",
                table: "DeviceFirmwareUpdateAlerts",
                column: "DetectedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFirmwareUpdateAlerts_IsAcknowledged",
                table: "DeviceFirmwareUpdateAlerts",
                column: "IsAcknowledged");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFirmwareUpdateAlerts_ResolvedAtUtc",
                table: "DeviceFirmwareUpdateAlerts",
                column: "ResolvedAtUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceFirmwareUpdateAlerts");
        }
    }
}
