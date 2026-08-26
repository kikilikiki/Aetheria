using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMonsterIvEvPrest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EvAttack",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EvDefense",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EvHealth",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EvIntelligence",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EvResistance",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EvSpeed",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IvAttack",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IvDefense",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IvHealth",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IvIntelligence",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IvResistance",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IvSpeed",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PrestAttack",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PrestDefense",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PrestHealth",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PrestIntelligence",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PrestResistance",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PrestSpeed",
                table: "Monsters",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvAttack",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "EvDefense",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "EvHealth",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "EvIntelligence",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "EvResistance",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "EvSpeed",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "IvAttack",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "IvDefense",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "IvHealth",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "IvIntelligence",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "IvResistance",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "IvSpeed",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "PrestAttack",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "PrestDefense",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "PrestHealth",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "PrestIntelligence",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "PrestResistance",
                table: "Monsters");

            migrationBuilder.DropColumn(
                name: "PrestSpeed",
                table: "Monsters");
        }
    }
}
