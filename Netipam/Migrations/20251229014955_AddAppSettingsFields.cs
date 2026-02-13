using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    /// <inheritdoc />
    public partial class AddAppSettingsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "UnifiUpdaterEnabled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "SiteTitle",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "Netipam",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "DateFormat",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "MM-dd-yyyy HH:mm",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<bool>(
                name: "DarkMode",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "UnifiUpdaterEnabled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "SiteTitle",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldDefaultValue: "Netipam");

            migrationBuilder.AlterColumn<string>(
                name: "DateFormat",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldDefaultValue: "MM-dd-yyyy HH:mm");

            migrationBuilder.AlterColumn<bool>(
                name: "DarkMode",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true);
        }
    }
}
