using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Netipam.Data;

#nullable disable

namespace Netipam.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260117120000_AddRackSize")]
    public partial class AddRackSize : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RackUnits",
                table: "Racks",
                type: "INTEGER",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RackUnits",
                table: "Racks");
        }
    }
}
