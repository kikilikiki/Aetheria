using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheria.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldBossAttackCountAndMonsterEquipSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttackCount",
                table: "WorldBossDamageEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EquippedSlot",
                table: "Monsters",
                type: "integer",
                nullable: true);

            // Voir GDD/demande utilisateur — "on doit équiper les monstres au lieu de juste les
            // mettre avec soi via la pension" : préserve les équipes actives existantes (assigne un
            // emplacement 0-3 par ordre stable) au lieu de perdre silencieusement l'information en
            // supprimant IsInActiveTeam ci-dessous.
            migrationBuilder.Sql("""
                UPDATE "Monsters" AS m
                SET "EquippedSlot" = sub.slot
                FROM (
                    SELECT "Id", (ROW_NUMBER() OVER (PARTITION BY "OwnerCharacterId" ORDER BY "Id") - 1) AS slot
                    FROM "Monsters"
                    WHERE "IsInActiveTeam" = TRUE
                ) AS sub
                WHERE m."Id" = sub."Id" AND sub.slot < 4;
                """);

            migrationBuilder.DropColumn(
                name: "IsInActiveTeam",
                table: "Monsters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInActiveTeam",
                table: "Monsters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "Monsters" SET "IsInActiveTeam" = TRUE WHERE "EquippedSlot" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "AttackCount",
                table: "WorldBossDamageEntries");

            migrationBuilder.DropColumn(
                name: "EquippedSlot",
                table: "Monsters");
        }
    }
}
