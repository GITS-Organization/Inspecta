using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropTenantForeignKeysToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CoordinatorEmailConfigs_Users_TenantId",
                table: "CoordinatorEmailConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_QuotationDrafts_Users_TenantId",
                table: "QuotationDrafts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_CoordinatorEmailConfigs_Users_TenantId",
                table: "CoordinatorEmailConfigs",
                column: "TenantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationDrafts_Users_TenantId",
                table: "QuotationDrafts",
                column: "TenantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
