using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftCodeDiscordAnnounce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AnnounceOnDiscord",
                table: "GiftCodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscordAnnouncedAtUtc",
                table: "GiftCodes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnounceOnDiscord",
                table: "GiftCodes");

            migrationBuilder.DropColumn(
                name: "DiscordAnnouncedAtUtc",
                table: "GiftCodes");
        }
    }
}
