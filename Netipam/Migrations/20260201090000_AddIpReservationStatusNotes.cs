using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netipam.Migrations
{
    public partial class AddIpReservationStatusNotes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "IpAssignments" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_IpAssignments" PRIMARY KEY AUTOINCREMENT,
                    "IpAddress" TEXT NOT NULL,
                    "SubnetId" INTEGER NOT NULL,
                    "DeviceId" INTEGER NULL,
                    "Status" INTEGER NOT NULL DEFAULT 1,
                    "Notes" TEXT NULL,
                    "UpdatedUtc" TEXT NOT NULL,
                    CONSTRAINT "FK_IpAssignments_Subnets_SubnetId" FOREIGN KEY ("SubnetId") REFERENCES "Subnets" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_IpAssignments_Devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "Devices" ("Id") ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_IpAssignments_SubnetId_IpAddress" ON "IpAssignments" ("SubnetId", "IpAddress");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_IpAssignments_SubnetId_IpAddress";
                DROP TABLE IF EXISTS "IpAssignments";
                """);
        }
    }
}
