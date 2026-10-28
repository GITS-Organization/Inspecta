using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandQuotationDraftEmailDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentMetadataJson",
                table: "QuotationDrafts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BccAddressesJson",
                table: "QuotationDrafts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CcAddressesJson",
                table: "QuotationDrafts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailDate",
                table: "QuotationDrafts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailHtmlBody",
                table: "QuotationDrafts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailMessageId",
                table: "QuotationDrafts",
                type: "character varying(998)",
                maxLength: 998,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InReplyTo",
                table: "QuotationDrafts",
                type: "character varying(998)",
                maxLength: 998,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "References",
                table: "QuotationDrafts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyToAddressesJson",
                table: "QuotationDrafts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "QuotationDrafts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToAddressesJson",
                table: "QuotationDrafts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentMetadataJson",
                table: "QuotationDrafts");

            migrationBuilder.DropColumn(
                name: "BccAddressesJson",
                table: "QuotationDrafts");

            migrationBuilder.DropColumn(
                name: "CcAddressesJson",
                table: "QuotationDrafts");

            migrationBuilder.DropColumn(
                name: "EmailDate",
                table: "QuotationDrafts");

            migrationBuilder.DropColumn(
                name: "EmailHtmlBody",
                table: "QuotationDrafts");

            migrationBuilder.DropColumn(
                name: "EmailMessageId",
                table: "QuotationDrafts");

            migrationBuilder.DropColumn(
                name: "InReplyTo",
                table: "QuotationDrafts");

            migrationBuilder.DropColumn(
                name: "References",
                table: "QuotationDrafts");

            migrationBuilder.DropColumn(
                name: "ReplyToAddressesJson",
                table: "QuotationDrafts");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "QuotationDrafts");

            migrationBuilder.DropColumn(
                name: "ToAddressesJson",
                table: "QuotationDrafts");
        }
    }
}
