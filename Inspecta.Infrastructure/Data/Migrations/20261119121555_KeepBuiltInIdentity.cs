using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class KeepBuiltInIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "KeepsBuiltInIdentity",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Every company that exists now is drawn exactly as before; only companies created from now on are drawn under their own name.
            migrationBuilder.Sql("UPDATE \"Companies\" SET \"KeepsBuiltInIdentity\" = TRUE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeepsBuiltInIdentity",
                table: "Companies");
        }
    }
}
