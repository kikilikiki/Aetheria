using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBattlePass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BattlePassHasPremium",
                table: "Characters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BattlePassLastPremiumRewardLevel",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BattlePassLevel",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "BattlePassXp",
                table: "Characters",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BattlePassHasPremium",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BattlePassLastPremiumRewardLevel",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BattlePassLevel",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BattlePassXp",
                table: "Characters");
        }
    }
}
