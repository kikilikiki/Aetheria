using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.World;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models.Account;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>Création de personnage (voir <c>Docs/GameDesign.md</c> — "Au début du jeu, chaque joueur choisit un royaume").</summary>
public sealed class CharacterService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private const int StarterCaptureSphereCount = 3;

    public async Task<CharacterSummary> CreateAsync(CreateCharacterRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var nameTaken = await db.Characters.AnyAsync(c => c.Name == request.Name, ct);
        if (nameTaken)
        {
            throw new AccountOperationException("Ce nom de personnage est déjà pris.");
        }

        var character = new CharacterEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Class = request.Class,
            Kingdom = request.Kingdom,
            SkinColorIndex = request.SkinColorIndex,
            HairStyleIndex = request.HairStyleIndex,
            HairColorIndex = request.HairColorIndex,
            ClothesColorIndex = request.ClothesColorIndex,
            AccessoryIndex = request.AccessoryIndex,
        };

        db.Characters.Add(character);
        db.Statistics.Add(new StatisticsEntity
        {
            Id = Guid.NewGuid(),
            CharacterId = character.Id,
        });

        // Kit de départ : de quoi capturer sa première créature sans attendre un métier/HDV.
        var captureSphere = await db.Items.FirstOrDefaultAsync(i => i.ItemType == ItemType.ObjetDeCapture, ct);
        if (captureSphere is not null)
        {
            db.InventoryItems.Add(new InventoryItemEntity
            {
                Id = Guid.NewGuid(),
                CharacterId = character.Id,
                ItemId = captureSphere.Id,
                Quantity = StarterCaptureSphereCount,
            });
        }

        await db.SaveChangesAsync(ct);

        await new AchievementService(db).UnlockAsync(userId, "bienvenue", ct);

        return ToSummary(character);
    }

    public async Task<IReadOnlyList<CharacterSummary>> GetMineAsync(string sessionToken, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var characters = await db.Characters.Where(c => c.UserId == userId).ToListAsync(ct);
        return characters.Select(ToSummary).ToList();
    }

    private static CharacterSummary ToSummary(CharacterEntity character) => new()
    {
        Id = character.Id,
        Name = character.Name,
        Level = character.Level,
        Kingdom = character.Kingdom,
        SkinColorIndex = character.SkinColorIndex,
        HairStyleIndex = character.HairStyleIndex,
        HairColorIndex = character.HairColorIndex,
        ClothesColorIndex = character.ClothesColorIndex,
        AccessoryIndex = character.AccessoryIndex,
    };
}
