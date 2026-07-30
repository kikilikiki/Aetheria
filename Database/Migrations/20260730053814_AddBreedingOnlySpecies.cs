using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBreedingOnlySpecies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BreedingOnly",
                table: "MonsterSpecies",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreedingOnly",
                table: "MonsterSpecies");
        }
    }
}
