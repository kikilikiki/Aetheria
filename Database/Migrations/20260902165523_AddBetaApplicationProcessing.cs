using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBetaApplicationProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAtUtc",
                table: "BetaApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncedStatus",
                table: "BetaApplications",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BetaApplications_ProcessedAtUtc",
                table: "BetaApplications",
                column: "ProcessedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BetaApplications_ProcessedAtUtc",
                table: "BetaApplications");

            migrationBuilder.DropColumn(
                name: "ProcessedAtUtc",
                table: "BetaApplications");

            migrationBuilder.DropColumn(
                name: "SyncedStatus",
                table: "BetaApplications");
        }
    }
}
