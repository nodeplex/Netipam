using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Netipam.Data;

#nullable disable

namespace Netipam.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260110120000_AddUserColumnPreferences")]
    public partial class AddUserColumnPreferences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserColumnPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    PageKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ColumnKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserColumnPreferences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserColumnPreferences_UserId_PageKey",
                table: "UserColumnPreferences",
                columns: new[] { "UserId", "PageKey" });

            migrationBuilder.CreateIndex(
                name: "IX_UserColumnPreferences_UserId_PageKey_ColumnKey",
                table: "UserColumnPreferences",
                columns: new[] { "UserId", "PageKey", "ColumnKey" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserColumnPreferences");
        }
    }
}
