using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Netipam.Data;

#nullable disable

namespace Netipam.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260117150000_AddDeviceStatusHistory")]
    public partial class AddDeviceStatusHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastStatusRollupAtUtc",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceStatusDaily",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    OnlineSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceStatusDaily", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceStatusDaily_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceStatusEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceStatusEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceStatusEvents_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceStatusDaily_Date",
                table: "DeviceStatusDaily",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceStatusDaily_DeviceId_Date",
                table: "DeviceStatusDaily",
                columns: new[] { "DeviceId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceStatusEvents_ChangedAtUtc",
                table: "DeviceStatusEvents",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceStatusEvents_DeviceId",
                table: "DeviceStatusEvents",
                column: "DeviceId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceStatusDaily");

            migrationBuilder.DropTable(
                name: "DeviceStatusEvents");

            migrationBuilder.DropColumn(
                name: "LastStatusRollupAtUtc",
                table: "Devices");
        }
    }
}
