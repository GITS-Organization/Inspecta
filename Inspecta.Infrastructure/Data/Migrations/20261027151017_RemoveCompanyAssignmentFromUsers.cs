using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCompanyAssignmentFromUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Companies_CompanyId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_CompanyId_Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                DELETE FROM "Companies";

                INSERT INTO "Companies"
                    ("Id", "Name", "SubscriptionStatus", "SubscriptionEndDate", "CreatedAt", "IsDeleted", "DeletedAt")
                SELECT "Id", "FullName", COALESCE("SubscriptionStatus", 'Active'),
                    "SubscriptionEndDate", "CreatedAt", 0, NULL
                FROM "Users"
                WHERE "Role" = 'TechnicalManager';

                UPDATE "Users"
                SET "CompanyId" = CASE
                    WHEN "Role" = 'TechnicalManager' THEN "Id"
                    ELSE "ManagerId"
                END
                WHERE "Role" <> 'Admin';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId_Role",
                table: "Users",
                columns: new[] { "CompanyId", "Role" });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Companies_CompanyId",
                table: "Users",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
