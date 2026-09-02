using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBetaFormReferralAndGiftCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferredByUserId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discovery",
                table: "BetaApplications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Motivation",
                table: "BetaApplications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReferralCodeUsed",
                table: "BetaApplications",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GiftCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    RewardPayload = table.Column<string>(type: "text", nullable: false),
                    MaxRedemptions = table.Column<int>(type: "integer", nullable: true),
                    RedemptionCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GiftCodeRedemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GiftCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    RedeemedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCodeRedemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiftCodeRedemptions_GiftCodes_GiftCodeId",
                        column: x => x.GiftCodeId,
                        principalTable: "GiftCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GiftCodeRedemptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferralCode",
                table: "Users",
                column: "ReferralCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiftCodeRedemptions_GiftCodeId_UserId",
                table: "GiftCodeRedemptions",
                columns: new[] { "GiftCodeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiftCodeRedemptions_UserId",
                table: "GiftCodeRedemptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftCodes_Code",
                table: "GiftCodes",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiftCodeRedemptions");

            migrationBuilder.DropTable(
                name: "GiftCodes");

            migrationBuilder.DropIndex(
                name: "IX_Users_ReferralCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReferredByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Discovery",
                table: "BetaApplications");

            migrationBuilder.DropColumn(
                name: "Motivation",
                table: "BetaApplications");

            migrationBuilder.DropColumn(
                name: "ReferralCodeUsed",
                table: "BetaApplications");
        }
    }
}
