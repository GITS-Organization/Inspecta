using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inspecta.Infrastructure.Data.Migrations;

public partial class RenameEquipmentToItemsAndAddItemMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_RequestItems_Equipment_EquipmentId",
            table: "RequestItems");

        migrationBuilder.RenameTable(
            name: "Equipment",
            newName: "Items");

        migrationBuilder.RenameIndex(
            name: "IX_Equipment_ClientId",
            table: "Items",
            newName: "IX_Items_ClientId");

        migrationBuilder.RenameIndex(
            name: "IX_Equipment_LegacyId",
            table: "Items",
            newName: "IX_Items_LegacyId");

        migrationBuilder.AddColumn<string>(
            name: "ItemType",
            table: "Items",
            type: "TEXT",
            maxLength: 24,
            nullable: false,
            defaultValue: "Equipment");

        migrationBuilder.AddColumn<string>(
            name: "LabelsJson",
            table: "Items",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "DatesJson",
            table: "Items",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "ChecklistJson",
            table: "Items",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "AttachmentsJson",
            table: "Items",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddForeignKey(
            name: "FK_RequestItems_Items_EquipmentId",
            table: "RequestItems",
            column: "EquipmentId",
            principalTable: "Items",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_RequestItems_Items_EquipmentId",
            table: "RequestItems");

        migrationBuilder.DropColumn(name: "ItemType", table: "Items");
        migrationBuilder.DropColumn(name: "LabelsJson", table: "Items");
        migrationBuilder.DropColumn(name: "DatesJson", table: "Items");
        migrationBuilder.DropColumn(name: "ChecklistJson", table: "Items");
        migrationBuilder.DropColumn(name: "AttachmentsJson", table: "Items");

        migrationBuilder.RenameTable(
            name: "Items",
            newName: "Equipment");

        migrationBuilder.RenameIndex(
            name: "IX_Items_ClientId",
            table: "Equipment",
            newName: "IX_Equipment_ClientId");

        migrationBuilder.RenameIndex(
            name: "IX_Items_LegacyId",
            table: "Equipment",
            newName: "IX_Equipment_LegacyId");

        migrationBuilder.AddForeignKey(
            name: "FK_RequestItems_Equipment_EquipmentId",
            table: "RequestItems",
            column: "EquipmentId",
            principalTable: "Equipment",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }
}
