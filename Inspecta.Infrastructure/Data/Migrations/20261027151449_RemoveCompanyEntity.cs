using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCompanyEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Companies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SubscriptionEndDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SubscriptionStatus = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "Companies"
                    ("Id", "Name", "SubscriptionStatus", "SubscriptionEndDate", "CreatedAt", "IsDeleted", "DeletedAt")
                SELECT "Id", "FullName", COALESCE("SubscriptionStatus", 'Active'),
                    "SubscriptionEndDate", "CreatedAt", 0, NULL
                FROM "Users"
                WHERE "Role" = 'TechnicalManager';
                """);
        }
    }
}
