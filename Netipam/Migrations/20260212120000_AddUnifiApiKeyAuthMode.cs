using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Netipam.Data;

#nullable disable

namespace Netipam.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260212120000_AddUnifiApiKeyAuthMode")]
    public partial class AddUnifiApiKeyAuthMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnifiApiKeyProtected",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnifiAuthMode",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Session");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnifiApiKeyProtected",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UnifiAuthMode",
                table: "AppSettings");
        }
    }
}
