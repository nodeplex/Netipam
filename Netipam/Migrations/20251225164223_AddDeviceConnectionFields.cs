using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceConnectionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConnectionDetail",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConnectionType",
                table: "Devices",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionDetail",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ConnectionType",
                table: "Devices");
        }
    }
}
