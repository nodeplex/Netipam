using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class AddUpstreamFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentDeviceId",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpstreamConnection",
                table: "Devices",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpstreamDeviceMac",
                table: "Devices",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpstreamDeviceName",
                table: "Devices",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ParentDeviceId",
                table: "Devices",
                column: "ParentDeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Devices_ParentDeviceId",
                table: "Devices",
                column: "ParentDeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Devices_ParentDeviceId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_ParentDeviceId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ParentDeviceId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "UpstreamConnection",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "UpstreamDeviceMac",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "UpstreamDeviceName",
                table: "Devices");
        }
    }
}
