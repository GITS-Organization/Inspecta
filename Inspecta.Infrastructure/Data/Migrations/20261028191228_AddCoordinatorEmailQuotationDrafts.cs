using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoordinatorEmailQuotationDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoordinatorEmailConfigs",
                columns: table => new
                {
                    CoordinatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImapServer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ImapPort = table.Column<int>(type: "integer", nullable: false),
                    EmailUsername = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    EncryptedPassword = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    UseSsl = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoordinatorEmailConfigs", x => x.CoordinatorId);
                    table.ForeignKey(
                        name: "FK_CoordinatorEmailConfigs_Users_CoordinatorId",
                        column: x => x.CoordinatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CoordinatorEmailConfigs_Users_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuotationDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoordinatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MachineDetails = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RawEmailBody = table.Column<string>(type: "text", nullable: false),
                    SenderEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    EmailSubject = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceUidValidity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceUid = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationDrafts_Users_CoordinatorId",
                        column: x => x.CoordinatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuotationDrafts_Users_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoordinatorEmailConfigs_TenantId",
                table: "CoordinatorEmailConfigs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDrafts_CoordinatorId_SourceUidValidity_SourceUid",
                table: "QuotationDrafts",
                columns: new[] { "CoordinatorId", "SourceUidValidity", "SourceUid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationDrafts_TenantId",
                table: "QuotationDrafts",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoordinatorEmailConfigs");

            migrationBuilder.DropTable(
                name: "QuotationDrafts");
        }
    }
}
