using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Netipam.Data;

#nullable disable

namespace Netipam.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260108204500_AddDiscoveryAlertsAndIgnoredMacs")]
    public partial class AddDiscoveryAlertsAndIgnoredMacs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientDiscoveryAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Mac = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ConnectionType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UpstreamDeviceName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    UpstreamDeviceMac = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UpstreamConnection = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ConnectionDetail = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientDiscoveryAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IgnoredDiscoveryMacs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Mac = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IgnoredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredDiscoveryMacs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientDiscoveryAlerts_DetectedAtUtc",
                table: "ClientDiscoveryAlerts",
                column: "DetectedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDiscoveryAlerts_IsAcknowledged",
                table: "ClientDiscoveryAlerts",
                column: "IsAcknowledged");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDiscoveryAlerts_Mac",
                table: "ClientDiscoveryAlerts",
                column: "Mac");

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredDiscoveryMacs_Mac",
                table: "IgnoredDiscoveryMacs",
                column: "Mac",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientDiscoveryAlerts");

            migrationBuilder.DropTable(
                name: "IgnoredDiscoveryMacs");
        }
    }
}
