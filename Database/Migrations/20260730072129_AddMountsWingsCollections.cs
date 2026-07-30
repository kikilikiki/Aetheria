using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMountsWingsCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Collections",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActiveMountKey",
                table: "Characters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActiveWingKey",
                table: "Characters",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "ActiveMountKey",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ActiveWingKey",
                table: "Characters");
        }
    }
}
