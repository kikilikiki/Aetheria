using Aetheria.Database.Context;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Profil de personnage (voir GDD/demande utilisateur — "un endroit pour modifier son profil :
/// description, item à montrer, titre, grade"). Le grade est <c>UserEntity.Rank</c>, lu seul
/// (jamais modifié ici) ; description/objet à montrer/titre actif sont les champs éditables.
/// </summary>
public sealed class ProfileService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public async Task<ProfileSummary?> GetAsync(Guid characterId, CancellationToken ct = default)
    {
        var character = await db.Characters.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == characterId, ct);
        if (character?.User is null)
        {
            return null;
        }

        string? showcaseItemName = null;
        if (character.ShowcaseItemId is { } showcaseId)
        {
            showcaseItemName = await db.Items.Where(i => i.Id == showcaseId).Select(i => i.Name).FirstOrDefaultAsync(ct);
        }

        var ownedTitles = await db.CharacterTitles
            .Where(t => t.CharacterId == characterId)
            .Select(t => t.TitleKey)
            .ToListAsync(ct);

        // Voir GDD/demande utilisateur — "Collections : montures, ailes" : possédées par le
        // COMPTE (voir CollectionEntity/AchievementService), pas par ce seul personnage.
        var ownedCollectionKeys = await db.Collections
            .Where(c => c.UserId == character.UserId && (c.Category == "Monture" || c.Category == "Ailes"))
            .ToListAsync(ct);
        var ownedMountKeys = ownedCollectionKeys.Where(c => c.Category == "Monture").Select(c => c.CollectionKey).ToList();
        var ownedWingKeys = ownedCollectionKeys.Where(c => c.Category == "Ailes").Select(c => c.CollectionKey).ToList();

        return new ProfileSummary
        {
            CharacterName = character.Name,
            Description = character.ProfileDescription,
            Level = character.Level,
            Rank = character.User.Rank,
            ShowcaseItemId = character.ShowcaseItemId,
            ShowcaseItemName = showcaseItemName,
            ActiveTitle = character.ActiveTitle,
            OwnedTitles = ownedTitles,
            ActiveMountKey = character.ActiveMountKey,
            OwnedMountKeys = ownedMountKeys,
            ActiveWingKey = character.ActiveWingKey,
            OwnedWingKeys = ownedWingKeys,
        };
    }

    public async Task<ProfileSummary> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        character.ProfileDescription = request.Description.Length > 200 ? request.Description[..200] : request.Description;

        if (request.ShowcaseItemId is { } itemId)
        {
            var owned = await db.InventoryItems.AnyAsync(i => i.CharacterId == character.Id && i.ItemId == itemId, ct);
            character.ShowcaseItemId = owned ? itemId : character.ShowcaseItemId;
        }
        else
        {
            character.ShowcaseItemId = null;
        }

        if (request.ActiveTitle is { } title)
        {
            var owned = await db.CharacterTitles.AnyAsync(t => t.CharacterId == character.Id && t.TitleKey == title, ct);
            character.ActiveTitle = owned ? title : character.ActiveTitle;
        }
        else
        {
            character.ActiveTitle = null;
        }

        if (request.ActiveMountKey is { } mountKey)
        {
            var owned = await db.Collections.AnyAsync(c => c.UserId == character.UserId && c.Category == "Monture" && c.CollectionKey == mountKey, ct);
            character.ActiveMountKey = owned ? mountKey : character.ActiveMountKey;
        }
        else
        {
            character.ActiveMountKey = null;
        }

        if (request.ActiveWingKey is { } wingKey)
        {
            var owned = await db.Collections.AnyAsync(c => c.UserId == character.UserId && c.Category == "Ailes" && c.CollectionKey == wingKey, ct);
            character.ActiveWingKey = owned ? wingKey : character.ActiveWingKey;
        }
        else
        {
            character.ActiveWingKey = null;
        }

        await db.SaveChangesAsync(ct);

        return await GetAsync(character.Id, ct) ?? throw new AccountOperationException("Profil introuvable après mise à jour.");
    }
}
