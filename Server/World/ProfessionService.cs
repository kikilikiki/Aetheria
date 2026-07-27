using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Récolte et artisanat (voir <c>Docs/GameDesign.md</c> — section Métiers). Courbe de niveau
/// volontairement simple pour cette première version : XP requise pour le niveau N = N × 100.
/// </summary>
public sealed class ProfessionService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private const int ExperiencePerLevel = 100;

    public async Task<ProfessionActionResponse> GatherAsync(GatherRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);

        var resourceItem = await db.Items.FirstOrDefaultAsync(i => i.Id == request.ResourceItemId, ct);
        if (resourceItem is not { ItemType: ItemType.Ressource })
        {
            throw new AccountOperationException("Cet objet n'est pas une ressource récoltable.");
        }

        var quantity = Math.Clamp(request.Quantity, 1, 10);
        await AddToInventoryAsync(character.Id, resourceItem.Id, quantity, ct);

        var profession = await GetOrCreateProfessionAsync(character.Id, request.Profession, ct);
        var leveledUp = GrantExperience(profession, quantity * 10);

        await db.SaveChangesAsync(ct);

        return BuildResponse(profession, leveledUp, $"{quantity}x {resourceItem.Name} récolté(s).");
    }

    public async Task<ProfessionActionResponse> CraftAsync(CraftRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);

        var recipe = await db.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);
        if (recipe is null)
        {
            throw new AccountOperationException("Recette inconnue.");
        }

        var profession = await GetOrCreateProfessionAsync(character.Id, recipe.Profession, ct);
        if (profession.Level < recipe.RequiredLevel)
        {
            throw new AccountOperationException($"Niveau {recipe.RequiredLevel} en {recipe.Profession} requis.");
        }

        // On vérifie d'abord TOUS les ingrédients avant d'en consommer un seul.
        var inventoryEntries = new List<InventoryItemEntity>();
        foreach (var ingredient in recipe.Ingredients)
        {
            var entry = await db.InventoryItems.FirstOrDefaultAsync(
                i => i.CharacterId == character.Id && i.ItemId == ingredient.ItemId && i.Quantity >= ingredient.Quantity, ct);

            if (entry is null)
            {
                throw new AccountOperationException("Ingrédients insuffisants dans l'inventaire.");
            }

            inventoryEntries.Add(entry);
        }

        for (var i = 0; i < recipe.Ingredients.Count; i++)
        {
            var entry = inventoryEntries[i];
            entry.Quantity -= recipe.Ingredients[i].Quantity;
            if (entry.Quantity <= 0)
            {
                db.InventoryItems.Remove(entry);
            }
        }

        await AddToInventoryAsync(character.Id, recipe.ResultItemId, recipe.ResultQuantity, ct);

        var leveledUp = GrantExperience(profession, 25);
        await db.SaveChangesAsync(ct);

        await new AchievementService(db).UnlockAsync(character.UserId, "premier_craft", ct);

        return BuildResponse(profession, leveledUp, $"{recipe.Name} fabriqué avec succès.");
    }

    private async Task<CharacterEntity> ResolveOwnedCharacterAsync(string sessionToken, Guid characterId, CancellationToken ct)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct);
        return character ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");
    }

    private async Task<CharacterProfessionEntity> GetOrCreateProfessionAsync(
        Guid characterId, ProfessionType profession, CancellationToken ct)
    {
        var entity = await db.CharacterProfessions.FirstOrDefaultAsync(
            p => p.CharacterId == characterId && p.Profession == profession, ct);

        if (entity is null)
        {
            entity = new CharacterProfessionEntity { Id = Guid.NewGuid(), CharacterId = characterId, Profession = profession };
            db.CharacterProfessions.Add(entity);
        }

        return entity;
    }

    private async Task AddToInventoryAsync(Guid characterId, int itemId, int quantity, CancellationToken ct)
    {
        var existing = await db.InventoryItems.FirstOrDefaultAsync(
            i => i.CharacterId == characterId && i.ItemId == itemId, ct);

        if (existing is not null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            db.InventoryItems.Add(new InventoryItemEntity
            {
                Id = Guid.NewGuid(),
                CharacterId = characterId,
                ItemId = itemId,
                Quantity = quantity,
            });
        }
    }

    private static bool GrantExperience(CharacterProfessionEntity profession, int amount)
    {
        profession.Experience += amount;
        var leveledUp = false;

        while (profession.Experience >= profession.Level * ExperiencePerLevel)
        {
            profession.Experience -= profession.Level * ExperiencePerLevel;
            profession.Level++;
            leveledUp = true;
        }

        return leveledUp;
    }

    private static ProfessionActionResponse BuildResponse(CharacterProfessionEntity profession, bool leveledUp, string message)
        => new()
        {
            Profession = profession.Profession,
            Level = profession.Level,
            Experience = profession.Experience,
            LeveledUp = leveledUp,
            Message = message,
        };
}
