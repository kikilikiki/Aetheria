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
    public async Task<int> OpenChestAsync(int dungeonId, int floorNumber, int roomIndex, OpenChestRequest request, CancellationToken ct = default)
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
        // plus" (voir PremiumService).
        var multiplier = await PremiumService.GetXpGoldMultiplierAsync(db, userId, ct);
        var boostedGold = (int)Math.Round(gold * multiplier);
        character.Gold += boostedGold;
        await db.SaveChangesAsync(ct);

        return boostedGold;
    }
}
