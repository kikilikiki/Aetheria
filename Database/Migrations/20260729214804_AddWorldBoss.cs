using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldBoss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorldBosses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    MaxHealth = table.Column<int>(type: "integer", nullable: false),
                    CurrentHealth = table.Column<int>(type: "integer", nullable: false),
                    IsAlive = table.Column<bool>(type: "boolean", nullable: false),
                    SpawnedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KilledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    KillerCharacterName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldBosses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorldBossDamageEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorldBossId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterName = table.Column<string>(type: "text", nullable: false),
                    TotalDamage = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldBossDamageEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorldBossDamageEntries_WorldBosses_WorldBossId",
                        column: x => x.WorldBossId,
                        principalTable: "WorldBosses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorldBossDamageEntries_WorldBossId",
                table: "WorldBossDamageEntries",
                column: "WorldBossId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorldBossDamageEntries");

            migrationBuilder.DropTable(
                name: "WorldBosses");
        }
    }
}
