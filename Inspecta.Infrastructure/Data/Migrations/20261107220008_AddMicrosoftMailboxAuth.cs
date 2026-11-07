using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMicrosoftMailboxAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuthMode",
                table: "CoordinatorEmailConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConnectionStatus",
                table: "CoordinatorEmailConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedRefreshToken",
                table: "CoordinatorEmailConfigs",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAuthError",
                table: "CoordinatorEmailConfigs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MicrosoftTenantId",
                table: "CoordinatorEmailConfigs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthMode",
                table: "CoordinatorEmailConfigs");

            migrationBuilder.DropColumn(
                name: "ConnectionStatus",
                table: "CoordinatorEmailConfigs");

            migrationBuilder.DropColumn(
                name: "EncryptedRefreshToken",
                table: "CoordinatorEmailConfigs");

            migrationBuilder.DropColumn(
                name: "LastAuthError",
                table: "CoordinatorEmailConfigs");

            migrationBuilder.DropColumn(
                name: "MicrosoftTenantId",
                table: "CoordinatorEmailConfigs");
        }
    }
}
