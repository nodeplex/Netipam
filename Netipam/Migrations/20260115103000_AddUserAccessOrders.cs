using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Netipam.Data;

#nullable disable

namespace Netipam.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260115103000_AddUserAccessOrders")]
    public partial class AddUserAccessOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAccessCategoryOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    AccessCategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccessCategoryOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccessCategoryOrders_AccessCategories_AccessCategoryId",
                        column: x => x.AccessCategoryId,
                        principalTable: "AccessCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAccessItemOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccessItemOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccessItemOrders_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessCategoryOrders_AccessCategoryId",
                table: "UserAccessCategoryOrders",
                column: "AccessCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessCategoryOrders_UserId",
                table: "UserAccessCategoryOrders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessCategoryOrders_UserId_AccessCategoryId",
                table: "UserAccessCategoryOrders",
                columns: new[] { "UserId", "AccessCategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessItemOrders_DeviceId",
                table: "UserAccessItemOrders",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessItemOrders_UserId",
                table: "UserAccessItemOrders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessItemOrders_UserId_DeviceId",
                table: "UserAccessItemOrders",
                columns: new[] { "UserId", "DeviceId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAccessCategoryOrders");

            migrationBuilder.DropTable(
                name: "UserAccessItemOrders");
        }
    }
}
