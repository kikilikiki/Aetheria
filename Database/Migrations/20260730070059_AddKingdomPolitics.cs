using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddKingdomPolitics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "KingCharacterId",
                table: "Kingdoms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TreasuryGold",
                table: "Kingdoms",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "KingdomVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KingdomId = table.Column<int>(type: "integer", nullable: false),
                    VoterCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateCharacterId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KingdomVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KingdomVotes_Kingdoms_KingdomId",
                        column: x => x.KingdomId,
                        principalTable: "Kingdoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KingdomVotes_KingdomId_VoterCharacterId",
                table: "KingdomVotes",
                columns: new[] { "KingdomId", "VoterCharacterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KingdomVotes");

            migrationBuilder.DropColumn(
                name: "KingCharacterId",
                table: "Kingdoms");

            migrationBuilder.DropColumn(
                name: "TreasuryGold",
                table: "Kingdoms");
        }
    }
}
