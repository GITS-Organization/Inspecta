using System;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientMailRules_Users_ManagerId",
                table: "ClientMailRules");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Users_ManagerId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyProfiles_Users_ManagerId",
                table: "CompanyProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Requests_Users_ManagerId",
                table: "Requests");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_ManagerId",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SubscriptionStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SubscriptionEndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DefaultReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            // Every technical manager becomes a company with the same id (so no row keyed by it changes), and joins it.
            migrationBuilder.Sql(CompanyBackfill.InsertCompanies);
            migrationBuilder.Sql(CompanyBackfill.AttachManagersToTheirCompany);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientMailRules_Users_ManagerId",
                table: "ClientMailRules",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Users_ManagerId",
                table: "Clients",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyProfiles_Users_ManagerId",
                table: "CompanyProfiles",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Requests_Users_ManagerId",
                table: "Requests",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_ManagerId",
                table: "Users",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
