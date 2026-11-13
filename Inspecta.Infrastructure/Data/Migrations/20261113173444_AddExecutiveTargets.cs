using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutiveTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExecutiveTargets",
                columns: table => new
                {
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionRatePercent = table.Column<int>(type: "integer", nullable: false),
                    DaysToCollect = table.Column<int>(type: "integer", nullable: false),
                    RequestToDeliveryDays = table.Column<int>(type: "integer", nullable: false),
                    ReviewDaysPerStep = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutiveTargets", x => x.ManagerId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutiveTargets");
        }
    }
}
