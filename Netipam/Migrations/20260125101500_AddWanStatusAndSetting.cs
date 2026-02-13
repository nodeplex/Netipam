using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Netipam.Data;

#nullable disable

namespace Netipam.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260125101500_AddWanStatusAndSetting")]
    public partial class AddWanStatusAndSetting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UiShowWanStatus",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "WanInterfaceStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GatewayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    GatewayMac = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    InterfaceName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsUp = table.Column<bool>(type: "INTEGER", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WanInterfaceStatuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WanInterfaceStatuses_GatewayMac",
                table: "WanInterfaceStatuses",
                column: "GatewayMac");

            migrationBuilder.CreateIndex(
                name: "IX_WanInterfaceStatuses_InterfaceName",
                table: "WanInterfaceStatuses",
                column: "InterfaceName");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WanInterfaceStatuses");

            migrationBuilder.DropColumn(
                name: "UiShowWanStatus",
                table: "AppSettings");
        }
    }
}
