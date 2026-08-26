using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldBossSpeciesAndKingdomReward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BossElement",
                table: "WorldBosses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SpeciesId",
                table: "WorldBosses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinningKingdom",
                table: "WorldBosses",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BossElement",
                table: "WorldBosses");

            migrationBuilder.DropColumn(
                name: "SpeciesId",
                table: "WorldBosses");

            migrationBuilder.DropColumn(
                name: "WinningKingdom",
                table: "WorldBosses");
        }
    }
}
