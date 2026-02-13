using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceHostname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hostname",
                table: "Devices",
                type: "TEXT",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hostname",
                table: "Devices");
        }
    }
}
