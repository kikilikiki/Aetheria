using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterTemporaryBoosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GoldBoostExpiresAtUtc",
                table: "Characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LuckBoostExpiresAtUtc",
                table: "Characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "XpBoostExpiresAtUtc",
                table: "Characters",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoldBoostExpiresAtUtc",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "LuckBoostExpiresAtUtc",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "XpBoostExpiresAtUtc",
                table: "Characters");
        }
    }
}
