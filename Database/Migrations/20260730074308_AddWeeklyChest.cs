using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyChest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeeklyChests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KingdomId = table.Column<int>(type: "integer", nullable: false),
                    WeekBucket = table.Column<string>(type: "text", nullable: false),
                    ClaimedByCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimedByCharacterName = table.Column<string>(type: "text", nullable: true),
                    ClaimedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RewardGold = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyChests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyChests_Kingdoms_KingdomId",
                        column: x => x.KingdomId,
                        principalTable: "Kingdoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyChests_KingdomId_WeekBucket",
                table: "WeeklyChests",
                columns: new[] { "KingdomId", "WeekBucket" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeeklyChests");
        }
    }
}
