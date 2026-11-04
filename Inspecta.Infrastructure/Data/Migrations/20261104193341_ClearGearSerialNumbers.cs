using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClearGearSerialNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Gears are replaced every six months, so the client's list no longer keeps their serial numbers (they are written on the
            // certificates when the inspector divides a gear). Only registered items named like a catalogue gear are touched.
            migrationBuilder.Sql(
                "UPDATE \"Items\" SET \"SerialNumber\" = NULL " +
                "WHERE \"SerialNumber\" IS NOT NULL AND lower(btrim(\"Name\")) IN ('bow shackle bolt and nut pin', 'bow shackle screw pin', 'chain sling grade 4', 'chain sling grade 8', 'clamp horizontal plate clamp', 'clamp vertical plate clamp', 'eye bolt', 'fall arrestor', 'flat woven webbing sling', 'full body safety harness', 'hook with safety latch', 'pipe hook', 'round webbing sling', 'shock absorber', 'snatch block', 'spreader bar', 'steel wire rope sling');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The serial numbers that were cleared are not kept.
        }
    }
}
