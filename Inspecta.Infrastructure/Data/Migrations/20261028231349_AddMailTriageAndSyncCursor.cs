using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMailTriageAndSyncCursor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Triage",
                table: "Requests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "request");

            migrationBuilder.AddColumn<long>(
                name: "LastUid",
                table: "CoordinatorEmailConfigs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "LastUidValidity",
                table: "CoordinatorEmailConfigs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SyncFromUtc",
                table: "CoordinatorEmailConfigs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateTable(
                name: "ClientMailRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pattern = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientMailRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientMailRules_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClientMailRules_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientMailRules_Users_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Triage_CreatedAt",
                table: "Requests",
                columns: new[] { "Triage", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientMailRules_ClientId",
                table: "ClientMailRules",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMailRules_CreatedById",
                table: "ClientMailRules",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMailRules_ManagerId_Pattern",
                table: "ClientMailRules",
                columns: new[] { "ManagerId", "Pattern" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientMailRules");

            migrationBuilder.DropIndex(
                name: "IX_Requests_Triage_CreatedAt",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Triage",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "LastUid",
                table: "CoordinatorEmailConfigs");

            migrationBuilder.DropColumn(
                name: "LastUidValidity",
                table: "CoordinatorEmailConfigs");

            migrationBuilder.DropColumn(
                name: "SyncFromUtc",
                table: "CoordinatorEmailConfigs");
        }
    }
}
