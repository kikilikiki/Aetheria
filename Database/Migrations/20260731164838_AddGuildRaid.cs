using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildRaid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuildRaids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SpeciesId = table.Column<int>(type: "integer", nullable: false),
                    BossElement = table.Column<int>(type: "integer", nullable: false),
                    MaxHealth = table.Column<int>(type: "integer", nullable: false),
                    CurrentHealth = table.Column<int>(type: "integer", nullable: false),
                    IsAlive = table.Column<bool>(type: "boolean", nullable: false),
                    SpawnedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KilledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    KillerCharacterName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildRaids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildRaids_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildRaidDamageEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuildRaidId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterName = table.Column<string>(type: "text", nullable: false),
                    TotalDamage = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildRaidDamageEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildRaidDamageEntries_GuildRaids_GuildRaidId",
                        column: x => x.GuildRaidId,
                        principalTable: "GuildRaids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildRaidDamageEntries_GuildRaidId",
                table: "GuildRaidDamageEntries",
                column: "GuildRaidId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildRaids_GuildId",
                table: "GuildRaids",
                column: "GuildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildRaidDamageEntries");

            migrationBuilder.DropTable(
                name: "GuildRaids");
        }
    }
}
