using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RenewalReminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentById = table.Column<Guid>(type: "uuid", nullable: false),
                    SendKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ToAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ToName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FromAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FromName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Subject = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    MachinesJson = table.Column<string>(type: "text", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReplyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestCreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenewalReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RenewalReminders_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RenewalReminders_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RenewalReminderReplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReminderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: true),
                    InReplyTo = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: true),
                    FromAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    FromName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Subject = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: true),
                    TextBody = table.Column<string>(type: "text", nullable: true),
                    HtmlBody = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenewalReminderReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RenewalReminderReplies_RenewalReminders_ReminderId",
                        column: x => x.ReminderId,
                        principalTable: "RenewalReminders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RenewalReminderReplies_ManagerId_MessageId",
                table: "RenewalReminderReplies",
                columns: new[] { "ManagerId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenewalReminderReplies_ReminderId",
                table: "RenewalReminderReplies",
                column: "ReminderId");

            migrationBuilder.CreateIndex(
                name: "IX_RenewalReminders_ClientId",
                table: "RenewalReminders",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_RenewalReminders_ManagerId_ClientId_CreatedAt",
                table: "RenewalReminders",
                columns: new[] { "ManagerId", "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RenewalReminders_ManagerId_SendKey",
                table: "RenewalReminders",
                columns: new[] { "ManagerId", "SendKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenewalReminders_MessageId",
                table: "RenewalReminders",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_RenewalReminders_RequestId",
                table: "RenewalReminders",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RenewalReminderReplies");

            migrationBuilder.DropTable(
                name: "RenewalReminders");
        }
    }
}
