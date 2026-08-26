using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Actions sur une salle de donjon précise en dehors du combat (voir GDD — exploration en
/// couloir linéaire, "mobs/loot au fil du chemin"). Le combat passe déjà par
/// <c>CombatService.StartFromDungeonAsync</c> ; ceci couvre les salles Coffre/Salle secrète,
/// Piège, Énigme et Événement (voir Docs/Idees.md — récompense mécanique pour ces types de
/// salle, jusqu'ici de simples textes d'ambiance côté Client). Marchand et Autel restent du
/// texte d'ambiance pour cette version — non simulés plutôt que du contenu inventé.
/// </summary>
public sealed class DungeonRoomService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    /// <summary>Voir retour utilisateur — "ajoute des items exclusifs que l'on peut avoir que en donjon" : réservés aux coffres de donjon (voir MonsterCatalogSeeder, IsObtainable = false). Interne plutôt que privé : réutilisé tel quel par <see cref="DungeonCompletionService"/> pour la récompense de fin de parcours.</summary>
    internal static readonly string[] DungeonExclusiveItems = ["Éclat de donjon", "Relique poussiéreuse", "Fragment d'ombre", "Cœur de labyrinthe"];

    public async Task<ChestLootResult> OpenChestAsync(int dungeonId, int floorNumber, int roomIndex, OpenChestRequest request, CancellationToken ct = default)
    {
        var (character, userId, room, dungeonSeed) = await ResolveRoomAsync(dungeonId, floorNumber, roomIndex, request.SessionToken, request.CharacterId, ct);

        // Voir Docs/Idees.md — Salle secrète : réutilise le même tirage que le coffre normal,
        // avec un taux d'objet exclusif supérieur (voir isSecretRoom plus bas) plutôt qu'une
        // seconde méthode dupliquée.
        var isSecretRoom = room.EncounterType == DungeonEncounterType.SalleSecrete;
        if (room.EncounterType != DungeonEncounterType.Coffre && !isSecretRoom)
        {
            throw new AccountOperationException("Cette salle ne contient pas de coffre.");
        }

        // Graine dérivée de celle du combat (dungeon+étage+salle) mais décalée (+1) pour ne pas
        // reproduire exactement le même tirage qu'un combat sur la même salle.
        var seed = DungeonFloorGenerator.StableSeed(dungeonSeed, floorNumber, roomIndex, 1);
        var random = new Random(seed);
        var gold = random.Next(20, 81);
        if (isSecretRoom)
        {
            gold = (int)Math.Round(gold * 1.5);
        }

        // Voir GDD/demande utilisateur — "grade payant... 0.1%/0.2%/0.3% de gain d'argent en
        // plus" (voir PremiumService) et "consommables pour booster... la money" (voir
        // TemporaryBoostService).
        var multiplier = await PremiumService.GetXpGoldMultiplierAsync(db, userId, ct) * TemporaryBoostService.GoldMultiplier(character);
        var boostedGold = (int)Math.Round(gold * multiplier);
        character.Gold += boostedGold;

        // Voir retour utilisateur — "pouvoir obtenir d'autre chose que de l'or dans les donjons" :
        // 40% de chances en plus de l'or (60% pour une Salle secrète), jamais à sa place (l'or
        // reste garanti).
        var itemChance = isSecretRoom ? 60 : 40;
        string? itemName = null;
        if (random.Next(100) < itemChance)
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

    /// <summary>Voir Docs/Idees.md — salle Piège : perte d'or symétrique du gain d'un coffre, jamais en dessous de 0.</summary>
    public async Task<TrapResult> TriggerTrapAsync(int dungeonId, int floorNumber, int roomIndex, OpenChestRequest request, CancellationToken ct = default)
    {
        var (character, _, room, dungeonSeed) = await ResolveRoomAsync(dungeonId, floorNumber, roomIndex, request.SessionToken, request.CharacterId, ct);
        if (room.EncounterType != DungeonEncounterType.Piege)
        {
            throw new AccountOperationException("Cette salle ne contient pas de piège.");
        }

        var seed = DungeonFloorGenerator.StableSeed(dungeonSeed, floorNumber, roomIndex, 2);
        var random = new Random(seed);
        var goldLost = Math.Min(character.Gold, random.Next(10, 41));
        character.Gold -= goldLost;
        await db.SaveChangesAsync(ct);

        return new TrapResult { GoldLost = (int)goldLost };
    }

    /// <summary>Voir Docs/Idees.md — salle Énigme : choix binaire résolu côté serveur (le "bon" choix est tiré par la même graine stable que le reste de la salle, jamais transmis au client avant résolution).</summary>
    public async Task<PuzzleResult> ResolvePuzzleAsync(int dungeonId, int floorNumber, int roomIndex, ResolvePuzzleRequest request, CancellationToken ct = default)
    {
        var (character, userId, room, dungeonSeed) = await ResolveRoomAsync(dungeonId, floorNumber, roomIndex, request.SessionToken, request.CharacterId, ct);
        if (room.EncounterType != DungeonEncounterType.Enigme)
        {
            throw new AccountOperationException("Cette salle ne contient pas d'énigme.");
        }

        if (request.ChoiceIndex is not (0 or 1))
        {
            throw new AccountOperationException("Choix invalide.");
        }

        var seed = DungeonFloorGenerator.StableSeed(dungeonSeed, floorNumber, roomIndex, 3);
        var random = new Random(seed);
        var correctChoice = random.Next(2);
        var wasCorrect = request.ChoiceIndex == correctChoice;

        var multiplier = await PremiumService.GetXpGoldMultiplierAsync(db, userId, ct) * TemporaryBoostService.GoldMultiplier(character);
        var baseAmount = random.Next(15, 51);
        var goldDelta = wasCorrect ? (int)Math.Round(baseAmount * multiplier) : -(int)Math.Min(character.Gold, baseAmount);
        character.Gold = Math.Max(0, character.Gold + goldDelta);
        await db.SaveChangesAsync(ct);

        return new PuzzleResult { WasCorrect = wasCorrect, GoldDelta = goldDelta };
    }

    /// <summary>Voir Docs/Idees.md — salle Événement : petit bonus or/XP instantané plutôt qu'un buff porté sur le reste de l'étage (aucun état de progression d'étage n'est aujourd'hui suivi côté serveur entre deux salles).</summary>
    public async Task<EventRoomResult> TriggerEventAsync(int dungeonId, int floorNumber, int roomIndex, OpenChestRequest request, CancellationToken ct = default)
    {
        var (character, userId, room, dungeonSeed) = await ResolveRoomAsync(dungeonId, floorNumber, roomIndex, request.SessionToken, request.CharacterId, ct);
        if (room.EncounterType != DungeonEncounterType.Evenement)
        {
            throw new AccountOperationException("Cette salle ne contient pas d'événement.");
        }

        var seed = DungeonFloorGenerator.StableSeed(dungeonSeed, floorNumber, roomIndex, 4);
        var random = new Random(seed);
        var multiplier = await PremiumService.GetXpGoldMultiplierAsync(db, userId, ct) * TemporaryBoostService.GoldMultiplier(character);
        var gold = (int)Math.Round(random.Next(15, 46) * multiplier);
        var experience = random.Next(10, 31);

        character.Gold += gold;
        CharacterProgressionService.GrantExperience(character, experience);
        await db.SaveChangesAsync(ct);

        return new EventRoomResult { Gold = gold, Experience = experience };
    }

    private async Task<(CharacterEntity Character, Guid UserId, Shared.Models.DungeonRoom Room, int DungeonSeed)> ResolveRoomAsync(int dungeonId, int floorNumber, int roomIndex, string sessionToken, Guid characterId, CancellationToken ct)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var dungeon = await db.Dungeons.FirstOrDefaultAsync(d => d.Id == dungeonId, ct)
            ?? throw new AccountOperationException("Donjon introuvable.");

        var floor = DungeonFloorGenerator.GenerateFloor(dungeon.Seed, floorNumber);
        var room = floor.Rooms.FirstOrDefault(r => r.Index == roomIndex)
            ?? throw new AccountOperationException("Salle introuvable à cet étage.");

        return (character, userId, room, dungeon.Seed);
    }
}
