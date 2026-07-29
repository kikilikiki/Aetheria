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

    /// <summary>Voir GDD/demande utilisateur — "ajoute un cooldown après extraction à un endroit (mine par exemple)".</summary>
    private static readonly TimeSpan GatherCooldown = TimeSpan.FromSeconds(30);

    public async Task<ProfessionActionResponse> GatherAsync(GatherRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);

        var resourceItem = await db.Items.FirstOrDefaultAsync(i => i.Id == request.ResourceItemId, ct);
        if (resourceItem is not { ItemType: ItemType.Ressource })
        {
            throw new AccountOperationException("Cet objet n'est pas une ressource récoltable.");
        }

        // Voir GDD/demande utilisateur — "si une team va dans la mine d'une autre team pour miner
        // il gagnera moins et aura moins de chance de drop" : la récolte hors territoire n'est
        // plus bloquée (comme avant) mais pénalisée — moitié moins de ressources ET une chance sur
        // deux de ne rien obtenir du tout, plutôt qu'un refus pur et simple.
        var isForeignTerritory = false;
        var territoryYieldMultiplier = 1.0;
        if (request.TerritoryId is { } territoryId)
        {
            var territory = await db.Territories.Include(t => t.ControllingKingdom).FirstOrDefaultAsync(t => t.Id == territoryId, ct)
                ?? throw new AccountOperationException("Territoire introuvable.");

            isForeignTerritory = territory.ControllingKingdom?.Type != character.Kingdom;

            // Voir GDD/demande utilisateur — "le premier gagne 2 bâtiments, le second 1..." :
            // le bonus de rendement du royaume qui contrôle CE territoire (voir
            // KingdomEntity.BonusTerritoryCount, KingdomWarService.ResolveWeeklyWarAsync)
            // s'applique à quiconque y récolte, +10% par palier de bonus.
            territoryYieldMultiplier = 1.0 + (territory.ControllingKingdom?.BonusTerritoryCount ?? 0) * 0.1;
        }

        var profession = await GetOrCreateProfessionAsync(character.Id, request.Profession, ct);

        if (profession.LastGatheredAtUtc is { } lastGatheredAt && DateTime.UtcNow - lastGatheredAt < GatherCooldown)
        {
            var remaining = GatherCooldown - (DateTime.UtcNow - lastGatheredAt);
            throw new AccountOperationException($"Il faut encore attendre {Math.Ceiling(remaining.TotalSeconds)}s avant de récolter à nouveau ici.");
        }

        profession.LastGatheredAtUtc = DateTime.UtcNow;

        // Voir GDD/demande utilisateur — "consommables pour booster la luck" (voir
        // TemporaryBoostService) : neutralise le risque de récolte manquée hors territoire.
        var hasLuckBoost = TemporaryBoostService.HasLuckBoost(character);
        if (isForeignTerritory && !hasLuckBoost && Random.Shared.Next(2) == 0)
        {
            await db.SaveChangesAsync(ct);
            return BuildResponse(profession, leveledUp: false, "Récolte manquée — ce territoire est contrôlé par un royaume rival.");
        }

        var quantity = Math.Clamp(request.Quantity, 1, 10);
        quantity = Math.Max(1, (int)Math.Round(quantity * territoryYieldMultiplier));
        if (isForeignTerritory && !hasLuckBoost)
        {
            quantity = Math.Max(1, quantity / 2);
        }

        await AddToInventoryAsync(character.Id, resourceItem.Id, quantity, ct);

        var leveledUp = GrantExperience(profession, quantity * 10);
        await db.SaveChangesAsync(ct);

        var suffix = isForeignTerritory ? " (rendement réduit — territoire rival)" : "";
        return BuildResponse(profession, leveledUp, $"{quantity}x {resourceItem.Name} récolté(s){suffix}.");
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

    // Voir GDD/demande utilisateur — "limite de stack d'item à 99 par item dans l'inventaire"
    // (voir ItemEntity.MaxStackSize, InventoryStackingService).
    private async Task AddToInventoryAsync(Guid characterId, int itemId, int quantity, CancellationToken ct)
    {
        var maxStackSize = await db.Items.Where(i => i.Id == itemId).Select(i => i.MaxStackSize).FirstOrDefaultAsync(ct);
        await InventoryStackingService.AddQuantityAsync(db, characterId, itemId, quantity, maxStackSize <= 0 ? 99 : maxStackSize, ct);
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

    /// <summary>
    /// Niveau/XP du personnage dans TOUS les métiers, y compris ceux jamais pratiqués (voir
    /// GDD/demande utilisateur — "un UI avec un bouton pour voir les métiers, les niveaux de
    /// chaque métier").
    /// </summary>
    public async Task<List<ProfessionSummary>> GetSummaryAsync(Guid characterId, CancellationToken ct = default)
    {
        var existing = await db.CharacterProfessions
            .Where(p => p.CharacterId == characterId)
            .ToDictionaryAsync(p => p.Profession, ct);

        return Enum.GetValues<ProfessionType>()
            .Select(profession =>
            {
                existing.TryGetValue(profession, out var entity);
                var level = entity?.Level ?? 1;
                return new ProfessionSummary
                {
                    Profession = profession,
                    Level = level,
                    Experience = entity?.Experience ?? 0,
                    ExperienceForNextLevel = level * ExperiencePerLevel,
                };
            })
            .ToList();
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
