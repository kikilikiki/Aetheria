using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionBidding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AuctionEndsAtUtc",
                table: "AuctionListings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CurrentBid",
                table: "AuctionListings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentBidderCharacterId",
                table: "AuctionListings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentBidderName",
                table: "AuctionListings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAuction",
                table: "AuctionListings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuctionEndsAtUtc",
                table: "AuctionListings");

            migrationBuilder.DropColumn(
                name: "CurrentBid",
                table: "AuctionListings");

            migrationBuilder.DropColumn(
                name: "CurrentBidderCharacterId",
                table: "AuctionListings");

            migrationBuilder.DropColumn(
                name: "CurrentBidderName",
                table: "AuctionListings");

            migrationBuilder.DropColumn(
                name: "IsAuction",
                table: "AuctionListings");
        }
    }
}
