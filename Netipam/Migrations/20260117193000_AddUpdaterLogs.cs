using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Netipam.Data;

#nullable disable

namespace Netipam.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260117193000_AddUpdaterLogs")]
    public partial class AddUpdaterLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UpdaterRunLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: true),
                    ChangedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdaterRunLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UpdaterChangeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: true),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdaterChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UpdaterChangeLogs_UpdaterRunLogs_RunId",
                        column: x => x.RunId,
                        principalTable: "UpdaterRunLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UpdaterRunLogs_StartedAtUtc",
                table: "UpdaterRunLogs",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UpdaterChangeLogs_DeviceId",
                table: "UpdaterChangeLogs",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_UpdaterChangeLogs_RunId",
                table: "UpdaterChangeLogs",
                column: "RunId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpdaterChangeLogs");

            migrationBuilder.DropTable(
                name: "UpdaterRunLogs");
        }
    }
}
