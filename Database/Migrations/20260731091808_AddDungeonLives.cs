using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDungeonLives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DungeonLives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    DungeonId = table.Column<int>(type: "integer", nullable: false),
                    LivesRemaining = table.Column<int>(type: "integer", nullable: false),
                    LastResetUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DungeonLives", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DungeonLives_CharacterId_DungeonId",
                table: "DungeonLives",
                columns: new[] { "CharacterId", "DungeonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DungeonLives");
        }
    }
}
