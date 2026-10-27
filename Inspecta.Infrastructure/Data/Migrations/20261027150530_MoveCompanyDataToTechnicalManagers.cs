using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveCompanyDataToTechnicalManagers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ManagerId",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubscriptionEndDate",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionStatus",
                table: "Users",
                type: "TEXT",
                maxLength: 24,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Users" AS staff
                SET "ManagerId" = staff."CreatedById"
                WHERE staff."CompanyId" IS NOT NULL
                    AND staff."Role" NOT IN ('Admin', 'TechnicalManager')
                    AND staff."CreatedById" IS NOT NULL
                    AND EXISTS (
                        SELECT 1
                        FROM "Users" AS manager
                        WHERE manager."Id" = staff."CreatedById"
                            AND manager."CompanyId" = staff."CompanyId"
                            AND manager."Role" = 'TechnicalManager'
                    );

                UPDATE "Users" AS staff
                SET "ManagerId" = (
                    SELECT manager."Id"
                    FROM "Users" AS manager
                    WHERE manager."CompanyId" = staff."CompanyId"
                        AND manager."Role" = 'TechnicalManager'
                    ORDER BY manager."CreatedAt", manager."Id"
                    LIMIT 1
                )
                WHERE staff."CompanyId" IS NOT NULL
                    AND staff."Role" NOT IN ('Admin', 'TechnicalManager')
                    AND staff."ManagerId" IS NULL;

                UPDATE "Users" AS staff
                SET "SubscriptionStatus" = (
                        SELECT CASE
                            WHEN company."IsDeleted" = 1 THEN 'Expired'
                            ELSE company."SubscriptionStatus"
                        END
                        FROM "Companies" AS company
                        WHERE company."Id" = staff."CompanyId"
                    ),
                    "SubscriptionEndDate" = (
                        SELECT company."SubscriptionEndDate"
                        FROM "Companies" AS company
                        WHERE company."Id" = staff."CompanyId"
                    )
                WHERE staff."Role" = 'TechnicalManager'
                    AND staff."CompanyId" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagerId_Role",
                table: "Users",
                columns: new[] { "ManagerId", "Role" });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_ManagerId",
                table: "Users",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_ManagerId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ManagerId_Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SubscriptionEndDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "Users");
        }
    }
}
