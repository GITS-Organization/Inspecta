using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyQuoteStatusAndStoreQuotePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScopeJson",
                table: "Quotes",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "ValidForDays",
                table: "Quotes",
                type: "integer",
                nullable: false,
                defaultValue: 14);

            // Quotations now have three statuses. Everything that was still in progress or waiting for the
            // client becomes open (SentAt tells drafts from sent ones); accepted and rejected are unchanged.
            migrationBuilder.Sql(
                "UPDATE \"Quotes\" SET \"Status\" = 'Open' WHERE \"Status\" IN ('Draft', 'Sent', 'Countered', 'Expired', 'Superseded');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql(
                "UPDATE \"Quotes\" SET \"Status\" = CASE WHEN \"SentAt\" IS NULL THEN 'Draft' ELSE 'Sent' END WHERE \"Status\" = 'Open';");
            migrationBuilder.DropColumn(
                name: "ScopeJson",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ValidForDays",
                table: "Quotes");
        }
    }
}
