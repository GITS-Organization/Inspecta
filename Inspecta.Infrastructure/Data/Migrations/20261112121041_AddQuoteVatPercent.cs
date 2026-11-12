using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteVatPercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VatPercent",
                table: "Quotes",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 14m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VatPercent",
                table: "Quotes");
        }
    }
}
