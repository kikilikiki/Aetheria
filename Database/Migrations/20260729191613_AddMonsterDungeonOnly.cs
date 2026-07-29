using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMonsterDungeonOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DungeonOnly",
                table: "MonsterSpecies",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DungeonOnly",
                table: "MonsterSpecies");
        }
    }
}
