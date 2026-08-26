using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EquippedAccessoryItemId",
                table: "Monsters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EquippedArmorItemId",
                table: "Monsters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EquippedWeaponItemId",
                table: "Monsters",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquippedAccessoryItemId",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "EquippedArmorItemId",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "EquippedWeaponItemId",
                table: "Monsters");
        }
    }
}
