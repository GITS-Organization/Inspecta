using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameQuoteAcceptedToUnassigned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Statuses are stored as text: the accepted-but-not-assigned status was renamed.
            migrationBuilder.Sql("UPDATE \"Requests\" SET \"Status\" = 'Unassigned' WHERE \"Status\" = 'QuoteAccepted';");
            migrationBuilder.Sql("UPDATE \"JobStatusHistory\" SET \"FromStatus\" = 'Unassigned' WHERE \"FromStatus\" = 'QuoteAccepted';");
            migrationBuilder.Sql("UPDATE \"JobStatusHistory\" SET \"ToStatus\" = 'Unassigned' WHERE \"ToStatus\" = 'QuoteAccepted';");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Requests\" SET \"Status\" = 'QuoteAccepted' WHERE \"Status\" = 'Unassigned';");
            migrationBuilder.Sql("UPDATE \"JobStatusHistory\" SET \"FromStatus\" = 'QuoteAccepted' WHERE \"FromStatus\" = 'Unassigned';");
            migrationBuilder.Sql("UPDATE \"JobStatusHistory\" SET \"ToStatus\" = 'QuoteAccepted' WHERE \"ToStatus\" = 'Unassigned';");

        }
    }
}
