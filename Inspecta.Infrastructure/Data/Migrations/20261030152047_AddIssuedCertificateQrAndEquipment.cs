using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuedCertificateQrAndEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EquipmentId",
                table: "IssuedCertificateFiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreparedBy",
                table: "IssuedCertificateFiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrData",
                table: "IssuedCertificateFiles",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificateFiles_EquipmentId",
                table: "IssuedCertificateFiles",
                column: "EquipmentId");

            // Certificates issued before this change take the machine, the QR link and the preparer from their certificate.
            migrationBuilder.Sql("""
                UPDATE "IssuedCertificateFiles" AS f
                SET "EquipmentId" = c."EquipmentId",
                    "QrData" = LEFT(c."QrData", 512),
                    "PreparedBy" = LEFT(c."TextsJson"::jsonb ->> 'preparedByName', 200)
                FROM "Certificates" AS c
                WHERE c."Id" = f."CertificateId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IssuedCertificateFiles_EquipmentId",
                table: "IssuedCertificateFiles");

            migrationBuilder.DropColumn(
                name: "EquipmentId",
                table: "IssuedCertificateFiles");

            migrationBuilder.DropColumn(
                name: "PreparedBy",
                table: "IssuedCertificateFiles");

            migrationBuilder.DropColumn(
                name: "QrData",
                table: "IssuedCertificateFiles");
        }
    }
}
