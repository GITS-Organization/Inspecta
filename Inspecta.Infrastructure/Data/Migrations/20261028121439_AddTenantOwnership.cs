using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ManagerId",
                table: "Requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerId",
                table: "Clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ManagerId",
                table: "Requests",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ManagerId",
                table: "Clients",
                column: "ManagerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Users_ManagerId",
                table: "Clients",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Users_ManagerId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Requests_ManagerId",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Clients_ManagerId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Clients");
        }
    }
}
