using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandDomainEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureImageId",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CertificateSubmittedAt",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailFrom",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailPreview",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasUnreadClientReply",
                table: "Requests",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SeededBy",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Requests",
                type: "TEXT",
                maxLength: 24,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "RequestItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "RequestItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "RequestItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "RequestItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "RequestItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrNumber",
                table: "RequestItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ClientCounterPricePiastres",
                table: "Quotes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientName",
                table: "Quotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClientRespondedAt",
                table: "Quotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquipmentSummary",
                table: "Quotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemsJson",
                table: "Quotes",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReminderAt",
                table: "Quotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Quotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotesJson",
                table: "Quotes",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "RequestNumber",
                table: "Quotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SentAt",
                table: "Quotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SubtotalPiastres",
                table: "Quotes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Quotes",
                type: "TEXT",
                maxLength: 24,
                nullable: false,
                defaultValue: "Offer");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Quotes",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "JobStatusHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssetTag",
                table: "Equipment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Equipment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CertificateExpiryDate",
                table: "Equipment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAngle",
                table: "Equipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Equipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastInspectedOn",
                table: "Equipment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastInspectionResult",
                table: "Equipment",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Equipment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Equipment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerReference",
                table: "Equipment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrNumber",
                table: "Equipment",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Equipment",
                type: "TEXT",
                maxLength: 24,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<short>(
                name: "YearOfManufacture",
                table: "Equipment",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificatesEmail",
                table: "Clients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Clients",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Clients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Clients",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxNumber",
                table: "Clients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "ClientContacts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EquipmentId",
                table: "Certificates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAngle",
                table: "Certificates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IssuedForJson",
                table: "Certificates",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "Certificates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrData",
                table: "Certificates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerLicense",
                table: "Certificates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerName",
                table: "Certificates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SentAt",
                table: "Certificates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Quotes\" SET \"SubtotalPiastres\" = \"AmountPiastres\", \"UpdatedAt\" = \"CreatedAt\";");
            migrationBuilder.Sql(
                "UPDATE \"Equipment\" SET \"IsArchived\" = 1 WHERE \"ArchivedAt\" IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE \"Equipment\" SET \"LastInspectedOn\" = substr(\"LastInspectionAt\", 1, 10) WHERE \"LastInspectionAt\" IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE \"Equipment\" SET \"CertificateExpiryDate\" = substr(\"NextInspectionDueAt\", 1, 10) WHERE \"NextInspectionDueAt\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SignatureImageId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CertificateSubmittedAt",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "EmailFrom",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "EmailPreview",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "HasUnreadClientReply",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "SeededBy",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "PrNumber",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "ClientCounterPricePiastres",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ClientName",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ClientRespondedAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "EquipmentSummary",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ItemsJson",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "LastReminderAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "NotesJson",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RequestNumber",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SubtotalPiastres",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "JobStatusHistory");

            migrationBuilder.DropColumn(
                name: "AssetTag",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "CertificateExpiryDate",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "HasAngle",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "LastInspectedOn",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "LastInspectionResult",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "OwnerReference",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "PrNumber",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "YearOfManufacture",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "CertificatesEmail",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TaxNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "EquipmentId",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "HasAngle",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "IssuedForJson",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "QrData",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "ReviewerLicense",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "ReviewerName",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "Certificates");
        }
    }
}
