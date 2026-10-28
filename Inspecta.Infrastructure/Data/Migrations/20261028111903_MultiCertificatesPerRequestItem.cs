using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MultiCertificatesPerRequestItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Certificates_RequestId",
                table: "Certificates");

            migrationBuilder.AddColumn<Guid>(
                name: "RequestItemId",
                table: "Certificates",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Certificates" AS certificate
                SET "RequestItemId" = (
                    SELECT request_item."Id"
                    FROM "RequestItems" AS request_item
                    WHERE request_item."RequestId" = certificate."RequestId"
                      AND request_item."EquipmentId" = certificate."EquipmentId")
                WHERE certificate."EquipmentId" IS NOT NULL
                  AND 1 = (
                    SELECT COUNT(*)
                    FROM "RequestItems" AS request_item
                    WHERE request_item."RequestId" = certificate."RequestId"
                      AND request_item."EquipmentId" = certificate."EquipmentId");

            UPDATE "Certificates" AS certificate
            SET "RequestItemId" = (
                    SELECT request_item."Id"
                    FROM "RequestItems" AS request_item
                    WHERE request_item."RequestId" = certificate."RequestId")
            WHERE certificate."RequestItemId" IS NULL
              AND 1 = (
                    SELECT COUNT(*)
                    FROM "RequestItems" AS request_item
                    WHERE request_item."RequestId" = certificate."RequestId");
            """);

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_RequestId_RequestItemId",
                table: "Certificates",
                columns: new[] { "RequestId", "RequestItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_RequestItemId",
                table: "Certificates",
                column: "RequestItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Certificates_RequestItems_RequestItemId",
                table: "Certificates",
                column: "RequestItemId",
                principalTable: "RequestItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Certificates_RequestItems_RequestItemId",
                table: "Certificates");

            migrationBuilder.DropIndex(
                name: "IX_Certificates_RequestId_RequestItemId",
                table: "Certificates");

            migrationBuilder.DropIndex(
                name: "IX_Certificates_RequestItemId",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "RequestItemId",
                table: "Certificates");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_RequestId",
                table: "Certificates",
                column: "RequestId",
                unique: true);
        }
    }
}
