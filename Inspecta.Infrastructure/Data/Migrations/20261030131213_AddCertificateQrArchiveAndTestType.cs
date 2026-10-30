using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateQrArchiveAndTestType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignatureStrokesJson",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TestType",
                table: "Items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CertificateCounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CertificateDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AttachmentNames = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Sent = table.Column<bool>(type: "boolean", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IssuedCertificateFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VerificationCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExaminationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextExaminationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuedCertificateFiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_CertificateNumber",
                table: "Certificates",
                column: "CertificateNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_VerificationCode",
                table: "Certificates",
                column: "VerificationCode");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateCounters_ManagerId_Year",
                table: "CertificateCounters",
                columns: new[] { "ManagerId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateDeliveries_RequestId",
                table: "CertificateDeliveries",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificateFiles_CertificateId",
                table: "IssuedCertificateFiles",
                column: "CertificateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificateFiles_ClientId_IssuedAt",
                table: "IssuedCertificateFiles",
                columns: new[] { "ClientId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificateFiles_VerificationCode",
                table: "IssuedCertificateFiles",
                column: "VerificationCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificateCounters");

            migrationBuilder.DropTable(
                name: "CertificateDeliveries");

            migrationBuilder.DropTable(
                name: "IssuedCertificateFiles");

            migrationBuilder.DropIndex(
                name: "IX_Certificates_CertificateNumber",
                table: "Certificates");

            migrationBuilder.DropIndex(
                name: "IX_Certificates_VerificationCode",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "SignatureStrokesJson",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TestType",
                table: "Items");
        }
    }
}
