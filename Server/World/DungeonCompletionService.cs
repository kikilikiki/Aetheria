using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "a la fin des 10 etage termine le dongon et affiche un message
/// fait le quitter le dongon donne lui des recompense et ajoute un cooldown de 1h avant que il
/// puisse retourne dans le dongon ou il vient d'aller" : le Client appelle
/// <see cref="CompleteAsync"/> une fois le dernier étage (voir DungeonProgression.MaxFloor)
/// entièrement nettoyé, et interroge <see cref="GetEntryStatusAsync"/> avant d'autoriser l'entrée
/// dans un donjon (voir DungeonSelectPanel côté Client).
/// </summary>
public sealed class DungeonCompletionService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public static readonly TimeSpan Cooldown = TimeSpan.FromHours(1);

    public async Task<DungeonEntryStatus> GetEntryStatusAsync(string sessionToken, Guid characterId, int dungeonId, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        _ = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var cooldown = await db.DungeonCooldowns.FirstOrDefaultAsync(c => c.CharacterId == characterId && c.DungeonId == dungeonId, ct);
        if (cooldown is null || cooldown.AvailableAtUtc <= DateTime.UtcNow)
        {
            return new DungeonEntryStatus { Allowed = true };
        }

        return new DungeonEntryStatus { Allowed = false, AvailableAtUtc = cooldown.AvailableAtUtc };
    }

    public async Task<DungeonCompletionResult> CompleteAsync(string sessionToken, Guid characterId, int dungeonId, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var dungeon = await db.Dungeons.FirstOrDefaultAsync(d => d.Id == dungeonId, ct)
            ?? throw new AccountOperationException("Donjon introuvable.");

        var cooldown = await db.DungeonCooldowns.FirstOrDefaultAsync(c => c.CharacterId == characterId && c.DungeonId == dungeonId, ct);
        if (cooldown is not null && cooldown.AvailableAtUtc > DateTime.UtcNow)
        {
            throw new AccountOperationException("Ce donjon est encore en recharge.");
        }

        // Voir GDD/demande utilisateur — récompense de fin de parcours, échelonnée sur le niveau
        // des monstres du dernier étage (même logique de multiplicateur que DungeonRoomService.OpenChestAsync).
        var random = Random.Shared;
        var baseGold = 100 + dungeon.MaxMonsterLevel * 5;
        var multiplier = await PremiumService.GetXpGoldMultiplierAsync(db, userId, ct) * TemporaryBoostService.GoldMultiplier(character);
        var gold = (int)Math.Round(baseGold * multiplier);
        character.Gold += gold;

        string? itemName = null;
        if (random.Next(100) < 60)
        {
            itemName = DungeonRoomService.DungeonExclusiveItems[random.Next(DungeonRoomService.DungeonExclusiveItems.Length)];
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

        var availableAt = DateTime.UtcNow + Cooldown;
        if (cooldown is null)
        {
            cooldown = new DungeonCooldownEntity { Id = Guid.NewGuid(), CharacterId = characterId, DungeonId = dungeonId };
            db.DungeonCooldowns.Add(cooldown);
        }

        cooldown.AvailableAtUtc = availableAt;

        await db.SaveChangesAsync(ct);

        return new DungeonCompletionResult { Gold = gold, ItemName = itemName, CooldownUntilUtc = availableAt };
    }
}
