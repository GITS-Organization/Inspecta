using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteEmailDeliveryAndRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SupersededAt",
                table: "Quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "CoordinatorEmailConfigs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpServer",
                table: "CoordinatorEmailConfigs",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SmtpUseSsl",
                table: "CoordinatorEmailConfigs",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OutboundEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ToAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ToName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FromAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FromName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Subject = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: false),
                    InReplyTo = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: true),
                    References = table.Column<string>(type: "text", nullable: true),
                    AttachmentFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    AttachmentContent = table.Column<byte[]>(type: "bytea", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboundEmails_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OutboundEmails_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundEmails_QuoteId_Kind",
                table: "OutboundEmails",
                columns: new[] { "QuoteId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboundEmails_RequestId",
                table: "OutboundEmails",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboundEmails");

            migrationBuilder.DropColumn(
                name: "SupersededAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "CoordinatorEmailConfigs");

            migrationBuilder.DropColumn(
                name: "SmtpServer",
                table: "CoordinatorEmailConfigs");

            migrationBuilder.DropColumn(
                name: "SmtpUseSsl",
                table: "CoordinatorEmailConfigs");
        }
    }
}
