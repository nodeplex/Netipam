using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class AddAppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UnifiUpdaterIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 60),
                    UnifiUpdateConnectionFieldsWhenOnline = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ShowLastSeenTooltips = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    UiAutoRefreshSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");
        }
    }
}
