using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class AddSubnetVlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VlanId",
                table: "Subnets",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VlanId",
                table: "Subnets");
        }
    }
}
