using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class AddHostMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HostDeviceId",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHost",
                table: "ClientTypes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_HostDeviceId",
                table: "Devices",
                column: "HostDeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Devices_HostDeviceId",
                table: "Devices",
                column: "HostDeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Devices_HostDeviceId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_HostDeviceId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "HostDeviceId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IsHost",
                table: "ClientTypes");
        }
    }
}
