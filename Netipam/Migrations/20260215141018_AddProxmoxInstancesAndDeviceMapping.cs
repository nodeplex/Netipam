using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class AddProxmoxInstancesAndDeviceMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProxmoxHost",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProxmoxInstanceId",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProxmoxNodeIdentifier",
                table: "Devices",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProxmoxInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ApiTokenId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ApiTokenSecretProtected = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    IntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 300),
                    UpdateExistingHostAssignments = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProxmoxInstances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ProxmoxInstanceId",
                table: "Devices",
                column: "ProxmoxInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ProxmoxNodeIdentifier",
                table: "Devices",
                column: "ProxmoxNodeIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_ProxmoxInstances_Name",
                table: "ProxmoxInstances",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_ProxmoxInstances_ProxmoxInstanceId",
                table: "Devices",
                column: "ProxmoxInstanceId",
                principalTable: "ProxmoxInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_ProxmoxInstances_ProxmoxInstanceId",
                table: "Devices");

            migrationBuilder.DropTable(
                name: "ProxmoxInstances");

            migrationBuilder.DropIndex(
                name: "IX_Devices_ProxmoxInstanceId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_ProxmoxNodeIdentifier",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IsProxmoxHost",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ProxmoxInstanceId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ProxmoxNodeIdentifier",
                table: "Devices");
        }
    }
}
