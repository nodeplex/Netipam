using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class AddClientOfflineAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientOfflineAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    NameAtTime = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IpAtTime = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    WentOfflineAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CameOnlineAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsAcknowledged = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientOfflineAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientOfflineAlerts_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientOfflineAlerts_DeviceId",
                table: "ClientOfflineAlerts",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOfflineAlerts_IsAcknowledged",
                table: "ClientOfflineAlerts",
                column: "IsAcknowledged");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOfflineAlerts_WentOfflineAtUtc",
                table: "ClientOfflineAlerts",
                column: "WentOfflineAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientOfflineAlerts");
        }
    }
}
