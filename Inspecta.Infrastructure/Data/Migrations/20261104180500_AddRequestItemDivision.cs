using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestItemDivision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DivisionGroupId",
                table: "RequestItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DivisionTotal",
                table: "RequestItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestItems_DivisionGroupId",
                table: "RequestItems",
                column: "DivisionGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RequestItems_DivisionGroupId",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "DivisionGroupId",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "DivisionTotal",
                table: "RequestItems");
        }
    }
}
