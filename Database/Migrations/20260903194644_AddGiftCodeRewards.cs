using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftCodeRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RewardGems",
                table: "GiftCodes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "RewardGold",
                table: "GiftCodes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "RewardMonsterLevel",
                table: "GiftCodes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RewardMonsterSpeciesId",
                table: "GiftCodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RewardMonsterVariant",
                table: "GiftCodes",
                type: "text",
                nullable: false,
                // "Normal" et non "" : la colonne est convertie en enum MonsterVariant à la lecture,
                // "" échouerait au parse pour d'éventuelles lignes existantes.
                defaultValue: "Normal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RewardGems",
                table: "GiftCodes");

            migrationBuilder.DropColumn(
                name: "RewardGold",
                table: "GiftCodes");

            migrationBuilder.DropColumn(
                name: "RewardMonsterLevel",
                table: "GiftCodes");

            migrationBuilder.DropColumn(
                name: "RewardMonsterSpeciesId",
                table: "GiftCodes");

            migrationBuilder.DropColumn(
                name: "RewardMonsterVariant",
                table: "GiftCodes");
        }
    }
}
