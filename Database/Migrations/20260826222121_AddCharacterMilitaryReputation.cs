using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterMilitaryReputation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MilitaryReputation",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MilitaryReputation",
                table: "Characters");
        }
    }
}
