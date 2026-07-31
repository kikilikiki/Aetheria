using Aetheria.Database.Context;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Actions sur une salle de donjon précise en dehors du combat (voir GDD — exploration en
/// couloir linéaire, "mobs/loot au fil du chemin"). Le combat passe déjà par
/// <c>CombatService.StartFromDungeonAsync</c> ; ceci couvre les salles Coffre. Les autres types de
/// salle non-combat (Énigme, Piège, Marchand, Événement, Autel, Salle secrète) restent du texte
/// d'ambiance côté Client pour cette version — non simulés plutôt que du contenu inventé.
/// </summary>
public sealed class DungeonRoomService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    /// <summary>Voir retour utilisateur — "ajoute des items exclusifs que l'on peut avoir que en donjon" : réservés aux coffres de donjon (voir MonsterCatalogSeeder, IsObtainable = false). Interne plutôt que privé : réutilisé tel quel par <see cref="DungeonCompletionService"/> pour la récompense de fin de parcours.</summary>
    internal static readonly string[] DungeonExclusiveItems = ["Éclat de donjon", "Relique poussiéreuse", "Fragment d'ombre", "Cœur de labyrinthe"];

    public async Task<ChestLootResult> OpenChestAsync(int dungeonId, int floorNumber, int roomIndex, OpenChestRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var dungeon = await db.Dungeons.FirstOrDefaultAsync(d => d.Id == dungeonId, ct)
            ?? throw new AccountOperationException("Donjon introuvable.");

        var floor = DungeonFloorGenerator.GenerateFloor(dungeon.Seed, floorNumber);
        var room = floor.Rooms.FirstOrDefault(r => r.Index == roomIndex)
            ?? throw new AccountOperationException("Salle introuvable à cet étage.");

        if (room.EncounterType != DungeonEncounterType.Coffre)
        {
            throw new AccountOperationException("Cette salle ne contient pas de coffre.");
        }

        // Graine dérivée de celle du combat (dungeon+étage+salle) mais décalée (+1) pour ne pas
        // reproduire exactement le même tirage qu'un combat sur la même salle.
        var seed = DungeonFloorGenerator.StableSeed(dungeon.Seed, floorNumber, roomIndex, 1);
        var random = new Random(seed);
        var gold = random.Next(20, 81);

        // Voir GDD/demande utilisateur — "grade payant... 0.1%/0.2%/0.3% de gain d'argent en
        // plus" (voir PremiumService) et "consommables pour booster... la money" (voir
        // TemporaryBoostService).
        var multiplier = await PremiumService.GetXpGoldMultiplierAsync(db, userId, ct) * TemporaryBoostService.GoldMultiplier(character);
        var boostedGold = (int)Math.Round(gold * multiplier);
        character.Gold += boostedGold;

        // Voir retour utilisateur — "pouvoir obtenir d'autre chose que de l'or dans les donjons" :
        // 40% de chances en plus de l'or, jamais à sa place (l'or reste garanti).
        string? itemName = null;
        if (random.Next(100) < 40)
        {
            itemName = DungeonExclusiveItems[random.Next(DungeonExclusiveItems.Length)];
            var item = await db.Items.FirstOrDefaultAsync(i => i.Name == itemName, ct);
            if (item is not null)
            {
                await InventoryStackingService.AddQuantityAsync(db, character.Id, item.Id, 1, item.MaxStackSize <= 0 ? 99 : item.MaxStackSize, ct);
            }
            else
            {
                itemName = null;
            }
        }

        await db.SaveChangesAsync(ct);

        return new ChestLootResult { Gold = boostedGold, ItemName = itemName };
    }
}
