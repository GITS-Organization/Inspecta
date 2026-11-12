using System;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestReviewer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerId",
                table: "Requests",
                type: "uuid",
                nullable: true);

            // Every existing request starts with its company's default reviewer.
            migrationBuilder.Sql(CompanyBackfill.SetReviewerOfExistingRequests);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewerId",
                table: "Requests");
        }
    }
}
