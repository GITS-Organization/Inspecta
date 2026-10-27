using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_LegacyId",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Items_LegacyId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Clients_LegacyId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "JobStatusHistory");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "Certificates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "Requests",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "RequestItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "JobStatusHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "Items",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "Clients",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyId",
                table: "Certificates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_LegacyId",
                table: "Requests",
                column: "LegacyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_LegacyId",
                table: "Items",
                column: "LegacyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_LegacyId",
                table: "Clients",
                column: "LegacyId",
                unique: true);
        }
    }
}
