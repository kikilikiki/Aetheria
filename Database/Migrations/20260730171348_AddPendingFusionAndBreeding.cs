using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingFusionAndBreeding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextBreedAllowedAtUtc",
                table: "Characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingBreedCompletesAtUtc",
                table: "Characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingBreedOffspringPassiveTalent",
                table: "Characters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingBreedOffspringSpeciesId",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingBreedOffspringVariant",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingBreedParentId1",
                table: "Characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingBreedParentId2",
                table: "Characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingFusionCompletesAtUtc",
                table: "Characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingFusionConsumedId",
                table: "Characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingFusionSurvivorId",
                table: "Characters",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextBreedAllowedAtUtc",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PendingBreedCompletesAtUtc",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PendingBreedOffspringPassiveTalent",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PendingBreedOffspringSpeciesId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PendingBreedOffspringVariant",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PendingBreedParentId1",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PendingBreedParentId2",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PendingFusionCompletesAtUtc",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PendingFusionConsumedId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PendingFusionSurvivorId",
                table: "Characters");
        }
    }
}
