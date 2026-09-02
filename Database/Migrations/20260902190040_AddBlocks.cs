using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Blocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockerCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedCharacterName = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Blocks_Characters_BlockedCharacterId",
                        column: x => x.BlockedCharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Blocks_Characters_BlockerCharacterId",
                        column: x => x.BlockerCharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_BlockedCharacterId",
                table: "Blocks",
                column: "BlockedCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_BlockerCharacterId_BlockedCharacterId",
                table: "Blocks",
                columns: new[] { "BlockerCharacterId", "BlockedCharacterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Blocks");
        }
    }
}
