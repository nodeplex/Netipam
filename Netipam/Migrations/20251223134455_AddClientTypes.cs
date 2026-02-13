using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class AddClientTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientTypeId",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsDevice = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ClientTypeId",
                table: "Devices",
                column: "ClientTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTypes_Name",
                table: "ClientTypes",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_ClientTypes_ClientTypeId",
                table: "Devices",
                column: "ClientTypeId",
                principalTable: "ClientTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_ClientTypes_ClientTypeId",
                table: "Devices");

            migrationBuilder.DropTable(
                name: "ClientTypes");

            migrationBuilder.DropIndex(
                name: "IX_Devices_ClientTypeId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ClientTypeId",
                table: "Devices");
        }
    }
}
