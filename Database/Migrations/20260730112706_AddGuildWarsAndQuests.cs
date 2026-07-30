using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildWarsAndQuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "WarPoints",
                table: "Guilds",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "WeeklyQuestCompleted",
                table: "Guilds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyQuestItemsDeposited",
                table: "Guilds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WeeklyQuestWeekBucket",
                table: "Guilds",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WarPoints",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "WeeklyQuestCompleted",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "WeeklyQuestItemsDeposited",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "WeeklyQuestWeekBucket",
                table: "Guilds");
        }
    }
}
