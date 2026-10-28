using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailInspectionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailFromName",
                table: "Requests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RequestEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoordinatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    MailboxUidValidity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MailboxUid = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: true),
                    InReplyTo = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: true),
                    References = table.Column<string>(type: "text", nullable: true),
                    FromAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    FromName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Subject = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: true),
                    ToAddressesJson = table.Column<string>(type: "text", nullable: true),
                    CcAddressesJson = table.Column<string>(type: "text", nullable: true),
                    BccAddressesJson = table.Column<string>(type: "text", nullable: true),
                    ReplyToAddressesJson = table.Column<string>(type: "text", nullable: true),
                    AttachmentMetadataJson = table.Column<string>(type: "text", nullable: true),
                    TextBody = table.Column<string>(type: "text", nullable: true),
                    HtmlBody = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestEmails_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestEmails_Users_CoordinatorId",
                        column: x => x.CoordinatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CoordinatorId",
                table: "Requests",
                column: "CoordinatorId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestEmails_CoordinatorId_MailboxUidValidity_MailboxUid",
                table: "RequestEmails",
                columns: new[] { "CoordinatorId", "MailboxUidValidity", "MailboxUid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestEmails_RequestId",
                table: "RequestEmails",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestEmails");

            migrationBuilder.DropIndex(
                name: "IX_Requests_CoordinatorId",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "EmailFromName",
                table: "Requests");
        }
    }
}
