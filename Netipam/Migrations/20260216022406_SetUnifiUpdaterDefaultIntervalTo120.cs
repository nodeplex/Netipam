using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class SetUnifiUpdaterDefaultIntervalTo120 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "UnifiUpdaterIntervalSeconds",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 120,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "UnifiUpdaterIntervalSeconds",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 60,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 120);
        }
    }
}
