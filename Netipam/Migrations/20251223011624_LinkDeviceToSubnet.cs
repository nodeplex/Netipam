using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class LinkDeviceToSubnet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubnetId",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SubnetId",
                table: "Devices",
                column: "SubnetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Subnets_SubnetId",
                table: "Devices",
                column: "SubnetId",
                principalTable: "Subnets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Subnets_SubnetId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_SubnetId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SubnetId",
                table: "Devices");
        }
    }
}
