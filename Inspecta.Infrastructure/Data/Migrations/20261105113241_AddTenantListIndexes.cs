using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_ManagerId",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ManagerId_Status_CreatedAt",
                table: "Requests",
                columns: new[] { "ManagerId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificateFiles_ManagerId_IssuedAt",
                table: "IssuedCertificateFiles",
                columns: new[] { "ManagerId", "IssuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_ManagerId_Status_CreatedAt",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_IssuedCertificateFiles_ManagerId_IssuedAt",
                table: "IssuedCertificateFiles");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ManagerId",
                table: "Requests",
                column: "ManagerId");
        }
    }
}
