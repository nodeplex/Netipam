using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Netipam.Data;

#nullable disable

namespace Netipam.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260123140000_AddTopologyOverrides")]
    public partial class AddTopologyOverrides : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ManualUpstreamDeviceId",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTopologyRoot",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ManualUpstreamDeviceId",
                table: "Devices",
                column: "ManualUpstreamDeviceId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_ManualUpstreamDeviceId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ManualUpstreamDeviceId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IsTopologyRoot",
                table: "Devices");
        }
    }
}
